using System;
using System.Collections.Generic;
using HarmonyLib;
using MaterialOverhaul.Core;
using MaterialOverhaul.Game;
using MaterialOverhaul.Orders;
using UnityEngine;

namespace MaterialOverhaul.Patches
{
    // ---- 建筑信息卡里单独的「订单队列」面板 ----
    //
    // 原版配方列表只显示每个配方的总数，看不出本 Mod 的订单顺序与轮换位置。
    // 这里在信息卡「状态」面板下方加一个可折叠面板，按添加顺序列出全部订单，
    // 标出正在生产的批次与下一张轮到的订单。没有订单或不是批次建筑时面板隐藏。

    /// <summary>信息卡建好时创建订单队列面板，放在状态面板之后。</summary>
    [Core.SystemPart(Core.ModSystem.Industrial)]
    [HarmonyPatch(typeof(SimpleInfoScreen), "OnPrefabInit")]
    internal static class Patch_SimpleInfoScreen_OrderQueuePanel
    {
        internal static SimpleInfoScreen Owner;
        internal static CollapsibleDetailContentPanel Panel;

        private static void Postfix(SimpleInfoScreen __instance)
        {
            try
            {
                Panel = Traverse.Create(__instance).Method("CreateCollapsableSection", new[] { typeof(string) })
                    .GetValue<CollapsibleDetailContentPanel>(MaterialOverhaulFab.LocalizedOr(
                        "STRINGS.UI.UISIDESCREENS.MATERIALOVERHAUL.ORDER_QUEUE.TITLE", "订单队列"));
                Owner = Panel != null ? __instance : null;
                CollapsibleDetailContentPanel status = Traverse.Create(__instance).Field("statusItemPanel").GetValue<CollapsibleDetailContentPanel>();
                if (Panel != null && status != null)
                {
                    Panel.transform.SetSiblingIndex(status.transform.GetSiblingIndex() + 1);
                }
                Panel?.gameObject.SetActive(false);
            }
            catch (Exception e)
            {
                Owner = null;
                Panel = null;
                ModLog.WarnOnce("order-queue-panel|create", "订单队列面板创建失败：" + e);
            }
        }
    }

    /// <summary>信息卡每次刷新时重填订单队列面板。</summary>
    [Core.SystemPart(Core.ModSystem.Industrial)]
    [HarmonyPatch(typeof(SimpleInfoScreen), "Refresh")]
    internal static class Patch_SimpleInfoScreen_OrderQueueRefresh
    {
        private const string Key = "STRINGS.UI.UISIDESCREENS.MATERIALOVERHAUL.ORDER_QUEUE.";
        private const int MaxRows = 20;
        private static readonly AccessTools.FieldRef<TargetPanel, GameObject> SelectedTarget =
            AccessTools.FieldRefAccess<TargetPanel, GameObject>("selectedTarget");

        private static void Postfix(SimpleInfoScreen __instance)
        {
            CollapsibleDetailContentPanel panel = Patch_SimpleInfoScreen_OrderQueuePanel.Panel;
            if (panel == null || !ReferenceEquals(Patch_SimpleInfoScreen_OrderQueuePanel.Owner, __instance))
            {
                return;
            }

            try
            {
                GameObject target = SelectedTarget(__instance);
                MaterialOverhaulFab fab = target != null ? target.GetComponent<MaterialOverhaulFab>() : null;
                if (fab != null && fab.Spawned && fab.HasOrderData)
                {
                    Fill(panel, fab);
                }
                panel.Commit();
            }
            catch (Exception e)
            {
                ModLog.WarnOnce("order-queue-panel|refresh", "订单队列面板刷新失败：" + e);
            }
        }

        private static void Fill(CollapsibleDetailContentPanel panel, MaterialOverhaulFab fab)
        {
            OrderQueue queue = fab.Orders;
            IList<OrderEntry> orders = queue.Orders;
            string tooltip = Text("TOOLTIP", "订单按添加顺序轮换：缺料的订单暂时跳过，下次轮到再检查。");
            int locked = queue.LockedIndex;
            int shown = Math.Min(orders.Count, MaxRows);
            for (int i = 0; i < shown; i++)
            {
                OrderEntry order = orders[i];
                string name = fab.RecipeDisplayName(order.RecipeId);
                string line = order.IsInfinite
                    ? string.Format(Text("INFINITE", "{0}. {1} · 持续生产（本轮 {2}/{3}）"), i + 1, name, order.BatchesDoneThisRound, order.BatchQuota)
                    : string.Format(Text("LIMITED", "{0}. {1} · 剩余 {2} 批"), i + 1, name, order.Remaining);
                if (i == locked)
                {
                    line += Text("CURRENT", "（生产中）");
                }
                else if (locked < 0 && i == queue.Cursor)
                {
                    line += Text("NEXT", "（轮到）");
                }
                panel.SetLabel("MaterialOverhaul.OrderQueue." + i, line, tooltip);
            }

            if (orders.Count > shown)
            {
                panel.SetLabel("MaterialOverhaul.OrderQueue.More", string.Format(Text("MORE", "还有 {0} 张订单"), orders.Count - shown), tooltip);
            }
        }

        private static string Text(string key, string fallback)
        {
            return MaterialOverhaulFab.LocalizedOr(Key + key, fallback);
        }
    }
}
