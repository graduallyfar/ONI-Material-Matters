using System.Collections.Generic;
using KSerialization;
using Klei.AI;
using MaterialOverhaul.Core;
using MaterialOverhaul.Hardness;
using MaterialOverhaul.Heating;
using MaterialOverhaul.Orders;
using MaterialOverhaul.Zones;
using UnityEngine;

namespace MaterialOverhaul.Game
{
    /// <summary>
    /// 建筑级组件：温区判断、加热判断与硬度磨损累计的游戏侧载体
    /// （V0.0.1 开发计划 §3.5：判断机制属于建筑级组件，所有启用建筑共用同一份实现）。
    ///
    /// 组件只挂在白名单建筑上（见 <see cref="BuildingWhitelist"/>），其余建筑行为等同原版。
    /// 判断函数本体在 Zones/Heating/Hardness 的判断层里，本类只负责：
    ///   - 读取建筑本体温度与主建造材料硬度；
    ///   - 按原版配方标识查侧表；
    ///   - 保存本 Mod 自有数据（§8.1）。
    ///
    /// 本版不接管原版产热、过热损伤、批次结算、取料与运行状态判断（§1.3 原则 1）。
    /// </summary>
    [SerializationConfig(MemberSerialization.OptIn)]
    public class MaterialOverhaulFab : KMonoBehaviour
    {
        // 磨损速率取自 Core.DebugValues：默认是占位值，不产生玩法影响；
        // 放入调试标记文件后改用样例值，便于在游戏里观察磨损与损坏原因。

        /// <summary>原版重新构建建筑用户菜单的事件（GameHashes.RefreshUserMenu = 493375141）。</summary>
        private const int RefreshUserMenuHash = 493375141;

        private const string KeyPrefix = "STRINGS.UI.USERMENUACTIONS.MATERIALOVERHAUL.";
        private const string KeyModeToDefault = KeyPrefix + "RUNMODE.TO_DEFAULT";
        private const string KeyModeToControl = KeyPrefix + "RUNMODE.TO_CONTROL";
        private const string KeyModeTooltipDefault = KeyPrefix + "RUNMODE.TOOLTIP_DEFAULT";
        private const string KeyModeTooltipControl = KeyPrefix + "RUNMODE.TOOLTIP_CONTROL";
        private const string KeyForcedEnable = KeyPrefix + "FORCEDRUN.ENABLE";
        private const string KeyForcedDisable = KeyPrefix + "FORCEDRUN.DISABLE";
        private const string KeyForcedTooltipEnable = KeyPrefix + "FORCEDRUN.TOOLTIP_ENABLE";
        private const string KeyForcedTooltipDisable = KeyPrefix + "FORCEDRUN.TOOLTIP_DISABLE";

        /// <summary>原版建筑受伤事件（GameHashes.DoBuildingDamage = -794517298）。</summary>
        private const int DoBuildingDamageHash = -794517298;

        private const string KeyDamageSourceMechanical = "STRINGS.BUILDINGS.DAMAGESOURCES.MATERIALOVERHAUL.MECHANICAL_WEAR";

        private ComplexFabricator fabricator;
        private PrimaryElement primaryElement;

        private readonly OrderQueue orders = new OrderQueue();
        private readonly WearAccumulator wear = new WearAccumulator();

        private string cachedRecipeId;
        private TempZone cachedZone = TempZone.Disabled;
        private bool cachedZoneConfigured;

        private TempState lastLoggedState = TempState.Disabled;
        private bool hasLoggedState;

        // 加热运行时状态：不持久化，读档后按实际温度与供电重新判断（§8.3）。
        private bool preheated;
        private bool topUpActive;
        private HandleVector<int>.Handle structureHandle;
        private bool structureHandleResolved;

        // 组件引用缓存，避免每次模拟更新重复 GetComponent。
        private EnergyConsumer energyConsumer;
        private BuildingEnabledButton enabledButton;
        private BuildingHP buildingHP;
        private Overheatable overheatable;
        private LogicPorts logicPorts;

        // 过热阈值（v0.0.2）：按主建造材料熔点改写，运行时不持久化，读档由 OnSpawn 重新计算。
        private bool overheatApplied;
        private float overheatTemperature;
        private AttributeModifier overheatModifier;

        // 损坏状态：只维护当前尚未修复的原因，不做历史统计。
        private bool overheated;
        private bool mechanicalDamaged;
        private DamageCause lastLoggedDamageCause = DamageCause.None;
        private bool hasLoggedDamageCause;
        private int lastZoneSignal = -1;

        /// <summary>本建筑的 prefab id，作为侧表索引。</summary>
        public string PrefabId { get; private set; }

        /// <summary>
        /// 是否已经生成。建造菜单里的**预制件**不算：过热阈值依赖建造材料，
        /// 只有真正建成的实例才有规则值，预制件保持原版显示（见信息卡补丁）。
        /// </summary>
        public bool Spawned { get; private set; }

        // ---- 本 Mod 自有数据，随存档落盘（§8.1）。字段在每次变化时同步，无需保存钩子。----
        [Serialize] private string[] orderRecipeIds = new string[0];
        [Serialize] private int[] orderRemaining = new int[0];
        [Serialize] private int[] orderQuotas = new int[0];
        [Serialize] private int[] orderRoundDone = new int[0];
        [Serialize] private int orderCursor;
        [Serialize] private float wearAccumulated;
        [Serialize] private string lockedRecipeId = string.Empty;
        [Serialize] private bool controlMode;
        [Serialize] private bool forcedRun;

        /// <summary>
        /// 运行模式与强制运行开关。V0.0.1 未接入建筑面板开关，默认模式且不强制；
        /// 面板控件与自动化接入属后续版本，字段已随存档持久化（§8.1）。
        /// </summary>
        public RunPolicy Policy
        {
            get { return new RunPolicy { ControlMode = controlMode, Forced = forcedRun }; }
        }

        /// <summary>当前配方的目标温区；无配方或未匹配时为 Disabled。</summary>
        public TempZone CurrentZone
        {
            get { return cachedZone; }
        }

        /// <summary>切换默认／控制模式。面板控件属后续版本，本版只保留语义入口。</summary>
        public void SetControlMode(bool control)
        {
            controlMode = control;
        }

        /// <summary>切换强制运行开关。本版未接入面板与自动化，只保留语义入口。</summary>
        public void SetForcedRun(bool forced)
        {
            forcedRun = forced;
        }

        public OrderQueue Orders
        {
            get { return orders; }
        }

        public float Wear
        {
            get { return wear.Wear; }
        }

        public TempState CurrentState { get; private set; }

        public float CurrentEfficiency { get; private set; }

        /// <summary>当前正在生产的配方；没有生产中的批次时为 null。</summary>
        public ComplexRecipe CurrentWorkingRecipe
        {
            get { return fabricator != null ? fabricator.CurrentWorkingOrder : null; }
        }

        protected override void OnSpawn()
        {
            base.OnSpawn();
            Spawned = true;
            fabricator = GetComponent<ComplexFabricator>();
            primaryElement = GetComponent<PrimaryElement>();
            energyConsumer = GetComponent<EnergyConsumer>();
            enabledButton = GetComponent<BuildingEnabledButton>();
            buildingHP = GetComponent<BuildingHP>();
            overheatable = GetComponent<Overheatable>();
            logicPorts = GetComponent<LogicPorts>();

            KPrefabID prefabIdComponent = GetComponent<KPrefabID>();
            PrefabId = prefabIdComponent != null ? prefabIdComponent.PrefabTag.ToString() : gameObject.name;

            CurrentEfficiency = 1f;
            CurrentState = TempState.Disabled;

            wear.Restore(wearAccumulated);
            RestoreOrders();

            // 旧存档或其它码路可能已经有原版队列数量：先纳入订单表，再按订单表重新决定下一笔。
            EnsureOrdersImported();

            // 过热阈值按主建造材料熔点设定；Overheatable.OnSpawn 之后还会再调用一次，两处都是幂等的。
            ApplyOverheatRule();

            // 面板开关随用户菜单刷新重建；对象清理时必须退订（AGENTS 实现约定）。
            Subscribe(RefreshUserMenuHash, OnRefreshUserMenu);

            // 接入证据：确认组件确实挂到了这栋建筑上。每栋实例 spawn 时打一条，便于在日志里核对。
            ModLog.Info("已接入建筑 " + PrefabId + "（格子 " + Grid.PosToCell(this) + "）。");
        }

        protected override void OnCleanUp()
        {
            Spawned = false;
            Unsubscribe(RefreshUserMenuHash, OnRefreshUserMenu);
            base.OnCleanUp();
        }

        /// <summary>建筑本体温度。不使用周围气体温度代替（§3.1）。</summary>
        public float BodyTemperature
        {
            get { return primaryElement != null ? primaryElement.Temperature : 0f; }
        }

        /// <summary>
        /// 评估该配方的生产许可与温度效率。配方标识变化时才重新查侧表，避免每次调用查表与刷日志。
        /// 模式与强制运行只影响生产许可，不改变效率曲线（判断层 <see cref="TempZoneEvaluator.Decide"/>）。
        /// </summary>
        public ProductionDecision EvaluateFor(ComplexRecipe recipe)
        {
            if (recipe == null)
            {
                return ProductionDecision.Passthrough;
            }

            UpdateCachedZone(recipe.id);

            ProductionDecision decision = TempZoneEvaluator.Decide(BodyTemperature, cachedZone, Policy);
            CurrentState = decision.State;
            CurrentEfficiency = decision.Efficiency;
            return decision;
        }

        private void UpdateCachedZone(string recipeId)
        {
            if (recipeId == cachedRecipeId)
            {
                return;
            }

            cachedRecipeId = recipeId;
            cachedZoneConfigured = RecipeZoneTable.TryGet(cachedRecipeId, out cachedZone);

            if (!cachedZoneConfigured && !ContentRegistration.ZonesResolved)
            {
                // 兜底：配方温区还没在「配方表可用」的时机登记过（加载顺序变化或钩子未命中）时，
                // 第一次有建筑要用配方时再补一次，避免整套温区静默失效。
                ContentRegistration.Apply();
                cachedZoneConfigured = RecipeZoneTable.TryGet(cachedRecipeId, out cachedZone);
            }

            if (!cachedZoneConfigured)
            {
                cachedZone = TempZone.Disabled;
                ModLog.WarnOnce("zone|missing|" + cachedRecipeId,
                    "配方温区：未匹配到配方 '" + cachedRecipeId + "'（建筑 " + PrefabId + "），该配方退化为原版行为。");
            }
        }

        /// <summary>
        /// 按实际生产时间累计机械磨损。只有实际生产推进时调用
        /// （预热、待机、暂停、维修、损坏停机都不调用，§6.3）。
        /// </summary>
        public void AccumulateProduction(float seconds)
        {
            if (seconds <= 0f || !HasHardnessDeficit())
            {
                return;
            }

            wear.AccumulateProduction(seconds, DebugValues.WearPerSecond);
            wearAccumulated = wear.Wear;
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

            if (!HardnessRequirementTable.TryGet(PrefabId, out byte required))
            {
                return false;
            }

            return primaryElement.Element.hardness < required;
        }

        /// <summary>由模拟更新驱动刷新（补丁挂在 ComplexFabricator.Sim200ms 上，不自行注册调度）。</summary>
        public void RefreshStatus()
        {
            ComplexRecipe working = CurrentWorkingRecipe;
            if (working == null)
            {
                cachedRecipeId = null;
                cachedZone = TempZone.Disabled;
                cachedZoneConfigured = false;
                CurrentState = TempState.Disabled;
                CurrentEfficiency = 1f;
                hasLoggedState = false;
                UpdateTemperatureZoneSignal();
                return;
            }

            ProductionDecision gate = EvaluateFor(working);
            LogStateIfChanged(working, gate);
            UpdateTemperatureZoneSignal();
        }

        /// <summary>
        /// 温区输出信号：有已锁定配方且处于最佳温区为 1，其余为 0。
        /// 没有配方时输出不活动的红信号，不设置第三种空信号；绿色只代表温度条件满足，不代表正在生产。
        /// </summary>
        private void UpdateTemperatureZoneSignal()
        {
            bool hasLockedBatch = CurrentWorkingRecipe != null;
            bool inBestZone = CurrentState == TempState.Normal;
            TemperatureZoneSignal.Send(logicPorts, ref lastZoneSignal, hasLockedBatch, inBestZone);
        }

        // ---- 建筑面板开关：模式与强制运行（建筑细节文档 §2.1、§2.1.4）----

        /// <summary>
        /// 往建筑面板加两个独立按钮：模式切换与强制运行开关。
        /// 两者都会立即重新判断生产许可与加热状态，但**保留当前批次**（不清进度、不丢原料）。
        /// </summary>
        private void OnRefreshUserMenu(object data)
        {
            if (fabricator == null || global::Game.Instance == null)
            {
                return;
            }

            // 按钮文本与提示描述的是「按下去会进入的状态」，所以取目标模式的那一条。
            string modeText = controlMode
                ? LocalizedOr(KeyModeToDefault, "Switch to Default Mode")
                : LocalizedOr(KeyModeToControl, "Switch to Control Mode");
            string modeTooltip = controlMode
                ? LocalizedOr(KeyModeTooltipDefault, "Default mode: production is allowed across the whole working temperature range.")
                : LocalizedOr(KeyModeTooltipControl, "Control mode: production only inside the best temperature range, with top-up heating while producing.");

            global::Game.Instance.userMenu.AddButton(gameObject, new KIconButtonMenu.ButtonInfo(
                iconName: "action_control",
                text: modeText,
                on_click: ToggleControlMode,
                tooltipText: modeTooltip));

            string forcedText = forcedRun
                ? LocalizedOr(KeyForcedDisable, "Disable Forced Run")
                : LocalizedOr(KeyForcedEnable, "Enable Forced Run");
            string forcedTooltip = forcedRun
                ? LocalizedOr(KeyForcedTooltipDisable, "Forced run is on. Turning it off re-evaluates production permission under the selected mode.")
                : LocalizedOr(KeyForcedTooltipEnable, "Forced run: keep producing outside the working temperature range and keep heating.");

            global::Game.Instance.userMenu.AddButton(gameObject, new KIconButtonMenu.ButtonInfo(
                iconName: "action_power",
                text: forcedText,
                on_click: ToggleForcedRun,
                tooltipText: forcedTooltip));
        }

        private void ToggleControlMode()
        {
            SetControlMode(!controlMode);
            OnPolicyChanged();
        }

        private void ToggleForcedRun()
        {
            SetForcedRun(!forcedRun);
            OnPolicyChanged();
        }

        /// <summary>切换开关后重新判断许可与加热状态；不清除批次、不动进度与磨损。</summary>
        private void OnPolicyChanged()
        {
            topUpActive = false;

            if (fabricator != null)
            {
                // 让原版在下一轮刷新队列时重新校验当前订单。
                fabricator.SetQueueDirty();
            }

            RefreshStatus();

            if (global::Game.Instance != null)
            {
                global::Game.Instance.userMenu.Refresh(gameObject);
            }
        }

        /// <summary>
        /// 取本地化文本；键缺失时回退到内置英文，避免界面出现空白或原始键名。
        /// 键本身是主路径（AGENTS 实现约定：新增界面文本使用本地化键）。
        /// </summary>
        private static string LocalizedOr(string key, string fallback)
        {
            string value = Strings.Get(key);
            return string.IsNullOrEmpty(value) ? fallback : value;
        }

        // ---- 预热与补热（判断在 HeatingPlanner，这里只负责取状态与注入热量）----

        /// <summary>
        /// 由模拟更新驱动（补丁挂在 ComplexFabricator.Sim200ms 上）。
        ///
        /// 自动化红信号**不参与**这里的判断：它只停止生产，不阻止预热与补热
        /// （建筑细节文档 §2.2.1、AGENTS 已确认约束），所以这里查的是供电、手动禁用与损坏，
        /// 而不是 Operational.IsOperational（后者会把自动化信号一并算进来）。
        /// </summary>
        public void TickHeating(float dt)
        {
            if (dt <= 0f || primaryElement == null)
            {
                return;
            }

            TempZone zone = CurrentZone;
            if (zone.IsDisabled)
            {
                preheated = false;
                topUpActive = false;
                return;
            }

            float temperature = BodyTemperature;
            TempState state = TempZoneEvaluator.Evaluate(temperature, zone);

            // 「已开工」状态：跌破最低可工作温度就复位，重新预热；
            // 达到启动阈值即视为已开工。不持久化，读档后按实际温度重建。
            if (state == TempState.BelowWorkMin)
            {
                preheated = false;
            }
            else if (temperature >= HeatingPlanner.PreheatTarget(zone, Policy))
            {
                preheated = true;
            }

            HeatingContext context = default(HeatingContext);
            context.HasLockedBatch = CurrentWorkingRecipe != null;
            context.BatchMaterialsReady = context.HasLockedBatch; // 已投料同样视为本批次材料齐全
            context.Powered = energyConsumer == null || energyConsumer.IsPowered;
            context.ManuallyDisabled = enabledButton != null && !enabledButton.IsEnabled;
            context.BrokenDown = buildingHP != null && buildingHP.IsBroken;

            if (!HeatingConditions.CanHeat(context))
            {
                topUpActive = false;
                return;
            }

            bool allowsProduction = TempZoneEvaluator.Decide(temperature, zone, Policy).AllowsProduction;
            bool productionRunning = preheated && allowsProduction;

            HeatingIntent intent = HeatingPlanner.Decide(temperature, zone, Policy, productionRunning, topUpActive);
            topUpActive = intent == HeatingIntent.TopUp;

            InjectHeat(intent, dt);
        }

        // ---- 机械磨损与损坏原因（判断在 DamageConversion / DamageCauseEvaluator）----

        /// <summary>当前损坏原因（机械／过热／双重），供显示与维修方式选择使用。</summary>
        public DamageCause CurrentDamageCause
        {
            get { return DamageCauseEvaluator.Evaluate(mechanicalDamaged, overheated, Policy.Forced); }
        }

        /// <summary>本次维修是否应重置温度：含过热时为真，只有机械损坏时为假。</summary>
        public bool RepairResetsTemperature
        {
            get { return DamageCauseEvaluator.ResetsTemperatureOnRepair(CurrentDamageCause); }
        }

        /// <summary>
        /// 由模拟更新驱动：检测过热、把累计磨损折算成伤害、维护当前损坏原因。
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
        /// 损坏原因变化时更新状态项。原版只保存一份 damageSourceInfo，
        /// 所以这里由本 Mod 按当前原因显示，双重原因显示合并状态项。
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
            KSelectable selectable = GetComponent<KSelectable>();
            if (selectable == null || DamageStatusItems.Mechanical == null)
            {
                // 状态项尚未登记（Db 未初始化）时跳过，不影响其他逻辑。
                return;
            }

            selectable.RemoveStatusItem(DamageStatusItems.Mechanical);
            selectable.RemoveStatusItem(DamageStatusItems.Overheat);
            selectable.RemoveStatusItem(DamageStatusItems.Both);

            StatusItem item = DamageStatusItems.For(cause);
            if (item != null)
            {
                selectable.SetStatusItem(Db.Get().StatusItemCategories.Main, item, null);
            }
        }

        /// <summary>
        /// 更新过热状态。**一旦达到过热条件就计入本次损坏原因，并保持到完全修复为止**：
        /// 建筑受损后即使温度回落，损失仍在、原因仍是过热，维修方式也要据此选择。
        /// 没有过热组件的建筑不参与过热判定。
        /// </summary>
        private void UpdateOverheatState()
        {
            if (overheatable == null || primaryElement == null || overheated)
            {
                return;
            }

            if (primaryElement.Temperature > overheatable.OverheatTemperature)
            {
                overheated = true;
            }
        }

        private void ClearDamageWhenFullyRepaired()
        {
            // 完全修复后结束本次损坏状态：机械与过热原因一并清除。
            if (buildingHP != null && !buildingHP.NeedsRepairs)
            {
                mechanicalDamaged = false;
                overheated = false;
            }
        }

        private void ApplyMechanicalWear()
        {
            if (buildingHP == null || primaryElement == null)
            {
                return;
            }

            // 生命值已耗尽就不再继续扣减（损坏停机后仍检测过热，但不重复扣血）。
            if (buildingHP.IsBroken)
            {
                return;
            }

            int wholeWear = (int)wear.Wear;
            if (wholeWear <= 0)
            {
                return;
            }

            int damage = DamageConversion.DamageFor(wholeWear);
            if (damage <= 0)
            {
                // 转换系数尚未配置：保留累计磨损，不扣血也不丢弃。
                return;
            }

            wear.TakeWholeWear();

            BuildingHP.DamageSourceInfo info = default(BuildingHP.DamageSourceInfo);
            info.damage = damage;
            info.source = LocalizedOr(KeyDamageSourceMechanical, "Mechanical Wear");
            BoxingTrigger(DoBuildingDamageHash, info);

            if (buildingHP.IsBroken)
            {
                mechanicalDamaged = true;
            }
        }

        /// <summary>
        /// 注入热量。功率未配置（默认 0）时不注入，也不会申请 handle。
        /// 走原版结构温度接口：传入的是 kJ，温升由 SIM 按建造材料热容与换热计算，不是固定温升。
        /// </summary>
        private void InjectHeat(HeatingIntent intent, float dt)
        {
            float kilowatts = HeatingPower.KilowattsFor(intent);
            if (kilowatts == 0f)
            {
                return;
            }

            if (!structureHandleResolved)
            {
                structureHandle = GameComps.StructureTemperatures.GetHandle(gameObject);
                structureHandleResolved = true;
            }

            GameComps.StructureTemperatures.ProduceEnergy(structureHandle, kilowatts * dt, HeatSourceDescription, dt);
        }

        /// <summary>
        /// 热量来源说明，原版会用它显示运行能量来源。
        /// 数值阶段接入实际加热后，应换成本地化键（AGENTS 实现约定：新增界面文本使用本地化键）。
        /// </summary>
        private const string HeatSourceDescription = "MaterialOverhaul.Heating";

        private void LogStateIfChanged(ComplexRecipe recipe, ProductionDecision gate)
        {
            if (hasLoggedState && gate.State == lastLoggedState)
            {
                return;
            }

            hasLoggedState = true;
            lastLoggedState = gate.State;

            if (!gate.ZoneConfigured)
            {
                ModLog.Info(PrefabId + "：" + recipe.id + " 未配置温区，温度 " + BodyTemperature.ToString("F1")
                    + " K，按原版行为运行。");
                return;
            }

            ModLog.Info(PrefabId + "：" + recipe.id + " 状态 " + gate.State
                + "，本体温度 " + BodyTemperature.ToString("F1") + " K，效率 " + gate.Efficiency.ToString("F3")
                + "，允许生产 " + gate.AllowsProduction + "。");
        }

        // ---- 订单流程（V0.0.2：以订单为单位接管顺序，建筑细节文档 §2.3）----

        /// <summary>本 Mod 订单表里是否有数据。没有数据时轮换与原版一致。</summary>
        public bool HasOrderData
        {
            get { return orders.Count > 0; }
        }

        /// <summary>
        /// 重新决定「下一个执行哪一笔订单」，写入原版 <c>nextOrderIdx</c> 与 <c>nextOrderIsWorkable</c>。
        ///
        /// 顺序完全由本 Mod 订单表给出（玩家添加顺序，同配方可重复）；
        /// 「还有没有剩余批次」与「原料是否齐备」仍用原版判断，避免复制一份原版逻辑后产生偏差。
        /// 全部订单都不可执行时不移动轮换指针，等材料或订单变化后再检查（不空转）。
        /// </summary>
        public void RefreshNextOrder()
        {
            if (fabricator == null)
            {
                return;
            }

            EnsureOrdersImported();
            if (orders.Count == 0)
            {
                return;
            }

            ComplexRecipe[] recipes = fabricator.GetRecipes();
            if (recipes == null || recipes.Length == 0)
            {
                return;
            }

            int index = orders.SelectNext(entry => IsOrderExecutable(entry, recipes));
            if (index < 0)
            {
                FabricatorAccess.NextOrderIsWorkable(fabricator) = false;
                return;
            }

            string recipeId = orders.Orders[index].RecipeId;
            int recipeIndex = IndexOfRecipe(recipes, recipeId);
            if (recipeIndex < 0)
            {
                FabricatorAccess.NextOrderIsWorkable(fabricator) = false;
                ModLog.WarnOnce("order|recipe-unavailable|" + PrefabId + "|" + recipeId,
                    PrefabId + "：订单表里的配方 '" + recipeId + "' 不在本建筑的配方列表中（DLC 未启用或配方被移除）。");
                return;
            }

            FabricatorAccess.NextOrderIdx(fabricator) = recipeIndex;
            FabricatorAccess.NextOrderIsWorkable(fabricator) = true;
        }

        /// <summary>
        /// 订单表里没有、但原版计数里还有数量的配方（旧存档、其它码路直接改原版计数）纳入订单表，
        /// 否则这些订单会因为不在订单表里而永远轮不到。
        /// </summary>
        public void EnsureOrdersImported()
        {
            if (fabricator == null)
            {
                return;
            }

            ComplexRecipe[] recipes = fabricator.GetRecipes();
            Dictionary<string, int> counts = FabricatorAccess.QueueCounts(fabricator);
            if (recipes == null || recipes.Length == 0 || counts == null)
            {
                return;
            }

            bool imported = false;
            foreach (ComplexRecipe recipe in recipes)
            {
                if (recipe == null || orders.ContainsRecipe(recipe.id))
                {
                    continue;
                }

                int count;
                if (!counts.TryGetValue(recipe.id, out count) || count == 0)
                {
                    continue;
                }

                if (FabricatorAccess.IsInfinite(count))
                {
                    orders.Add(OrderEntry.Infinite(recipe.id, OrderEntry.PlaceholderInfiniteBatchQuota));
                }
                else
                {
                    orders.Add(OrderEntry.Limited(recipe.id, count));
                }

                imported = true;
                ModLog.Info("订单迁移：" + PrefabId + " 把原版队列里的 " + recipe.id
                    + "（数量 " + count + "）纳入本 Mod 订单表。");
            }

            if (imported)
            {
                SyncOrderFields();
            }
        }

        /// <summary>原版「+」之后同步订单表：新增一笔 1 批订单；计数越过上限变无限时整笔替换为无限订单。</summary>
        public void OnVanillaIncrement(ComplexRecipe recipe, int previousCount)
        {
            if (fabricator == null || recipe == null)
            {
                return;
            }

            int count = fabricator.GetRecipeQueueCount(recipe);
            if (FabricatorAccess.IsInfinite(count))
            {
                orders.ReplaceRecipeWithInfinite(recipe.id, OrderEntry.PlaceholderInfiniteBatchQuota);
            }
            else if (FabricatorAccess.IsInfinite(previousCount))
            {
                // 原版在无限状态下点「+」会把计数清成 0：订单表同样取消该配方。
                orders.RemoveAllOfRecipe(recipe.id);
            }
            else
            {
                orders.AddBatchRequest(recipe.id);
            }

            AfterOrderChange(recipe.id);
        }

        /// <summary>原版「−」之后同步订单表：按订单逐笔回退。</summary>
        public void OnVanillaDecrement(ComplexRecipe recipe, int previousCount)
        {
            if (fabricator == null || recipe == null)
            {
                return;
            }

            int count = fabricator.GetRecipeQueueCount(recipe);
            if (count == previousCount)
            {
                // respectInfinite 为真且已是无限：原版不做改动，保留无限订单及其本轮进度。
                return;
            }

            if (FabricatorAccess.IsInfinite(count))
            {
                // 原版会把数量 0 递减成无限。本 Mod 不凭一次递减凭空创建订单，并把计数改回 0。
                ForceVanillaCount(recipe.id, 0);
                ModLog.Info(PrefabId + "：" + recipe.id
                    + " 原版数量为 0 时递减会变成无限，本 Mod 不创建订单，已把计数改回 0。");
                return;
            }

            if (FabricatorAccess.IsInfinite(previousCount))
            {
                // 原版把无限降到队列上限：改记为对应批次数，之后继续逐笔回退。
                orders.ReplaceRecipeWithBatches(recipe.id, count);
            }
            else if (count <= 0)
            {
                orders.RemoveAllOfRecipe(recipe.id);
            }
            else
            {
                orders.DecrementLastBatch(recipe.id);
            }

            AfterOrderChange(recipe.id);
        }

        /// <summary>侧屏直接设置数量：0 取消、-1 记为无限订单、正数改写为一笔对应批次的订单。</summary>
        public void OnVanillaSetCount(ComplexRecipe recipe, int count)
        {
            if (fabricator == null || recipe == null)
            {
                return;
            }

            if (FabricatorAccess.IsInfinite(count))
            {
                orders.ReplaceRecipeWithInfinite(recipe.id, OrderEntry.PlaceholderInfiniteBatchQuota);
            }
            else if (count > 0)
            {
                orders.ReplaceRecipeWithBatches(recipe.id, count);
            }
            else
            {
                orders.RemoveAllOfRecipe(recipe.id);
            }

            AfterOrderChange(recipe.id);
        }

        /// <summary>原版开始生产时锁定对应的那一笔订单（重复配方靠轮换位置区分）。</summary>
        public void LockOrderForStart(string recipeId)
        {
            if (!orders.LockForStart(recipeId))
            {
                ModLog.WarnOnce("order|lock-missing|" + PrefabId + "|" + recipeId,
                    PrefabId + "：原版开始的批次 '" + recipeId + "' 在订单表里找不到对应订单，批次仍按原版结算。");
                return;
            }

            SyncOrderFields();
        }

        /// <summary>批次完成前的订单记账。必须在原版正文之前完成，无限订单的本轮额度才会即时生效。</summary>
        public void OnBatchCompleting(string recipeId)
        {
            OrderEntry completed = orders.CompleteBatch(recipeId);
            if (completed == null)
            {
                ModLog.WarnOnce("order|complete-unmanaged|" + PrefabId + "|" + recipeId,
                    PrefabId + "：完成了一个不在订单表里的批次 '" + recipeId + "'。");
                return;
            }

            SyncOrderFields();
        }

        /// <summary>
        /// 批次完成后的计数对齐：原版正文内部已经自己扣过一次计数（走私有的
        /// <c>DecrementRecipeQueueCountInternal</c>），这里以订单表为准写回并让队列重新计算。
        /// </summary>
        public void OnBatchCompleted(string recipeId)
        {
            SyncVanillaCount(recipeId);
            if (fabricator != null)
            {
                fabricator.SetQueueDirty();
            }
        }

        /// <summary>订单表变化后统一处理：写回原版计数、持久化、让队列重新计算。</summary>
        private void AfterOrderChange(string recipeId)
        {
            SyncVanillaCount(recipeId);
            SyncOrderFields();
            if (fabricator != null)
            {
                fabricator.SetQueueDirty();
            }
        }

        /// <summary>把订单表里该配方的总需求写回原版计数字典（无限订单写原版的无限哨兵值）。</summary>
        private void SyncVanillaCount(string recipeId)
        {
            RecipeTotal total = orders.TotalFor(recipeId);
            ForceVanillaCount(recipeId, total.Infinite ? ComplexFabricator.QUEUE_INFINITE : total.Batches);
        }

        private void ForceVanillaCount(string recipeId, int batches)
        {
            if (fabricator == null || string.IsNullOrEmpty(recipeId))
            {
                return;
            }

            Dictionary<string, int> counts = FabricatorAccess.QueueCounts(fabricator);
            if (counts != null)
            {
                counts[recipeId] = batches;
            }
        }

        /// <summary>
        /// 该订单当前是否可执行：配方存在、还有剩余批次、原料齐备。
        ///
        /// 剩余批次与原版一致：无限订单恒有剩余，正在生产的那一批已从计数里扣掉。
        /// 原料沿用原版判断（金属精炼器的冷却液要求也在其中）；「已投料视为本批次材料齐全」
        /// 由原版锁定批次加计数扣减体现，因此这里不再把加工存储重复计入，避免同一批原料算两次。
        /// </summary>
        private bool IsOrderExecutable(OrderEntry entry, ComplexRecipe[] recipes)
        {
            if (entry == null)
            {
                return false;
            }

            int index = IndexOfRecipe(recipes, entry.RecipeId);
            if (index < 0)
            {
                return false;
            }

            ComplexRecipe recipe = recipes[index];
            if (FabricatorAccess.GetRemainingQueueCount(fabricator, recipe) <= 0)
            {
                return false;
            }

            return FabricatorAccess.HasIngredients(fabricator, recipe, fabricator.inStorage);
        }

        private static int IndexOfRecipe(ComplexRecipe[] recipes, string recipeId)
        {
            if (recipes == null || string.IsNullOrEmpty(recipeId))
            {
                return -1;
            }

            for (int i = 0; i < recipes.Length; i++)
            {
                if (recipes[i] != null && recipes[i].id == recipeId)
                {
                    return i;
                }
            }

            return -1;
        }

        // ---- 过热阈值（V0.0.2：主建造材料熔点 × 85%，建筑细节文档 §2.1.5）----

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
        /// 只把属性总值补偿到规则值，并让结构温度重新注册，使 SIM 使用新阈值
        /// （原版 <c>UpdateSimState</c> 每次脏标记都会重新读取该属性）。
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
            // 这里用「总值 − 本 Mod 修正项」得到固定部分，重复调用时结果稳定（不会叠加）。
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
        /// 原版 <c>Overheatable.OnSpawn</c> 用的就是这个办法。
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

        private void SyncOrderFields()
        {
            int count = orders.Count;
            orderRecipeIds = new string[count];
            orderRemaining = new int[count];
            orderQuotas = new int[count];
            orderRoundDone = new int[count];

            for (int i = 0; i < count; i++)
            {
                OrderEntry entry = orders.Orders[i];
                orderRecipeIds[i] = entry.RecipeId;
                orderRemaining[i] = entry.Remaining;
                orderQuotas[i] = entry.IsInfinite ? entry.BatchQuota : 0;
                orderRoundDone[i] = entry.BatchesDoneThisRound;
            }

            orderCursor = orders.Cursor;
            OrderEntry locked = orders.LockedBatch;
            lockedRecipeId = locked != null ? locked.RecipeId : string.Empty;
        }

        private void RestoreOrders()
        {
            List<OrderEntry> entries = new List<OrderEntry>();
            int count = orderRecipeIds != null ? orderRecipeIds.Length : 0;

            for (int i = 0; i < count; i++)
            {
                string recipeId = orderRecipeIds[i];
                if (string.IsNullOrEmpty(recipeId))
                {
                    continue;
                }

                int quota = ArrayValue(orderQuotas, i, 0);
                OrderEntry entry = quota > 0
                    ? OrderEntry.Infinite(recipeId, quota)
                    : OrderEntry.Limited(recipeId, ArrayValue(orderRemaining, i, 1));
                entry.BatchesDoneThisRound = ArrayValue(orderRoundDone, i, 0);
                entries.Add(entry);
            }

            orders.Restore(entries, orderCursor);

            if (entries.Count > 0)
            {
                // 读档只恢复顺序与轮换位置；锁定批次需要按实际温度与供电重新校验（§8.3）。
                ModLog.Info("读档恢复订单：" + PrefabId + " 共 " + entries.Count + " 项，轮换位置 " + orders.Cursor + "。");
            }

            lockedRecipeId = string.Empty;
        }

        private static int ArrayValue(int[] array, int index, int fallback)
        {
            if (array == null || index < 0 || index >= array.Length)
            {
                return fallback;
            }

            return array[index];
        }
    }
}
