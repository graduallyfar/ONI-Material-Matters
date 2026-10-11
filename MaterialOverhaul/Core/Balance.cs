namespace MaterialOverhaul.Core
{
    /// <summary>
    /// 本版数值的唯一集中配置（V0.0.3 开发计划 §2.1）。
    ///
    /// 单位口径严格区分，禁止把同一数值分别硬编码到判断、注热、界面与测试里：
    ///   - **W**：电功率（电网需求与实际消费）；
    ///   - **kW**：注热功率，注入热量 = kW × dt(s) = kJ；
    ///   - **K**：温度，代码与 SIM 内部统一用开尔文，摄氏度只在配置与显示处换算；
    ///   - **s**：时间。
    ///
    /// 数值性质分三档，改动时必须同时更新这里的注释与文档：
    ///   - **已确认**：用户定下的规则（铝配方四边界、过热系数 0.85、补热 200 W、启动线公式）。
    ///   - **本版暂定**：为让机制可运行、可验收而给出的工作值，不代表最终平衡（倍率 1000、
    ///     加热电功率、要求硬度、磨损速率、伤害转换、无限额度默认值与上限、恢复线回差）。
    ///   - **实验参数**：只影响观察方式、不改变规则（降温趋势显示阈值）。
    /// </summary>
    public static class Balance
    {
        // ---- 电力与注热（§2.1、§4.1）----

        /// <summary>原版金属精炼器额定生产用电（W）。原版建筑定义值，用于耗电矩阵与显示。</summary>
        public const float ProductionWatts = 1200f;

        /// <summary>预热电功率（W）。本版暂定。</summary>
        public const float PreheatWatts = 1200f;

        /// <summary>正常生产中补热的**额外**电功率（W）。已确认：与生产额定用电相加。</summary>
        public const float TopUpWatts = 200f;

        /// <summary>强制运行的持续加热电功率（W）。本版暂定。</summary>
        public const float SustainedWatts = 1200f;

        /// <summary>
        /// 「用电（W）→ 注热（kW）」测试倍率。本版暂定，沿用 v0.0.2 最后的测试口径。
        ///
        /// 已知它会放大温度跨步与能源收益；本版不把它认定为平衡完成，也不是正式换算关系。
        /// </summary>
        public const float HeatMultiplier = 1000f;

        /// <summary>预热注热功率（kW）= 1200 W × 倍率。</summary>
        public const float PreheatKilowatts = PreheatWatts / 1000f * HeatMultiplier;

        /// <summary>补热注热功率（kW）= 200 W × 倍率。</summary>
        public const float TopUpKilowatts = TopUpWatts / 1000f * HeatMultiplier;

        /// <summary>强制运行持续加热注热功率（kW）= 1200 W × 倍率。</summary>
        public const float SustainedKilowatts = SustainedWatts / 1000f * HeatMultiplier;

        // ---- 温度效率边界（§2.1、§2.2）----

        /// <summary>最低可工作温度处的效率 η_T（≈0.30）。沿用暂定值。</summary>
        public const float LowBoundaryEfficiency = 0.3f;

        /// <summary>最高可工作温度处的效率 η_T（≈0.80）。沿用暂定值。</summary>
        public const float HighBoundaryEfficiency = 0.8f;

        // ---- 启动、补热与恢复回差（§2.1、§3.2）----

        /// <summary>启动线公式的十分之一系数。已确认公式：WorkMin + 0.1 × 低温段宽度。</summary>
        public const float StartFraction = 0.1f;

        /// <summary>
        /// 控制模式补热回差（K）：T &lt; 460 ℃ 开始补热，T ≥ 470 ℃ 停止。
        /// 本版暂定，等于最佳温区宽度 200 K 的 5%（对应铝配方）。
        /// </summary>
        public const float TopUpHysteresisKelvin = 10f;

        /// <summary>
        /// 默认模式高温暂停后的恢复回差（K）：T &gt; 800 ℃ 暂停，回落到 T ≤ 785 ℃ 才重新检查。
        /// 本版暂定，取值等于 0.1 × 低温段宽度（铝配方 15 K）。
        /// </summary>
        public const float DefaultHighRecoveryKelvin = 15f;

        /// <summary>
        /// 控制模式高温暂停后的恢复回差（K）：T &gt; 650 ℃ 暂停，回落到 T ≤ 640 ℃ 才重新检查。
        /// 本版暂定，与补热回差同值。
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
