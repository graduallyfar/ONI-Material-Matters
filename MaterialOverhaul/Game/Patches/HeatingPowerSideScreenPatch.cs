using HarmonyLib;
using MaterialOverhaul.Game;

namespace MaterialOverhaul.Patches
{
    /// <summary>
    /// 原版配置卡按 sortOrder 从大到小排，配方卡是 0。
    /// 只将本 Mod 的加热滑条放到 -1，使其位于配方卡下方。
    /// SingleSliderSideScreen 未覆写排序方法，因此补丁挂在声明它的基类。
    /// </summary>
    [Core.SystemPart(Core.ModSystem.Industrial)]
    [HarmonyPatch(typeof(SideScreenContent), "GetSideScreenSortOrder")]
    internal static class Patch_HeatingPowerSideScreen_SortOrder
    {
        private static readonly AccessTools.FieldRef<SingleSliderSideScreen, ISingleSliderControl> SliderTarget =
            AccessTools.FieldRefAccess<SingleSliderSideScreen, ISingleSliderControl>("target");

        private static void Postfix(SideScreenContent __instance, ref int __result)
        {
            SingleSliderSideScreen slider = __instance as SingleSliderSideScreen;
            if (slider != null && SliderTarget(slider) is MaterialOverhaulFab fab && fab.SupportsBuiltInHeating)
            {
                __result = -1;
            }
        }
    }
}
