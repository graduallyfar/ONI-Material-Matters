using System;
using MaterialOverhaul.Core;

namespace MaterialOverhaul.Heating
{
    /// <summary>
    /// v0.1.1：预热、补热、强制持续加热共用每栋建筑的设置。
    /// 电网与注热从同一个 W 值推导；不再维护全局可写的两套功率。
    /// 注热仍沿用暂定倍率，Q(kJ) = kW × dt(s)，不是固定温升。
    /// </summary>
    public static class HeatingPower
    {
        public const float TestHeatMultiplier = Balance.HeatMultiplier;

        /// <summary>非法值回默认，越界钳制，按 1 W 精度保存；0 是旧数据缺省值。</summary>
        public static float NormalizeWatts(float watts)
        {
            if (float.IsNaN(watts) || float.IsInfinity(watts) || watts == 0f)
            {
                return Balance.DefaultHeatingWatts;
            }

            return (float)Math.Round(Math.Max(Balance.MinHeatingWatts, Math.Min(Balance.MaxHeatingWatts, watts)));
        }

        public static float HeatingWattsFor(HeatingIntent intent, float configuredWatts = Balance.DefaultHeatingWatts)
        {
            switch (intent)
            {
                case HeatingIntent.Preheat:
                case HeatingIntent.TopUp:
                case HeatingIntent.Sustained:
                    return NormalizeWatts(configuredWatts);
                default:
                    return 0f;
            }
        }

        public static float KilowattsFor(HeatingIntent intent, float configuredWatts = Balance.DefaultHeatingWatts)
        {
            return Balance.KilowattsForWatts(HeatingWattsFor(intent, configuredWatts));
        }

        public static float AppliedHeatingWatts(HeatingIntent intent, bool powered, float configuredWatts = Balance.DefaultHeatingWatts)
        {
            return powered ? HeatingWattsFor(intent, configuredWatts) : 0f;
        }
    }
}
