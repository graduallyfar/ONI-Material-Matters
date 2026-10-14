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
                return Text("PENDING", "等待自动化就绪");
            string name = fab.Policy.ControlMode ? Text("CONTROL", "控制模式") : Text("DEFAULT", "默认模式");
            return fab.Policy.Forced ? name + " · " + Text("FORCED", "强制运行已开启") : name;
        }

        private static string Tooltip(MaterialOverhaulFab fab)
        {
            if (fab == null || !fab.ModeConnectionReady)
                return Text("PENDING_TOOLTIP", "自动化网络未就绪：保留存档模式与温控回差，暂不生产或加热。");
            string text = fab.Policy.ControlMode
                ? Text("CONTROL_TOOLTIP", "输出端口已接线：只在最佳温区内生产。红绿值不决定模式，绿色不代表正在生产。")
                : Text("DEFAULT_TOOLTIP", "输出端口未接线：在整个工作温区生产。输入端接线与红绿值不决定模式。");
            if (fab.Policy.Forced) text += "\n" + Text("FORCED_TOOLTIP", "覆盖温区停机限制；过热受损后停止内置加热，完全维修后解除。关闭后按当时接线重判；材料、供电、自动化许可、操作与建筑完好仍需满足。");
            return text;
        }

        private static string Text(string key, string fallback) { return MaterialOverhaulFab.LocalizedOr(Prefix + key, fallback); }
    }
}
