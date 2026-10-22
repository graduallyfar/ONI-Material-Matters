using System;
using System.Collections;
using HarmonyLib;
using MaterialOverhaul.Core;
using MaterialOverhaul.Game;

namespace MaterialOverhaul.Patches
{
    /// <summary>
    /// 整合版：在原版模组列表本 Mod 那一行加“选项”按钮，打开系统开关对话框。
    /// 属于外壳，不随任何系统开关关闭，否则四个系统全关后就再也打不开开关。
    /// </summary>
    [Core.SystemPart(Core.ModSystem.Shell)]
    [HarmonyPatch(typeof(ModsScreen), "BuildDisplay")]
    internal static class Patch_ModsScreen_BuildDisplay
    {
        private static void Postfix(ModsScreen __instance, IEnumerable ___displayedMods)
        {
            try
            {
                SystemOptionsDialog.AddButton(__instance, ___displayedMods);
            }
            catch (Exception e)
            {
                // 选项按钮只是入口，失败时不能拖垮原版模组界面。
                ModLog.WarnOnce("options-button-failed", "模组界面：添加系统开关“选项”按钮失败：" + e);
            }
        }
    }
}
