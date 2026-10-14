using MaterialOverhaul.Core;
using MaterialOverhaul.Heating;
using MaterialOverhaul.Zones;
using UnityEngine;

namespace MaterialOverhaul.Game
{
    /// <summary>
    /// 运行状态机接入、加热注热与电网需求（V0.0.3 开发计划 §3、§4）。
    ///
    /// 三件事在这里分开处理（§3.1）：
    ///   1. **批次准备**由原版 <c>StartWorkingOrder</c> 完成，温度与自动化都不阻止它取料与锁定；
    ///   2. **加热许可与意图**由 <see cref="RunController"/> 给出，本类再叠加批次、供电、手动禁用与损坏，
    ///      并把需求交给电网（未获供电时需求仍然存在，但不注热）；
    ///   3. **真实生产**以「操作任务确实在推进」为准：温度不允许时撤销操作任务、释放复制人、
    ///      关掉原版生产活动状态（否则 16 kW 生产热与生产电耗会跟着一起启动）。
    /// </summary>
    public partial class MaterialOverhaulFab
    {
        /// <summary>本建筑的功率读数是否由本 Mod 接管（只有明确纳入加热范围的建筑）。</summary>
        public bool HasPowerOverride
        {
            get { return SupportsBuiltInHeating; }
        }

        /// <summary>是否存在已锁定批次。</summary>
        public bool HasLockedBatch
        {
            get { return fabricator != null && fabricator.CurrentWorkingOrder != null; }
        }

        /// <summary>
        /// 是否挂上了「允许／暂停生产」自动化输入端口。
        /// 没挂端口的建筑（本版只有金属精炼器挂了）不参与自动化判断，行为等同原版。
        /// </summary>
        public bool HasAutomationInput
        {
            get { return hasAutomationInput; }
        }

        /// <summary>
        /// 自动化是否允许生产（建筑细节文档 §2.2.3）。绿色允许、红色暂停。
        ///
        /// 读的是原版 <see cref="LogicOperationalController.LogicOperationalFlag"/>：
        /// 它由原版组件按「未接线 → <c>unNetworkedValue = 1</c> → 允许」写入，
        /// 因此**没有接线的建筑默认放行**，不会因为新增端口而变成默认停产。
        /// 标记尚未初始化时同样按放行处理。
        /// </summary>
        public bool AutomationAllowsProduction
        {
            get
            {
                if (!hasAutomationInput)
                {
                    return true;
                }

                bool known = false;
                bool allowed = true;
                if (operational != null && operational.Flags != null)
                {
                    known = operational.Flags.TryGetValue(LogicOperationalController.LogicOperationalFlag, out allowed);
                }

                return AutomationGate.AllowsProduction(hasAutomationInput, known, allowed);
            }
        }

        /// <summary>自动化是否为红信号（仅当建筑确实挂了输入端口时才可能为真）。</summary>
        public bool AutomationRed
        {
            get { return hasAutomationInput && !AutomationAllowsProduction; }
        }

        /// <summary>
        /// 自动化红信号的**唯一**来源判定：机器本身可用（供电、未禁用、未损坏），
        /// 只是自动化给了红信号。原版「不可运行就取消订单需求与取料」的行为只在不是这种情况时使用。
        /// </summary>
        public bool BlockedByAutomationOnly
        {
            get
            {
                bool known = false;
                bool allowed = true;
                if (operational != null && operational.Flags != null)
                {
                    known = operational.Flags.TryGetValue(LogicOperationalController.LogicOperationalFlag, out allowed);
                }

                return AutomationGate.IsAutomationOnlyBlock(
                    hasAutomationInput,
                    known,
                    allowed,
                    IsPowered,
                    IsManuallyDisabled,
                    IsBrokenDown,
                    operational == null || operational.IsFunctional);
            }
        }

        /// <summary>是否已有操作工到位；非复制人操作的建筑视为恒真。</summary>
        public bool HasWorker
        {
            get
            {
                if (fabricator == null || !fabricator.duplicantOperated)
                {
                    return true;
                }

                return workable != null && workable.worker != null;
            }
        }

        /// <summary>
        /// 评估该配方的生产许可与温度效率。配方标识变化时才重新查侧表，避免每次调用查表与刷日志。
        /// 效率曲线由判断层给出（<see cref="RunController"/>），模式与强制运行只影响生产许可。
        /// </summary>
        public ProductionDecision EvaluateFor(ComplexRecipe recipe)
        {
            if (recipe == null)
            {
                return ProductionDecision.Passthrough;
            }

            UpdateCachedZone(recipe.id);
            if (!cachedZoneConfigured)
            {
                return ProductionDecision.Passthrough;
            }

            RefreshModeFromOutputConnection();
            if (!modeConnectionReady)
            {
                ProductionDecision pending = ProductionDecision.Passthrough;
                pending.ZoneConfigured = true;
                pending.AllowsProduction = false;
                return pending;
            }

            // 操作效率可能早于 Sim200ms 被读取，按本次温度和最新模式评估。
            return ToProduction(RunController.Update(BodyTemperature, cachedZone, Policy, runState));
        }

        private static ProductionDecision ToProduction(RunDecision decision)
        {
            ProductionDecision production = default(ProductionDecision);
            production.State = decision.State;
            production.Efficiency = decision.Efficiency;
            production.AllowsProduction = decision.AllowsProduction;
            production.ZoneConfigured = decision.ZoneConfigured;
            return production;
        }

        private void UpdateCachedZone(string recipeId)
        {
            if (recipeId == cachedRecipeId)
            {
                return;
            }

            cachedRecipeId = recipeId;
            cachedRecipeZone = RecipeZoneTable.GetOrUnconfigured(recipeId);

            if (!cachedRecipeZone.Configured && !ContentRegistration.ZonesResolved)
            {
                // 兜底：配方温区还没在「配方表可用」的时机登记过（加载顺序变化或钩子未命中）时，
                // 第一次有建筑要用配方时再补一次，避免整套温区静默失效。
                ContentRegistration.Apply();
                cachedRecipeZone = RecipeZoneTable.GetOrUnconfigured(recipeId);
            }

            ResolveCachedZone(recipeId);
        }

        /// <summary>
        /// 把「建筑下限 L + 配方最佳区 + 主材料上限 U」合成可运算温区（V0.1.2 §2.1）。
        ///
        /// 三步来源各自独立：切配方只换配方最佳区，换建材只换 U，两者都不会改动对方的数值；
        /// 任何一步缺失都退化为未配置（原版行为），并留下可定位日志（在判断层打印）。
        /// </summary>
        private void ResolveCachedZone(string recipeId)
        {
            float floorKelvin;
            if (!BuildingZoneTable.TryGet(PrefabId, out floorKelvin))
            {
                cachedZone = ResolvedZone.Unconfigured;
                cachedZoneConfigured = false;

                if (cachedRecipeZone.Configured)
                {
                    ModLog.WarnOnce("zone|no-floor|" + PrefabId,
                        PrefabId + "：配方 '" + recipeId + "' 已登记最佳温区，但本建筑没有最低工作温度数值，"
                        + "该建筑按原版行为运行。");
                }

                return;
            }

            cachedZone = TempZoneResolver.Resolve(
                PrefabId + "|" + recipeId, floorKelvin, cachedRecipeZone, TryGetHeatCeiling);
            cachedZoneConfigured = cachedZone.Configured;
        }

        /// <summary>
        /// 主建造材料变化后重算建筑上限（V0.1.2 §2.1、§2.5 的建材口径）。
        /// 只在主材料元素或熔点真的变了时才动过热阈值与温区，避免每步重建。
        ///
        /// 换建材只改变上限 U，**不改变配方最佳区**：`cachedRecipeZone` 保持不动，
        /// 重算后再按新模式重判启动资格与目标线。
        /// </summary>
        private void RefreshZoneForMaterial()
        {
            if (!Spawned || primaryElement == null || primaryElement.Element == null)
            {
                return;
            }

            Element element = primaryElement.Element;
            if (!materialRuleKnown || element != materialRuleElement)
            {
                ApplyOverheatRule();

                // ApplyOverheatRule 只处理规则值本身；这里负责把温区与目标线一起更新。
                // 即使规则应用失败（熔点不可用）也要记下材料，避免每步重复尝试。
                materialRuleElement = element;
                materialRuleMeltingPoint = element.highTemp;
                materialRuleKnown = true;

                if (cachedRecipeZone.Configured)
                {
                    ResolveCachedZone(cachedRecipeId);
                    runState = RunController.OnPolicyChanged(BodyTemperature, cachedZone, Policy, runState);
                    SyncRunStateFields();
                }

                return;
            }

            if (materialRuleMeltingPoint != element.highTemp)
            {
                ApplyOverheatRule();
                materialRuleMeltingPoint = element.highTemp;
            }
        }

        /// <summary>
        /// 每个模拟步的完整接入（补丁挂在 <c>ComplexFabricator.Sim200ms</c> 上）。
        /// 顺序固定：先算运行状态，再算电力与注热，再执行生产闸门，最后处理磨损与损坏。
        /// </summary>
        public void SimStep(float dt)
        {
            if (fabricator == null)
            {
                return;
            }

            EnsureOrdersImported();
            RefreshModeFromOutputConnection();
            RefreshZoneForMaterial();
            UpdateModeStatusItem();
            if (!modeConnectionReady)
            {
                powerDemand = default(PowerDemand);
                currentRequestedWatts = 0f;
                currentUsedWatts = 0f;
                EnforceProductionGate();
                TickDamage();
                return;
            }
            // 先清理完全修复的原因，再判断本步加热；损坏检测仍在步末执行。
            ClearDamageWhenFullyRepaired();
            UpdateCoolingTrend(dt);
            UpdateRunState();
            PrepareBatchWhenAutomationBlocks();
            UpdatePowerAndHeating(dt);
            EnforceProductionGate();
            TickDamage();
            UpdateRunStatusItem();
            UpdateMaterialRiskStatusItems();
        }

        private void UpdateRunState()
        {
            ComplexRecipe working = CurrentWorkingRecipe;
            if (working == null)
            {
                cachedRecipeId = null;
                cachedRecipeZone = TempZone.Unconfigured;
                cachedZone = ResolvedZone.Unconfigured;
                cachedZoneConfigured = false;
                CurrentState = TempState.Disabled;
                CurrentEfficiency = 1f;
                runDecision = RunDecision.Passthrough;
                hasLoggedState = false;
                powerDemand = default(PowerDemand);
                currentRequestedWatts = 0f;
                currentUsedWatts = 0f;
                UpdateTemperatureZoneSignal();
                return;
            }

            UpdateCachedZone(working.id);
            runDecision = RunController.Update(BodyTemperature, cachedZone, Policy, runState);

            if (runDecision.ZoneConfigured)
            {
                runState = runDecision.Next;
                SyncRunStateFields();
            }

            CurrentState = runDecision.State;
            CurrentEfficiency = runDecision.Efficiency;

            LogStateIfChanged(working);
            UpdateTemperatureZoneSignal();
        }

        private void UpdatePowerAndHeating(float dt)
        {
            HeatingContext heatingContext = CreateHeatingContext();

            HeatingIntent commanded = HeatingIntent.None;
            if (SupportsBuiltInHeating && cachedZoneConfigured && HeatingConditions.CanRequestHeat(heatingContext))
            {
                commanded = runDecision.Heating;
            }

            PowerDemandContext context = default(PowerDemandContext);
            context.HasLockedBatch = HasLockedBatch;
            context.PowerAvailable = IsPowered;
            context.ManuallyDisabled = IsManuallyDisabled;
            context.BrokenDown = IsBrokenDown;
            context.OverheatDamaged = HasOverheatDamage;
            context.TemperatureAllowsProduction = runDecision.AllowsProduction;
            context.AutomationAllowsProduction = AutomationAllowsProduction;
            context.HasWorker = HasWorker;
            context.CommandedHeating = commanded;
            context.ConfiguredHeatingWatts = HeatingWatts;

            powerDemand = PowerDemand.Evaluate(context);

            if (HasPowerOverride)
            {
                // 交给电网的是**需求**：断电时消费为 0，但需求必须保留，来电后才能重新请求。
                currentRequestedWatts = powerDemand.RequestedTotalWatts;
                currentUsedWatts = powerDemand.UsedTotalWatts;
            }
            else
            {
                currentRequestedWatts = 0f;
                currentUsedWatts = 0f;
            }

            if (powerDemand.AppliedHeating != HeatingIntent.None)
            {
                InjectHeat(powerDemand.AppliedHeating, dt);
            }
        }

        /// <summary>
        /// 生产闸门：温度不允许生产时撤销操作任务并释放复制人，同时关掉原版生产活动状态。
        ///
        /// 只把效率乘为零是不够的（§1.2）：原版 <c>Sim200ms</c> 按 <c>HasWorkingOrder && HasWorker</c>
        /// 设活动状态，只改效率不会取消活动状态，复制人仍被占用、16 kW 生产热与生产电耗照旧。
        /// 这里不调用任何取消批次的路径：批次、原料与进度全部保留。
        /// </summary>
        private void EnforceProductionGate()
        {
            if (fabricator == null)
            {
                return;
            }

            if (!HasLockedBatch)
            {
                productionPausedByTemperature = false;
                return;
            }

            bool allowProduction = modeConnectionReady && runDecision.AllowsProduction;

            if (!allowProduction)
            {
                // 原版本步可能已经把活动状态设为 true（它还认为有工作），这里明确关掉，
                // 避免生产热与生产电耗在本步被计入。
                PauseOperationForTemperature();
                return;
            }

            if (productionPausedByTemperature)
            {
                productionPausedByTemperature = false;
                // 只重建操作任务，不重新投料、不重新结算。
                FabricatorAccess.UpdateChore(fabricator);
                ModLog.Info(PrefabId + "：温度条件恢复，已重建操作任务（不重新投料）。");
            }
        }

        private void SetOperationalActive(bool active)
        {
            if (operational != null && operational.IsActive != active)
            {
                operational.SetActive(active);
            }
        }

        /* ---- 信息卡状态栏（V0.0.4 开发计划 §2.3）---- */

        /// <summary>
        /// 把运行状态写进建筑信息卡的状态栏。只在状态分类真的变化时动状态项，
        /// 不在每个模拟步反复增删（AGENTS 约定：不每步刷日志、不做无意义重建）。
        /// </summary>
        private void UpdateRunStatusItem()
        {
            if (selectable == null || RunStatusItem.Item == null || RunStatusItem.Category == null)
            {
                return;
            }

            RunStatusKind kind = RunStatusText.Classify(this);
            bool shouldShow = RunStatusItem.ShouldShow(this, kind);

            if (hasRunStatusItemApplied && shouldShow == lastAppliedRunStatusVisible && kind == lastAppliedRunStatusKind)
            {
                return;
            }

            hasRunStatusItemApplied = true;
            lastAppliedRunStatusKind = kind;
            lastAppliedRunStatusVisible = shouldShow;

            // SetStatusItem 会先移除同类别里的旧条目再按新条目重建，因此状态项文本随最新状态刷新。
            selectable.SetStatusItem(RunStatusItem.Category, shouldShow ? RunStatusItem.Item : null, this);
        }

        /// <summary>
        /// 注入热量。走原版结构温度接口：传入的是 kJ，温升由 SIM 按建造材料热容与换热计算，
        /// 不是固定温升，也不是直接设目标温度。
        ///
        /// 普通预热／补热达到目标时用剩余所需热量限制末步注热；**强制持续加热不封顶**（§2.1）。
        /// </summary>
        private void InjectHeat(HeatingIntent intent, float dt)
        {
            // 使用本步实际计入消费的功率，避免设置变化时电网与注热取不同值。
            float kilowatts = Balance.KilowattsForWatts(powerDemand.UsedHeatingWatts);
            if (kilowatts == 0f)
            {
                return;
            }

            float kilojoules = kilowatts * dt;

            if (intent != HeatingIntent.Sustained)
            {
                kilojoules = CapToTarget(intent, kilojoules);
                if (kilojoules <= 0f)
                {
                    return;
                }
            }

            if (!structureHandleResolved)
            {
                structureHandle = GameComps.StructureTemperatures.GetHandle(gameObject);
                structureHandleResolved = true;
            }

            if (!structureHandle.IsValid())
            {
                ModLog.WarnOnce("heat|no-handle|" + PrefabId,
                    PrefabId + "：结构温度句柄无效，本步未注入热量。");
                return;
            }

            GameComps.StructureTemperatures.ProduceEnergy(structureHandle, kilojoules, HeatSourceDescription, dt);
        }

        /// <summary>
        /// 按剩余所需热量限制本步注热：剩余热量 =（目标温度 − 当前温度）× 原版有效热容。
        /// 温升仍交给 SIM，这里只避免末步大幅越过目标。热容不可用时不做封顶。
        ///
        /// V0.1.2：封顶用的是**原目标**（默认模式 L + 0.1 × 低温段，控制模式 B₁ + 0.1 × 最佳区宽度），
        /// 不是封顶后的实际目标。这样建材上限低于原目标时仍能一路预热到 U，
        /// 而不会在「刚越过 U」的那一步因为剩余热量归零而停止注热（实际目标由状态的启动线把关）。
        /// 普通预热／补热都不允许越过建筑上限，强制持续加热不封顶。
        /// </summary>
        private float CapToTarget(HeatingIntent intent, float requestedKilojoules)
        {
            if (building == null || building.Def == null || primaryElement == null || primaryElement.Element == null)
            {
                return requestedKilojoules;
            }

            float target;
            if (intent == HeatingIntent.Preheat)
            {
                target = StartThreshold.Original(cachedZone, Policy);
            }
            else
            {
                target = HeatingPlanner.TopUpStopLine(cachedZone, Policy);
            }

            float remaining = target - BodyTemperature;
            if (remaining <= 0f)
            {
                return 0f;
            }

            float heatCapacity = building.Def.MassForTemperatureModification * primaryElement.Element.specificHeatCapacity;
            if (heatCapacity <= 0f || float.IsNaN(heatCapacity) || float.IsInfinity(heatCapacity))
            {
                return requestedKilojoules;
            }

            float maxKilojoules = remaining * heatCapacity;
            return requestedKilojoules > maxKilojoules ? maxKilojoules : requestedKilojoules;
        }

        /// <summary>热量来源说明，供原版显示运行能量来源。数值口径见 <see cref="Balance"/>。</summary>
        private string HeatSourceDescription
        {
            get { return LocalizedOr("STRINGS.UI.BUILDINGEFFECTS.MATERIALOVERHAUL.HEAT_SOURCE", "材料革新：内置加热"); }
        }

        /* ---- 降温趋势（只影响显示）---- */

        private void UpdateCoolingTrend(float dt)
        {
            coolingSampleTimer += dt;
            if (!coolingSampleValid)
            {
                coolingSampleValid = true;
                coolingSampleTemperature = BodyTemperature;
                coolingSampleTimer = 0f;
                return;
            }

            if (coolingSampleTimer < Balance.CoolingTrendSampleSeconds)
            {
                return;
            }

            float drop = coolingSampleTemperature - BodyTemperature;
            cooling = drop > Balance.CoolingTrendKelvin;
            coolingSampleTemperature = BodyTemperature;
            coolingSampleTimer = 0f;
        }

        /* ---- 温区输出信号 ---- */

        /// <summary>
        /// 温区输出信号：有已锁定配方且本体处于其**配方最佳温区**为绿，其余为红。
        /// 没有配方时输出不活动的红信号，不设置第三种空信号；绿色只代表温度条件满足，不代表正在生产。
        /// 红色输入、等操作工、断电或损坏都不会把已经符合温区条件的输出强制变红。
        ///
        /// V0.1.2：信号继续只报告配方最佳区，**不用可用交集代替它**——建材上限低于最佳上限时，
        /// 建筑在交集内生产，但信号仍按配方最佳区判断，玩家才能看出是被建材限制。
        /// 强制运行越过建筑上限但在最佳区内时，输出保持绿色。
        /// 温区整体未配置（没有建筑下限或熔点不可用）时不发绿：这表示本建筑没有温区约束，
        /// 而不是「温度不达标」。
        /// </summary>
        private void UpdateTemperatureZoneSignal()
        {
            bool hasLockedBatch = HasLockedBatch;
            bool inBestZone = runDecision.ZoneConfigured && CurrentState == TempState.Normal;
            TemperatureZoneSignal.Send(logicPorts, ref lastZoneSignal, hasLockedBatch, inBestZone);
        }

        /* ---- 热度状态日志 ---- */

        private void LogStateIfChanged(ComplexRecipe recipe)
        {
            if (hasLoggedState && runDecision.State == lastLoggedState)
            {
                return;
            }

            hasLoggedState = true;
            lastLoggedState = runDecision.State;

            if (!runDecision.ZoneConfigured)
            {
                ModLog.Info(PrefabId + "：" + recipe.id + " 未启用温区（缺少建筑下限或主材料上限），温度 "
                    + Temperatures.ToCelsius(BodyTemperature).ToString("F1") + " ℃，按原版行为运行。");
                return;
            }

            ModLog.Info(PrefabId + "：" + recipe.id + " 状态 " + runDecision.State
                + "，本体温度 " + Temperatures.ToCelsius(BodyTemperature).ToString("F1") + " ℃，效率 "
                + runDecision.Efficiency.ToString("F3")
                + "，允许生产 " + runDecision.AllowsProduction
                + "，加热 " + runDecision.Heating + "。" + ZoneSummaryForLog());
        }

        /// <summary>状态变化时把当前温区口径写进日志，便于用户按实际数值核对（含建材上限）。</summary>
        private string ZoneSummaryForLog()
        {
            if (!cachedZone.Configured)
            {
                return string.Empty;
            }

            return " [建筑下限 " + Celsius(cachedZone.Min) + "，配方最佳 " + Celsius(cachedZone.BestMin)
                + "~" + Celsius(cachedZone.BestMax) + "，建筑上限 " + Celsius(cachedZone.Max)
                + "（" + MaterialName + "）"
                + (cachedZone.ControlRangeUnreachable ? "，最佳温区不可达"
                    : cachedZone.ControlRangeLimited ? "，最佳温区受限" : string.Empty) + "]";
        }

        private static string Celsius(float kelvin)
        {
            return Temperatures.ToCelsius(kelvin).ToString("F1") + " ℃";
        }
    }
}
