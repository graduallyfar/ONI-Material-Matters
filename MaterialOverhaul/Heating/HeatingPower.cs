namespace MaterialOverhaul.Heating
{
    /// <summary>
    /// 加热功率配置。数值集中在这里，判断层与接入层只读这里的结果。
    ///
    /// 单位是 kW：注入热量 = kW × dt（秒）= kJ，与原版 SteamTurbine2 的写法一致
    /// （`ProduceEnergy(handle, 8f * dt, ...)`）。
    ///
    /// **注入的是热量而不是固定温升**：实际温升由 SIM 按建筑热容与换热计算
    /// （原版建筑有效热容 = `Def.MassForTemperatureModification × Element.specificHeatCapacity`）。
    ///
    /// **当前是「用电 → 注热 1:1000」的测试口径（用户指定，只为快速验证机制）**：
    ///   - 预热：1200 W 用电 → 1.2 kW × 1000 = **1200 kW** 注热；
    ///   - 补热：设计确认的 200 W 额外用电 → 0.2 kW × 1000 = **200 kW**；
    ///   - 强制运行持续加热：沿用预热值 1200 kW。
    ///
    /// 1200 kW 会让金属精炼器在不到一个周期内就达到 300 ℃ 级温区，**不是平衡数值**；
    /// 1:1000 也不是已确认的正式换算关系（正式关系仍是待定项）。
    /// 要回到正式评估，把 <see cref="TestHeatMultiplier"/> 改回 1（即 1:1）或直接改三档功率。
    /// </summary>
    public static class HeatingPower
    {
        /// <summary>测试用「用电 → 注热」倍率。1 = 1:1（此前的口径），1000 = 用户指定的测试口径。</summary>
        public const float TestHeatMultiplier = 1000f;

        /// <summary>预热注热功率（kW）：1200 W 用电 × 测试倍率。</summary>
        public static float PreheatKilowatts = 1.2f * TestHeatMultiplier;

        /// <summary>补热注热功率（kW）：设计的 200 W 额外用电 × 测试倍率。</summary>
        public static float TopUpKilowatts = 0.2f * TestHeatMultiplier;

        /// <summary>强制运行的持续加热功率（kW），暂沿用预热值。</summary>
        public static float SustainedKilowatts = 1.2f * TestHeatMultiplier;

        /// <summary>是否已经配置过任意一项功率。</summary>
        public static bool IsConfigured
        {
            get { return PreheatKilowatts != 0f || TopUpKilowatts != 0f || SustainedKilowatts != 0f; }
        }

        /// <summary>取某个意图对应的注热功率。未知意图与 None 都返回 0。</summary>
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
    }
}
