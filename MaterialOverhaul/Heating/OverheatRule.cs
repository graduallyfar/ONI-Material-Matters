namespace MaterialOverhaul.Heating
{
    /// <summary>
    /// 过热阈值规则（已确认设计）：
    ///
    /// <code>过热阈值 = 主建造材料熔点 × 0.85</code>
    ///
    /// 说明与边界：
    ///   - 「主材料」指**主建造材料**，与硬度系统同一口径（建造记录中的第一个材料，
    ///     回退到建筑本体元素），辅料与配方输入材料都不参与。
    ///   - 熔点取元素的熔点（固体的 highTemp）。
    ///   - 该阈值与配方温区是两个独立机制：温区上限只停止生产与内置加热，
    ///     过热阈值决定是否受损；阈值高于温区上限时，温区内运行不会受损。
    ///   - 熔点缺失、非正或非有限时**不覆盖**原版阈值，并记录可定位日志，
    ///     不让建筑因为一条算不出来的规则而失去原版保护。
    /// </summary>
    public static class OverheatRule
    {
        /// <summary>已确认系数：过热阈值取熔点的 85%。</summary>
        public const float MeltingPointFraction = 0.85f;

        /// <summary>
        /// 按熔点与系数计算过热阈值。输入不可用时返回 false，调用方保持原版阈值。
        /// </summary>
        public static bool TryCompute(float meltingPointKelvin, float fraction, out float overheatKelvin)
        {
            overheatKelvin = 0f;

            if (!IsUsable(meltingPointKelvin) || !IsUsable(fraction))
            {
                return false;
            }

            overheatKelvin = meltingPointKelvin * fraction;
            return IsUsable(overheatKelvin);
        }

        /// <summary>按已确认系数（0.85）计算过热阈值。</summary>
        public static bool TryCompute(float meltingPointKelvin, out float overheatKelvin)
        {
            return TryCompute(meltingPointKelvin, MeltingPointFraction, out overheatKelvin);
        }

        /// <summary>该阈值是否足以覆盖给定的最高可工作温度（用于记录材料选型的后果，不做强制拦截）。</summary>
        public static bool CoversWorkMax(float overheatKelvin, float workMaxKelvin)
        {
            return IsUsable(overheatKelvin) && IsUsable(workMaxKelvin) && overheatKelvin >= workMaxKelvin;
        }

        private static bool IsUsable(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
        }
    }
}
