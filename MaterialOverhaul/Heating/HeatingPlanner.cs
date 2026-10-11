using MaterialOverhaul.Zones;

namespace MaterialOverhaul.Heating
{
    /// <summary>
    /// 预热与补热的意图判定。纯逻辑，不依赖游戏对象。
    ///
    /// 已确认规则（建筑细节文档 §2.1.1、§2.1.2、§2.1.4、§2.2.1）：
    ///   - 默认模式只在「未开工且低于启动阈值」时预热；开工后在整个可工作温区内生产，不再补热。
    ///   - 控制模式预热到最佳温区启动阈值；已在生产且低于补热启动线时边生产边补热。
    ///   - **补热停止线高于补热启动线**，两者都在最佳温区内部，避免在同一温度点反复启停。
    ///     补热一旦开始就要加热到停止线才关闭，因此判定需要传入当前补热状态。
    ///   - 超过最高可工作温度时停止内置加热，等待降温；不提供耐热保护。
    ///   - 强制运行开启后持续加热，不因达到最佳温区或越过最高可工作温度而停止。
    /// </summary>
    public static class HeatingPlanner
    {
        /// <summary>
        /// 补热回差**占位值**：占最佳温区宽度的比例。
        ///
        /// 已确认的只是结构（停止线高于启动线、两者都在最佳温区内部），
        /// 回差具体数值属数值阶段（开发计划 §10 第 7 项）。当前占位值用于让回差机制可运行、可测试，
        /// **不是最终数值**，定案后只替换本常量。
        /// </summary>
        public const float HysteresisFractionPlaceholder = 0.05f;

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
            return policy.ControlMode ? StartThreshold.Control(zone) : StartThreshold.Default(zone);
        }

        /// <summary>当前温区下的补热回差。</summary>
        public static float Hysteresis(TempZone zone)
        {
            float width = zone.BestMax - zone.BestMin;
            return width <= 0f ? 0f : HysteresisFractionPlaceholder * width;
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

            if (TempZoneEvaluator.Evaluate(bodyTemp, zone) == TempState.AboveWorkMax)
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
