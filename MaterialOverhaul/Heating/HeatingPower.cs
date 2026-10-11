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
    /// **以下数值是测试值，不是最终平衡数值**（V0.0.2 开发计划 §7）：
    ///   - 预热：用户给定 1200 W 用电，按 1:1 换算为 1.2 kW 注入热量；
    ///   - 补热：设计文档已确认「补热额外消耗 200 W」，按同一 1:1 口径为 0.2 kW；
    ///   - 强制运行持续加热：沿用预热值占位（设计文档明确该功率属数值阶段）。
    ///
    /// 换算关系本身（用电 → 注热）仍是待定项：这里按 1:1 测试口径实现，
    /// **没有**把「用电全部用于加热」当成已确认的统一换算比例。
    ///
    /// 另外，V0.0.2 尚未接入预热／补热的**额外用电**：注入的热量目前不会在电网侧多扣功率，
    /// 建筑只要求「有实际供电」（见 <see cref="HeatingConditions"/>）。
    /// </summary>
    public static class HeatingPower
    {
        /// <summary>预热阶段注热功率（kW），测试值。</summary>
        public static float PreheatKilowatts = 1.2f;

        /// <summary>补热阶段注热功率（kW）：设计的 200 W 额外用电按 1:1 换算，测试值。</summary>
        public static float TopUpKilowatts = 0.2f;

        /// <summary>强制运行的持续加热功率（kW），暂沿用预热值，测试值。</summary>
        public static float SustainedKilowatts = 1.2f;

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
