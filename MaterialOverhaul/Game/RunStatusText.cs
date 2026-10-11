namespace MaterialOverhaul.Game
{
    /// <summary>
    /// 运行状态的分类。用于信息卡文字与日志，取值本身不参与规则运算。
    /// </summary>
    public enum RunStatusKind
    {
        None = 0,

        /// <summary>没有订单，也没有当前批次。</summary>
        NoTask = 1,

        /// <summary>有订单但全部缺料，等待材料。</summary>
        WaitingMaterials = 2,

        /// <summary>损坏停机。</summary>
        BrokenDown = 3,

        /// <summary>被玩家手动禁用。</summary>
        ManuallyDisabled = 4,

        /// <summary>有加热需求但未获供电，等待供电。</summary>
        Unpowered = 5,

        /// <summary>高温保护：超过暂停线，等待降温。</summary>
        HighTempWaiting = 6,

        /// <summary>预热中。</summary>
        Preheat = 7,

        /// <summary>补热中。</summary>
        TopUp = 8,

        /// <summary>强制运行持续加热。</summary>
        Sustained = 9,

        /// <summary>自动化或其它非温度条件不允许生产。</summary>
        AutomationPaused = 10,

        /// <summary>等待操作工。</summary>
        WaitingWorker = 11,

        /// <summary>正常生产中。</summary>
        Producing = 12,

        /// <summary>低温低效生产中。</summary>
        ProducingLow = 13,

        /// <summary>高温低效生产中。</summary>
        ProducingHigh = 14
    }

    /// <summary>
    /// 把建筑当前的真实条件组合成一句状态文字（V0.0.3 开发计划 §7.2）。
    ///
    /// 规则：
    ///   - 「允许生产」不等于「正在生产」：温度许可、自动化许可、操作工与供电都成立才算生产；
    ///   - 只有**实际注热**时才写预热／补热／持续加热；仅有需求但断电时写等待供电；
    ///   - 「降温中」只在满足实测趋势时追加，本版没有主动制冷；
    ///   - 可组合，例如「生产已被自动化暂停 · 补热中」。
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

            if (fab.CurrentState == Zones.TempState.AboveWorkMax && !fab.Policy.Forced)
            {
                return RunStatusKind.HighTempWaiting;
            }

            if (fab.AppliedHeating == Heating.HeatingIntent.Preheat)
            {
                return RunStatusKind.Preheat;
            }

            if (fab.AppliedHeating == Heating.HeatingIntent.TopUp)
            {
                return RunStatusKind.TopUp;
            }

            if (fab.AppliedHeating == Heating.HeatingIntent.Sustained)
            {
                return RunStatusKind.Sustained;
            }

            if (fab.CurrentPower.RequestedHeatingWatts > 0f && !fab.IsPowered)
            {
                return RunStatusKind.Unpowered;
            }

            if (!fab.AutomationAllowsProduction && fab.IsPowered)
            {
                return RunStatusKind.AutomationPaused;
            }

            if (fab.ProductionRunning)
            {
                if (fab.CurrentState == Zones.TempState.LowEfficiency)
                {
                    return RunStatusKind.ProducingLow;
                }

                if (fab.CurrentState == Zones.TempState.HighEfficiency)
                {
                    return RunStatusKind.ProducingHigh;
                }

                return RunStatusKind.Producing;
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
                    return Localized("HIGH_TEMP_WAITING", "高温保护");

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

        /// <summary>除主状态之外还要补充的加热说明（例如自动化暂停时仍在补热）。</summary>
        private static string HeatingSuffix(MaterialOverhaulFab fab, RunStatusKind kind)
        {
            if (kind == RunStatusKind.Preheat || kind == RunStatusKind.TopUp || kind == RunStatusKind.Sustained)
            {
                return null;
            }

            switch (fab.AppliedHeating)
            {
                case Heating.HeatingIntent.Preheat:
                    return MainText(RunStatusKind.Preheat);

                case Heating.HeatingIntent.TopUp:
                    return MainText(RunStatusKind.TopUp);

                case Heating.HeatingIntent.Sustained:
                    return MainText(RunStatusKind.Sustained);

                default:
                    return null;
            }
        }

        /// <summary>是否还需要降温：处于高温等待或高于最佳温区上限。</summary>
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

            return fab.CurrentState == Zones.TempState.AboveWorkMax
                || fab.CurrentState == Zones.TempState.HighEfficiency;
        }

        private static string Localized(string keySuffix, string fallback)
        {
            return MaterialOverhaulFab.LocalizedOr("STRINGS.UI.BUILDINGEFFECTS.MATERIALOVERHAUL.STATUS." + keySuffix, fallback);
        }
    }
}
