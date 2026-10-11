using KSerialization;
using Klei.AI;
using MaterialOverhaul.Core;
using MaterialOverhaul.Hardness;
using MaterialOverhaul.Heating;
using UnityEngine;

namespace MaterialOverhaul.Game
{
    /// <summary>
    /// 磨损计时、损坏原因与维修决策（V0.0.3 开发计划 §6）。
    ///
    /// 计时口径（§6.1）：累计的是**确实推进工作的真实时间**，不是「等效满效率工作量」。
    /// 复制人路径上原版把效率乘进 dt 后才回调 <c>OnWorkTick</c>，因此本类记录
    /// <c>GetEfficiencyMultiplier</c> 给出的最终倍率，用它把回调的 dt 还原成真实秒数。
    /// 效率 0.3 下实际工作 20 s 仍扣 1 HP，但完成同一批需要更久，磨损因此更多。
    ///
    /// 不累计的情形：预热、补热但未生产、等待工人、红信号、断电、温度暂停、维修与损坏停机。
    /// </summary>
    public partial class MaterialOverhaulFab
    {
        /// <summary>当前尚未修复的损坏原因（机械／过热／双重／迁移未知）。</summary>
        public DamageCause CurrentDamageCause
        {
            get { return DamageCauseEvaluator.Evaluate(damageState); }
        }

        /// <summary>本次维修是否应重置温度：含过热时为真，只有机械损坏时为假。</summary>
        public bool RepairResetsTemperature
        {
            get { return DamageCauseEvaluator.ResetsTemperatureOnRepair(CurrentDamageCause); }
        }

        /// <summary>是否已经存在机械磨损原因（用于界面区分「轻微磨损」与「损坏停机」）。</summary>
        public bool HasMechanicalWear
        {
            get { return damageState.MechanicalLatched || damageState.ForcedOverheatLatched; }
        }

        /// <summary>本体温度是否已经超过（本 Mod 规则下的）过热阈值。</summary>
        public bool IsAboveOverheatThreshold
        {
            get
            {
                return overheatable != null
                    && primaryElement != null
                    && primaryElement.Temperature > overheatable.OverheatTemperature;
            }
        }

        /// <summary>是否已经锁存过热原因。</summary>
        public bool HasOverheatDamage
        {
            get { return damageState.OverheatLatched; }
        }

        /// <summary>
        /// 主建造材料硬度是否低于要求硬度。
        /// 实际硬度取主建造材料（原版索引 0 的材料写入 PrimaryElement.ElementID），辅料不参与（§6.1）。
        /// 未列入要求硬度表的建筑不产生机械磨损（§6.2）。
        /// </summary>
        public bool HasHardnessDeficit()
        {
            if (primaryElement == null || primaryElement.Element == null)
            {
                return false;
            }

            byte required;
            if (!HardnessRequirementTable.TryGet(PrefabId, out required))
            {
                return false;
            }

            return primaryElement.Element.hardness < required;
        }

        /* ---- 生产计时 ---- */

        /// <summary>记录本步复制人操作效率的最终倍率（含温度效率与属性加成）。</summary>
        public void NoteEfficiencyMultiplier(float multiplier)
        {
            workEfficiencyMultiplier = multiplier;
        }

        /// <summary>
        /// 复制人路径的生产推进（原版 <c>ComplexFabricatorWorkable.OnWorkTick</c> 后置）。
        /// 收到的 dt 已经乘过效率，这里按记录的倍率还原真实秒数后再计磨损。
        /// </summary>
        public void OnDuplicantWorkTick(float scaledDt)
        {
            AccumulateProduction(DamageConversion.RealProductionSeconds(
                scaledDt, workEfficiencyMultiplier, Balance.MaxWearSecondsPerTick));
        }

        /// <summary>
        /// 非复制人路径的生产推进（原版 <c>ComputeWorkProgress</c> 收到的就是真实 dt）。
        /// 已投料批次与复制人路径互斥，因此不会重复累计。
        /// </summary>
        public void OnNonDuplicantWorkProgress(float realDt)
        {
            if (fabricator != null && fabricator.duplicantOperated)
            {
                return;
            }

            AccumulateProduction(realDt);
        }

        /// <summary>
        /// 按真实生产秒数累计机械磨损。只有硬度不足时才累计；
        /// 单步时长做上限保护，避免「极速工人」等调试选项把整批时间算成一次磨损。
        /// </summary>
        public void AccumulateProduction(float realSeconds)
        {
            if (realSeconds <= 0f || !HasHardnessDeficit() || !DamageConversion.IsConfigured)
            {
                return;
            }

            float capped = realSeconds > Balance.MaxWearSecondsPerTick ? Balance.MaxWearSecondsPerTick : realSeconds;
            DamageConversion.AccumulateProduction(wear, capped);
            SyncWearField();
        }

        /* ---- 损坏原因 ---- */

        /// <summary>
        /// 每个模拟步的损坏处理：检测过热、把累计磨损折算成伤害、维护当前原因。
        ///
        /// 过热检测**独立于生命值**：建筑损坏停机后仍要继续检测，才能把环境升温造成的过热
        /// 追加进原因（原版过热组件在建筑损坏后进入免伤状态，只依赖扣血事件无法更新原因）。
        /// </summary>
        public void TickDamage()
        {
            // 兜底：spawn 时主建造材料还没就绪（元素缺失等）时补一次，避免高温柔区运行却仍用原版阈值。
            if (!overheatApplied)
            {
                ApplyOverheatRule();
            }

            UpdateOverheatState();
            ClearDamageWhenFullyRepaired();
            ApplyMechanicalWear();

            DamageCause cause = CurrentDamageCause;
            if (hasLoggedDamageCause && cause == lastLoggedDamageCause)
            {
                return;
            }

            hasLoggedDamageCause = true;
            lastLoggedDamageCause = cause;
            OnDamageCauseChanged(cause);
        }

        /// <summary>
        /// 更新过热状态。普通运行时**只在真正发生过热扣血时**锁存原因
        /// （由 <see cref="LatchOverheatDamage"/> 在伤害事件里调用），
        /// 因此「刚超过阈值但还没扣血」不会被写成「已过热受损」。
        ///
        /// 生命值已经耗尽（损坏停机）后原版进入免伤状态、不再发伤害事件，
        /// 这时才改按本体温度是否超过阈值追加过热原因（§6.2）。
        /// </summary>
        private void UpdateOverheatState()
        {
            if (overheatable == null || primaryElement == null || damageState.OverheatLatched)
            {
                return;
            }

            if (buildingHP != null && !buildingHP.IsBroken)
            {
                return;
            }

            if (IsAboveOverheatThreshold)
            {
                LatchOverheatDamage();
            }
        }

        /// <summary>
        /// 锁存过热损坏原因。强制运行期间发生的过热损坏会额外锁存一条「附加机械显示原因」，
        /// 使硬度合格的材料也同时显示两种原因；这只是显示，不额外制造机械伤害。
        /// 锁存只看**事件发生当时**的强制开关，关闭开关不会丢原因，也不会伪造一次强制过热。
        /// </summary>
        public void LatchOverheatDamage()
        {
            if (damageState.OverheatLatched && (!forcedRun || damageState.ForcedOverheatLatched))
            {
                return;
            }

            damageState.OverheatLatched = true;
            if (forcedRun)
            {
                damageState.ForcedOverheatLatched = true;
            }

            SyncDamageFields();
        }

        /// <summary>
        /// 消费「本次扣血来自本 Mod 机械磨损」的标记。伤害事件处理器用它区分来源：
        /// 不是本 Mod 发的扣血事件才可能是过热伤害。
        /// </summary>
        public bool ConsumeMechanicalDamageMark()
        {
            bool marked = mechanicalDamageMark;
            mechanicalDamageMark = false;
            return marked;
        }

        private void ClearDamageWhenFullyRepaired()
        {
            if (buildingHP != null && !buildingHP.NeedsRepairs && !damageState.IsClean)
            {
                damageState.Clear();
                SyncDamageFields();
            }
        }

        /// <summary>
        /// 把累计磨损折算成生命值伤害。整点伤害从余额中精确扣除，小数余额保留；
        /// 生命值已耗尽时不再扣减，但仍保留余额与原因（不继续扣血，不代表磨损消失）。
        /// </summary>
        private void ApplyMechanicalWear()
        {
            if (buildingHP == null || buildingHP.IsBroken)
            {
                return;
            }

            int damage = wear.TakeWholeDamage();
            if (damage <= 0)
            {
                return;
            }

            SyncWearField();

            // 首次实际扣血就记录机械原因，不等到生命值耗尽。
            if (!damageState.MechanicalLatched)
            {
                damageState.MechanicalLatched = true;
                SyncDamageFields();
            }

            BuildingHP.DamageSourceInfo info = default(BuildingHP.DamageSourceInfo);
            info.damage = damage;
            info.source = LocalizedOr(KeyDamageSourceMechanical, "Mechanical Wear");
            mechanicalDamageMark = true;
            BoxingTrigger(DoBuildingDamageHash, info);
            mechanicalDamageMark = false;
        }

        /// <summary>
        /// 损坏原因变化时更新状态项。原版只保存一份 <c>damageSourceInfo</c>，
        /// 所以这里由本 Mod 按当前原因显示，双重原因显示合并状态项，且不占用 Main 类别，
        /// 避免把原版断电、缺料等关键状态遮掉。
        /// </summary>
        private void OnDamageCauseChanged(DamageCause cause)
        {
            UpdateDamageStatusItem(cause);

            if (cause == DamageCause.None)
            {
                return;
            }

            ModLog.Info(PrefabId + " 损坏原因：" + cause
                + "；过热维修 " + DamageCauseEvaluator.UsesOverheatRepair(cause)
                + "，重置温度 " + DamageCauseEvaluator.ResetsTemperatureOnRepair(cause)
                + "，显示两种原因 " + DamageCauseEvaluator.ShowsBothCauses(cause) + "。");
        }

        private void UpdateDamageStatusItem(DamageCause cause)
        {
            if (selectable == null || DamageStatusItems.Mechanical == null)
            {
                // 状态项尚未登记（Db 未初始化）时跳过，不影响其他逻辑。
                return;
            }

            selectable.RemoveStatusItem(DamageStatusItems.Mechanical);
            selectable.RemoveStatusItem(DamageStatusItems.Overheat);
            selectable.RemoveStatusItem(DamageStatusItems.Both);
            selectable.RemoveStatusItem(DamageStatusItems.Unknown);

            StatusItem item = DamageStatusItems.For(cause);
            if (item != null)
            {
                selectable.SetStatusItem(DamageStatusItems.Category, item, this);
            }
        }

        /* ---- 过热阈值（主建造材料熔点 × 85%）---- */

        /// <summary>
        /// 取本实例按规则算出的过热阈值与显示来源：主建造材料名、材料熔点。
        ///
        /// 未登记过热规则的建筑、没有本体元素、或熔点不可用时返回 false，
        /// 调用方保持原版显示（信息卡里的过热温度行就不会被改写）。
        /// </summary>
        public bool TryGetOverheatRuleValue(out float overheatKelvin, out string materialName, out float meltingPointKelvin)
        {
            overheatKelvin = 0f;
            materialName = null;
            meltingPointKelvin = 0f;

            if (primaryElement == null)
            {
                return false;
            }

            Element element = primaryElement.Element;
            if (element == null)
            {
                return false;
            }

            float fraction;
            if (!OverheatRuleTable.TryGet(PrefabId, out fraction))
            {
                return false;
            }

            if (!OverheatRule.TryCompute(element.highTemp, fraction, out overheatKelvin))
            {
                return false;
            }

            materialName = element.name;
            meltingPointKelvin = element.highTemp;
            return true;
        }

        /// <summary>
        /// 按「主建造材料熔点 × 规则比例」改写本建筑的过热阈值。
        ///
        /// 阈值由**建造材料**而不是配方决定：同一栋建筑换成熔点更高的材料就能在更高温度下工作，
        /// 这是材料选择的一部分代价。做法是保留原版 <c>Overheatable</c> 的行为，
        /// 只把属性总值补偿到规则值，并让结构温度重新注册，使 SIM 使用新阈值。
        ///
        /// 幂等：值没变时不做任何事；两处调用（组件 OnSpawn 与 Overheatable.OnSpawn）谁先谁后都成立。
        /// </summary>
        public void ApplyOverheatRule()
        {
            if (overheatable == null || primaryElement == null)
            {
                return;
            }

            Element element = primaryElement.Element;
            if (element == null)
            {
                return;
            }

            float value;
            string materialName;
            float meltingPoint;
            if (!TryGetOverheatRuleValue(out value, out materialName, out meltingPoint))
            {
                float fraction;
                if (OverheatRuleTable.TryGet(PrefabId, out fraction))
                {
                    ModLog.WarnOnce("overheat|bad-melting|" + PrefabId,
                        PrefabId + "：主建造材料 " + element.name + " 的熔点 " + element.highTemp
                        + " K 不可用，过热阈值保持原版设定。");
                }

                return;
            }

            if (overheatApplied && Mathf.Abs(value - overheatTemperature) < 0.01f)
            {
                return;
            }

            overheatApplied = true;
            overheatTemperature = value;

            // 先写基础值，再读一次属性：这会触发原版 InitializeModifiers，
            // 之后补偿差额不会因为修饰项在别处建立而重复叠加。
            overheatable.baseOverheatTemp = value;
            float current = overheatable.OverheatTemperature;

            if (overheatable.GetAttributes() == null || Db.Get() == null)
            {
                return;
            }

            AttributeInstance instance = overheatable.GetAttributes().Get(Db.Get().BuildingAttributes.OverheatTemperature);
            if (instance == null)
            {
                ModLog.WarnOnce("overheat|no-attribute|" + PrefabId,
                    PrefabId + "：找不到过热温度属性，无法把阈值设为规则值（当前 " + current.ToString("F1") + " K）。");
                return;
            }

            if (overheatModifier == null)
            {
                overheatModifier = new AttributeModifier(
                    Db.Get().BuildingAttributes.OverheatTemperature.Id,
                    0f,
                    Strings.Get("STRINGS.BUILDING.ATTRIBUTES.MATERIALOVERHAUL.OVERHEAT.DESCRIPTION"));
                instance.Add(overheatModifier);
            }

            // 把原版基础值与建造材料自带的过热修正一起补偿掉，使属性总值恰好等于规则值。
            float fixedPart = instance.GetTotalValue() - overheatModifier.Value;
            overheatModifier.SetValue(value - fixedPart);

            MarkStructureTemperatureDirty();

            float appliedFraction;
            if (!OverheatRuleTable.TryGet(PrefabId, out appliedFraction))
            {
                appliedFraction = OverheatRule.MeltingPointFraction;
            }

            ModLog.Info(PrefabId + "：主建造材料 " + materialName + " 熔点 " + meltingPoint.ToString("F1")
                + " K，过热阈值设为 " + value.ToString("F1") + " K（熔点 × " + appliedFraction.ToString("F2") + "）。");
        }

        /// <summary>
        /// 让原版把新的过热阈值推给 SIM：<c>Disable</c> + <c>Enable</c> 会把结构温度标脏，
        /// 原版 <c>UpdateSimState</c> 随后重新读取 <c>Overheatable.OverheatTemperature</c>。
        /// </summary>
        private void MarkStructureTemperatureDirty()
        {
            if (!structureHandleResolved)
            {
                structureHandle = GameComps.StructureTemperatures.GetHandle(gameObject);
                structureHandleResolved = true;
            }

            if (!structureHandle.IsValid() || !GameComps.StructureTemperatures.IsEnabled(structureHandle))
            {
                // 结构温度还没注册：原版注册时会读取当前属性值，因此不需要在这里处理。
                return;
            }

            GameComps.StructureTemperatures.Disable(structureHandle);
            GameComps.StructureTemperatures.Enable(structureHandle);
        }
    }
}
