using MaterialOverhaul.Core;
using MaterialOverhaul.Heating;
using UnityEngine;

namespace MaterialOverhaul.Game
{
    /// <summary>
    /// 建筑信息卡**状态栏**里的运行状态条目（V0.0.4 开发计划 §2.3）。
    ///
    /// v0.0.3 把运行状态写在信息卡的**效果列表**里，与「效果」的语义不符：效果列表是静态说明，
    /// 而「预热中／等待降温」是实时状态。本版改到信息卡顶部的状态项区域（原版在那里显示
    /// 「操作任务」「等待材料」等）。
    ///
    /// 与原版的关系（已在目标版本核实）：
    ///   - 原版 <c>ComplexFabricatorWorkable</c> 的状态机已经会给出 <c>FabricatorIdle</c>（无订单）、
    ///     <c>FabricatorEmpty</c>（等材料）、<c>PendingWork</c>（等操作工）与
    ///     <c>ComplexFabricatorProducing</c>（生产中）。这些**保持原版显示**，本 Mod 不替换、不重复；
    ///   - 本 Mod 只补充温度与自动化造成的状态（预热、补热、持续加热、等待供电、高温保护、
    ///     生产已被自动化暂停），因此与「无生产任务」「等待材料」等原版条目**不重复**，
    ///     也不会占用原版 <c>Main</c> 类别去遮盖它们；
    ///   - 类别用本 Mod 自己的 <see cref="Category"/>，与损坏原因（<c>Main</c>）并列显示。
    ///
    /// 状态文字与 v0.0.3 的 <see cref="RunStatusText"/> 完全一致，只是换了显示位置；
    /// 悬浮提示给出同一步的真实数值（本体温度、温度效率、功率、批次进度）。
    /// </summary>
    public static class RunStatusItem
    {
        public const string StatusId = "MaterialOverhaulRunStatus";

        private const string NameKey = "STRINGS.BUILDING.STATUSITEMS.MATERIALOVERHAUL.RUNSTATUS.NAME";
        private const string TooltipKey = "STRINGS.BUILDING.STATUSITEMS.MATERIALOVERHAUL.RUNSTATUS.TOOLTIP";

        /// <summary>沿用原版已有的配方建筑图标，不新增美术资源。</summary>
        private const string Icon = "status_item_fabricator_select";

        private static bool registered;
        private static string nameFormat = "{0}";
        private static string tooltipFormat = "{0}";

        public static StatusItem Item { get; private set; }

        /// <summary>运行状态使用的类别：本 Mod 自己的类别，不占用原版 <c>Main</c>。</summary>
        public static StatusItemCategory Category { get; private set; }

        /// <summary>在 Db 初始化之后登记状态项与类别（Db.Initialize 会重建这些集合）。</summary>
        public static void Register()
        {
            if (registered)
            {
                return;
            }

            registered = true;

            nameFormat = MaterialOverhaulFab.LocalizedOr(NameKey, "{0}");
            tooltipFormat = MaterialOverhaulFab.LocalizedOr(
                TooltipKey,
                "建筑温度：{1}\n温度效率：{2} · 生产进度：{6}\n耗电：{4}（需求：{3}）\n内置加热：{5}");

            Db db = Db.Get();
            if (db == null)
            {
                ModLog.Error("运行状态状态项：Db 不可用，本版未登记状态项。");
                return;
            }

            Category = new StatusItemCategory("MaterialOverhaulRunStatusCategory", db.StatusItemCategories, "Material Overhaul status");
            db.StatusItemCategories.Add(Category);

            Item = new StatusItem(
                StatusId,
                nameFormat,
                tooltipFormat,
                Icon,
                StatusItem.IconType.Custom,
                NotificationType.Neutral,
                allow_multiples: false,
                OverlayModes.None.ID,
                showWorldIcon: false);

            Item.resolveStringCallback = (text, data) => Resolve(text, data, false);
            Item.resolveTooltipCallback = (text, data) => Resolve(text, data, true);

            db.BuildingStatusItems.Add(Item);

            ModLog.Info("已注册运行状态状态项：类别 " + Category.Id + "，名称格式「" + nameFormat + "」。");
        }

        /// <summary>
        /// 该状态是否属于本 Mod 补充的温度／自动化状态。
        /// 「无生产任务」「等待材料」「等待操作」「生产中」都由原版状态项承担，本 Mod 不重复显示。
        /// </summary>
        public static bool IsTemperatureOrAutomationStatus(RunStatusKind kind)
        {
            switch (kind)
            {
                case RunStatusKind.Preheat:
                case RunStatusKind.TopUp:
                case RunStatusKind.Sustained:
                case RunStatusKind.Unpowered:
                case RunStatusKind.HighTempWaiting:
                case RunStatusKind.WaitingHeat:
                case RunStatusKind.WaitingCool:
                case RunStatusKind.TemperatureLimited:
                case RunStatusKind.AutomationPaused:
                case RunStatusKind.ProducingLow:
                case RunStatusKind.ProducingHigh:
                case RunStatusKind.ManuallyDisabled:
                case RunStatusKind.BrokenDown:
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>是否应该显示本 Mod 的运行状态条目。</summary>
        public static bool ShouldShow(MaterialOverhaulFab fab, RunStatusKind kind)
        {
            if (fab == null)
            {
                return false;
            }

            // 原版已提供等待材料、操作任务、正常生产、断电、禁用和损坏停机状态。
            // 只显示本 Mod 独有的温度／自动化条件；空闲模式由独立状态承担。
            return fab.HasLockedBatch && IsTemperatureOrAutomationStatus(kind)
                && kind != RunStatusKind.Unpowered
                && kind != RunStatusKind.ManuallyDisabled
                && kind != RunStatusKind.BrokenDown;
        }

        /// <summary>名称与提示都用同一套数值，保证界面显示的就是本步判断用的值。</summary>
        private static string Resolve(string text, object data, bool tooltip)
        {
            MaterialOverhaulFab fab = data as MaterialOverhaulFab;
            if (fab == null)
            {
                return text;
            }

            try
            {
                string status = RunStatusText.Compose(fab);
                if (!tooltip)
                {
                    return string.Format(text, status);
                }

                string body = string.Format(
                    text,
                    status,
                    GameUtil.GetFormattedTemperature(fab.BodyTemperature),
                    GameUtil.GetFormattedPercent(fab.CurrentEfficiency * 100f),
                    GameUtil.GetFormattedWattage(fab.CurrentRequestedWatts),
                    GameUtil.GetFormattedWattage(fab.CurrentUsedWatts),
                    HeatingIntentText(fab.AppliedHeating),
                    GameUtil.GetFormattedPercent(Mathf.Clamp01(fab.BatchProgress) * 100f));

                // V0.1.2 §4.3：悬停要给出本体温度、建筑范围、配方最佳区、
                // 原／实际加热目标（仅受限时需要两者）与限制原因。
                string zoneDetail = RunStatusText.ZoneDetailText(fab);
                if (!string.IsNullOrEmpty(zoneDetail))
                {
                    body = body + "\n" + zoneDetail;
                }

                return body + "\n" + fab.HardnessText();
            }
            catch (System.Exception e)
            {
                ModLog.Error("运行状态状态项文本生成失败：" + e);
                return text;
            }
        }

        private static string HeatingIntentText(HeatingIntent intent)
        {
            switch (intent)
            {
                case HeatingIntent.Preheat:
                    return MaterialOverhaulFab.LocalizedOr(
                        "STRINGS.UI.BUILDINGEFFECTS.MATERIALOVERHAUL.HEAT_PREHEAT", "预热中");

                case HeatingIntent.TopUp:
                    return MaterialOverhaulFab.LocalizedOr(
                        "STRINGS.UI.BUILDINGEFFECTS.MATERIALOVERHAUL.HEAT_TOPUP", "补热中");

                case HeatingIntent.Sustained:
                    return MaterialOverhaulFab.LocalizedOr(
                        "STRINGS.UI.BUILDINGEFFECTS.MATERIALOVERHAUL.HEAT_SUSTAINED", "持续加热");

                default:
                    return MaterialOverhaulFab.LocalizedOr(
                        "STRINGS.UI.BUILDINGEFFECTS.MATERIALOVERHAUL.HEAT_NONE", "无");
            }
        }
    }
}
