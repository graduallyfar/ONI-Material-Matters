using MaterialOverhaul.Core;

namespace MaterialOverhaul.Heating
{
    /// <summary>
    /// 加热的电力与注热功率视图。数值集中定义在 <see cref="Balance"/>，这里只做
    /// 「加热意图 → 电功率（W）／注热功率（kW）」的映射，避免同一数值散落在判断、注热与界面里。
    ///
    /// 单位口径（V0.0.3 开发计划 §2.1、§4.2）：
    ///   - 注入热量 Q(kJ) = kW × dt(s)，与原版 <c>SteamTurbine2</c> 的
    ///     <c>ProduceEnergy(handle, 8f * dt, ...)</c> 写法一致；
    ///   - 注入的是**热量**而不是固定温升：实际温升由 SIM 按建筑有效热容与换热计算；
    ///   - 倍率 1000 是本版暂定的「用电 → 注热」测试口径，已知会放大温度跨步与能源收益，
    ///     **不是平衡完成值**，也不是正式换算关系。
    ///
    /// 三个字段保留可写，供测试与调试覆盖默认值；游戏内默认值一律来自 <see cref="Balance"/>。
    /// </summary>
    public static class HeatingPower
    {
        /// <summary>测试用「用电 → 注热」倍率，见 <see cref="Balance.HeatMultiplier"/>。</summary>
        public const float TestHeatMultiplier = Balance.HeatMultiplier;

        /// <summary>预热电功率（W）。</summary>
        public static float PreheatWatts = Balance.PreheatWatts;

        /// <summary>补热的额外电功率（W）；生产时的生产用电另计。</summary>
        public static float TopUpWatts = Balance.TopUpWatts;

        /// <summary>强制运行持续加热的电功率（W）。</summary>
        public static float SustainedWatts = Balance.SustainedWatts;

        /// <summary>预热注热功率（kW）。</summary>
        public static float PreheatKilowatts = Balance.PreheatKilowatts;

        /// <summary>补热注热功率（kW）。</summary>
        public static float TopUpKilowatts = Balance.TopUpKilowatts;

        /// <summary>强制运行持续加热注热功率（kW）。</summary>
        public static float SustainedKilowatts = Balance.SustainedKilowatts;

        /// <summary>是否已经配置过任意一项功率。</summary>
        public static bool IsConfigured
        {
            get { return PreheatKilowatts != 0f || TopUpKilowatts != 0f || SustainedKilowatts != 0f; }
        }

        /// <summary>取某个意图对应的注热功率（kW）。未知意图与 None 都返回 0。</summary>
        public static float KilowattsFor(HeatingIntent intent)
        {
            switch (intent)
            {
                case HeatingIntent.Preheat:
                    return PreheatKilowatts;

                case HeatingIntent.TopUp:
                    return TopUpKilowatts;

                case HeatingIntent.Sustained:
                    return SustainedKilowatts;

                default:
                    return 0f;
            }
        }

        /// <summary>取某个意图对应的**加热**电功率（W）。None 为 0。</summary>
        public static float HeatingWattsFor(HeatingIntent intent)
        {
            switch (intent)
            {
                case HeatingIntent.Preheat:
                    return PreheatWatts;

                case HeatingIntent.TopUp:
                    return TopUpWatts;

                case HeatingIntent.Sustained:
                    return SustainedWatts;

                default:
                    return 0f;
            }
        }

        /// <summary>实际有电、正在注热时的电功率（W）；断电时为 0。</summary>
        public static float AppliedHeatingWatts(HeatingIntent intent, bool powered)
        {
            return powered ? HeatingWattsFor(intent) : 0f;
        }

        /// <summary>恢复本版默认值（测试与调试覆盖后复位用）。</summary>
        public static void ResetToDefaults()
        {
            PreheatWatts = Balance.PreheatWatts;
            TopUpWatts = Balance.TopUpWatts;
            SustainedWatts = Balance.SustainedWatts;
            PreheatKilowatts = Balance.PreheatKilowatts;
            TopUpKilowatts = Balance.TopUpKilowatts;
            SustainedKilowatts = Balance.SustainedKilowatts;
        }
    }
}
