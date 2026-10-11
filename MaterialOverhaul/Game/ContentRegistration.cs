using System.Collections.Generic;
using MaterialOverhaul.Core;
using MaterialOverhaul.Heating;
using MaterialOverhaul.Zones;

namespace MaterialOverhaul.Game
{
    /// <summary>
    /// 把已确认的设计数值登记进侧表（V0.0.2 开发计划 §5、§6）。
    ///
    /// 原版配方数据结构里不存在「每个配方拥有自己的温度范围」的对应物，因此温区数值由本 Mod 提供；
    /// 配方标识由建筑名与材料名拼接而成，这里按「建筑 + 输入材料 + 输出材料」在配方表里解析，
    /// 解析失败、以及名单内还没有数值的配方都会留下可定位日志，不静默失效。
    ///
    /// 侧表不参与存档，每次启动（<c>Db.Initialize</c> 之后）重建。
    /// </summary>
    public static class ContentRegistration
    {
        private static bool zonesResolved;

        /// <summary>
        /// 配方温区是否已经在「配方表可用」的前提下登记过。
        ///
        /// **已核实的加载顺序（游戏内日志确认）**：`Db.Initialize` 早于建筑预制件创建，
        /// 那时 `ComplexRecipeManager.recipes` 还是空的（配方先进 `preProcessRecipes`，
        /// 直到 `Assets.CreatePrefabs` 末尾的 `ComplexRecipeManager.PostProcess()` 才转入 `recipes`）。
        /// 因此 Db 钩子里登记配方温区一定会落空，必须等 PostProcess 之后再登记。
        /// </summary>
        public static bool ZonesResolved
        {
            get { return zonesResolved; }
        }

        /// <summary>登记过热规则与配方温区。可重复调用：已登记的条目不重复写入。</summary>
        public static void Apply()
        {
            RegisterOverheatRules();
            RegisterRecipeZones();
        }

        /// <summary>
        /// 该建筑是否已经有确认的配方温区数值。信息卡提示与配方温度文本只对它生效，
        /// 没有数值的建筑不显示温度相关信息（避免暗示一个还不存在的约束）。
        /// </summary>
        public static bool HasConfirmedZone(string prefabId)
        {
            if (string.IsNullOrEmpty(prefabId))
            {
                return false;
            }

            foreach (RecipeZoneSpec spec in RecipeZoneCatalogue.All)
            {
                if (spec.FabricatorId == prefabId)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 纳入温区数值的建筑同时启用「过热阈值 = 主建造材料熔点 × 85%」的规则。
        /// 没有确认温区数值的建筑暂不启用，避免在没有温度需求的前提下改动原版过热阈值。
        /// </summary>
        public static void RegisterOverheatRules()
        {
            HashSet<string> fabricators = new HashSet<string>();
            foreach (RecipeZoneSpec spec in RecipeZoneCatalogue.All)
            {
                if (!string.IsNullOrEmpty(spec.FabricatorId))
                {
                    fabricators.Add(spec.FabricatorId);
                }
            }

            foreach (string prefabId in fabricators)
            {
                float existing;
                if (OverheatRuleTable.TryGet(prefabId, out existing))
                {
                    continue;
                }

                OverheatRuleTable.Register(prefabId, OverheatRule.MeltingPointFraction);
                ModLog.Info("过热规则：" + prefabId + " 的过热阈值取主建造材料熔点的 "
                    + OverheatRule.MeltingPointFraction.ToString("F2") + "。");
            }
        }

        /// <summary>按目录条目解析原版配方标识并登记四边界。</summary>
        public static void RegisterRecipeZones()
        {
            ComplexRecipeManager manager = ComplexRecipeManager.Get();
            if (manager == null || manager.recipes == null || manager.recipes.Count == 0)
            {
                // 配方表还没建好：不标记完成，等 ComplexRecipeManager.PostProcess 之后重试
                // （MaterialOverhaulFab 在第一次要用配方时还有一次兜底重试）。
                ModLog.WarnOnce("zone|recipes-empty",
                    "配方温区：登记时原版配方表还是空的（Db.Initialize 早于配方注册），将在配方注册完成后重试。");
                return;
            }

            zonesResolved = true;

            int registered = 0;
            foreach (RecipeZoneSpec spec in RecipeZoneCatalogue.All)
            {
                ComplexRecipe recipe = FindRecipe(manager, spec);
                if (recipe == null)
                {
                    ModLog.WarnOnce("zone|unresolved|" + spec.FabricatorId + "|" + Join(spec.Inputs),
                        "配方温区：未能在原版配方表里找到 " + Describe(spec)
                        + "（可能是 DLC 未启用或配方标识变化），该条目本次未生效。");
                    continue;
                }

                TempZone existing;
                if (RecipeZoneTable.TryGet(recipe.id, out existing))
                {
                    continue;
                }

                RecipeZoneTable.Register(recipe.id, spec.Zone);
                registered++;
                ModLog.Info("配方温区：" + recipe.id + " 登记为 " + spec.Zone
                    + "（" + spec.Source + "）。");
            }

            if (registered > 0)
            {
                ModLog.Info("配方温区：本次登记 " + registered + " 条，侧表共 " + RecipeZoneTable.Count + " 条。");
            }

            LogPendingRecipes(manager);
        }

        /// <summary>名单内建筑还有多少配方没有温区数值：按建筑汇总一条待定日志（§9）。</summary>
        private static void LogPendingRecipes(ComplexRecipeManager manager)
        {
            foreach (string prefabId in BuildingWhitelist.All)
            {
                int total = 0;
                int configured = 0;

                foreach (ComplexRecipe recipe in manager.recipes)
                {
                    if (recipe == null || !HasFabricator(recipe, prefabId))
                    {
                        continue;
                    }

                    total++;
                    TempZone zone;
                    if (RecipeZoneTable.TryGet(recipe.id, out zone))
                    {
                        configured++;
                    }
                }

                if (total == 0 || configured == total)
                {
                    continue;
                }

                ModLog.WarnOnce("zone|pending|" + prefabId,
                    prefabId + "：配方温区已配置 " + configured + "/" + total
                    + " 条，其余配方暂按原版行为运行（四边界数值待定）。");
            }
        }

        private static ComplexRecipe FindRecipe(ComplexRecipeManager manager, RecipeZoneSpec spec)
        {
            foreach (ComplexRecipe recipe in manager.recipes)
            {
                if (recipe == null || recipe.fabricators == null)
                {
                    continue;
                }

                if (!HasFabricator(recipe, spec.FabricatorId))
                {
                    continue;
                }

                if (spec.MatchesMaterials(MaterialIds(recipe.ingredients), MaterialIds(recipe.results)))
                {
                    return recipe;
                }
            }

            return null;
        }

        private static bool HasFabricator(ComplexRecipe recipe, string prefabId)
        {
            if (recipe == null || recipe.fabricators == null)
            {
                return false;
            }

            foreach (Tag fabricator in recipe.fabricators)
            {
                if (fabricator.ToString() == prefabId)
                {
                    return true;
                }
            }

            return false;
        }

        private static IList<string> MaterialIds(ComplexRecipe.RecipeElement[] elements)
        {
            List<string> ids = new List<string>();
            if (elements != null)
            {
                foreach (ComplexRecipe.RecipeElement element in elements)
                {
                    ids.Add(element.material.ToString());
                }
            }

            return ids;
        }

        private static string Describe(RecipeZoneSpec spec)
        {
            return spec.FabricatorId + "：" + Join(spec.Inputs) + " → " + Join(spec.Outputs);
        }

        private static string Join(string[] values)
        {
            return values == null ? string.Empty : string.Join("+", values);
        }
    }
}
