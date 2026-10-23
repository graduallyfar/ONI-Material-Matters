using MaterialOverhaul.Core;

namespace MaterialOverhaul.Hardness
{
    /// <summary>
    /// 磨损与伤害的转换配置。数值集中定义在 <see cref="Balance"/>，本类只负责把它们交给
    /// <see cref="WearAccumulator"/>，让「磨损速率」与「伤害转换」只有一处来源。
    ///
    /// 本版取值（均为**暂定值**）：
    ///   - 磨损速率 1 磨损单位／实际生产秒 × 硬度差倍率 2·(要求 − 实际)/要求：
    ///     差一半时为 1 倍，硬度 0 时为 2 倍，刚好不达标时接近 0（设计评审 2026-10-23）；
    ///   - 伤害转换 0.05 HP／磨损单位，即实际生产 20 s 扣 1 HP，全部小数余额保留。
    ///
    /// 字段保留可写，供测试与调试覆盖；游戏内默认值一律来自 <see cref="Balance"/>。
    /// </summary>
    public static class DamageConversion
    {
        /// <summary>每个磨损单位折算的生命值伤害点数。</summary>
        public static float DamagePerWearUnit = Balance.DamagePerWearUnit;

        /// <summary>硬度不足时的磨损速率（磨损单位／实际生产秒）。</summary>
        public static float WearPerProductionSecond = Balance.WearPerProductionSecond;

        /// <summary>转换系数是否已配置；未配置时不累计磨损。</summary>
        public static bool IsConfigured
        {
            get { return DamagePerWearUnit > 0f && WearPerProductionSecond > 0f; }
        }

        /// <summary>按实际生产秒数累计磨损与伤害余额。</summary>
        public static void AccumulateProduction(WearAccumulator accumulator, float productionSeconds)
        {
            AccumulateProduction(accumulator, productionSeconds, DamagePerWearUnit);
        }

        public static void AccumulateProduction(WearAccumulator accumulator, float productionSeconds, float damagePerWearUnit)
        {
            if (accumulator == null || damagePerWearUnit <= 0f || WearPerProductionSecond <= 0f) return;
            accumulator.AccumulateProduction(productionSeconds, WearPerProductionSecond, damagePerWearUnit);
        }

        /// <summary>
        /// 硬度差倍率：2·(要求 − 实际)/要求，限制在 [0, 2]。硬度达标或没有要求时为 0。
        /// </summary>
        public static float DeficitWearFactor(int actualHardness, int requiredHardness)
        {
            if (requiredHardness <= 0 || actualHardness >= requiredHardness)
            {
                return 0f;
            }

            int actual = actualHardness < 0 ? 0 : actualHardness;
            return 2f * (requiredHardness - actual) / requiredHardness;
        }

        /// <summary>按硬度差倍率累计磨损：磨损单位速率 = 基础速率 × 倍率。</summary>
        public static void AccumulateProduction(WearAccumulator accumulator, float productionSeconds, float damagePerWearUnit, float deficitFactor)
        {
            if (accumulator == null || damagePerWearUnit <= 0f || WearPerProductionSecond <= 0f || !(deficitFactor > 0f)) return;
            accumulator.AccumulateProduction(productionSeconds, WearPerProductionSecond * deficitFactor, damagePerWearUnit);
        }

        /// <summary>把伤害余额换算回磨损单位数（显示用）。</summary>
        public static float WearUnitsOf(float damageBalance)
        {
            return DamagePerWearUnit <= 0f ? 0f : damageBalance / DamagePerWearUnit;
        }

        /// <summary>
        /// 把「已乘过操作效率的 dt」还原成**真实生产秒数**。
        ///
        /// 原版在调用 <c>OnWorkTick</c> 之前已经把效率乘进 dt（见
        /// <c>WorkChore.StatesInstance.Update</c>：<c>dt2 = dt × GetEfficiencyMultiplier(worker)</c>），
        /// 因此效率 0.3 时回调收到的 0.2 s 其实对应约 0.667 s 的真实时间。
        /// 磨损按真实时间计（§6.1）：效率越低，完成同一批用的实际时间越长，磨损越多。
        ///
        /// 单步结果做上限保护，避免「极速工人」等调试选项把整批时间算成一次磨损。
        /// </summary>
        public static float RealProductionSeconds(float scaledDt, float efficiencyMultiplier, float maxSecondsPerTick)
        {
            if (scaledDt <= 0f || efficiencyMultiplier <= 0.000001f)
            {
                return 0f;
            }

            float seconds = scaledDt / efficiencyMultiplier;
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0f)
            {
                return 0f;
            }

            return seconds > maxSecondsPerTick ? maxSecondsPerTick : seconds;
        }

        /// <summary>
        /// 把整数磨损单位折算成伤害点数。**仅供按磨损单位建模的外部调用使用**；
        /// 建筑侧的实数计时走 <see cref="AccumulateProduction"/>，以伤害余额累计、不在这里取整。
        /// </summary>
        public static int DamageFor(int wearUnits)
        {
            if (wearUnits <= 0 || DamagePerWearUnit <= 0f)
            {
                return 0;
            }

            return (int)(wearUnits * DamagePerWearUnit);
        }
    }
}
