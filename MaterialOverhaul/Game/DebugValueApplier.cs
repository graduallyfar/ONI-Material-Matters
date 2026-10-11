using System.Collections.Generic;
using MaterialOverhaul.Core;
using MaterialOverhaul.Hardness;
using MaterialOverhaul.Heating;
using MaterialOverhaul.Zones;

namespace MaterialOverhaul.Game
{
    /// <summary>
    /// 调试样例值的写入器（V0.0.3 开发计划 §8「实现规则」）。
    ///
    /// **本版常规配置已经自带数值，调试标记不再是任何机制生效的前提。** 标记文件只做两件事：
    ///   1. 给还没登记温区的白名单配方补一份样例温区，便于观察五态与效率；
    ///   2. 给还没登记要求硬度的白名单建筑补样例硬度，便于观察磨损与损坏。
    ///
    /// 它**不会覆盖**：
    ///   - 已确认的铝配方温区（300/450/650/800 ℃）；
    ///   - 金属精炼器的要求硬度 25；
    ///   - 本版的加热功率与磨损／伤害转换系数。
    ///
    /// 启动日志会明确列出调试扩展的实际生效范围，避免把调试扩展当成默认范围。
    /// </summary>
    public static class DebugValueApplier
    {
        private static bool applied;

        public static void Apply()
        {
            if (!DebugValues.Enabled)
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

            if (applied)
            {
                return;
            }

            applied = true;

            List<string> zoneRecipes = RegisterSampleZones();
            List<string> hardnessBuildings = RegisterSampleHardness();

            ModLog.Warn("调试数值已启用（**不是正式平衡数值**）："
                + "样例温区写入 " + zoneRecipes.Count + " 条配方"
                + (zoneRecipes.Count > 0 ? "（" + string.Join("、", zoneRecipes.ToArray()) + "）" : string.Empty)
                + "；样例要求硬度写入 " + hardnessBuildings.Count + " 栋建筑"
                + (hardnessBuildings.Count > 0 ? "（" + string.Join("、", hardnessBuildings.ToArray()) + "）" : string.Empty)
                + "。常规数值（铝配方温区、金属精炼器硬度、加热功率、磨损与伤害转换）不受调试标记影响。"
                + "删掉 " + DebugValues.MarkerFileName + " 即只保留常规数值。");
        }

        /// <summary>给还没有温区数值的白名单配方补样例温区；返回被写入的配方标识。</summary>
        private static List<string> RegisterSampleZones()
        {
            List<string> written = new List<string>();
            ComplexRecipeManager manager = ComplexRecipeManager.Get();
            if (manager == null || manager.recipes == null)
            {
                ModLog.Warn("配方管理器尚不可用，调试温区未写入。");
                return written;
            }

            foreach (ComplexRecipe recipe in manager.recipes)
            {
                if (recipe == null || recipe.fabricators == null)
                {
                    continue;
                }

                bool whitelisted = false;
                foreach (Tag fabricator in recipe.fabricators)
                {
                    if (BuildingWhitelist.Contains(fabricator.ToString()))
                    {
                        whitelisted = true;
                        break;
                    }
                }

                if (!whitelisted)
                {
                    continue;
                }

                TempZone existing;
                if (RecipeZoneTable.TryGet(recipe.id, out existing))
                {
                    continue;
                }

                RecipeZoneTable.Register(recipe.id, DebugValues.SampleZone);
                written.Add(recipe.id);
            }

            return written;
        }

        /// <summary>给还没有要求硬度的白名单建筑补样例硬度；返回被写入的建筑。</summary>
        private static List<string> RegisterSampleHardness()
        {
            List<string> written = new List<string>();
            foreach (string prefabId in BuildingWhitelist.All)
            {
                byte existing;
                if (HardnessRequirementTable.TryGet(prefabId, out existing))
                {
                    continue;
                }

                HardnessRequirementTable.Register(prefabId, DebugValues.SampleRequiredHardness);
                written.Add(prefabId);
            }

            return written;
        }
    }
}
