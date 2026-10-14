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
    /// 2. 配方详情：AdditionalEffectsForRecipe 追加所选配方温区，配方名保持原版。
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

    /// <summary>
    /// 配方详情面板的温度行（V0.1.2 §2.3.5）。
    ///
    /// 侧屏「配方详情」里的**效果**列表由 <c>SelectedRecipeQueueScreen.RefreshResultDescriptors</c>
    /// 取 `target.AdditionalEffectsForRecipe(配方)` 逐条生成，所以这里给配方追加一条说明：
    ///
    /// <code>最佳温区：450 °C ~ 650 °C</code>
    ///
    /// **只显示配方自己的最佳温区。** V0.1.2 起建筑最低工作温度属于建筑类型、最高工作温度由
    /// 主建造材料决定，两者都不再是配方属性；继续把旧「工作温区：300 °C ~ 800 °C」写在这里
    /// 会让玩家以为换个建材也还是这个范围。
    ///
    /// 只对**已有最佳温区数值**的配方生效；`AdditionalEffectsForRecipe` 每次调用都新建列表，
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
                if (!RecipeZoneTable.TryGet(recipe.id, out zone) || !zone.Configured)
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

    /// <summary>
    /// 建筑右侧信息卡里的**过热温度**行。
    ///
    /// 原版 <c>Overheatable.GetDescriptors</c> 给出的「过热温度」一行用的是属性总值。
    /// 本 Mod 的规则值已经补偿进该属性，这里再显式重建这一行，作用有两个：
    ///   1. 规则值与实际属性一致时缩短常驻行；补偿失败时保留实际阈值；
    ///   2. 把规则来源写进提示（主建造材料与熔点），玩家能看出这个阈值是怎么来的。
    ///
    /// **只在真正建成的实例上生效**（<c>MaterialOverhaulFab.Spawned</c>），
    /// 因此建造菜单里的预制件、以及其它显示位置保持原版不变（用户要求先只改右侧卡片）。
    /// 文本全部走本 Mod 的键，不依赖原版 LocString 的类路径。
    /// </summary>
    [HarmonyPatch(typeof(Overheatable), "GetDescriptors")]
    internal static class Patch_Overheatable_GetDescriptors
    {
        private const string KeyOverheatLine = "STRINGS.UI.BUILDINGEFFECTS.MATERIALOVERHAUL.OVERHEAT_LINE";
        private const string KeyOverheatTooltip = "STRINGS.UI.BUILDINGEFFECTS.MATERIALOVERHAUL.OVERHEAT_TOOLTIP";

        private static void Postfix(Overheatable __instance, ref List<Descriptor> __result)
        {
            try
            {
                if (__instance == null || __result == null)
                {
                    return;
                }

                MaterialOverhaulFab fab = __instance.GetComponent<MaterialOverhaulFab>();
                if (fab == null || !fab.Spawned)
                {
                    return;
                }

                float overheat;
                string materialName;
                float meltingPoint;
                if (!fab.TryGetOverheatRuleValue(out overheat, out materialName, out meltingPoint))
                {
                    return;
                }

                // 属性补偿失败时保持真实阈值显示，不伪造规则已经生效。
                if (Math.Abs(__instance.OverheatTemperature - overheat) > 0.1f)
                {
                    ModLog.WarnOnce("overheat|display-mismatch|" + fab.PrefabId,
                        fab.PrefabId + "：实际过热阈值与规则值不一致，信息卡保留实际值。");
                    return;
                }

                string overheatText = GameUtil.GetFormattedTemperature(__instance.OverheatTemperature);
                string meltingPointText = GameUtil.GetFormattedTemperature(meltingPoint);
                string fatalText = GameUtil.GetFormattedTemperature(__instance.baseFatalTemp);

                string lineTemplate = Strings.Get(KeyOverheatLine);
                string tooltipTemplate = Strings.Get(KeyOverheatTooltip);

                string text = string.IsNullOrEmpty(lineTemplate)
                    ? overheatText
                    : string.Format(lineTemplate, overheatText);

                string tooltip = string.IsNullOrEmpty(tooltipTemplate)
                    ? text
                    : string.Format(tooltipTemplate, materialName, meltingPointText, overheatText, fatalText);

                // 原版这一行最多一条，直接替换，避免同一信息出现两次。
                __result.Clear();
                __result.Add(new Descriptor(text, tooltip));
            }
            catch (Exception e)
            {
                ModLog.Error("信息卡过热温度行补丁异常：" + e);
            }
        }
    }
}
