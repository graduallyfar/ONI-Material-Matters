using System;
using System.Collections.Generic;
using MaterialOverhaul.Core;
using MaterialOverhaul.Radiation;
using UnityEngine;

namespace MaterialOverhaul.Game
{
    /// <summary>
    /// 辐射「累积 → 显现」的统一扫描与结算。
    ///
    /// 每 0.2 秒扫描世界格子的二十五分之一，五秒扫完一遍；只处理有辐射或仍带累积量的格子：
    /// - 格子上的建筑与建造砖块累积受照量，一遍扫完后统一步进（建筑自身的放射先扣掉）；
    /// - 每格累积表的四份——格子里的物质、液体管、气体管、地上的物品——各自累积，物质一换就清零；
    ///   超过显现限度后写入原版放射性污染物，已有其他病菌时先清除（污染物优先）；
    /// - 原版放射物质（铀矿、核废料等）本身就是来源，不累积。
    ///
    /// 另外每秒结算一次：复制人身上污染物的接触剂量、辐射粒子发生器吸收的辐射由周围显现物付账。
    /// 辐射粒子撞击固体、复制人吸入污染物由各自的补丁即时结算。
    /// 未开启辐射（基础版或经典星球）时整个系统不运行。
    /// </summary>
    public static class RadiationInducedMonitor
    {
        private const string DiseaseReason = "MaterialOverhaul.Radiation";

        private static readonly HashSet<RadiationActivation> Active = new HashSet<RadiationActivation>();
        private static readonly Dictionary<RadiationActivation, float> Exposure = new Dictionary<RadiationActivation, float>();
        private static readonly List<RadiationActivation> Buffer = new List<RadiationActivation>();
        private static readonly Dictionary<int, float> PendingDrain = new Dictionary<int, float>();
        private static readonly List<KeyValuePair<RadiationActivation, float>> DrainTargets = new List<KeyValuePair<RadiationActivation, float>>();
        private static readonly List<int> KindBuffer = new List<int>();
        private static readonly List<PrimaryElement> ItemBuffer = new List<PrimaryElement>();

        private static AccumulationTable table;
        private static int nextCell;
        private static float passElapsed;
        private static float settleElapsed;
        private static bool radiationChecked;
        private static bool radiationEnabled;
        private static bool contaminantResolved;
        private static byte contaminantIdx = ContaminationModel.NoDisease;

        /// <summary>读档或退出时清空静态状态。</summary>
        public static void Clear()
        {
            Active.Clear();
            Exposure.Clear();
            Buffer.Clear();
            PendingDrain.Clear();
            DrainTargets.Clear();
            KindBuffer.Clear();
            ItemBuffer.Clear();
            table = null;
            nextCell = 0;
            passElapsed = 0f;
            settleElapsed = 0f;
            radiationChecked = false;
            radiationEnabled = false;
            contaminantResolved = false;
            contaminantIdx = ContaminationModel.NoDisease;
        }

        public static void Register(RadiationActivation activation)
        {
            if (activation != null)
            {
                Active.Add(activation);
            }
        }

        public static void Unregister(RadiationActivation activation)
        {
            if (activation != null)
            {
                Active.Remove(activation);
                Exposure.Remove(activation);
            }
        }

        /// <summary>读档：存档组件在第一次模拟更新时交来存档里的累积表。</summary>
        public static void LoadSaved(int[] keys, float[] values, int[] identities)
        {
            if (keys == null || keys.Length == 0 || !RadiationEnabled() || !EnsureTable())
            {
                return;
            }

            int loaded = table.Unpack(keys, values, identities);
            ModLog.Info("辐射：已从存档读回每格累积量 " + loaded + " 份（存档中 " + keys.Length + " 份）。");
        }

        /// <summary>存档：写出有累积量的格子；累积表尚未建立时返回 false。</summary>
        public static bool TryPack(out int[] keys, out float[] values, out int[] identities)
        {
            if (table == null)
            {
                keys = null;
                values = null;
                identities = null;
                return false;
            }

            table.Pack(RadiationBalance.SaveMinRads, out keys, out values, out identities);
            return true;
        }

        /// <summary>由存档组件每 0.2 秒的模拟更新驱动；暂停时原版不调用。</summary>
        public static void Tick(float dt)
        {
            if (!(dt > 0f) || !RadiationEnabled() || !EnsureTable())
            {
                return;
            }

            ResolveContaminant();
            int total = table.CellCount;
            int slices = RadiationBalance.ScanSlicesPerPass;
            int sliceSize = (total + slices - 1) / slices;
            int end = Math.Min(total, nextCell + sliceSize);
            float cellDt = dt * slices;
            ConduitFlow liquid = global::Game.Instance == null ? null : global::Game.Instance.liquidConduitFlow;
            ConduitFlow gas = global::Game.Instance == null ? null : global::Game.Instance.gasConduitFlow;
            for (int cell = nextCell; cell < end; cell++)
            {
                ScanCell(cell, cellDt, liquid, gas);
            }

            nextCell = end;
            passElapsed += dt;
            if (nextCell >= total)
            {
                FinishPass(passElapsed);
                nextCell = 0;
                passElapsed = 0f;
            }

            settleElapsed += dt;
            if (settleElapsed >= RadiationBalance.SettleIntervalSeconds)
            {
                Settle(settleElapsed);
                settleElapsed = 0f;
            }
        }

        private static void ScanCell(int cell, float dt, ConduitFlow liquid, ConduitFlow gas)
        {
            float rads = Grid.Radiation[cell];
            if (float.IsNaN(rads) || !(rads > 0f))
            {
                rads = 0f;
                if (!table.HasAny(cell))
                {
                    return;
                }
            }
            else
            {
                AddBuildingExposure(cell, rads);
            }

            ScanCellMatter(cell, rads, dt);
            ScanPipe(cell, AccumulationLayer.LiquidPipe, liquid, rads, dt);
            ScanPipe(cell, AccumulationLayer.GasPipe, gas, rads, dt);
            ScanItems(cell, rads, dt);
        }

        /// <summary>按物质身份步进一份累积量；没有物质、身份变化或累积量过低时清零。返回新的累积量。</summary>
        private static float StepLayer(int cell, AccumulationLayer layer, int identity, float rads, float dt)
        {
            float current = table.Reconcile(cell, layer, identity);
            if (identity == 0 || (!(rads > 0f) && !(current > 0f)))
            {
                table.Clear(cell, layer);
                return 0f;
            }

            float next = AccumulationModel.Step(current, rads, dt);
            if (AccumulationModel.CanDrop(next, rads))
            {
                table.Clear(cell, layer);
                return 0f;
            }

            table.Set(cell, layer, identity, next);
            return next;
        }

        private static void AddBuildingExposure(int cell, float rads)
        {
            GameObject building = Grid.Objects[cell, (int)ObjectLayer.Building];
            AddExposure(building, rads);
            GameObject tile = Grid.Objects[cell, (int)ObjectLayer.FoundationTile];
            if (tile != building)
            {
                AddExposure(tile, rads);
            }
        }

        private static void AddExposure(GameObject go, float rads)
        {
            if (go == null)
            {
                return;
            }

            RadiationActivation activation = go.GetComponent<RadiationActivation>();
            if (activation == null || !activation.Eligible)
            {
                return;
            }

            // 自己不累积自己：扣掉建筑自身放射（按放射源处的强度，是上界）。
            float received = rads - activation.CurrentEmitRads;
            float current;
            Exposure.TryGetValue(activation, out current);
            Exposure[activation] = current + (received > 0f ? received / activation.CellCount : 0f);
        }

        /// <summary>格子里的物质（气体、液体、天然固体）；建造砖块的格子由砖块自己累积。</summary>
        private static void ScanCellMatter(int cell, float rads, float dt)
        {
            if (Grid.Foundation[cell])
            {
                table.Clear(cell, AccumulationLayer.Cell);
                return;
            }

            Element element = Grid.Element[cell];
            float mass = Grid.Mass[cell];
            if (!(mass > 0f) || !RadiationMaterials.CanAccumulate(element))
            {
                table.Clear(cell, AccumulationLayer.Cell);
                return;
            }

            byte idx = Grid.DiseaseIdx[cell];
            int count = Grid.DiseaseCount[cell];
            // 自己不累积自己：扣掉本格污染物的放射。
            float received = rads - ContaminationModel.SelfRads(idx, count, contaminantIdx);
            float accumulation = StepLayer(cell, AccumulationLayer.Cell, RadiationMaterials.IdentityOf(element), received, dt);
            float excess = AccumulationModel.ExcessRate(accumulation, RadiationMaterials.ProfileOf(element).Limit);
            if (!(excess > 0f) || contaminantIdx == ContaminationModel.NoDisease)
            {
                return;
            }

            float effective = ContaminationModel.EffectiveMassKg(mass, RadiationMaterials.PhaseOf(element));
            if (ContaminationModel.SteadySelfRads(effective, excess) < RadiationBalance.EmitMinRads)
            {
                return;
            }

            int toAdd = ContaminationModel.ContaminantsToAdd(effective, excess, dt);
            ContaminationPlan plan = ContaminationModel.Plan(idx, count, contaminantIdx, toAdd);
            if (!plan.HasWork)
            {
                return;
            }

            if (plan.ClearIdx != ContaminationModel.NoDisease)
            {
                SimMessages.ModifyDiseaseOnCell(cell, plan.ClearIdx, -plan.ClearCount);
                ModLog.InfoOnce("radiation|contaminate|clear", "辐射显现：在显现的格子中清除其他病菌后写入放射性污染物（污染物优先）。");
            }

            SimMessages.ModifyDiseaseOnCell(cell, contaminantIdx, plan.AddCount);
            ModLog.InfoOnce("radiation|contaminate", "辐射显现：已开始向显现的格子物质写入放射性污染物。");
        }

        /// <summary>管道里的流体：每段管道按流过的物质累积，物质一换就清零；显现后往管道里写污染物。</summary>
        private static void ScanPipe(int cell, AccumulationLayer layer, ConduitFlow flow, float rads, float dt)
        {
            if (flow == null || !flow.HasConduit(cell))
            {
                table.Clear(cell, layer);
                return;
            }

            ConduitFlow.ConduitContents contents = flow.GetContents(cell);
            float mass = contents.mass;
            Element element = mass > 0f ? ElementLoader.FindElementByHash(contents.element) : null;
            if (!RadiationMaterials.CanAccumulate(element))
            {
                table.Clear(cell, layer);
                return;
            }

            float accumulation = StepLayer(cell, layer, RadiationMaterials.IdentityOf(element), rads, dt);
            float excess = AccumulationModel.ExcessRate(accumulation, RadiationMaterials.ProfileOf(element).Limit);
            if (!(excess > 0f) || contaminantIdx == ContaminationModel.NoDisease)
            {
                return;
            }

            float effective = ContaminationModel.EffectiveMassKg(mass, RadiationMaterials.PhaseOf(element));
            int toAdd = ContaminationModel.ContaminantsToAdd(effective, excess, dt);
            ContaminationPlan plan = ContaminationModel.Plan(contents.diseaseIdx, contents.diseaseCount, contaminantIdx, toAdd);
            if (!plan.HasWork)
            {
                return;
            }

            int existing = contents.diseaseIdx == contaminantIdx && contents.diseaseCount > 0 ? contents.diseaseCount : 0;
            contents.diseaseIdx = contaminantIdx;
            contents.diseaseCount = existing + plan.AddCount;
            flow.SetContents(cell, contents);
            ModLog.InfoOnce("radiation|pipe", "辐射显现：已开始向显现的管道流体写入放射性污染物。");
        }

        /// <summary>地上的物品：一格里的物品种类变了就清零；显现后给这一格的所有物品加污染物。动物不累积。</summary>
        private static void ScanItems(int cell, float rads, float dt)
        {
            GameObject head = Grid.Objects[cell, (int)ObjectLayer.Pickupables];
            Pickupable first = head == null ? null : head.GetComponent<Pickupable>();
            if (first == null)
            {
                table.Clear(cell, AccumulationLayer.Items);
                return;
            }

            ItemBuffer.Clear();
            KindBuffer.Clear();
            Element lead = null;
            for (ObjectLayerListItem item = first.objectLayerListItem; item != null; item = item.nextItem)
            {
                Pickupable pickupable = item.pickupable;
                KPrefabID id = pickupable == null ? null : pickupable.KPrefabID;
                if (id == null || id.HasTag(GameTags.Creature))
                {
                    continue;
                }

                PrimaryElement primary = pickupable.PrimaryElement;
                if (primary == null || !(primary.Mass > 0f) || !RadiationMaterials.CanAccumulate(primary.Element))
                {
                    continue;
                }

                ItemBuffer.Add(primary);
                if (lead == null)
                {
                    lead = primary.Element;
                }

                int kind = id.PrefabTag.GetHashCode();
                if (!KindBuffer.Contains(kind))
                {
                    KindBuffer.Add(kind);
                }
            }

            float accumulation = StepLayer(cell, AccumulationLayer.Items, AccumulationTable.KindIdentity(KindBuffer), rads, dt);
            float excess = AccumulationModel.ExcessRate(accumulation, RadiationMaterials.ProfileOf(lead).Limit);
            if (!(excess > 0f) || contaminantIdx == ContaminationModel.NoDisease)
            {
                ItemBuffer.Clear();
                return;
            }

            for (int i = 0; i < ItemBuffer.Count; i++)
            {
                PrimaryElement primary = ItemBuffer[i];
                float effective = ContaminationModel.EffectiveMassKg(primary.Mass, RadiationMaterials.PhaseOf(primary.Element));
                int toAdd = ContaminationModel.ContaminantsToAdd(effective, excess, dt);
                ContaminationPlan plan = ContaminationModel.Plan(primary.DiseaseIdx, primary.DiseaseCount, contaminantIdx, toAdd);
                if (!plan.HasWork)
                {
                    continue;
                }

                if (plan.ClearIdx != ContaminationModel.NoDisease)
                {
                    primary.ModifyDiseaseCount(-plan.ClearCount, DiseaseReason);
                }

                primary.AddDisease(contaminantIdx, plan.AddCount, DiseaseReason);
                ModLog.InfoOnce("radiation|item", "辐射显现：已开始向显现的地上物品写入放射性污染物。");
            }

            ItemBuffer.Clear();
        }

        private static void FinishPass(float passSeconds)
        {
            if (!(passSeconds > 0f))
            {
                Exposure.Clear();
                return;
            }

            Buffer.Clear();
            Buffer.AddRange(Active);
            foreach (RadiationActivation activation in Exposure.Keys)
            {
                if (!Active.Contains(activation))
                {
                    Buffer.Add(activation);
                }
            }

            for (int i = 0; i < Buffer.Count; i++)
            {
                RadiationActivation activation = Buffer[i];
                if (activation == null)
                {
                    Active.Remove(activation);
                    continue;
                }

                float rads;
                Exposure.TryGetValue(activation, out rads);
                activation.ApplyStep(rads, passSeconds);
            }

            if (Buffer.Count > 0)
            {
                ModLog.InfoOnce("radiation|activation", "辐射累积：建筑累积量已开始按受照量步进（本轮 " + Buffer.Count + " 栋）。");
            }

            Exposure.Clear();
            Buffer.Clear();
        }

        private static void Settle(float dt)
        {
            try
            {
                ContactDose(dt);
            }
            catch (Exception e)
            {
                ModLog.WarnOnce("radiation|contact|error", "辐射：接触剂量结算失败：" + e.Message);
            }

            try
            {
                DistributeDrain();
            }
            catch (Exception e)
            {
                PendingDrain.Clear();
                ModLog.WarnOnce("radiation|drain|error", "辐射：辐射粒子发生器付账失败：" + e.Message);
            }
        }

        /// <summary>接触：身上带污染物的复制人按个数累积剂量；穿防护服时不计。</summary>
        private static void ContactDose(float dt)
        {
            if (contaminantIdx == ContaminationModel.NoDisease)
            {
                return;
            }

            List<MinionIdentity> minions = Components.LiveMinionIdentities.Items;
            for (int i = 0; i < minions.Count; i++)
            {
                MinionIdentity minion = minions[i];
                if (minion == null)
                {
                    continue;
                }

                KPrefabID id = minion.GetComponent<KPrefabID>();
                if (id != null && (id.HasTag(GameTags.HasSuitTank) || id.HasTag(GameTags.HasAirtightSuit)))
                {
                    continue;
                }

                PrimaryElement primary = minion.GetComponent<PrimaryElement>();
                if (primary == null || primary.DiseaseIdx != contaminantIdx || primary.DiseaseCount <= 0)
                {
                    continue;
                }

                if (ApplyDose(minion.gameObject, DoseModel.ContactRads(primary.DiseaseCount, dt)))
                {
                    ModLog.InfoOnce("radiation|contact", "辐射：复制人身上的放射性污染物已开始按接触途径累积剂量。");
                }
            }
        }

        /// <summary>吸入：原版每次呼吸报告吸入的病菌，吸入放射性污染物时增加剂量。</summary>
        public static void OnInhaled(GameObject minion, byte diseaseIdx, int count)
        {
            if (minion == null || count <= 0 || diseaseIdx == ContaminationModel.NoDisease || !RadiationEnabled())
            {
                return;
            }

            ResolveContaminant();
            if (diseaseIdx != contaminantIdx)
            {
                return;
            }

            if (ApplyDose(minion, DoseModel.InhaledRads(count)))
            {
                ModLog.InfoOnce("radiation|inhale", "辐射：复制人吸入的放射性污染物已开始按吸入途径累积剂量。");
            }
        }

        private static bool ApplyDose(GameObject minion, float rads)
        {
            if (minion == null || !(rads > 0f))
            {
                return false;
            }

            Klei.AI.AmountInstance dose = Db.Get().Amounts.RadiationBalance.Lookup(minion);
            if (dose == null)
            {
                return false;
            }

            dose.ApplyDelta(rads);
            return true;
        }

        /// <summary>辐射粒子撞到固体：剩余能量全部存进被撞的砖块、建筑或天然格。</summary>
        public static void DepositParticle(int cell, float payload)
        {
            if (!RadiationEnabled() || !Grid.IsValidCell(cell))
            {
                return;
            }

            float rads = AccumulationModel.ParticleRads(payload);
            if (!(rads > 0f))
            {
                return;
            }

            RadiationActivation target = EligibleAt(cell, ObjectLayer.FoundationTile);
            if (target == null)
            {
                target = EligibleAt(cell, ObjectLayer.Building);
            }

            if (target != null)
            {
                target.AddAccumulation(rads);
                ModLog.InfoOnce("radiation|particle|building", "辐射粒子：撞击建筑或砖块，能量已存入其累积量。");
                return;
            }

            // 原版自带辐射源的建筑本身就是来源；其余被建筑占用的格子不结算。
            if (Grid.Foundation[cell] || Grid.Objects[cell, (int)ObjectLayer.Building] != null)
            {
                return;
            }

            Element element = Grid.Element[cell];
            if (element == null || !element.IsSolid || !RadiationMaterials.CanAccumulate(element) || !EnsureTable())
            {
                return;
            }

            table.Add(cell, AccumulationLayer.Cell, RadiationMaterials.IdentityOf(element), rads);
            ModLog.InfoOnce("radiation|particle|cell", "辐射粒子：撞击天然格，能量已存入格子累积量。");
        }

        private static RadiationActivation EligibleAt(int cell, ObjectLayer layer)
        {
            GameObject go = Grid.Objects[cell, (int)layer];
            RadiationActivation activation = go == null ? null : go.GetComponent<RadiationActivation>();
            return activation != null && activation.Eligible ? activation : null;
        }

        /// <summary>辐射粒子发生器一次采样吸收的辐射，等到每秒结算时由周围显现物付账。</summary>
        public static void QueueGeneratorDrain(int cell, float absorbedRads)
        {
            if (!(absorbedRads > 0f) || !Grid.IsValidCell(cell) || !RadiationEnabled())
            {
                return;
            }

            float current;
            PendingDrain.TryGetValue(cell, out current);
            PendingDrain[cell] = current + absorbedRads;
        }

        private static void DistributeDrain()
        {
            if (PendingDrain.Count == 0)
            {
                return;
            }

            int width = Grid.WidthInCells;
            foreach (KeyValuePair<int, float> pending in PendingDrain)
            {
                int cell = pending.Key;
                float absorbed = pending.Value;
                float measured = Grid.Radiation[cell];
                DrainTargets.Clear();
                float total = 0f;
                foreach (RadiationActivation source in Active)
                {
                    if (source == null || !(source.CurrentEmitRads > 0f) || source.EmitCell < 0)
                    {
                        continue;
                    }

                    float estimate = GeneratorDrainModel.EstimatedContribution(source.CurrentEmitRads,
                        CellDistance(cell, source.EmitCell, width), RadiationBalance.EmitRadiusCells);
                    if (estimate > 0f)
                    {
                        DrainTargets.Add(new KeyValuePair<RadiationActivation, float>(source, estimate));
                        total += estimate;
                    }
                }

                // 发生器所在格的物质若已显现，它的污染物只照本格，也按份额付账。
                float cellRads = table.Get(cell, AccumulationLayer.Cell) > 0f
                    ? ContaminationModel.SelfRads(Grid.DiseaseIdx[cell], Grid.DiseaseCount[cell], contaminantIdx)
                    : 0f;
                total += cellRads;

                for (int i = 0; i < DrainTargets.Count; i++)
                {
                    DrainTargets[i].Key.Drain(GeneratorDrainModel.Charge(absorbed, DrainTargets[i].Value, total, measured));
                }

                if (cellRads > 0f)
                {
                    float charge = GeneratorDrainModel.Charge(absorbed, cellRads, total, measured);
                    float next = AccumulationModel.Drain(table.Get(cell, AccumulationLayer.Cell), charge, RadiationBalance.CellDrainFraction);
                    table.Set(cell, AccumulationLayer.Cell, table.IdentityOf(cell, AccumulationLayer.Cell), next);
                }

                if (DrainTargets.Count > 0 || cellRads > 0f)
                {
                    ModLog.InfoOnce("radiation|drain", "辐射粒子发生器：吸收的辐射已开始由周围显现物付账。");
                }
            }

            DrainTargets.Clear();
            PendingDrain.Clear();
        }

        private static float CellDistance(int a, int b, int width)
        {
            if (width <= 0)
            {
                return float.MaxValue;
            }

            int dx = a % width - b % width;
            int dy = a / width - b / width;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        private static bool EnsureTable()
        {
            int count = Grid.CellCount;
            if (count <= 0)
            {
                return false;
            }

            if (table != null && table.CellCount == count)
            {
                return true;
            }

            if (table != null)
            {
                ModLog.Warn("辐射：世界格子数变化（" + table.CellCount + " → " + count + "），每格累积表重建。");
            }

            table = new AccumulationTable(count);
            nextCell = 0;
            passElapsed = 0f;
            ModLog.Info("辐射：每格累积表已建立（" + count + " 格，每格 4 份）。");
            return true;
        }

        private static bool RadiationEnabled()
        {
            if (!radiationChecked)
            {
                radiationChecked = true;
                try
                {
                    radiationEnabled = Sim.IsRadiationEnabled();
                }
                catch (Exception e)
                {
                    radiationEnabled = false;
                    ModLog.Warn("辐射：无法判断辐射是否开启，本局不启用：" + e.Message);
                }

                ModLog.Info(radiationEnabled
                    ? "辐射：本局已开启辐射，开始扫描累积与显现。"
                    : "辐射：本局未开启辐射（基础版或经典星球），累积与显现不运行。");
            }

            return radiationEnabled;
        }

        private static void ResolveContaminant()
        {
            if (contaminantResolved)
            {
                return;
            }

            contaminantResolved = true;
            try
            {
                Klei.AI.Disease disease = Db.Get().Diseases.RadiationPoisoning;
                contaminantIdx = disease == null ? ContaminationModel.NoDisease : Db.Get().Diseases.GetIndex(disease.id);
            }
            catch (Exception e)
            {
                contaminantIdx = ContaminationModel.NoDisease;
                ModLog.Warn("辐射：找不到原版放射性污染物：" + e.Message);
            }

            if (contaminantIdx == ContaminationModel.NoDisease)
            {
                ModLog.Warn("辐射：未找到放射性污染物病菌，格子、管道和物品显现后不会写入污染物；建筑显现照常进行。");
            }
            else
            {
                ModLog.Info("辐射：放射性污染物病菌编号 " + contaminantIdx + "。");
            }
        }
    }
}
