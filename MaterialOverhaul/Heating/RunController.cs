using MaterialOverhaul.Zones;

namespace MaterialOverhaul.Heating
{
    /// <summary>
    /// 温控运行状态机（V0.0.3 开发计划 §3、V0.1.2 开发计划 §2.2–§2.4）。纯逻辑，不依赖游戏对象。
    ///
    /// 设计上把三件事分开：
    ///   1. **批次准备**：由原版与订单层负责，红信号不阻止取料与锁定；
    ///   2. **加热许可与意图**：本状态机给出意图，再由 <see cref="HeatingConditions"/> 叠加
    ///      「有无有效批次、是否供电、是否手动禁用、是否损坏停机」；
    ///   3. **真实生产**：本状态机只给出「温度条件是否允许」，
    ///      供电、自动化生产许可、操作工等条件仍由原版判断，不能在这里代填。
    ///
    /// V0.1.2 起生产许可按**建筑范围优先**判断：
    ///   - 默认模式：`L ≤ T ≤ U` 即允许生产，配方最佳区以外只是效率下降；
    ///   - 控制模式：还需要 `B₁ ≤ T ≤ min(B₂, U)`；
    ///   - 两种模式：`T &gt; U` 都停产并停止内置加热（强制运行除外）；
    ///   - `B₁ &gt; U`（最佳温区不可达）：控制模式预热到 U 后停止加热并保持生产暂停，不自动跳单；
    ///   - `B₁ = U`（单点交集）：只有恰好落在该点才允许控制模式生产，恢复回差退化为 0。
    /// </summary>
    public static class RunController
    {
        /// <summary>
        /// 每个模拟步的运行判断。调用方负责把 <see cref="RunDecision.Next"/> 写回持久化状态。
        /// </summary>
        public static RunDecision Update(float bodyTemp, ResolvedZone zone, RunPolicy policy, RunState state)
        {
            if (!zone.Configured)
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

            bool control = policy.ControlMode;

            if (policy.Forced)
            {
                // 强制运行覆盖温区停机与启动等待，并持续加热；效率仍按温度状态取值。
                decision.AllowsProduction = true;
                decision.Heating = zone.NoBuiltInHeating ? HeatingIntent.None : HeatingIntent.Sustained;
                decision.Next.Started = true;
                decision.Next.HighTempWaiting = false;
                decision.Next.TopUpActive = false;
                decision.Next.ZoneUnreachableLatched = false;
                return decision;
            }

            if (state.HighTempWaiting)
            {
                float resumeLine = StartThreshold.ResumeLine(zone, policy);

                if (bodyTemp > resumeLine)
                {
                    // 仍高于恢复线：保持高温等待，生产与普通加热都关闭。
                    decision.AllowsProduction = false;
                    decision.Heating = HeatingIntent.None;
                    decision.Next.HighTempWaiting = true;
                    decision.Next.TopUpActive = false;
                    decision.Next.ZoneUnreachableLatched = zone.ControlRangeUnreachable;
                    return decision;
                }

                // 已回落到恢复线或以下：解除等待，继续按低温侧与生产条件重新检查。
                decision.Next.HighTempWaiting = false;
            }
            else if (bodyTemp > StartThreshold.PauseLine(zone, policy))
            {
                decision.AllowsProduction = false;
                decision.Heating = HeatingIntent.None;
                decision.Next.HighTempWaiting = true;
                decision.Next.TopUpActive = false;
                // 高温保护与「最佳温区不可达」可以同时成立：状态栏据此区分
                // 「等降温」与「温度受限 · 生产暂停」，不把建材限制说成普通等待。
                decision.Next.ZoneUnreachableLatched = zone.ControlRangeUnreachable;
                return decision;
            }

            float lowStart = StartThreshold.Start(zone, policy);
            float lowWork = StartThreshold.WorkLine(zone, policy);

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
                decision.Heating = !zone.NoBuiltInHeating && bodyTemp < lowStart ? HeatingIntent.Preheat : HeatingIntent.None;
                decision.Next.ZoneUnreachableLatched = control && zone.ControlRangeUnreachable
                    && (state.ZoneUnreachableLatched || bodyTemp >= lowStart);
                return decision;
            }

            if (control)
            {
                // 控制模式只在配方最佳区与建筑范围的交集内生产。
                // B₁ > U 时交集为空：温度永远不会满足，生产保持暂停，但预热到 U 仍然发生。
                decision.AllowsProduction = evaluated.AllowsControlProduction();
                decision.Next.ZoneUnreachableLatched = zone.ControlRangeUnreachable;

                decision.Heating = DecideTopUp(bodyTemp, zone, policy, decision.Next.TopUpActive);
                decision.Next.TopUpActive = decision.Heating == HeatingIntent.TopUp;
                return decision;
            }

            // 默认模式：开工后在整个建筑可工作温区 [L, U] 内生产，不补热。
            decision.AllowsProduction = evaluated.AllowsProduction();
            decision.Heating = HeatingIntent.None;
            decision.Next.TopUpActive = false;
            decision.Next.ZoneUnreachableLatched = false;
            return decision;
        }

        /// <summary>
        /// 新批次锁定时的状态重建。低于当前模式的实际启动目标就先预热，
        /// 已经高于它则直接取得开工资格；已投料批次不会因此丢进度，只是重新决定要不要加热。
        /// </summary>
        public static RunState OnBatchLocked(float bodyTemp, ResolvedZone zone, RunPolicy policy, RunState state)
        {
            return Rebuild(bodyTemp, zone, policy, state);
        }

        /// <summary>
        /// 模式切换或关闭强制运行后的状态重建。保留批次与进度，只按新模式重新建立
        /// 启动资格、高温等待与加热意图；旧目标与旧绿信号不能留到下一批。
        ///
        /// 控制模式重建时立即重新判定「最佳温区不可达」，使切到低熔点建材或高最佳区配方后
        /// 状态栏不会等到下一步才更新。
        /// </summary>
        public static RunState OnPolicyChanged(float bodyTemp, ResolvedZone zone, RunPolicy policy, RunState state)
        {
            return Rebuild(bodyTemp, zone, policy, state);
        }

        /// <summary>
        /// 读档后的状态校验（V0.0.3 §5.3）。保留已保存的回差状态：
        /// 若保存时正在补热且温度仍在停止线以下，不能因为读档就清掉补热状态。
        ///
        /// V0.1.2 追加：读档时按**当前主建造材料**重算的建筑上限重新校验，
        /// 新阈值更低时不会抢跑生产（`Started` 与 `HighTempWaiting` 都按新线重判）。
        /// </summary>
        public static RunState OnLoaded(float bodyTemp, ResolvedZone zone, RunPolicy policy, RunState saved)
        {
            if (!zone.Configured)
            {
                return RunState.Fresh;
            }

            RunState state = saved;

            if (policy.Forced)
            {
                state.Started = true;
                state.HighTempWaiting = false;
                state.TopUpActive = false;
                state.ZoneUnreachableLatched = false;
                return state;
            }

            // 高温等待与低温资格按实际温度重新校验，不能无条件重新预热或保留等待。
            state.HighTempWaiting = saved.HighTempWaiting
                ? bodyTemp > StartThreshold.ResumeLine(zone, policy)
                : bodyTemp > StartThreshold.PauseLine(zone, policy);

            if (!state.HighTempWaiting)
            {
                if (state.Started && bodyTemp < StartThreshold.WorkLine(zone, policy))
                {
                    state.Started = false;
                }
                else if (!state.Started && bodyTemp >= StartThreshold.Start(zone, policy))
                {
                    state.Started = true;
                }
            }

            if (!policy.ControlMode)
            {
                // 默认模式没有补热阶段。
                state.TopUpActive = false;
                state.ZoneUnreachableLatched = false;
            }
            else if (!state.Started || state.HighTempWaiting
                || (state.TopUpActive && bodyTemp >= HeatingPlanner.TopUpStopLine(zone, policy)))
            {
                state.TopUpActive = false;
            }

            if (policy.ControlMode)
            {
                state.ZoneUnreachableLatched = zone.ControlRangeUnreachable;
            }

            if (zone.NoBuiltInHeating) state.TopUpActive = false;
            return state;
        }

        private static RunState Rebuild(float bodyTemp, ResolvedZone zone, RunPolicy policy, RunState state)
        {
            if (!zone.Configured)
            {
                return RunState.Fresh;
            }

            RunState next = state;
            next.TopUpActive = false;
            next.HighTempWaiting = bodyTemp > StartThreshold.PauseLine(zone, policy);
            next.Started = policy.Forced || bodyTemp >= StartThreshold.Start(zone, policy);
            next.ZoneUnreachableLatched = policy.ControlMode && zone.ControlRangeUnreachable;
            return next;
        }

        /// <summary>
        /// 控制模式补热意图，带「开始线 / 停止线」回差：已经开始就加热到停止线，
        /// 否则要跌破开始线才启动（避免在同一温度点反复启停）。
        /// </summary>
        private static HeatingIntent DecideTopUp(float bodyTemp, ResolvedZone zone, RunPolicy policy, bool topUpActive)
        {
            if (zone.NoBuiltInHeating) return HeatingIntent.None;
            if (zone.ControlRangeUnreachable)
                return bodyTemp < StartThreshold.Start(zone, policy) ? HeatingIntent.Preheat : HeatingIntent.None;
            float line = topUpActive
                ? HeatingPlanner.TopUpStopLine(zone, policy)
                : HeatingPlanner.TopUpLine(zone, policy);

            return bodyTemp < line ? HeatingIntent.TopUp : HeatingIntent.None;
        }
    }
}
