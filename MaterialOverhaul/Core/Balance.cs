namespace MaterialOverhaul.Core
{
    /// <summary>
    /// 共用数值配置；首发建筑独立参数见 BuildingProfiles（V0.0.3 开发计划 §2.1）。
    ///
    /// 单位口径严格区分，禁止把同一数值分别硬编码到判断、注热、界面与测试里：
    ///   - **W**：电功率（电网需求与实际消费）；
    ///   - **kW**：注热功率，注入热量 = kW × dt(s) = kJ；
    ///   - **K**：温度，代码与 SIM 内部统一用开尔文，摄氏度只在配置与显示处换算；
    ///   - **s**：时间。
    ///
    /// 数值性质分三档，改动时必须同时更新这里的注释与文档：
    ///   - **已确认**：用户定下的规则（炼铝最佳温区 450–650 ℃、金属精炼器建筑下限 80 ℃、
    ///     过热系数 0.75 按 K 计算、加热共用 20–4000 W 滑条、启动线公式比例 0.1）。
    ///   - **本版暂定**：为让机制可运行、可验收而给出的工作值，不代表最终平衡（倍率 100、
    ///     加热默认值、要求硬度、磨损速率、伤害转换、无限额度默认值与上限、
    ///     效率两侧底值 0.30／0.80、补热与高温恢复回差、上限不高于下限时的退回口径）。
    ///   - **实验参数**：只影响观察方式、不改变规则（降温趋势显示阈值）。
    /// </summary>
    public static class Balance
    {
        // ---- 电力与注热（§2.1、§4.1）----

        /// <summary>原版金属精炼器额定生产用电（W）。原版建筑定义值，用于耗电矩阵与显示。</summary>
        public const float ProductionWatts = 1200f;

        /// <summary>预热、补热与强制持续加热共用滑条的下限（W）。v0.1.1 已确认。</summary>
        public const float MinHeatingWatts = 20f;

        /// <summary>共用加热滑条的上限（W），不包含原版生产用电。v0.1.1 已确认。</summary>
        public const float MaxHeatingWatts = 4000f;

        /// <summary>新建筑与旧存档的共用加热功率默认值（W），沿用原预热暂定值。</summary>
        public const float DefaultHeatingWatts = 1200f;

        /// <summary>
        /// 「用电（W）→ 注热（kW）」测试倍率。本版暂定，首发接入初值 100（由旧测试值 1000 下调）。
        ///
        /// 已知它会放大温度跨步与能源收益；本版不把它认定为平衡完成，也不是正式换算关系。
        /// </summary>
        public const float HeatMultiplier = 100f;

        // ---- 温度效率边界（§2.1、§2.2）----

        /// <summary>
        /// 低温侧边界效率 η_T（≈0.30）：在**建筑最低工作温度 L** 处取值，
        /// 到配方最佳下限 B₁ 连续升到 100%。
        ///
        /// 本版暂定值，沿用旧实现的 0.30；V0.1.2 只改了段宽（旧实现用 L→B₁ 的配方低温段），
        /// 没有改边界值。该常量保留为金属精炼器的兼容默认值；实际三栋系数读取 BuildingProfiles。
        /// </summary>
        public const float LowBoundaryEfficiency = 0.3f;

        /// <summary>
        /// 高温侧边界效率 η_T（≈0.80）：在**建筑最高工作温度 U** 处取值，
        /// 从配方最佳上限 B₂ 起连续下降。本版暂定值，沿用旧实现的 0.80。
        /// </summary>
        public const float HighBoundaryEfficiency = 0.8f;

        // ---- 启动、补热与恢复回差（§2.1、§3.2）----

        /// <summary>
        /// 建筑最低工作温度 L 的登记值：金属精炼器 80 ℃（用户 2026-10-14 给定）。
        ///
        /// V0.1.2 起 L **由建筑类型决定**，不随配方或建材改变；三栋实际下限读取 BuildingProfiles。
        /// 登记动作在 <c>ContentRegistration.RegisterBuildingZones</c>，这里只放数值。
        /// </summary>
        public const float MetalRefineryWorkMinCelsius = 80f;

        /// <summary>启动线公式的十分之一系数。已确认公式：原目标 = 下限 + 0.1 × 区间宽度。</summary>
        public const float StartFraction = 0.1f;

        /// <summary>
        /// 控制模式补热回差（K）。本版暂定，等于最佳温区宽度 200 K 的 5%（对应铝配方）。
        ///
        /// 保留作旧铝配方的兼容常量。实际回差由 StartThreshold.ControlHysteresis
        /// 按有效交集宽度的5%与启动余量共同计算。
        /// </summary>
        public const float TopUpHysteresisKelvin = 10f;

        /// <summary>
        /// 默认模式高温暂停后的恢复回差（K）：暂停线为建筑上限 U，回落到 U − 回差 才重新检查。
        /// 本版暂定，沿用旧实现的 15 K（旧口径为 0.1 × 铝配方低温段宽度）。
        ///
        /// 实际回差还受低温段宽度的10%与启动余量限制，恢复线不低于生产下限。
        /// </summary>
        public const float DefaultHighRecoveryKelvin = 15f;

        /// <summary>
        /// 控制模式高温暂停后的恢复回差（K）：T &gt; 650 ℃ 暂停，回落到 T ≤ 640 ℃ 才重新检查。
        /// 旧铝配方兼容常量；实际按有效交集宽度的5%计算。
        /// </summary>
        public const float ControlHighRecoveryKelvin = 10f;

        // ---- 硬度、磨损与伤害（§2.1、§6.1）----

        /// <summary>金属精炼器要求硬度。本版暂定，使用原版 <c>Element.hardness</c> 分级 FIRM = 25。</summary>
        public const int MetalRefineryRequiredHardness = 25;

        /// <summary>硬度不足时的磨损速率：1 磨损单位 / 实际生产秒。本版暂定，硬度差只决定有无磨损。</summary>
        public const float WearPerProductionSecond = 1f;

        /// <summary>磨损到生命值的转换：0.05 HP / 磨损单位（实际生产 20 s 扣 1 HP）。本版暂定。</summary>
        public const float DamagePerWearUnit = 0.05f;

        // ---- 订单（§2.1、§5.1）----

        /// <summary>无限订单每轮默认额度（批／轮）。本版暂定。</summary>
        public const int DefaultInfiniteBatchQuota = 1;

        /// <summary>无限订单每轮额度上限（批／轮）。本版暂定交互上限。</summary>
        public const int MaxInfiniteBatchQuota = 99;

        /// <summary>有限订单数量上限，沿用原版队列上限。</summary>
        public const int MaxQueueSize = 99;

        // ---- 显示（实验参数，不改变规则）----

        /// <summary>判定「降温中」的取样间隔（s）。</summary>
        public const float CoolingTrendSampleSeconds = 1f;

        /// <summary>判定「降温中」的降温幅度阈值（K）：相隔 1 s 降低超过该值才显示降温中。</summary>
        public const float CoolingTrendKelvin = 0.1f;

        /// <summary>
        /// 单步生产计时上限（s）。实验参数：只用于防止「极速工人」等调试选项把整批时间
        /// 算成一次磨损，正常帧远低于它，不影响常规计时口径。
        /// </summary>
        public const float MaxWearSecondsPerTick = 1f;

        // ---- 换算 ----

        /// <summary>电功率（W）换算成注热功率（kW），集中体现测试倍率。</summary>
        public static float KilowattsForWatts(float watts)
        {
            return watts / 1000f * HeatMultiplier;
        }
    }
}
