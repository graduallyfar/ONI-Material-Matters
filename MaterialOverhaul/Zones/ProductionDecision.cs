namespace MaterialOverhaul.Zones
{
    /// <summary>一次温区判断的完整结论。</summary>
    public struct ProductionDecision
    {
        /// <summary>五态判断结果。</summary>
        public TempState State;

        /// <summary>当前模式与强制开关下是否允许推进生产。</summary>
        public bool AllowsProduction;

        /// <summary>温度效率 η_T。越界时保持对应侧的边界效率，不归零。</summary>
        public float Efficiency;

        /// <summary>该配方是否匹配到温区条目；false 表示已退化为原版行为。</summary>
        public bool ZoneConfigured;

        public static ProductionDecision Passthrough
        {
            get
            {
                ProductionDecision decision = default(ProductionDecision);
                decision.State = TempState.Disabled;
                decision.AllowsProduction = true;
                decision.Efficiency = 1f;
                decision.ZoneConfigured = false;
                return decision;
            }
        }
    }
}
