using MaterialOverhaul.Heating;
using MaterialOverhaul.Zones;

namespace MaterialOverhaul.Game
{
    /// <summary>
    /// 把建筑当前的真实条件组合成状态文字（V0.1.2 开发计划 §3、§4）。
    ///
    /// 规则（每一条都对应文档里的一行）：
    ///   - 「允许生产」不等于「正在生产」：温度许可、自动化许可、操作工与供电都成立才算生产；
    ///   - 只有**实际注热**时才写预热／补热／持续加热；仅有需求但断电时写等待供电；
    ///   - 「等待升温」用于需要升温但没有注热的情况，不假称正在预热；
    ///   - 「降温中」只在满足实测趋势时追加，本版没有主动制冷；
    ///   - **过热风险与温度效率彼此独立**：强制运行越过建筑上限但仍在配方最佳区内时，
    ///     运行项仍写「生产中 · 持续加热」，不误报高温低效，过热风险由独立状态项给出；
    ///   - 可组合，例如「生产已被自动化暂停 · 补热中」「低温低效生产 · 持续加热」。
    ///
    /// 分类顺序参考 V0.1.2 §4.3：先判真实阻断（损坏、禁用、无批次），
    /// 再判实际注热，再判温度故障与自动化，最后才判生产与等待。
    /// </summary>
    public static class RunStatusText
    {
        /// <summary>按优先级给出主状态。</summary>
        public static RunStatusKind Classify(MaterialOverhaulFab fab)
        {
            if (fab == null)
            {
                return RunStatusKind.None;
            }

            // 1. 真实阻断：损坏、手动禁用、没有可执行批次。
            if (fab.IsBrokenDown)
            {
                return RunStatusKind.BrokenDown;
            }

            if (fab.IsManuallyDisabled)
            {
                return RunStatusKind.ManuallyDisabled;
            }

            if (!fab.HasLockedBatch)
            {
                return fab.Orders.Count == 0 ? RunStatusKind.NoTask : RunStatusKind.WaitingMaterials;
            }

            // 2. 实际注热优先于其它说明：断电、损坏与批次条件已在上面排除，
            //    这里能走到就说明注热是真发生的。
            switch (fab.AppliedHeating)
            {
                case HeatingIntent.Preheat:
                    return RunStatusKind.Preheat;

                case HeatingIntent.TopUp:
                    return RunStatusKind.TopUp;

                case HeatingIntent.Sustained:
                    return RunStatusKind.Sustained;
            }

            // 3. 温度故障。普通模式超过建筑上限时优先显示高温保护，
            //    不写成「只是自动化暂停」。
            if (!fab.Policy.Forced)
            {
                if (fab.CurrentDecision.Next.HighTempWaiting)
                {
                    // 控制模式最佳温区不可达且已经停在受限目标：写成温度受限，不写等待操作。
                    return IsHeldAtMaterialLimit(fab)
                        ? RunStatusKind.TemperatureLimited
                        : RunStatusKind.HighTempWaiting;
                }

                if (IsControlBandBlocked(fab))
                {
                    return RunStatusKind.WaitingCool;
                }
            }

            // 4. 自动化生产许可（红信号不停止加热，注热已在上面返回）。
            if (!fab.AutomationAllowsProduction && fab.IsPowered)
            {
                return RunStatusKind.AutomationPaused;
            }

            // 5. 有加热需求但没有供电：注热不会发生，明确写成等待供电。
            if (fab.CurrentPower.RequestedHeatingWatts > 0f && !fab.IsPowered)
            {
                return RunStatusKind.Unpowered;
            }

            // 6. 实际生产：按配方温度关系写效率文字。
            if (fab.ProductionRunning)
            {
                return ProductionKind(fab);
            }

            // 7. 温度已经达标但还没开工：沿用原版操作提示，不写生产中。
            //    温度还不到、又没有注热时，明确写成等待升温（不假称正在预热）。
            if (NeedsMoreHeat(fab))
            {
                return RunStatusKind.WaitingHeat;
            }

            return RunStatusKind.WaitingWorker;
        }

        /// <summary>主状态文字。</summary>
        public static string MainText(RunStatusKind kind)
        {
            switch (kind)
            {
                case RunStatusKind.NoTask:
                    return Localized("NO_TASK", "无生产任务");

                case RunStatusKind.WaitingMaterials:
                    return Localized("WAITING_MATERIALS", "等待材料");

                case RunStatusKind.BrokenDown:
                    return Localized("BROKEN_DOWN", "损坏停机");

                case RunStatusKind.ManuallyDisabled:
                    return Localized("MANUALLY_DISABLED", "手动禁用");

                case RunStatusKind.Unpowered:
                    return Localized("UNPOWERED", "等待供电");

                case RunStatusKind.HighTempWaiting:
                    return Localized("HIGH_TEMP_WAITING", "等待降温");

                case RunStatusKind.Preheat:
                    return Localized("PREHEAT", "预热中");

                case RunStatusKind.TopUp:
                    return Localized("TOPUP", "补热中");

                case RunStatusKind.Sustained:
                    return Localized("SUSTAINED", "持续加热");

                case RunStatusKind.AutomationPaused:
                    return Localized("AUTOMATION_PAUSED", "生产已被自动化暂停");

                case RunStatusKind.WaitingWorker:
                    return Localized("WAITING_WORKER", "等待操作");

                case RunStatusKind.Producing:
                    return Localized("PRODUCING", "生产中");

                case RunStatusKind.ProducingLow:
                    return Localized("PRODUCING_LOW", "低温低效生产");

                case RunStatusKind.ProducingHigh:
                    return Localized("PRODUCING_HIGH", "高温低效生产");

                case RunStatusKind.WaitingHeat:
                    return Localized("WAITING_HEAT", "等待升温");

                case RunStatusKind.WaitingCool:
                    return Localized("HIGH_TEMP_WAITING", "等待降温");

                case RunStatusKind.TemperatureLimited:
                    return Localized("TEMP_LIMITED", "温度受限 · 生产暂停");

                default:
                    return string.Empty;
            }
        }

        /// <summary>完整状态文字：主状态 + 可组合的补充说明。</summary>
        public static string Compose(MaterialOverhaulFab fab)
        {
            if (fab == null)
            {
                return string.Empty;
            }

            RunStatusKind kind = Classify(fab);
            string text = MainText(kind);
            string heatingSuffix = HeatingSuffix(fab, kind);

            if (!string.IsNullOrEmpty(heatingSuffix))
            {
                text = text + " · " + heatingSuffix;
            }

            if (fab.IsCooling && NeedsCooling(fab))
            {
                text = text + " · " + Localized("COOLING", "降温中");
            }

            return text;
        }

        /// <summary>
        /// 温区限制提示：普通控制生产受主建造材料上限限制时显示。
        /// 返回空字符串表示不需要独立提示（配方最佳区完整可用，或本建筑没有温区数值）。
        /// </summary>
        public static string ZoneLimitText(MaterialOverhaulFab fab)
        {
            if (fab == null || !fab.ZoneConfigured)
            {
                return string.Empty;
            }

            ResolvedZone zone = fab.CurrentZone;

            if (zone.ControlRangeUnreachable)
            {
                return Localized("ZONE_UNREACHABLE", "最佳温区不可达");
            }

            if (zone.ControlRangeLimited)
            {
                return Localized("ZONE_LIMITED", "最佳温区受限");
            }

            return string.Empty;
        }

        /// <summary>
        /// 温度说明行：本体温度、建筑范围、配方最佳区、原／实际加热目标、限制原因。
        /// 只在受限时才同时给出原目标与实际目标，避免平时多一行噪音。
        /// </summary>
        public static string ZoneDetailText(MaterialOverhaulFab fab)
        {
            if (fab == null || !fab.ZoneConfigured)
            {
                return string.Empty;
            }

            ResolvedZone zone = fab.CurrentZone;
            string text = string.Format(
                Localized("ZONE_DETAIL",
                    "本体温度 {0}；建筑工作范围 {1} ~ {2}（主建造材料 {3}）；配方最佳温区 {4} ~ {5}"),
                GameUtil.GetFormattedTemperature(fab.BodyTemperature),
                GameUtil.GetFormattedTemperature(zone.Min),
                GameUtil.GetFormattedTemperature(zone.Max),
                fab.MaterialName,
                GameUtil.GetFormattedTemperature(zone.BestMin),
                GameUtil.GetFormattedTemperature(zone.BestMax));

            float original = StartThreshold.Original(zone, fab.Policy);
            float actual = StartThreshold.Start(zone, fab.Policy);

            if (actual < original - 0.01f)
            {
                text = text + "\n" + string.Format(
                    Localized("ZONE_TARGET_CAPPED", "加热目标：原目标 {0}，实际目标 {1}（受建筑上限限制）"),
                    GameUtil.GetFormattedTemperature(original),
                    GameUtil.GetFormattedTemperature(actual));
            }
            else
            {
                text = text + "\n" + string.Format(
                    Localized("ZONE_TARGET", "加热目标 {0}"),
                    GameUtil.GetFormattedTemperature(actual));
            }

            if (zone.ControlRangeUnreachable)
            {
                text = text + "\n" + string.Format(
                    Localized("ZONE_UNREACHABLE_DETAIL",
                        "普通控制模式需要配方最佳下限 {0}，但本建筑上限只有 {1}：普通控制模式没有可用温区，预热到上限后停止并保持暂停，不自动跳单。改用默认模式或熔点更高的建材才能生产。"),
                    GameUtil.GetFormattedTemperature(zone.BestMin),
                    GameUtil.GetFormattedTemperature(zone.Max));
            }
            else if (zone.ControlRangeLimited)
            {
                text = text + "\n" + string.Format(
                    Localized("ZONE_LIMITED_DETAIL",
                        "普通控制模式可用区间 {0} ~ {1}（受建筑上限限制，配方最佳上限 {2}）；默认模式仍在 {3} ~ {4} 内按低温／高温效率生产。"),
                    GameUtil.GetFormattedTemperature(zone.BestMin),
                    GameUtil.GetFormattedTemperature(zone.ControlMax),
                    GameUtil.GetFormattedTemperature(zone.BestMax),
                    GameUtil.GetFormattedTemperature(zone.Min),
                    GameUtil.GetFormattedTemperature(zone.Max));
            }

            return text;
        }

        /// <summary>除主状态之外还要补充的加热说明（例如自动化暂停时仍在补热）。</summary>
        private static string HeatingSuffix(MaterialOverhaulFab fab, RunStatusKind kind)
        {
            if (kind == RunStatusKind.Preheat || kind == RunStatusKind.TopUp || kind == RunStatusKind.Sustained)
            {
                return null;
            }

            switch (fab.AppliedHeating)
            {
                case HeatingIntent.Preheat:
                    return MainText(RunStatusKind.Preheat);

                case HeatingIntent.TopUp:
                    return MainText(RunStatusKind.TopUp);

                case HeatingIntent.Sustained:
                    return MainText(RunStatusKind.Sustained);

                default:
                    return null;
            }
        }

        /// <summary>是否还需要降温：处于高温等待，或控制模式越过了最佳上限。</summary>
        private static bool NeedsCooling(MaterialOverhaulFab fab)
        {
            if (fab.Policy.Forced)
            {
                return false;
            }

            if (!fab.ZoneConfigured)
            {
                return false;
            }

            return fab.CurrentDecision.Next.HighTempWaiting || IsControlBandBlocked(fab);
        }

        /// <summary>是否还需要升温：已声明启动目标但温度还没到。</summary>
        private static bool NeedsMoreHeat(MaterialOverhaulFab fab)
        {
            if (!fab.ZoneConfigured)
            {
                return false;
            }

            // 尚未取得开工资格（预热未完成）时才算需要升温；已开工后等待操作不算。
            return !fab.CurrentDecision.Next.Started
                && fab.BodyTemperature < StartThreshold.Start(fab.CurrentZone, fab.Policy);
        }

        /// <summary>控制模式下越过最佳上限但还没有超过建筑上限（含恢复回差）。</summary>
        private static bool IsControlBandBlocked(MaterialOverhaulFab fab)
        {
            if (!fab.Policy.ControlMode || !fab.ZoneConfigured)
            {
                return false;
            }

            ResolvedZone zone = fab.CurrentZone;

            // 最佳区整体不可用（B₁ > U）不是「等降温」，那由温度受限与不可达提示表达。
            if (zone.ControlRangeUnreachable)
            {
                return false;
            }

            return fab.BodyTemperature > zone.ControlMax;
        }

        /// <summary>
        /// 是否已经停在建筑上限、且控制模式在这个上限下仍然没有可用温区。
        /// 用于把「温度受限 · 生产暂停」与普通的等待降温区分开。
        /// </summary>
        private static bool IsHeldAtMaterialLimit(MaterialOverhaulFab fab)
        {
            if (!fab.Policy.ControlMode || !fab.ZoneConfigured)
            {
                return false;
            }

            ResolvedZone zone = fab.CurrentZone;
            if (!zone.ControlRangeUnreachable)
            {
                return false;
            }

            return fab.AppliedHeating == HeatingIntent.None && fab.BodyTemperature >= zone.Max - 0.01f;
        }

        /// <summary>按配方温度关系给出生产中的效率文字；不把建材过热写成高温低效。</summary>
        private static RunStatusKind ProductionKind(MaterialOverhaulFab fab)
        {
            switch (fab.CurrentState)
            {
                case TempState.BelowMin:
                case TempState.LowEfficiency:
                    return RunStatusKind.ProducingLow;

                case TempState.HighEfficiency:
                    return RunStatusKind.ProducingHigh;

                default:
                    // 最佳区内（含强制运行越过建筑上限但仍在最佳区的情形）与未配置温区都写生产中。
                    return RunStatusKind.Producing;
            }
        }

        private static string Localized(string keySuffix, string fallback)
        {
            return MaterialOverhaulFab.LocalizedOr("STRINGS.UI.BUILDINGEFFECTS.MATERIALOVERHAUL.STATUS." + keySuffix, fallback);
        }
    }
}
