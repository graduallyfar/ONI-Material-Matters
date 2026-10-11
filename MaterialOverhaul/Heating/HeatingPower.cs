namespace MaterialOverhaul.Heating
{
    /// <summary>
    /// 加热功率配置。**数值阶段填写，当前全部为 0。**
    ///
    /// 开发计划 §5.3、§5.5 明确「加热功率与热量转换关系为空函数占位」，
    /// 因此这里默认不注入任何热量：机制就位，但不产生未定数值的玩法影响。
    /// 填入数值即为生效，不需要改动判断层与接入层。
    ///
    /// 单位是 kW：注入热量 = kW × dt（秒）= kJ，与原版 SteamTurbine2 的写法一致
    /// （`ProduceEnergy(handle, 8f * dt, ...)`）。
    ///
    /// **注入的是热量而不是固定温升**：实际温升由 SIM 按建筑热容与换热计算
    /// （原版建筑有效热容写作 Def.MassForTemperatureModification × Element.specificHeatCapacity）。
    /// </summary>
    public static class HeatingPower
    {
        /// <summary>预热阶段功率（kW）。</summary>
        public static float PreheatKilowatts;

        /// <summary>补热阶段功率（kW）。设计文档另定「补热额外消耗 200 W 用电」，那是用电口径，与注热功率不是同一个量。</summary>
        public static float TopUpKilowatts;

        /// <summary>强制运行的持续加热功率（kW）。</summary>
        public static float SustainedKilowatts;

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
