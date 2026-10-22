using System;
using System.Collections.Generic;
using MaterialOverhaul.Core;
using MaterialOverhaul.Radiation;
using UnityEngine;

namespace MaterialOverhaul.Game
{
    /// <summary>
    /// 感生放射性的统一扫描（设计方案 §9）。
    ///
    /// 每秒扫描世界格子的五分之一，五秒扫完一遍；只处理辐射超过门槛的格子：
    /// - 格子上的建筑与建造砖块累积受照量，一遍扫完后统一步进活化度；
    /// - 格子里的液体、气体、天然固体按受照量添加原版放射性污染物，已有其他病菌时先清除（污染物优先）。
    ///
    /// 计算受照时扣掉活化物自身贡献的上界（附近活化建筑的放射 + 本格污染物的放射），
    /// 活化物之间不能互相维持，离开真正的辐射源后一定会衰减。
    /// 未开启辐射（基础版或经典星球）时整个扫描不运行。
    /// </summary>
    public static class RadiationInducedMonitor
    {
        private static readonly InducedSourceIndex Sources = new InducedSourceIndex();
        private static readonly HashSet<RadiationActivation> Active = new HashSet<RadiationActivation>();
        private static readonly Dictionary<RadiationActivation, float> Exposure = new Dictionary<RadiationActivation, float>();
        private static readonly List<RadiationActivation> Buffer = new List<RadiationActivation>();

        private static int nextCell;
        private static float passElapsed;
        private static bool radiationChecked;
        private static bool radiationEnabled;
        private static bool contaminantResolved;
        private static byte contaminantIdx = ContaminationModel.NoDisease;

        /// <summary>读档或退出时清空静态状态。</summary>
        public static void Clear()
        {
            Sources.Clear();
            Active.Clear();
            Exposure.Clear();
            Buffer.Clear();
            nextCell = 0;
            passElapsed = 0f;
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

        /// <summary>活化建筑的放射强度或位置变化时更新位置索引。</summary>
        public static void MoveSource(int oldCell, float oldRads, int newCell, float newRads)
        {
            if (oldCell >= 0 && oldRads > 0f)
            {
                Sources.Add(oldCell, -oldRads);
            }

            if (newCell >= 0 && newRads > 0f)
            {
                Sources.Add(newCell, newRads);
            }
        }

        /// <summary>由 1 秒一次的模拟更新驱动。</summary>
        public static void Tick(float dt)
        {
            if (!RadiationEnabled() || !(dt > 0f))
            {
                return;
            }

            int total = Grid.CellCount;
            if (total <= 0)
            {
                return;
            }

            ResolveContaminant();
            int slices = RadiationBalance.ScanSlicesPerPass;
            int sliceSize = (total + slices - 1) / slices;
            int end = Math.Min(total, nextCell + sliceSize);
            float cellDt = dt * slices;
            int width = Grid.WidthInCells;
            for (int cell = nextCell; cell < end; cell++)
            {
                ScanCell(cell, width, cellDt);
            }

            nextCell = end;
            passElapsed += dt;
            if (nextCell >= total)
            {
                FinishPass(passElapsed);
                nextCell = 0;
                passElapsed = 0f;
            }
        }

        private static void ScanCell(int cell, int width, float cellDt)
        {
            float rads = Grid.Radiation[cell];
            if (!(rads > RadiationBalance.ActivationThresholdRads))
            {
                return;
            }

            byte idx = Grid.DiseaseIdx[cell];
            int count = Grid.DiseaseCount[cell];
            float induced = Sources.UpperBound(cell, width, RadiationBalance.InducedSearchRadiusCells)
                + ContaminationModel.SelfRads(idx, count, contaminantIdx);
            float excess = ActivationModel.EffectiveExcess(rads, induced);
            if (!(excess > 0f))
            {
                return;
            }

            GameObject building = Grid.Objects[cell, (int)ObjectLayer.Building];
            AddExposure(building, excess);
            GameObject tile = Grid.Objects[cell, (int)ObjectLayer.FoundationTile];
            if (tile != building)
            {
                AddExposure(tile, excess);
            }

            // 建造砖块的格子由砖块自身的活化度表示，不再重复添加污染物。
            if (!Grid.Foundation[cell])
            {
                Contaminate(cell, excess, idx, count, cellDt);
            }
        }

        private static void AddExposure(GameObject go, float excess)
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

            float current;
            Exposure.TryGetValue(activation, out current);
            Exposure[activation] = current + excess / activation.CellCount;
        }

        private static void Contaminate(int cell, float excess, byte idx, int count, float dt)
        {
            if (contaminantIdx == ContaminationModel.NoDisease)
            {
                return;
            }

            Element element = Grid.Element[cell];
            CellPhase phase = PhaseOf(element);
            float mass = ContaminationModel.EffectiveMassKg(Grid.Mass[cell], phase);
            int toAdd = ContaminationModel.ContaminantsToAdd(mass, excess, dt);
            ContaminationPlan plan = ContaminationModel.Plan(idx, count, contaminantIdx, toAdd);
            if (!plan.HasWork)
            {
                return;
            }

            if (plan.ClearIdx != ContaminationModel.NoDisease)
            {
                SimMessages.ModifyDiseaseOnCell(cell, plan.ClearIdx, -plan.ClearCount);
                ModLog.InfoOnce("radiation|contaminate|clear", "感生放射：在强辐射格子中清除其他病菌后写入放射性污染物（污染物优先）。");
            }

            SimMessages.ModifyDiseaseOnCell(cell, contaminantIdx, plan.AddCount);
            ModLog.InfoOnce("radiation|contaminate", "感生放射：已开始向强辐射格子中的物质添加放射性污染物。");
        }

        private static CellPhase PhaseOf(Element element)
        {
            if (element == null || element.IsVacuum)
            {
                return CellPhase.Vacuum;
            }

            if (element.IsGas)
            {
                return CellPhase.Gas;
            }

            return element.IsLiquid ? CellPhase.Liquid : CellPhase.Solid;
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

                float excess;
                Exposure.TryGetValue(activation, out excess);
                activation.ApplyStep(excess, passSeconds);
            }

            if (Buffer.Count > 0)
            {
                ModLog.InfoOnce("radiation|activation", "感生放射：建筑活化度已开始按受照量步进（本轮 " + Buffer.Count + " 栋）。");
            }

            Exposure.Clear();
            Buffer.Clear();
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
                    ModLog.Warn("感生放射：无法判断辐射是否开启，本局不启用：" + e.Message);
                }

                ModLog.Info(radiationEnabled
                    ? "感生放射：本局已开启辐射，开始扫描强辐射区域。"
                    : "感生放射：本局未开启辐射（基础版或经典星球），感生放射不运行。");
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
                ModLog.Warn("感生放射：找不到原版放射性污染物：" + e.Message);
            }

            if (contaminantIdx == ContaminationModel.NoDisease)
            {
                ModLog.Warn("感生放射：未找到放射性污染物病菌，液体、气体与天然格不会被污染；建筑活化照常进行。");
            }
            else
            {
                ModLog.Info("感生放射：放射性污染物病菌编号 " + contaminantIdx + "。");
            }
        }
    }
}
