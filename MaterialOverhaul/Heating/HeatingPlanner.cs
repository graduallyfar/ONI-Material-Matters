using MaterialOverhaul.Core;
using MaterialOverhaul.Zones;

namespace MaterialOverhaul.Heating
{
    /// <summary>
    /// 预热与补热的意图判定。纯逻辑，不依赖游戏对象。
    ///
    /// 已确认规则（建筑细节文档 §2.1.1、§2.1.2、§2.1.4、§2.2.1）：
    ///   - 默认模式只在「尚未开工且低于启动线」时预热；开工后在整个可工作温区内生产，不再补热。
    ///   - 控制模式预热到最佳温区启动线；已在生产且低于补热启动线时边生产边补热。
    ///   - **补热停止线高于补热启动线**，两者都在最佳温区内部，避免在同一温度点反复启停；
    ///     补热一旦开始就要加热到停止线才关闭，因此判定需要传入当前补热状态。
    ///   - 超过高温暂停线时停止内置加热，等待降温；不提供耐热保护。
    ///   - 强制运行开启后持续加热，不因达到最佳温区或越界而停止。
    ///
    /// 完整的运行状态机（启动资格、高温等待、回差）在 <see cref="RunController"/>；
    /// 本类只回答「该注入哪一档热量」，供状态机与显示共用。
    /// </summary>
    public static class HeatingPlanner
    {
        /// <summary>补热回差（K）。本版暂定值，见 <see cref="Balance.TopUpHysteresisKelvin"/>。</summary>
        public const float HysteresisKelvin = Balance.TopUpHysteresisKelvin;

        /// <summary>补热启动线：低于此线开始补热。位于最佳温区内部、低于停止线。</summary>
        public static float TopUpLine(TempZone zone, RunPolicy policy)
        {
            return TopUpStopLine(zone, policy) - Hysteresis(zone);
        }

        /// <summary>补热停止线：达到此线关闭补热。控制模式下取预热停止线。</summary>
        public static float TopUpStopLine(TempZone zone, RunPolicy policy)
        {
            return PreheatTarget(zone, policy);
        }

        /// <summary>预热目标线：默认模式取最低工作启动阈值，控制模式取最佳工作启动阈值。</summary>
        public static float PreheatTarget(TempZone zone, RunPolicy policy)
        {
            return StartThreshold.LowStart(zone, policy);
        }

        /// <summary>
        /// 当前温区下的补热回差。
        ///
        /// 已确认的只是结构（停止线高于启动线、两者都在最佳温区内部）；回差数值属本版暂定。
        /// 这里用固定开尔文数而不是比例，是为了让「460 开始 / 470 停止」在配置里直接可读；
        /// 同时不允许回差大到把启动线推到最佳温区之外。
        /// </summary>
        public static float Hysteresis(TempZone zone)
        {
            float width = zone.BestMax - zone.BestMin;
            if (width <= 0f)
            {
                return 0f;
            }

            return HysteresisKelvin > width ? width : HysteresisKelvin;
        }

        /// <summary>未跟踪补热状态时的简化入口：按「尚未补热」判断，只会启动补热、不会维持。</summary>
        public static HeatingIntent Decide(float bodyTemp, TempZone zone, RunPolicy policy, bool productionRunning)
        {
            return Decide(bodyTemp, zone, policy, productionRunning, false);
        }

        /// <summary>
        /// 给出加热意图。调用方负责再叠加批次、供电、手动禁用与损坏条件
        /// （见 <see cref="HeatingConditions.CanHeat"/>）。
        /// </summary>
        /// <param name="productionRunning">当前是否正在推进生产（已锁定批次且已开工）。</param>
        /// <param name="topUpActive">当前是否正在补热；为 true 时按停止线判断是否关闭，实现回差。</param>
        public static HeatingIntent Decide(float bodyTemp, TempZone zone, RunPolicy policy, bool productionRunning, bool topUpActive)
        {
            if (zone.IsDisabled)
            {
                return HeatingIntent.None;
            }

            if (policy.Forced)
            {
                return HeatingIntent.Sustained;
            }

            if (bodyTemp > StartThreshold.HighPauseLine(zone, policy))
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
