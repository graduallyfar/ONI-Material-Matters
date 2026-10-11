using MaterialOverhaul.Core;
using MaterialOverhaul.Zones;

namespace MaterialOverhaul.Heating
{
    /// <summary>
    /// 各模式下的温控边界线。已确认公式（V0.0.1 开发计划 §5.1、总体设计 §2.2、V0.0.3 开发计划 §2.1、§3.2）：
    ///
    ///   - 低温启动线（**开工资格**线）：本批尚未开工时必须先到这条线才开始生产。
    ///       默认模式：WorkMin + 0.1 × (BestMin − WorkMin)   → 铝配方 315 ℃
    ///       控制模式：BestMin + 0.1 × (BestMax − BestMin)   → 铝配方 470 ℃
    ///   - 生产下限（**已开工**后仍可生产的线）：开工后跌到这条线以下才重新进入低温停机。
    ///       默认模式：WorkMin（铝配方 300 ℃）
    ///       控制模式：BestMin（铝配方 450 ℃）
    ///   - 高温暂停线：超过即暂停生产并关闭普通加热。
    ///       默认模式：WorkMax（800 ℃）；控制模式：BestMax（650 ℃）
    ///   - 高温恢复线：暂停后回落到这条线以下才重新检查全部条件（含低温侧）。
    ///     回差为本版暂定值（默认模式 15 K、控制模式 10 K，见 <see cref="Balance"/>）。
    ///
    /// 「启动线」不是每个模拟步都替代生产下限：默认模式已经开工后在 305 ℃仍能生产，
    /// 控制模式已经开工后在 455 ℃仍能生产并补热。
    /// </summary>
    public static class StartThreshold
    {
        /// <summary>十分之一系数，来自已确认的阈值公式。</summary>
        public const float Fraction = Balance.StartFraction;

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

        /// <summary>当前模式对应的低温启动线。</summary>
        public static float LowStart(TempZone zone, RunPolicy policy)
        {
            return policy.ControlMode ? Control(zone) : Default(zone);
        }

        /// <summary>当前模式对应的生产下限：开工后跌破它才重新进入低温停机。</summary>
        public static float LowWorkLine(TempZone zone, RunPolicy policy)
        {
            return policy.ControlMode ? zone.BestMin : zone.WorkMin;
        }

        /// <summary>当前模式对应的高温暂停线。</summary>
        public static float HighPauseLine(TempZone zone, RunPolicy policy)
        {
            return policy.ControlMode ? zone.BestMax : zone.WorkMax;
        }

        /// <summary>高温暂停后的恢复线；本版暂定回差，位于暂停线之下。</summary>
        public static float HighResumeLine(TempZone zone, RunPolicy policy)
        {
            float margin = policy.ControlMode ? Balance.ControlHighRecoveryKelvin : Balance.DefaultHighRecoveryKelvin;
            float line = HighPauseLine(zone, policy) - margin;
            return line < zone.WorkMin ? zone.WorkMin : line;
        }
    }
}
