namespace MaterialOverhaul.Zones
{
    /// <summary>
    /// 工作温区四边界（单位：开尔文）。
    ///
    /// 字段按温度升序排列：WorkMin &lt; BestMin &lt; BestMax &lt; WorkMax。
    ///
    /// 注意构造顺序：原版既有温度组件的配置函数参数顺序是
    /// 「低温警告、低温致命、高温警告、高温致命」（低侧两项在前、高侧两项在后），
    /// 与本结构体的升序排列不同，映射时必须逐项对应，避免顺序错位。
    /// </summary>
    public struct TempZone
    {
        /// <summary>最低可工作温度。</summary>
        public float WorkMin;

        /// <summary>最佳温区下限。</summary>
        public float BestMin;

        /// <summary>最佳温区上限。</summary>
        public float BestMax;

        /// <summary>最高可工作温度。</summary>
        public float WorkMax;

        public TempZone(float workMin, float bestMin, float bestMax, float workMax)
        {
            WorkMin = workMin;
            BestMin = bestMin;
            BestMax = bestMax;
            WorkMax = workMax;
        }

        /// <summary>
        /// 退化用温区：极宽区间，使判断恒通过、效率恒为 1。
        /// 未配置建筑的退化行为使用它，不抛异常（V0.0.1 开发计划 §3.7、§9）。
        /// </summary>
        public static TempZone Disabled
        {
            get { return new TempZone(float.NegativeInfinity, float.NegativeInfinity, float.PositiveInfinity, float.PositiveInfinity); }
        }

        /// <summary>本温区是否为退化温区。</summary>
        public bool IsDisabled
        {
            get { return float.IsNegativeInfinity(WorkMin) && float.IsNegativeInfinity(BestMin) && float.IsPositiveInfinity(BestMax) && float.IsPositiveInfinity(WorkMax); }
        }

        /// <summary>四边界是否严格升序且均为有限值。</summary>
        public bool IsOrdered
        {
            get
            {
                return IsFinite(WorkMin) && IsFinite(BestMin) && IsFinite(BestMax) && IsFinite(WorkMax)
                    && WorkMin <= BestMin && BestMin <= BestMax && BestMax <= WorkMax;
            }
        }

        /// <summary>边界相等是否出现（合法，但最佳段退化为一点，需要提示日志）。</summary>
        public bool HasDegenerateSegment
        {
            get { return WorkMin == BestMin || BestMin == BestMax || BestMax == WorkMax; }
        }

        public override string ToString()
        {
            if (IsDisabled)
            {
                return "TempZone(Disabled)";
            }

            return "TempZone[work " + WorkMin.ToString("F2") + ".." + WorkMax.ToString("F2")
                + ", best " + BestMin.ToString("F2") + ".." + BestMax.ToString("F2") + "]";
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
