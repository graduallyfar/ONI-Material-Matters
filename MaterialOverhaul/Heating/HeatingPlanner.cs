using MaterialOverhaul.Zones;

namespace MaterialOverhaul.Heating
{
    /// <summary>
    /// 预热与补热的意图判定。纯逻辑，不依赖游戏对象。
    ///
    /// 已确认规则（建筑细节文档 §2.1.1、§2.1.2、§2.1.4、§2.2.1）：
    ///   - 默认模式只在"未开工且低于启动阈值"时预热；开工后在整个可工作温区内生产，不再补热。
    ///   - 控制模式预热到最佳温区启动阈值；已在生产且低于补热线时边生产边补热。
    ///   - 超过最高可工作温度时停止内置加热，等待降温。
    ///   - 强制运行开启后持续加热，不因达到最佳温区或越过最高可工作温度而停止。
    ///
    /// **待确认的文档冲突：**开发计划 §5.4 记「补热启动线与补热停止线均取预热停止线（已确认）」，
    /// 而设计细节 §2.1.2 要求「补热停止线高于补热启动线，两者都在最佳温区内部……避免在同一温度点反复启停」。
    /// 两者不能同时成立：取同一条线会在同一温度点反复启停。当前按开发计划实现（同一条线），
    /// 是否改为带回差的两条线待确认；改为两线时只需替换 <see cref="TopUpLine"/> 与 <see cref="TopUpStopLine"/>。
    /// </summary>
    public static class HeatingPlanner
    {
        /// <summary>补热启动线。当前与补热停止线取同一点（开发计划 §5.4）。</summary>
        public static float TopUpLine(TempZone zone, RunPolicy policy)
        {
            return policy.ControlMode ? StartThreshold.Control(zone) : StartThreshold.Default(zone);
        }

        /// <summary>补热停止线。当前与补热启动线取同一点（开发计划 §5.4），见类型说明中的冲突记录。</summary>
        public static float TopUpStopLine(TempZone zone, RunPolicy policy)
        {
            return TopUpLine(zone, policy);
        }

        /// <summary>预热目标线：默认模式取最低工作启动阈值，控制模式取最佳工作启动阈值。</summary>
        public static float PreheatTarget(TempZone zone, RunPolicy policy)
        {
            return policy.ControlMode ? StartThreshold.Control(zone) : StartThreshold.Default(zone);
        }

        /// <summary>
        /// 给出加热意图。调用方负责再叠加批次、供电、手动禁用与损坏条件
        /// （见 <see cref="HeatingConditions.CanHeat"/>）。
        /// </summary>
        /// <param name="productionRunning">当前是否正在推进生产（已锁定批次且已开工）。</param>
        public static HeatingIntent Decide(float bodyTemp, TempZone zone, RunPolicy policy, bool productionRunning)
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
                // 高温保护：停止生产和内置加热，等待降温；不提供耐热保护。
                return HeatingIntent.None;
            }

            if (productionRunning)
            {
                // 只有控制模式有补热阶段；默认模式开工后不补热。
                if (policy.ControlMode && bodyTemp < TopUpStopLine(zone, policy))
                {
                    return HeatingIntent.TopUp;
                }

                return HeatingIntent.None;
            }

            return bodyTemp < PreheatTarget(zone, policy) ? HeatingIntent.Preheat : HeatingIntent.None;
        }
    }
}
