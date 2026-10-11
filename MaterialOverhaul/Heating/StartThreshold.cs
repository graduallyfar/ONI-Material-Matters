using MaterialOverhaul.Zones;

namespace MaterialOverhaul.Heating
{
    /// <summary>
    /// 启动阈值。公式属于已确认设计（V0.0.1 开发计划 §5.1、总体设计 §2.2）：
    ///   默认模式：WorkMin + 0.1 × (BestMin − WorkMin)
    ///   控制模式：BestMin + 0.1 × (BestMax − BestMin)
    /// 控制模式只在最佳温区内推进生产，但不限制预热只能发生在最佳温区内。
    /// </summary>
    public static class StartThreshold
    {
        /// <summary>十分之一系数，来自已确认的阈值公式。</summary>
        public const float Fraction = 0.1f;

        /// <summary>默认模式的开工启动阈值 T_work,start。</summary>
        public static float Default(TempZone zone)
        {
            return zone.WorkMin + Fraction * (zone.BestMin - zone.WorkMin);
        }

        /// <summary>控制模式的开工启动阈值 T_best,start。</summary>
        public static float Control(TempZone zone)
        {
            return zone.BestMin + Fraction * (zone.BestMax - zone.BestMin);
        }
    }
}
