using System;
using HarmonyLib;
using MaterialOverhaul.Core;
using MaterialOverhaul.Game;
using UnityEngine;

namespace MaterialOverhaul.Patches
{
    [HarmonyPatch(typeof(AdditionalDetailsPanel), "RefreshEnergyOverviewPanel")]
    internal static class Patch_PowerInfoCard_WireCount
    {
        internal const string LabelId = "MaterialOverhaul.PowerWireCount";
        private const string Key = "STRINGS.UI.UISIDESCREENS.MATERIALOVERHAUL.POWER_WIRE_COUNT";

        // 必须在原版Commit之前SetLabel；后置补丁再Commit会隐藏原版本轮已提交的其他行。
        internal static void Prefix(CollapsibleDetailContentPanel targetPanel, GameObject targetEntity)
        {
            if (targetPanel == null || targetEntity == null
                || (targetEntity.GetComponent<ICircuitConnected>() == null && targetEntity.GetComponent<Wire>() == null)) return;
            try
            {
                int count;
                string text = PowerWireCount.TryGet(targetEntity, out count)
                    ? string.Format(Localized(Key, "电线数量：{0}"), count)
                    : Localized(Key + "_PENDING", "电线数量：统计中");
                targetPanel.SetLabel(LabelId, text, Localized(Key + "_TOOLTIP",
                    "选中对象所在独立电网中已建成的电线格数。\n不计规划线路、电桥本体和建筑；未连接电网时为0。"));
            }
            catch (Exception e) { ModLog.WarnOnce("power-count-card", "电线数量信息卡显示失败：" + e); }
        }

        private static string Localized(string key, string fallback)
        {
            string text = Strings.Get(key);
            return string.IsNullOrEmpty(text) || text.StartsWith("MISSING.", StringComparison.Ordinal)
                || text == key ? fallback : text;
        }
    }
}
