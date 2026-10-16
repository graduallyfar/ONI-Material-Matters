namespace MaterialOverhaul.Heating
{
    /// <summary>
    /// 建筑上限／过热阈值规则（V0.1.2 开发计划 §1.1、§2.1，已确认设计）：
    ///
    /// <code>最高工作温度 = 过热阈值 = 主建造材料熔点(K) × 0.75 × 主材料过热倍率</code>
    ///
    /// 说明与边界：
    ///   - 「主材料」指**主建造材料**，与硬度系统同一口径（建造记录中的第一个材料，
    ///     回退到建筑本体元素），辅料与配方输入材料都不参与。
    ///   - 熔点取元素的熔点（固体的 highTemp），**按 K 计算**；摄氏显示是换算后的结果，
    ///     不是「摄氏熔点 × 0.75」（V0.1.2 §1.1 的明确要求）。
    ///   - V0.1.2 起这个值同时是**建筑最高工作温度**：超过它普通模式停产并停止内置加热，
    ///     只有强制运行允许越界生产。旧版把「温区上限」与「过热阈值」当成两个阈值，本版合并。
    ///   - 该规则取代原版固定基础阈值，再应用主材料百分比，但**不关闭**
    ///     原版过热损伤、过热画面与维修流程。
    ///   - 熔点缺失、非正或非有限时**不覆盖**原版阈值，并记录可定位日志，
    ///     不让建筑因为一条算不出来的规则而失去原版保护。
    ///   - 规则只对**已经纳入温区数值的建筑**生效（首发三栋）；名单外建筑保留原版基础阈值并应用材料百分比。
    /// </summary>
    public static class OverheatRule
    {
        /// <summary>已确认系数：建筑上限／过热阈值取熔点的 75%（按 K 计算）。</summary>
        public const float MeltingPointFraction = 0.75f;

        /// <summary>
        /// 按熔点与系数计算上限。输入不可用时返回 false，调用方保持原版阈值。
        /// </summary>
        public static bool TryCompute(float meltingPointKelvin, float fraction, out float overheatKelvin)
        {
            return TryCompute(meltingPointKelvin, fraction, 1f, out overheatKelvin);
        }

        /// <summary>材料倍率作用于熔点 75% 的开尔文基准，不作用于摄氏数值。</summary>
        public static bool TryCompute(float meltingPointKelvin, float fraction, float materialMultiplier, out float overheatKelvin)
        {
            overheatKelvin = 0f;

            if (!IsUsable(meltingPointKelvin) || !IsUsable(fraction) || !IsUsable(materialMultiplier))
            {
                return false;
            }

            overheatKelvin = meltingPointKelvin * fraction * materialMultiplier;
            return IsUsable(overheatKelvin);
        }

        /// <summary>按已确认系数（0.75）计算无材料加成的基础上限。</summary>
        public static bool TryCompute(float meltingPointKelvin, out float overheatKelvin)
        {
            return TryCompute(meltingPointKelvin, MeltingPointFraction, out overheatKelvin);
        }

        /// <summary>
        /// 该阈值是否足以覆盖给定的配方最佳上限（用于记录材料选型的后果，不做强制拦截）。
        ///
        /// V0.1.2 起包不住最佳区**不是配置错误**：`B₂ &gt; U` 是「最佳温区受限」，
        /// `B₁ &gt; U` 是「最佳温区不可达」，都由建材选择造成，属于正常玩法分支。
        /// </summary>
        public static bool CoversBestMax(float overheatKelvin, float bestMaxKelvin)
        {
            return IsUsable(overheatKelvin) && IsUsable(bestMaxKelvin) && overheatKelvin >= bestMaxKelvin;
        }

        private static bool IsUsable(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
        }
    }
}
