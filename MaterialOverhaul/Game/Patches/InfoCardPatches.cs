using System;
using System.Collections.Generic;
using HarmonyLib;
using MaterialOverhaul.Core;
using MaterialOverhaul.Game;
using MaterialOverhaul.Zones;

namespace MaterialOverhaul.Patches
{
    /// <summary>
    /// 信息卡与配方选择界面的温度提示（细节文档 §2.3.5、§2.4.5 的第一步）。
    ///
    /// 两处都只针对**已经有配方温区数值的建筑与配方**：没有数值的对象保持原版显示，
    /// 不向玩家暗示一个还不存在的约束。
    ///
    /// 1. 建筑信息卡：原版 <c>BuildingDef.Desc</c> 是计算属性，读取
    ///    <c>STRINGS.BUILDINGS.PREFABS.&lt;ID&gt;.DESC</c>，因此在这里追加一句
    ///    「本建筑需要在当前配方的工作温区内运行」。建筑名下方的描述与建造菜单同源，都会看到这句话。
    ///
    /// 2. 配方选择：原版 <c>ComplexRecipe.GetUIName</c> 给出配方显示名，侧屏的配方行、
    ///    配方详情标题、当前／下一订单标签都用它，所以在名字后面追加工作温区
    ///    （「铝矿 → 铝（工作温区 300 °C ~ 800 °C）」），配方选择时直接能看到。
    ///    温度用 <c>GameUtil.GetFormattedTemperature</c> 格式化，跟随玩家的温度单位设置。
    /// </summary>
    [HarmonyPatch(typeof(BuildingDef), "get_Desc")]
    internal static class Patch_BuildingDef_Desc
    {
        private const string KeyTempZoneNote = "STRINGS.BUILDINGS.PREFABS.MATERIALOVERHAUL.TEMPZONE_NOTE";

        private static void Postfix(BuildingDef __instance, ref string __result)
        {
            try
            {
                if (__instance == null || !ContentRegistration.HasConfirmedZone(__instance.PrefabID))
                {
                    return;
                }

                string note = Strings.Get(KeyTempZoneNote);
                if (string.IsNullOrEmpty(note) || (!string.IsNullOrEmpty(__result) && __result.Contains(note)))
                {
                    return;
                }

                __result = string.IsNullOrEmpty(__result) ? note : __result + "\n\n" + note;
            }
            catch (Exception e)
            {
                ModLog.Error("信息卡温度提示补丁异常：" + e);
            }
        }
    }

    [HarmonyPatch(typeof(ComplexRecipe), "GetUIName")]
    internal static class Patch_ComplexRecipe_GetUIName
    {
        private const string KeyRecipeTempRange = "STRINGS.UI.UISIDESCREENS.MATERIALOVERHAUL.RECIPE_TEMP_RANGE";

        private static void Postfix(ComplexRecipe __instance, ref string __result)
        {
            try
            {
                if (__instance == null || string.IsNullOrEmpty(__result))
                {
                    return;
                }

                TempZone zone;
                if (!RecipeZoneTable.TryGet(__instance.id, out zone) || zone.IsDisabled)
                {
                    return;
                }

                string template = Strings.Get(KeyRecipeTempRange);
                if (string.IsNullOrEmpty(template))
                {
                    return;
                }

                __result = string.Format(
                    template,
                    __result,
                    GameUtil.GetFormattedTemperature(zone.WorkMin),
                    GameUtil.GetFormattedTemperature(zone.WorkMax));
            }
            catch (Exception e)
            {
                ModLog.Error("配方温度提示补丁异常：" + e);
            }
        }
    }

    /// <summary>
    /// 配方详情面板的温度行。
    ///
    /// 侧屏「配方详情」里的**效果**列表由 <c>SelectedRecipeQueueScreen.RefreshResultDescriptors</c>
    /// 取 `target.AdditionalEffectsForRecipe(配方)` 逐条生成，所以这里给配方追加一条说明：
    ///
    /// <code>工作温区：300 °C ~ 800 °C（最佳温区 450 °C ~ 650 °C）</code>
    ///
    /// 只对**已有温区数值**的配方生效；`AdditionalEffectsForRecipe` 每次调用都新建列表，
    /// 因此每次刷新只追加一条，不会重复叠加。温度用 <c>GameUtil.GetFormattedTemperature</c>，
    /// 跟随玩家的温度单位设置。
    /// </summary>
    [HarmonyPatch(typeof(ComplexFabricator), "AdditionalEffectsForRecipe")]
    internal static class Patch_ComplexFabricator_AdditionalEffectsForRecipe
    {
        private const string KeyRecipeTempRangeDetail = "STRINGS.UI.UISIDESCREENS.MATERIALOVERHAUL.RECIPE_TEMP_RANGE_DETAIL";

        private static void Postfix(ComplexRecipe recipe, ref List<Descriptor> __result)
        {
            try
            {
                if (recipe == null)
                {
                    return;
                }

                TempZone zone;
                if (!RecipeZoneTable.TryGet(recipe.id, out zone) || zone.IsDisabled)
                {
                    return;
                }

                string template = Strings.Get(KeyRecipeTempRangeDetail);
                if (string.IsNullOrEmpty(template))
                {
                    return;
                }

                string text = string.Format(
                    template,
                    GameUtil.GetFormattedTemperature(zone.WorkMin),
                    GameUtil.GetFormattedTemperature(zone.WorkMax),
                    GameUtil.GetFormattedTemperature(zone.BestMin),
                    GameUtil.GetFormattedTemperature(zone.BestMax));

                if (__result == null)
                {
                    __result = new List<Descriptor>();
                }

                __result.Add(new Descriptor(text, text, Descriptor.DescriptorType.Effect));
            }
            catch (Exception e)
            {
                ModLog.Error("配方温度行补丁异常：" + e);
            }
        }
    }
}
