using MaterialOverhaul.Core;
using MaterialOverhaul.Zones;

namespace MaterialOverhaul.Heating
{
    /// <summary>
    /// 预热与补热的意图判定（V0.1.2 开发计划 §2.3、§2.4）。纯逻辑，不依赖游戏对象。
    ///
    /// 全部目标线都经过 <see cref="StartThreshold"/> 的建筑上限封顶：
    ///   - 预热目标 = 实际启动目标 S = min(原启动目标, U)；
    ///   - 补热停止线 = S（原实现取同一值，本版只增加封顶）；
    ///   - 补热启动线 = max(控制模式生产下限 B₁, 停止线 − 回差)。
    ///
    /// **回差的取法是本版暂定值**（V0.1.2 §7 第 3 项）：仍用固定开尔文数而不是比例，
    /// 但不允许启动线跌到 B₁ 以下，也不允许出现「预热目标高于停止线」的倒置目标。
    /// 单点交集（`B₁ = U`）时回差为 0，不给玩家一个达不到的保温目标。
    ///
    /// 完整运行状态机（启动资格、高温等待、回差状态）在 <see cref="RunController"/>；
    /// 本类只回答「该注入哪一档热量」。
    /// </summary>
    public static class HeatingPlanner
    {
        /// <summary>补热回差（K）。本版暂定值，见 <see cref="Balance.TopUpHysteresisKelvin"/>。</summary>
        public const float HysteresisKelvin = Balance.TopUpHysteresisKelvin;

        /// <summary>补热停止线：达到此线关闭补热。等于（已封顶的）启动目标。</summary>
        public static float TopUpStopLine(ResolvedZone zone, RunPolicy policy)
        {
            return StartThreshold.Start(zone, policy);
        }

        /// <summary>
        /// 预热目标线：与补热停止线同值。
        /// 保留这个方法名是为了让「预热目标」与「补热目标」在调用处仍然读得出来，
        /// 两者共用同一个封顶后的实际目标，不允许各自取一套线。
        /// </summary>
        public static float PreheatTarget(ResolvedZone zone, RunPolicy policy)
        {
            return StartThreshold.Start(zone, policy);
        }

        /// <summary>补热启动线：低于此线开始补热。</summary>
        public static float TopUpLine(ResolvedZone zone, RunPolicy policy)
        {
            if (!zone.Configured)
            {
                return 0f;
            }

            float line = TopUpStopLine(zone, policy) - Hysteresis(zone);

            // 控制模式的补热只服务于最佳区生产：不把启动线压到生产下限以下。
            if (policy.ControlMode && line < zone.BestMin)
            {
                line = zone.BestMin;
            }

            return line;
        }

        /// <summary>当前温区下的补热回差：固定开尔文数，但不允许超过最佳温区宽度。</summary>
        public static float Hysteresis(ResolvedZone zone)
        {
            if (!zone.Configured)
            {
                return 0f;
            }

            float width = zone.BestMax - zone.BestMin;
            if (width <= 0f)
            {
                return 0f;
            }

            return HysteresisKelvin > width ? width : HysteresisKelvin;
        }

        /// <summary>未跟踪补热状态时的简化入口：按「尚未补热」判断，只会启动补热、不会维持。</summary>
        public static HeatingIntent Decide(float bodyTemp, ResolvedZone zone, RunPolicy policy, bool productionRunning)
        {
            return Decide(bodyTemp, zone, policy, productionRunning, false);
        }

        /// <summary>
        /// 给出加热意图。调用方负责再叠加批次、供电、手动禁用与损坏条件
        /// （见 <see cref="HeatingConditions.CanRequestHeat"/>）。
        /// </summary>
        /// <param name="productionRunning">当前是否正在推进生产（已锁定批次且已开工）。</param>
        /// <param name="topUpActive">当前是否正在补热；为 true 时按停止线判断是否关闭，实现回差。</param>
        public static HeatingIntent Decide(float bodyTemp, ResolvedZone zone, RunPolicy policy, bool productionRunning, bool topUpActive)
        {
            if (!zone.Configured)
            {
                return HeatingIntent.None;
            }

            if (policy.Forced)
            {
                return HeatingIntent.Sustained;
            }

            if (bodyTemp > StartThreshold.PauseLine(zone, policy))
            {
                // 高温保护：停止生产和内置加热，等待降温。
                return HeatingIntent.None;
            }

            if (productionRunning)
            {
                // 只有控制模式有补热阶段；默认模式开工后不补热。
                if (!policy.ControlMode)
                {
                    return HeatingIntent.None;
                }

                float line = topUpActive ? TopUpStopLine(zone, policy) : TopUpLine(zone, policy);
                return bodyTemp < line ? HeatingIntent.TopUp : HeatingIntent.None;
            }

            return bodyTemp < PreheatTarget(zone, policy) ? HeatingIntent.Preheat : HeatingIntent.None;
        }
    }
}
