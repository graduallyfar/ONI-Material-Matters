using System;
using HarmonyLib;
using MaterialOverhaul.Core;
using MaterialOverhaul.Game;
using UnityEngine;

namespace MaterialOverhaul.Patches
{
    [HarmonyPatch(typeof(AdditionalDetailsPanel), "RefreshEnergyOverviewPanel")]
    internal static class Patch_PowerInfoCard_NetworkCount
    {
        internal const string LabelId = "MaterialOverhaul.PowerNetworkCount";
        private const string Key = "STRINGS.UI.UISIDESCREENS.MATERIALOVERHAUL.POWER_NETWORK_COUNT";

        // 必须在原版Commit之前SetLabel；后置补丁再Commit会隐藏原版本轮已提交的其他行。
        internal static void Prefix(CollapsibleDetailContentPanel targetPanel, GameObject targetEntity)
        {
            if (targetPanel == null || targetEntity == null
                || (targetEntity.GetComponent<ICircuitConnected>() == null && targetEntity.GetComponent<Wire>() == null)) return;
            try
            {
                int count;
                string text = PowerCircuitCount.TryGet(out count)
                    ? string.Format(Localized(Key, "电网数量：{0}"), count)
                    : Localized(Key + "_PENDING", "电网数量：统计中");
                targetPanel.SetLabel(LabelId, text, Localized(Key + "_TOOLTIP",
                    "当前存档中所有已建成的独立电网总数，包含未发电的电网及虚拟电网，不计规划线路。\n变压器两侧若未通过其他线路连通，分别计数。"));
            }
            catch (Exception e) { ModLog.WarnOnce("power-count-card", "电网数量信息卡显示失败：" + e); }
        }

        private static string Localized(string key, string fallback)
        {
            string text = Strings.Get(key);
            return string.IsNullOrEmpty(text) || text.StartsWith("MISSING.", StringComparison.Ordinal)
                || text == key ? fallback : text;
        }
    }
}
