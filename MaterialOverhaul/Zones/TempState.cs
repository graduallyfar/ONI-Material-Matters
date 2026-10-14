namespace MaterialOverhaul.Zones
{
    /// <summary>
    /// 温度状态六态（V0.1.2 开发计划 §2.1、§3）。
    ///
    /// 两件事在这里分开表达，不再互相掩盖：
    ///   - **效率段**：相对配方最佳区的位置（低温低效／最佳区／高温低效）；
    ///   - **建筑范围**：是否低于建筑最低工作温度 L，或高于主材料决定的建筑上限 U。
    ///
    /// 旧版把「最高可工作温度」当成配方边界，因此 `AboveWorkMax` 同时表示
    /// 「配方越界」与「材料过热」；V0.1.2 起 `AboveMax` 只表示超过建筑上限（= 材料过热阈值），
    /// 是否高温低效改由与配方最佳区的关系判断——建材上限落在最佳区内时不会误报高温低效
    /// （建筑细节文档 §2.1.4）。
    /// </summary>
    public enum TempState
    {
        /// <summary>未配置温区：判断恒通过、效率恒为 1，行为等同原版。</summary>
        Disabled = 0,

        /// <summary>T &lt; L：低于建筑最低工作温度，普通模式停止生产并进入预热判断。</summary>
        BelowMin = 1,

        /// <summary>L ≤ T &lt; B₁：可以生产，效率按低温段曲线下降。</summary>
        LowEfficiency = 2,

        /// <summary>B₁ ≤ T ≤ B₂：最佳温区，效率 100%。</summary>
        Normal = 3,

        /// <summary>B₂ &lt; T ≤ U：可以生产，效率按高温段曲线下降。</summary>
        HighEfficiency = 4,

        /// <summary>T &gt; U：超过建筑上限（= 过热阈值），普通模式停止生产与内置加热。</summary>
        AboveMax = 5
    }

    public static class TempStateExtensions
    {
        /// <summary>该状态在**默认模式**下是否允许生产：整个 [L, U] 内都可以。</summary>
        public static bool AllowsProduction(this TempState state)
        {
            return state == TempState.Disabled
                || state == TempState.LowEfficiency
                || state == TempState.Normal
                || state == TempState.HighEfficiency;
        }

        /// <summary>该状态在**控制模式**下是否允许生产：只有最佳温区。</summary>
        public static bool AllowsControlProduction(this TempState state)
        {
            return state == TempState.Disabled || state == TempState.Normal;
        }

        /// <summary>该状态是否要求停止内置加热：只有超过建筑上限。</summary>
        public static bool StopsHeating(this TempState state)
        {
            return state == TempState.AboveMax;
        }

        /// <summary>该状态是否属于低效生产段（用于状态栏文字）。</summary>
        public static bool IsLowEfficiencyBand(this TempState state)
        {
            return state == TempState.BelowMin || state == TempState.LowEfficiency;
        }

        /// <summary>该状态是否属于超过最佳区上限的高温段。</summary>
        public static bool IsHighEfficiencyBand(this TempState state)
        {
            return state == TempState.HighEfficiency || state == TempState.AboveMax;
        }
    }
}
