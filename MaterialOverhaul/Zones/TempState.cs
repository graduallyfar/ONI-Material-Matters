namespace MaterialOverhaul.Zones
{
    /// <summary>
    /// 温区五态。语义与 V0.0.1 开发计划 §3.1 的五态表一一对应。
    /// </summary>
    public enum TempState
    {
        /// <summary>未配置温区：判断恒通过、效率恒为 1，行为等同原版。</summary>
        Disabled = 0,

        /// <summary>T &lt; WorkMin：停止生产（并进入预热判断）。</summary>
        BelowWorkMin = 1,

        /// <summary>WorkMin ≤ T &lt; BestMin：可以生产，效率下降。</summary>
        LowEfficiency = 2,

        /// <summary>BestMin ≤ T ≤ BestMax：100% 效率。</summary>
        Normal = 3,

        /// <summary>BestMax &lt; T ≤ WorkMax：可以生产，效率下降。</summary>
        HighEfficiency = 4,

        /// <summary>T &gt; WorkMax：停止生产和内置加热，等待降温。</summary>
        AboveWorkMax = 5
    }

    public static class TempStateExtensions
    {
        /// <summary>该状态是否允许生产。</summary>
        public static bool AllowsProduction(this TempState state)
        {
            return state == TempState.Disabled
                || state == TempState.LowEfficiency
                || state == TempState.Normal
                || state == TempState.HighEfficiency;
        }

        /// <summary>该状态是否要求停止内置加热。</summary>
        public static bool StopsHeating(this TempState state)
        {
            return state == TempState.AboveWorkMax;
        }
    }
}
