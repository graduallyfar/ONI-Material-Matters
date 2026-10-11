using System;
using System.Collections.Generic;
using HarmonyLib;
using MaterialOverhaul.Core;
using MaterialOverhaul.Game;
using UnityEngine;

namespace MaterialOverhaul.Patches
{
    /// <summary>
    /// 把逐笔订单侧屏挂进建筑详情面板（V0.0.3 开发计划 §5.1）。
    ///
    /// 已核实原版机制：<c>DetailsScreen</c> 在 <c>OnPrefabInit</c> 里建立侧屏标签，
    /// 之后 <c>Refresh</c> 遍历序列化的 <c>sideScreens</c> 列表，对每个
    /// <c>screenPrefab.IsValidForTarget(target)</c> 为真的条目用
    /// <c>Util.KInstantiateUI&lt;SideScreenContent&gt;(screenPrefab.gameObject, tab.bodyInstance)</c>
    /// 实例化。因此只要把一条 <c>SideScreenRef</c> 加进那个列表，并在 <c>Config</c> 标签下，
    /// 就能像原版侧屏一样显示与刷新。
    ///
    /// 预制件由代码构建（<see cref="OrderListSideScreen"/>"），不依赖原版侧屏预制件的层级；
    /// 引入失败只影响这块界面，不影响建筑运行。
    /// </summary>
    [HarmonyPatch(typeof(DetailsScreen), "OnPrefabInit")]
    internal static class Patch_DetailsScreen_OnPrefabInit
    {
        private static readonly AccessTools.FieldRef<DetailsScreen, List<DetailsScreen.SideScreenRef>> SideScreens =
            AccessTools.FieldRefAccess<DetailsScreen, List<DetailsScreen.SideScreenRef>>("sideScreens");

        private static bool applied;

        private static void Postfix(DetailsScreen __instance)
        {
            try
            {
                if (applied || __instance == null)
                {
                    return;
                }

                List<DetailsScreen.SideScreenRef> list = SideScreens(__instance);
                if (list == null)
                {
                    ModLog.WarnOnce("orderscreen|no-list",
                        "订单列表侧屏：找不到 DetailsScreen 的侧屏列表，本版不显示逐笔订单面板。");
                    return;
                }

                foreach (DetailsScreen.SideScreenRef existing in list)
                {
                    if (existing != null && existing.name == ScreenName)
                    {
                        applied = true;
                        return;
                    }
                }

                OrderListSideScreen prefab = OrderListSideScreen.CreatePrefab();
                if (prefab == null)
                {
                    return;
                }

                DetailsScreen.SideScreenRef reference = new DetailsScreen.SideScreenRef();
                reference.name = ScreenName;
                reference.screenPrefab = prefab;
                reference.offset = Vector2.zero;
                reference.tab = DetailsScreen.SidescreenTabTypes.Config;
                list.Add(reference);

                applied = true;
                ModLog.Info("已注册逐笔订单侧屏（建筑详情面板 · 配置标签）。");
            }
            catch (Exception e)
            {
                ModLog.Error("注册逐笔订单侧屏失败：" + e);
            }
        }

        private const string ScreenName = "MaterialOverhaulOrderList";
    }
}
