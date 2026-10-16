using System.Collections.Generic;
using MaterialOverhaul.Core;
using MaterialOverhaul.Hardness;
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
    /// 侧表不参与存档，每次启动重建；配方必须在 <c>ComplexRecipeManager.PostProcess</c> 之后登记。
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

        /// <summary>登记过热规则、建筑最低工作温度、配方最佳温区与要求硬度。可重复调用：已登记的条目不重复写入。</summary>
        public static void Apply()
        {
            RegisterOverheatRules();
            RegisterBuildingZones();
            RegisterHardnessRequirements();
            RegisterRecipeZones();
        }

        /// <summary>
        /// 建筑最低工作温度 L（V0.1.2 开发计划 §1.1、§2.1）：L **由建筑类型决定**，
        /// 切配方与换建材都不改变它，因此按建筑登记一次。
        ///
        /// 六栋独立下限读取 BuildingProfiles；首发三栋参数不变。
        /// 未登记的建筑不启用本 Mod 温控，行为等同原版。
        /// </summary>
        public static void RegisterBuildingZones()
        {
            foreach (KeyValuePair<string, float> entry in BuildingZoneCatalogue.All)
            {
                float existing;
                if (BuildingZoneTable.TryGet(entry.Key, out existing))
                {
                    continue;
                }

                BuildingZoneTable.Register(entry.Key, entry.Value);
                ModLog.Info("建筑温区：" + entry.Key + " 最低工作温度 " + entry.Value.ToString("F2")
                    + " K（" + Temperatures.ToCelsius(entry.Value).ToString("F1")
                    + " ℃），由建筑类型决定，不随配方或建材改变。");
            }
        }

        /// <summary>
        /// 要求硬度常规配置（V0.0.3 开发计划 §2.1、§6.1）：本版六栋要求硬度25（FIRM），新增数值为工作初值。
        ///
        /// **不走调试路径**：调试样例值只在常规配置之上填补清单里未登记的建筑，
        /// 不能把样例值扩散给全部白名单，也不能覆盖这里的数值。
        /// </summary>
        public static void RegisterHardnessRequirements()
        {
            foreach (string prefabId in BuildingWhitelist.All)
            {
                byte existing;
                if (HardnessRequirementTable.TryGet(prefabId, out existing))
                {
                    continue;
                }

                HardnessRequirementTable.Register(prefabId, BuildingProfiles.All[prefabId].RequiredHardness);
                ModLog.Info("要求硬度：" + prefabId + " 要求硬度 "
                    + BuildingProfiles.All[prefabId].RequiredHardness + "（本版暂定，使用原版 Element.hardness 分级）。");
            }
        }

        /// <summary>该建筑是否已经有了最低工作温度数值（建筑级，与配方无关）。</summary>
        public static bool HasBuildingFloor(string prefabId)
        {
            float floor;
            return BuildingZoneTable.TryGet(prefabId, out floor);
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
        /// 纳入温区数值的建筑同时启用「最高工作温度 = 过热阈值 = 主建造材料熔点 × 0.75」的规则
        /// （V0.1.2 §2.1.5）。没有确认温区数值的建筑暂不启用，避免在没有温度需求的前提下改动原版过热阈值。
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
                BuildingProfile profile;
                bool immune = BuildingProfiles.TryGet(prefabId, out profile) && profile.PreserveOverheatImmunity;
                ModLog.Info("材料工作上限：" + prefabId + " 使用主材料熔点 × "
                    + OverheatRule.MeltingPointFraction.ToString("F2") + " × 材料倍率"
                    + (immune ? "；保留原版过热豁免，不设置过热伤害阈值。" : "，同时作为过热阈值。"));
            }
        }

        /// <summary>按目录条目解析原版配方标识并登记最佳温区。</summary>
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

            int registered = 0;
            HashSet<RecipeZoneSpec> matched = new HashSet<RecipeZoneSpec>();
            foreach (ComplexRecipe recipe in manager.recipes)
            {
                if (recipe == null) continue;
                foreach (RecipeZoneSpec spec in RecipeZoneCatalogue.All)
                {
                    if (!HasFabricator(recipe, spec.FabricatorId)
                        || !spec.MatchesRecipe(MaterialsFor(recipe.ingredients), MaterialsFor(recipe.results))) continue;
                    matched.Add(spec);
                    TempZone existing;
                    if (!RecipeZoneTable.TryGet(recipe.id, out existing))
                    {
                        RecipeZoneTable.Register(recipe.id, spec.Zone);
                        registered++;
                        ModLog.Info("配方温区：" + recipe.id + " 最佳温区 " + spec.Zone + "。");
                    }
                    break;
                }
            }
            // 真实配方表已处理；DLC/版本中未出现的候选正常跳过，不在切配方时反复扫描。
            zonesResolved = true;
            ModLog.Info("配方温区：新增 " + registered + "，匹配候选 " + matched.Count
                + "/" + RecipeZoneCatalogue.Count + "；缺失候选本次未接入（DLC、版本或内容差异）。");

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
                    prefabId + "：配方最佳温区已配置 " + configured + "/" + total
                    + " 条，其余配方暂按原版行为运行（最佳温区数值待定）。");
            }
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

        private static IList<RecipeMaterial> MaterialsFor(ComplexRecipe.RecipeElement[] elements)
        {
            List<RecipeMaterial> values = new List<RecipeMaterial>();
            if (elements == null) return values;
            foreach (ComplexRecipe.RecipeElement element in elements)
            {
                List<string> ids = new List<string>();
                if (element.possibleMaterials != null && element.possibleMaterials.Length > 0)
                {
                    foreach (Tag material in element.possibleMaterials)
                        if (material != null) ids.Add(material.ToString());
                }
                else if (element.material != null) ids.Add(element.material.ToString());
                values.Add(element.possibleMaterialAmounts != null
                    ? new RecipeMaterial(ids.ToArray(), element.possibleMaterialAmounts)
                    : new RecipeMaterial(ids.ToArray(), element.amount));
            }
            return values;
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
