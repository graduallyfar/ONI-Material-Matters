using System;
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
    /// 建筑级组件：温区运行状态、加热与电网需求、订单身份、硬度磨损与损坏原因的游戏侧载体。
    ///
    /// 组件只挂在白名单建筑上（见 <see cref="BuildingWhitelist"/>），其余建筑行为等同原版。
    /// 判断本体在 Zones／Heating／Hardness／Orders 的判断层里，本类只负责：
    ///   - 读取建筑本体温度、主建造材料硬度与运行条件；
    ///   - 按原版配方标识查侧表；
    ///   - 把判断结果接到原版流程（效率、操作任务、功率、损坏）上；
    ///   - 保存本 Mod 自有数据（V0.0.3 开发计划 §5.3）。
    ///
    /// 按职责拆成四个 partial 文件，避免单个建筑组件过大：
    ///   - 本文件：生命周期、状态、序列化与对外只读视图；
    ///   - <c>MaterialOverhaulFab.Orders.cs</c>：订单身份、批次锁定与逐笔编辑；
    ///   - <c>MaterialOverhaulFab.Heating.cs</c>：运行状态机、加热注热与电网需求；
    ///   - <c>MaterialOverhaulFab.Damage.cs</c>：磨损计时、损坏原因与维修。
    /// </summary>
    [SerializationConfig(MemberSerialization.OptIn)]
    public partial class MaterialOverhaulFab : KMonoBehaviour, IGameObjectEffectDescriptor
    {
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

        /// <summary>原版存储变化事件（GameHashes.OnStorageChange = -1697596308）。</summary>
        private const int StorageChangeHash = -1697596308;

        private const string KeyDamageSourceMechanical = "STRINGS.BUILDINGS.DAMAGESOURCES.MATERIALOVERHAUL.MECHANICAL_WEAR";

        /* ---- 原版组件引用（不序列化）---- */

        private ComplexFabricator fabricator;
        private PrimaryElement primaryElement;
        private EnergyConsumer energyConsumer;
        private BuildingEnabledButton enabledButton;
        private BuildingHP buildingHP;
        private Overheatable overheatable;
        private LogicPorts logicPorts;
        private Operational operational;
        private KSelectable selectable;
        private Building building;
        private ComplexFabricatorWorkable workable;
        private HandleVector<int>.Handle structureHandle;
        private bool structureHandleResolved;

        /* ---- 判断层状态（部分序列化）---- */

        private readonly OrderQueue orders = new OrderQueue();
        private readonly WearAccumulator wear = new WearAccumulator();

        private string cachedRecipeId;
        private TempZone cachedZone = TempZone.Disabled;
        private bool cachedZoneConfigured;

        private RunState runState = RunState.Fresh;
        private RunDecision runDecision = RunDecision.Passthrough;
        private PowerDemand powerDemand;
        private DamageCauseState damageState = DamageCauseState.Fresh;

        private bool productionPausedByTemperature;
        private bool mechanicalDamageMark;
        private bool hasAutomationInput;
        private bool queueRefreshNeeded;
        private float currentRequestedWatts;
        private float currentUsedWatts;
        private float workEfficiencyMultiplier = 1f;

        private float coolingSampleTemperature;
        private float coolingSampleTimer;
        private bool cooling;
        private bool coolingSampleValid;

        private bool overheatApplied;
        private float overheatTemperature;
        private AttributeModifier overheatModifier;

        /// <summary>状态栏条目的上一次分类与是否已经写入过一次（避免每个模拟步重建状态项）。</summary>
        private RunStatusKind lastAppliedRunStatusKind = RunStatusKind.None;
        private bool hasRunStatusItemApplied;

        private int lastZoneSignal = -1;
        private TempState lastLoggedState = TempState.Disabled;
        private bool hasLoggedState;
        private DamageCause lastLoggedDamageCause = DamageCause.None;
        private bool hasLoggedDamageCause;

        /* ---- 本 Mod 自有数据，随存档落盘（§5.3）。字段在每次变化时同步，无需保存钩子。 ---- */

        [Serialize] private int[] orderIds = new int[0];
        [Serialize] private string[] orderRecipeIds = new string[0];
        [Serialize] private bool[] orderInfinite = new bool[0];
        [Serialize] private int[] orderRemaining = new int[0];
        [Serialize] private int[] orderQuotas = new int[0];
        [Serialize] private int[] orderRoundDone = new int[0];
        [Serialize] private int orderCursor;
        [Serialize] private int lockedOrderId;
        [Serialize] private string lockedRecipeId = string.Empty;

        [Serialize] private float wearDamageBalance;

        /// <summary>
        /// v0.0.2 的磨损字段（单位是磨损单位数）。保留只为迁移：读档时若新字段为空而它非零，
        /// 按本版转换系数换算一次并记录迁移日志，之后不再写入。
        /// </summary>
        [Serialize] private float wearAccumulated;

        [Serialize] private bool runStarted;
        [Serialize] private bool runHighTempWaiting;
        [Serialize] private bool runTopUpActive;

        [Serialize] private bool causeMechanical;
        [Serialize] private bool causeOverheat;
        [Serialize] private bool causeForcedOverheat;
        [Serialize] private bool causeLegacyUnknown;
        [Serialize] private bool causeMigrationChecked;

        [Serialize] private bool controlMode;
        [Serialize] private bool forcedRun;

        /// <summary>本建筑的 prefab id，作为侧表索引。</summary>
        public string PrefabId { get; private set; }

        /// <summary>
        /// 是否已经生成。建造菜单里的**预制件**不算：过热阈值依赖建造材料，
        /// 只有真正建成的实例才有规则值，预制件保持原版显示（见信息卡补丁）。
        /// </summary>
        public bool Spawned { get; private set; }

        /// <summary>运行模式与强制运行开关。</summary>
        public RunPolicy Policy
        {
            get { return new RunPolicy { ControlMode = controlMode, Forced = forcedRun }; }
        }

        /// <summary>当前配方的目标温区；无配方或未匹配时为 Disabled。</summary>
        public TempZone CurrentZone
        {
            get { return cachedZone; }
        }

        /// <summary>当前配方的温区是否已配置（未配置即按原版行为运行）。</summary>
        public bool ZoneConfigured
        {
            get { return cachedZoneConfigured; }
        }

        /// <summary>温区五态（用于显示）。</summary>
        public TempState CurrentState { get; private set; }

        /// <summary>温度效率 η_T。</summary>
        public float CurrentEfficiency { get; private set; }

        /// <summary>本步的运行判断结论。</summary>
        public RunDecision CurrentDecision
        {
            get { return runDecision; }
        }

        /// <summary>本步的电力需求与实际消费。</summary>
        public PowerDemand CurrentPower
        {
            get { return powerDemand; }
        }

        /// <summary>本步向电网请求的总功率（W）。未获供电时仍然存在。</summary>
        public float CurrentRequestedWatts
        {
            get { return currentRequestedWatts; }
        }

        /// <summary>本步实际消费的总功率（W）。</summary>
        public float CurrentUsedWatts
        {
            get { return currentUsedWatts; }
        }

        /// <summary>本步实际注入的加热意图。</summary>
        public HeatingIntent AppliedHeating
        {
            get { return powerDemand.AppliedHeating; }
        }

        /// <summary>本步是否真的在推进生产（界面、磨损与生产热共享这个事实）。</summary>
        public bool ProductionRunning
        {
            get { return powerDemand.ProductionRunning; }
        }

        /// <summary>本体温度是否处于下降趋势（相隔 1 s 降低超过阈值）。只影响显示文字。</summary>
        public bool IsCooling
        {
            get { return cooling; }
        }

        /// <summary>当前是否因温度条件被暂停（操作任务已撤销、复制人已释放）。</summary>
        public bool ProductionPausedByTemperature
        {
            get { return productionPausedByTemperature; }
        }

        /// <summary>当前是否供电。</summary>
        public bool IsPowered
        {
            get { return energyConsumer == null || energyConsumer.IsPowered; }
        }

        /// <summary>是否被玩家手动禁用。</summary>
        public bool IsManuallyDisabled
        {
            get { return enabledButton != null && !enabledButton.IsEnabled; }
        }

        /// <summary>是否因损坏停机。</summary>
        public bool IsBrokenDown
        {
            get { return buildingHP != null && buildingHP.IsBroken; }
        }

        /// <summary>当前配方的目标温区（用于界面显示与自动化输出）。</summary>
        public OrderQueue Orders
        {
            get { return orders; }
        }

        /// <summary>累计磨损（磨损单位，含未成形的部分）；用于显示。</summary>
        public float WearUnits
        {
            get { return wear.WearUnits(Balance.DamagePerWearUnit); }
        }

        /// <summary>当前正在生产的配方；没有生产中的批次时为 null。</summary>
        public ComplexRecipe CurrentWorkingRecipe
        {
            get { return fabricator != null ? fabricator.CurrentWorkingOrder : null; }
        }

        /// <summary>当前批次关联的订单；没有或已丢失时为 null。</summary>
        public OrderEntry CurrentOrder
        {
            get { return orders.LockedBatch; }
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
            operational = GetComponent<Operational>();
            selectable = GetComponent<KSelectable>();
            building = GetComponent<Building>();
            workable = GetComponent<ComplexFabricatorWorkable>();

            KPrefabID prefabIdComponent = GetComponent<KPrefabID>();
            PrefabId = prefabIdComponent != null ? prefabIdComponent.PrefabTag.ToString() : gameObject.name;

            CurrentEfficiency = 1f;
            CurrentState = TempState.Disabled;

            RestoreState();

            // v0.0.4：模式固定默认，旧存档的控制模式在这里迁移（见方法注释）。
            MigrateControlModeToDefault();

            // 旧存档或其它码路可能已经有原版队列数量：先纳入订单表，再按订单表重新决定下一笔。
            EnsureOrdersImported();

            // 读档后以订单表为准把原版计数写回一次，保证红信号准备路径读到的额度是自洽的。
            SyncAllVanillaCounts();
            queueRefreshNeeded = true;

            // 过热阈值按主建造材料熔点设定；Overheatable.OnSpawn 之后还会再调用一次，两处都是幂等的。
            ApplyOverheatRule();

            // 「允许／暂停生产」输入端口是否真的挂上了：决定是否启用自动化红信号相关逻辑。
            hasAutomationInput = HasInputPort(AutomationControlScope.PortId);

            // 面板开关随用户菜单刷新重建；对象清理时必须退订（AGENTS 实现约定）。
            Subscribe(RefreshUserMenuHash, OnRefreshUserMenu);

            // 存储变化事件：红信号下原版不会刷新队列，这里用它决定何时补一次刷新（原料到货等）。
            Subscribe(StorageChangeHash, OnStorageChanged);

            // 电网补丁按消费者反查组件；不注册就不能改写功率读数。
            PowerRegistry.Register(energyConsumer, this);

            ModLog.Info("已接入建筑 " + PrefabId + "（格子 " + Grid.PosToCell(this)
                + (hasAutomationInput ? "，含「允许／暂停生产」自动化输入端口" : string.Empty) + "）。");
        }

        protected override void OnCleanUp()
        {
            Spawned = false;
            Unsubscribe(RefreshUserMenuHash, OnRefreshUserMenu);
            Unsubscribe(StorageChangeHash, OnStorageChanged);
            PowerRegistry.Unregister(energyConsumer);
            base.OnCleanUp();
        }

        private void OnStorageChanged(object data)
        {
            queueRefreshNeeded = true;
        }

        private bool HasInputPort(HashedString portId)
        {
            if (logicPorts == null || logicPorts.inputPortInfo == null)
            {
                return false;
            }

            foreach (LogicPorts.Port port in logicPorts.inputPortInfo)
            {
                if (port.id == portId)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>建筑本体温度。不使用周围气体温度代替（§3.1）。</summary>
        public float BodyTemperature
        {
            get { return primaryElement != null ? primaryElement.Temperature : 0f; }
        }

        /// <summary>该建筑是否启用本 Mod 的内置加热与加热用电。</summary>
        public bool SupportsBuiltInHeating
        {
            get { return HeatingScope.IsEnabled(PrefabId); }
        }

        /* ---- 序列化 ---- */

        private void RestoreState()
        {
            wear.Restore(wearDamageBalance);
            MigrateLegacyWear();

            runState = new RunState
            {
                Started = runStarted,
                HighTempWaiting = runHighTempWaiting,
                TopUpActive = runTopUpActive
            };

            damageState = new DamageCauseState
            {
                MechanicalLatched = causeMechanical,
                OverheatLatched = causeOverheat,
                ForcedOverheatLatched = causeForcedOverheat,
                LegacyUnknown = causeLegacyUnknown
            };

            MigrateLegacyDamageCause();
            RestoreOrders();
        }

        /// <summary>
        /// 迁移 v0.0.2 的磨损字段：旧存档记的是磨损单位数，本版按伤害余额记。
        /// 只在旧值非零、新值为零且尚未迁移过时换算一次，并把旧字段清零避免重复换算。
        /// </summary>
        private void MigrateLegacyWear()
        {
            if (wearDamageBalance > 0f || wearAccumulated <= 0f)
            {
                return;
            }

            wear.RestoreWearUnits(wearAccumulated, DamageConversion.DamagePerWearUnit);
            ModLog.Info(PrefabId + "：迁移旧存档磨损 " + wearAccumulated.ToString("F3")
                + " 磨损单位 → " + wear.DamageBalance.ToString("F3") + " HP 伤害余额。");
            wearAccumulated = 0f;
            wearDamageBalance = wear.DamageBalance;
        }

        /// <summary>
        /// 迁移旧存档的损坏原因：v0.0.2 的布尔原因只在「已损坏停机」时才置位，无法区分来源时
        /// 明确标为迁移未知，保留原版维修温度行为，不猜成纯机械（§5.3）。
        /// </summary>
        private void MigrateLegacyDamageCause()
        {
            if (causeMigrationChecked)
            {
                return;
            }

            causeMigrationChecked = true;

            if (!damageState.IsClean)
            {
                return;
            }

            if (buildingHP != null && buildingHP.NeedsRepairs)
            {
                damageState.LegacyUnknown = true;
                ModLog.Info(PrefabId + "：旧存档存在未修复损伤但缺少来源记录，按「来源未知」处理，保留原版维修温度行为。");
            }
        }

        private void SyncWearField()
        {
            wearDamageBalance = wear.DamageBalance;
        }

        private void SyncRunStateFields()
        {
            runStarted = runState.Started;
            runHighTempWaiting = runState.HighTempWaiting;
            runTopUpActive = runState.TopUpActive;
        }

        private void SyncDamageFields()
        {
            causeMechanical = damageState.MechanicalLatched;
            causeOverheat = damageState.OverheatLatched;
            causeForcedOverheat = damageState.ForcedOverheatLatched;
            causeLegacyUnknown = damageState.LegacyUnknown;
            causeMigrationChecked = true;
        }

        /* ---- 运行模式与强制运行（建筑细节文档 §2.1、§2.1.4）---- */

        /// <summary>
        /// v0.0.4：界面不再提供默认／控制模式切换（用户要求），模式固定为默认模式。
        /// 旧存档里 `controlMode == true` 的建筑如果直接沿用，玩家会被永久锁在控制模式且没有入口，
        /// 因此在读档时迁移一次并留下可定位日志。控制模式的判定代码保留，以后恢复入口即可再用。
        /// </summary>
        private void MigrateControlModeToDefault()
        {
            if (!controlMode)
            {
                return;
            }

            controlMode = false;
            ModLog.Info(PrefabId + "：v0.0.4 起模式固定为默认模式，本建筑旧存档中的控制模式已迁移为默认模式。");
        }

        /// <summary>切换默认／控制模式。保留批次与进度，只重建启动条件与加热意图。</summary>
        public void SetControlMode(bool control)
        {
            if (controlMode == control)
            {
                return;
            }

            controlMode = control;
            OnPolicyOrModeChanged();
        }

        /// <summary>切换强制运行开关。保留批次、磨损与损坏原因。</summary>
        public void SetForcedRun(bool forced)
        {
            if (forcedRun == forced)
            {
                return;
            }

            forcedRun = forced;
            OnPolicyOrModeChanged();
        }

        /// <summary>模式／强制切换后重建运行状态，并立刻重新判断（不清批次、不清进度与原因）。</summary>
        private void OnPolicyOrModeChanged()
        {
            ComplexRecipe working = CurrentWorkingRecipe;
            if (working != null)
            {
                UpdateCachedZone(working.id);
                if (cachedZoneConfigured)
                {
                    runState = RunController.OnPolicyChanged(BodyTemperature, cachedZone, Policy, runState);
                    SyncRunStateFields();
                }
            }

            productionPausedByTemperature = false;

            if (fabricator != null)
            {
                // 让原版在下一轮刷新队列时重新校验当前订单。
                fabricator.SetQueueDirty();
            }

            if (global::Game.Instance != null)
            {
                global::Game.Instance.userMenu.Refresh(gameObject);
            }
        }

        /* ---- 界面入口 ---- */

        /// <summary>
        /// 建筑面板用户菜单。v0.0.4 只保留强制运行开关：默认／控制模式的切换按用户要求移除，
        /// 模式固定为默认模式（旧存档由 <see cref="MigrateControlModeToDefault"/> 迁移）。
        /// </summary>
        private void OnRefreshUserMenu(object data)
        {
            if (fabricator == null || global::Game.Instance == null)
            {
                return;
            }

            string forcedText = forcedRun
                ? LocalizedOr(KeyForcedDisable, "Disable Forced Run")
                : LocalizedOr(KeyForcedEnable, "Enable Forced Run");
            string forcedTooltip = forcedRun
                ? LocalizedOr(KeyForcedTooltipDisable, "Forced run is on. Turning it off re-evaluates production permission under the selected mode.")
                : LocalizedOr(KeyForcedTooltipEnable, "Forced run: keep producing outside the working temperature range and keep heating.");

            global::Game.Instance.userMenu.AddButton(gameObject, new KIconButtonMenu.ButtonInfo(
                iconName: "action_power",
                text: forcedText,
                on_click: () => SetForcedRun(!forcedRun),
                tooltipText: forcedTooltip));
        }

        /// <summary>
        /// 取本地化文本；键缺失时回退到内置英文，避免界面出现空白或原始键名。
        /// 键本身是主路径（AGENTS 实现约定：新增界面文本使用本地化键）。
        /// </summary>
        internal static string LocalizedOr(string key, string fallback)
        {
            string value = Strings.Get(key);
            return string.IsNullOrEmpty(value) ? fallback : value;
        }

        /* ---- 信息卡 ---- */

        /// <summary>
        /// 建筑信息卡的效果条目（V0.0.4 开发计划 §2.1）。
        ///
        /// 与 v0.0.3 的差别：
        ///   - **不再**写运行状态文字：状态改到信息卡状态栏（<see cref="RunStatusItem"/>）；
        ///   - **不再**列订单列表：逐笔查看与编辑在「材料革新：订单列表」侧屏里；
        ///   - **当前生产／下一生产改读原版数据**（<c>CurrentWorkingOrder</c> / <c>NextOrder</c>）
        ///     与原版文案，与配方侧屏完全同源，不再由本 Mod 自己推算调度。
        /// </summary>
        public List<Descriptor> GetDescriptors(GameObject go)
        {
            List<Descriptor> list = new List<Descriptor>();
            try
            {
                if (cachedZoneConfigured)
                {
                    list.Add(DescriptorText(string.Format(
                        LocalizedOr("STRINGS.UI.BUILDINGEFFECTS.MATERIALOVERHAUL.TEMPZONE_LINE", "工作温区：{0} ~ {1}（最佳温区 {2} ~ {3}）"),
                        GameUtil.GetFormattedTemperature(cachedZone.WorkMin),
                        GameUtil.GetFormattedTemperature(cachedZone.WorkMax),
                        GameUtil.GetFormattedTemperature(cachedZone.BestMin),
                        GameUtil.GetFormattedTemperature(cachedZone.BestMax))));
                }

                list.Add(DescriptorText(VanillaOrderLine()));

                list.Add(DescriptorText(string.Format(
                    LocalizedOr("STRINGS.UI.BUILDINGEFFECTS.MATERIALOVERHAUL.POWER_LINE", "请求功率 {0}，实际用电 {1}，实际注热 {2}"),
                    GameUtil.GetFormattedWattage(currentRequestedWatts),
                    GameUtil.GetFormattedWattage(currentUsedWatts),
                    HeatingIntentText(powerDemand.AppliedHeating))));

                list.Add(DescriptorText(HardnessText()));

                // 过热「条件」与「已受损」分开显示：只超过阈值时说明后果，不写成已经受损。
                if (IsAboveOverheatThreshold && !HasOverheatDamage)
                {
                    list.Add(DescriptorText(string.Format(
                        LocalizedOr("STRINGS.UI.BUILDINGEFFECTS.MATERIALOVERHAUL.OVERHEAT_CONDITION", "本体温度已超过过热阈值 {0}，继续升温会过热受损。"),
                        GameUtil.GetFormattedTemperature(overheatable != null ? overheatable.OverheatTemperature : 0f))));
                }
            }
            catch (Exception e)
            {
                ModLog.Error("生成建筑信息卡条目失败：" + e);
            }

            return list;
        }

        /// <summary>
        /// 原版口径的「当前生产 / 下一生产」：直接取原版配方侧屏用的同一对字段与同一套文案
        /// （<c>CurrentWorkingOrder</c>、<c>NextOrder</c>、<c>NO_WORKABLE_ORDER</c>），
        /// 因此信息卡与配方侧屏不会出现两套说法。
        /// </summary>
        private string VanillaOrderLine()
        {
            if (fabricator == null)
            {
                return string.Empty;
            }

            ComplexRecipe working = fabricator.CurrentWorkingOrder;
            if (working != null)
            {
                return string.Format(
                    VanillaText(
                        "STRINGS.UI.UISIDESCREENS.FABRICATORSIDESCREEN.CURRENT_ORDER",
                        "Current order: {0}",
                        "当前订单：{0}"),
                    working.GetUIName(includeAmounts: false));
            }

            ComplexRecipe next = fabricator.NextOrder;
            if (next != null)
            {
                return string.Format(
                    VanillaText(
                        "STRINGS.UI.UISIDESCREENS.FABRICATORSIDESCREEN.NEXT_ORDER",
                        "Next order: {0}",
                        "下一订单：{0}"),
                    next.GetUIName(includeAmounts: false));
            }

            return VanillaText(
                "STRINGS.UI.UISIDESCREENS.FABRICATORSIDESCREEN.NO_WORKABLE_ORDER",
                "No workable order",
                "无可执行订单");
        }

        /// <summary>
        /// 取原版键的文本。原版键缺失时 <c>Strings.Get</c> 会返回空串**或原样返回键名**，
        /// 两种都按缺失处理并回退到本 Mod 内置文案（中文回退与游戏文案口径一致），
        /// 保证信息卡不会出现键名或空白。
        /// </summary>
        private static string VanillaText(string key, string englishFallback, string chineseFallback)
        {
            string value = Strings.Get(key);
            if (!string.IsNullOrEmpty(value) && value != key)
            {
                return value;
            }

            return LocalizedOr(key, IsChineseLocale() ? chineseFallback : englishFallback);
        }

        private static bool IsChineseLocale()
        {
            try
            {
                string code = Localization.GetCurrentLanguageCode();
                return !string.IsNullOrEmpty(code) && code.StartsWith("zh");
            }
            catch (Exception)
            {
                return true;
            }
        }

        /// <summary>批次进度：优先用原版进度值，缺失时返回 0；信息卡与状态栏共用同一个口径。</summary>
        public float BatchProgress
        {
            get
            {
                if (fabricator == null)
                {
                    return 0f;
                }

                float progress = fabricator.GetPercentComplete();
                return float.IsNaN(progress) ? 0f : progress;
            }
        }

        private static Descriptor DescriptorText(string text)
        {
            return new Descriptor(text, text, Descriptor.DescriptorType.Effect);
        }

        private static string HeatingIntentText(HeatingIntent intent)
        {
            switch (intent)
            {
                case HeatingIntent.Preheat:
                    return LocalizedOr("STRINGS.UI.BUILDINGEFFECTS.MATERIALOVERHAUL.HEAT_PREHEAT", "预热中");

                case HeatingIntent.TopUp:
                    return LocalizedOr("STRINGS.UI.BUILDINGEFFECTS.MATERIALOVERHAUL.HEAT_TOPUP", "补热中");

                case HeatingIntent.Sustained:
                    return LocalizedOr("STRINGS.UI.BUILDINGEFFECTS.MATERIALOVERHAUL.HEAT_SUSTAINED", "持续加热");

                default:
                    return LocalizedOr("STRINGS.UI.BUILDINGEFFECTS.MATERIALOVERHAUL.HEAT_NONE", "无");
            }
        }

        private string HardnessText()
        {
            byte required;
            bool hasRequirement = HardnessRequirementTable.TryGet(PrefabId, out required);
            string material = primaryElement != null && primaryElement.Element != null ? primaryElement.Element.name : "-";
            int hp = buildingHP != null ? Mathf.RoundToInt(buildingHP.HitPoints) : 0;

            if (!hasRequirement)
            {
                return string.Format(
                    LocalizedOr("STRINGS.UI.BUILDINGEFFECTS.MATERIALOVERHAUL.HARDNESS_NONE", "主建造材料 {0}，生命值 {1}；该建筑未登记要求硬度，不产生机械磨损。"),
                    material, hp);
            }

            string actual = primaryElement != null && primaryElement.Element != null
                ? primaryElement.Element.hardness.ToString()
                : "-";

            if (!HasHardnessDeficit())
            {
                return string.Format(
                    LocalizedOr("STRINGS.UI.BUILDINGEFFECTS.MATERIALOVERHAUL.HARDNESS_OK", "主建造材料 {0}，硬度 {1}/{2}，生命值 {3}；硬度合格，机械磨损为零。"),
                    material, actual, required, hp);
            }

            return string.Format(
                LocalizedOr("STRINGS.UI.BUILDINGEFFECTS.MATERIALOVERHAUL.HARDNESS_WEAK", "主建造材料 {0}，硬度 {1}/{2}，生命值 {3}；硬度不足，按实际生产时间磨损（{4} 磨损单位/生产秒，{5} HP/磨损单位）。"),
                material, actual, required, hp,
                DamageConversion.WearPerProductionSecond.ToString("F2"),
                DamageConversion.DamagePerWearUnit.ToString("F3"));
        }
    }
}
