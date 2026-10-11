using MaterialOverhaul.Core;
using MaterialOverhaul.Hardness;
using MaterialOverhaul.Heating;
using MaterialOverhaul.Zones;

namespace MaterialOverhaul.Game
{
    /// <summary>
    /// 把 <see cref="DebugValues"/> 的样例值写入两张侧表与三处配置，供游戏内验证机制。
    ///
    /// 只在调试标记文件存在时执行；默认什么都不做，因此正式数值未定期间
    /// 所有建筑行为与效率、损伤、加热都保持原版。
    /// </summary>
    public static class DebugValueApplier
    {
        private static bool applied;

        public static void Apply()
        {
            if (applied || !DebugValues.Enabled)
            {
                return;
            }

            // 与真实温区同一个时机问题：Db.Initialize 时配方表还是空的，这里不标记完成，
            // 等 ComplexRecipeManager.PostProcess 之后再补一次（见 Patches/DbPatches.cs）。
            ComplexRecipeManager manager = ComplexRecipeManager.Get();
            if (manager == null || manager.recipes == null || manager.recipes.Count == 0)
            {
                return;
            }

            applied = true;

            int recipeCount = RegisterSampleZones();

            foreach (string prefabId in BuildingWhitelist.All)
            {
                HardnessRequirementTable.Register(prefabId, DebugValues.SampleRequiredHardness);
            }

            HeatingPower.PreheatKilowatts = DebugValues.HeatingKilowatts;
            HeatingPower.TopUpKilowatts = DebugValues.HeatingKilowatts;
            HeatingPower.SustainedKilowatts = DebugValues.HeatingKilowatts;
            DamageConversion.DamagePerWearUnit = DebugValues.DamagePerWearUnit;

            ModLog.Warn("调试数值已写入：配方温区 " + recipeCount + " 条，要求硬度 "
                + HardnessRequirementTable.Count + " 条，加热功率 " + DebugValues.HeatingKilowatts
                + " kW，磨损转换系数 " + DebugValues.DamagePerWearUnit + "。");
        }

        private static int RegisterSampleZones()
        {
            int count = 0;
            ComplexRecipeManager manager = ComplexRecipeManager.Get();
            if (manager == null || manager.recipes == null)
            {
                ModLog.Warn("配方管理器尚不可用，调试温区未写入。");
                return 0;
            }

            foreach (ComplexRecipe recipe in manager.recipes)
            {
                if (recipe == null || recipe.fabricators == null)
                {
                    continue;
                }

                foreach (Tag fabricator in recipe.fabricators)
                {
                    if (!BuildingWhitelist.Contains(fabricator.ToString()))
                    {
                        continue;
                    }

                    if (!RecipeZoneTable.TryGet(recipe.id, out TempZone _))
                    {
                        RecipeZoneTable.Register(recipe.id, DebugValues.SampleZone);
                        count++;
                    }

                    break;
                }
            }

            return count;
        }
    }
}
