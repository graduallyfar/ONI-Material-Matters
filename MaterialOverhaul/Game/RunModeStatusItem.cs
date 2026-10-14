using MaterialOverhaul.Core;

namespace MaterialOverhaul.Game
{
    /// <summary>独立模式状态，无订单或损坏停机时也显示，强制运行保留基础模式。</summary>
    public static class RunModeStatusItem
    {
        private const string Prefix = "STRINGS.BUILDING.STATUSITEMS.MATERIALOVERHAUL.RUNMODE.";
        public static StatusItem Item { get; private set; }
        public static StatusItemCategory Category { get; private set; }

        public static void Register()
        {
            if (Item != null) return;
            Db db = Db.Get();
            Category = new StatusItemCategory("MaterialOverhaulRunModeCategory", db.StatusItemCategories, "Material Overhaul mode");
            db.StatusItemCategories.Add(Category);
            Item = new StatusItem("MaterialOverhaulRunMode", "{0}", "{0}", "status_item_fabricator_select",
                StatusItem.IconType.Custom, NotificationType.Neutral, false, OverlayModes.None.ID);
            Item.resolveStringCallback = (text, data) => Name(data as MaterialOverhaulFab);
            Item.resolveTooltipCallback = (text, data) => Tooltip(data as MaterialOverhaulFab);
            db.BuildingStatusItems.Add(Item);
            ModLog.Info("已注册独立运行模式状态项。");
        }

        public static string Name(MaterialOverhaulFab fab)
        {
            if (fab == null || !fab.ModeConnectionReady)
                return Text("PENDING", "等待自动化连接");
            string name = fab.Policy.ControlMode ? Text("CONTROL", "控制模式") : Text("DEFAULT", "默认模式");
            return fab.Policy.Forced ? name + " · " + Text("FORCED", "强制运行") : name;
        }

        private static string Tooltip(MaterialOverhaulFab fab)
        {
            if (fab == null || !fab.ModeConnectionReady)
                return Text("PENDING_TOOLTIP", "正在确认温区信号输出的连接状态。\n确认前暂不生产或加热。");
            string text = fab.Policy.ControlMode
                ? Text("CONTROL_TOOLTIP", "这个建筑只在工作温区内的配方最佳温区中生产。\n断开温区信号输出后切换为默认模式。")
                : Text("DEFAULT_TOOLTIP", "这个建筑可以在工作温区内生产。\n连接温区信号输出后切换为控制模式。");
            if (fab.Policy.Forced) text += "\n" + (fab.SupportsBuiltInHeating
                ? Text("FORCED_TOOLTIP", "强制运行允许这个建筑忽略温区限制继续生产，并持续加热。\n因过热受损后，内置加热会停止，完全修复后恢复。\n自动化信号仍能暂停生产。")
                : Text("FORCED_NO_HEATING_TOOLTIP", "强制运行允许这个建筑忽略温区限制继续生产。\n没有内置加热，需要依靠生产热量或外部热源升温。\n自动化信号仍能暂停生产。"));
            return text;
        }

        private static string Text(string key, string fallback) { return MaterialOverhaulFab.LocalizedOr(Prefix + key, fallback); }
    }
}
