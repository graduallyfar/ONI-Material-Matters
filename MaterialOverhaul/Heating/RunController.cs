using MaterialOverhaul.Zones;

namespace MaterialOverhaul.Heating
{
    /// <summary>
    /// 温控运行状态机（V0.0.3 开发计划 §3.1–§3.3）。纯逻辑，不依赖游戏对象。
    ///
    /// 设计上把三件事分开（§3.1）：
    ///   1. **批次准备**：由原版与订单层负责，红信号不阻止取料与锁定；
    ///   2. **加热许可与意图**：本状态机给出意图，再由 <see cref="HeatingConditions"/> 叠加
    ///      「有无有效批次、是否供电、是否手动禁用、是否损坏停机」；
    ///   3. **真实生产**：本状态机只给出「温度条件是否允许」，
    ///      供电、自动化生产许可、操作工等条件仍由原版判断，不能在这里代填。
    ///
    /// 因此 <see cref="RunDecision.AllowsProduction"/> 与 <see cref="RunDecision.Heating"/> 是两个
    /// 独立事实：红信号下可以补热但不生产，高温等待下两者都关。
    /// </summary>
    public static class RunController
    {
        /// <summary>
        /// 每个模拟步的运行判断。调用方负责把 <see cref="RunDecision.Next"/> 写回持久化状态。
        /// </summary>
        public static RunDecision Update(float bodyTemp, TempZone zone, RunPolicy policy, RunState state)
        {
            if (zone.IsDisabled)
            {
                return RunDecision.Passthrough;
            }

            TempState evaluated = TempZoneEvaluator.Evaluate(bodyTemp, zone);
            float efficiency = TempZoneEvaluator.Efficiency(evaluated, bodyTemp, zone);

            RunDecision decision = default(RunDecision);
            decision.State = evaluated;
            decision.Efficiency = efficiency;
            decision.ZoneConfigured = true;
            decision.Next = state;

            if (policy.Forced)
            {
                // 强制运行覆盖温区停机与启动等待，并持续加热；效率仍按五态取值。
                decision.AllowsProduction = true;
                decision.Heating = HeatingIntent.Sustained;
                decision.Next.Started = true;
                decision.Next.HighTempWaiting = false;
                decision.Next.TopUpActive = false;
                return decision;
            }

            float pauseLine = StartThreshold.HighPauseLine(zone, policy);

            if (state.HighTempWaiting)
            {
                if (bodyTemp > StartThreshold.HighResumeLine(zone, policy))
                {
                    // 仍高于恢复线：保持高温等待，生产与普通加热都关闭。
                    decision.AllowsProduction = false;
                    decision.Heating = HeatingIntent.None;
                    decision.Next.HighTempWaiting = true;
                    decision.Next.TopUpActive = false;
                    return decision;
                }

                // 已回落到恢复线以下：解除等待，继续按低温侧与生产条件重新检查。
                decision.Next.HighTempWaiting = false;
            }
            else if (bodyTemp > pauseLine)
            {
                decision.AllowsProduction = false;
                decision.Heating = HeatingIntent.None;
                decision.Next.HighTempWaiting = true;
                decision.Next.TopUpActive = false;
                return decision;
            }

            float lowStart = StartThreshold.LowStart(zone, policy);
            float lowWork = StartThreshold.LowWorkLine(zone, policy);

            if (decision.Next.Started)
            {
                if (bodyTemp < lowWork)
                {
                    // 跌破生产下限：低温停机，必须重新预热到启动线。
                    decision.Next.Started = false;
                    decision.Next.TopUpActive = false;
                }
            }
            else if (bodyTemp >= lowStart)
            {
                // 初始化、读档或模式切换时温度已经高于启动线：直接取得开工资格。
                decision.Next.Started = true;
            }

            if (!decision.Next.Started)
            {
                decision.AllowsProduction = false;
                decision.Heating = bodyTemp < lowStart ? HeatingIntent.Preheat : HeatingIntent.None;
                return decision;
            }

            if (policy.ControlMode)
            {
                decision.AllowsProduction = bodyTemp >= zone.BestMin && bodyTemp <= zone.BestMax;
                decision.Heating = DecideTopUp(bodyTemp, zone, policy, decision.Next.TopUpActive);
                decision.Next.TopUpActive = decision.Heating == HeatingIntent.TopUp;
                return decision;
            }

            // 默认模式：开工后在整个可工作温区内生产，不再补热。
            decision.AllowsProduction = bodyTemp >= zone.WorkMin && bodyTemp <= zone.WorkMax;
            decision.Heating = HeatingIntent.None;
            decision.Next.TopUpActive = false;
            return decision;
        }

        /// <summary>
        /// 新批次锁定时的状态重建（§3.2「新批次」）。低于当前模式的启动线就先预热，
        /// 已经高于启动线则直接取得开工资格；已投料批次不会因此丢进度，只是重新决定要不要加热。
        /// </summary>
        public static RunState OnBatchLocked(float bodyTemp, TempZone zone, RunPolicy policy, RunState state)
        {
            return Rebuild(bodyTemp, zone, policy, state);
        }

        /// <summary>
        /// 模式切换或关闭强制运行后的状态重建（§3.3）。保留批次与进度，只按新模式重新建立
        /// 启动资格、高温等待与加热意图；旧目标与旧绿信号不能留到下一批。
        /// </summary>
        public static RunState OnPolicyChanged(float bodyTemp, TempZone zone, RunPolicy policy, RunState state)
        {
            return Rebuild(bodyTemp, zone, policy, state);
        }

        /// <summary>
        /// 读档后的状态校验（§5.3）。保留已保存的回差状态：
        /// 若保存时正在补热且温度仍在停止线以下，不能因为读档就清掉补热状态。
        /// </summary>
        public static RunState OnLoaded(float bodyTemp, TempZone zone, RunPolicy policy, RunState saved)
        {
            if (zone.IsDisabled)
            {
                return RunState.Fresh;
            }

            RunState state = saved;

            if (policy.Forced)
            {
                state.Started = true;
                state.HighTempWaiting = false;
                state.TopUpActive = false;
                return state;
            }

            // 高温等待与低温资格按实际温度重新校验，不能无条件重新预热或保留等待。
            state.HighTempWaiting = bodyTemp > StartThreshold.HighPauseLine(zone, policy);

            if (!state.HighTempWaiting)
            {
                if (state.Started && bodyTemp < StartThreshold.LowWorkLine(zone, policy))
                {
                    state.Started = false;
                }
                else if (!state.Started && bodyTemp >= StartThreshold.LowStart(zone, policy))
                {
                    state.Started = true;
                }
            }

            if (!policy.ControlMode)
            {
                // 默认模式没有补热阶段。
                state.TopUpActive = false;
            }
            else if (state.TopUpActive && bodyTemp >= HeatingPlanner.TopUpStopLine(zone, policy))
            {
                state.TopUpActive = false;
            }

            return state;
        }

        private static RunState Rebuild(float bodyTemp, TempZone zone, RunPolicy policy, RunState state)
        {
            if (zone.IsDisabled)
            {
                return RunState.Fresh;
            }

            RunState next = state;
            next.TopUpActive = false;
            next.HighTempWaiting = bodyTemp > StartThreshold.HighPauseLine(zone, policy);
            next.Started = policy.Forced || bodyTemp >= StartThreshold.LowStart(zone, policy);
            return next;
        }

        /// <summary>
        /// 控制模式补热意图，带「开始线 / 停止线」回差：已经开始就加热到停止线，
        /// 否则要跌破开始线才启动（避免在同一温度点反复启停）。
        /// </summary>
        private static HeatingIntent DecideTopUp(float bodyTemp, TempZone zone, RunPolicy policy, bool topUpActive)
        {
            float line = topUpActive
                ? HeatingPlanner.TopUpStopLine(zone, policy)
                : HeatingPlanner.TopUpLine(zone, policy);

            return bodyTemp < line ? HeatingIntent.TopUp : HeatingIntent.None;
        }
    }
}
