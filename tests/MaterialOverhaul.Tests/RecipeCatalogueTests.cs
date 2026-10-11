using System.Collections.Generic;
using MaterialOverhaul.Heating;
using MaterialOverhaul.Zones;

namespace MaterialOverhaul.Tests
{
    /// <summary>
    /// v0.0.2 已确认的配方温区数值（建筑细节文档 §2.3.6、V0.0.2 开发计划 §5）。
    /// 数值由用户给定（摄氏度），判断层统一换算为开尔文。
    /// </summary>
    public static class RecipeCatalogueTests
    {
        private const float Tol = 0.01f;

        public static void Run()
        {
            AluminumRecipeZone();
            AllEntriesAreUsable();
            MaterialMatching();
        }

        private static void AluminumRecipeZone()
        {
            TestRunner.Section("金属精炼器 铝矿 → 铝");
            RecipeZoneSpec spec = Find("MetalRefinery", "AluminumOre");
            TestRunner.Check(spec != null, "目录中存在铝矿配方条目");
            if (spec == null)
            {
                return;
            }

            TestRunner.CheckEqual("Aluminum", spec.Outputs[0], "产物为铝");
            TestRunner.CheckClose(573.15f, spec.Zone.WorkMin, Tol, "最低可工作温度 300 ℃");
            TestRunner.CheckClose(723.15f, spec.Zone.BestMin, Tol, "最佳温区下限 450 ℃");
            TestRunner.CheckClose(923.15f, spec.Zone.BestMax, Tol, "最佳温区上限 650 ℃");
            TestRunner.CheckClose(1073.15f, spec.Zone.WorkMax, Tol, "最高可工作温度 800 ℃");
            TestRunner.Check(spec.Zone.IsOrdered, "四边界升序且有限");
            TestRunner.Check(!spec.Zone.IsDisabled, "不是退化温区");
            TestRunner.Check(!string.IsNullOrEmpty(spec.Source), "记录数值来源");

            // 铝矿 1357 K × 0.85 = 1153.45 K，高于 800 ℃ 的最高可工作温度：
            // 用铝矿本身作为建造材料时，过热阈值足以覆盖该配方的整个工作温区。
            float overheat;
            OverheatRule.TryCompute(1357f, out overheat);
            TestRunner.Check(overheat > spec.Zone.WorkMax, "铝矿熔点推出的过热阈值高于该配方最高可工作温度");
        }

        private static void AllEntriesAreUsable()
        {
            TestRunner.Section("目录条目基本校验");
            TestRunner.Check(RecipeZoneCatalogue.Count > 0, "目录至少有一条已确认数值");

            foreach (RecipeZoneSpec spec in RecipeZoneCatalogue.All)
            {
                TestRunner.Check(!string.IsNullOrEmpty(spec.FabricatorId), "条目有建筑标识");
                TestRunner.Check(spec.Inputs != null && spec.Inputs.Length > 0, "条目有输入材料");
                TestRunner.Check(spec.Outputs != null && spec.Outputs.Length > 0, "条目有输出材料");
                TestRunner.Check(spec.Zone.IsOrdered && !spec.Zone.IsDisabled,
                    spec.FabricatorId + " 的温区数值合法");
            }
        }

        private static void MaterialMatching()
        {
            TestRunner.Section("配方材料匹配（与顺序无关）");
            RecipeZoneSpec spec = new RecipeZoneSpec
            {
                FabricatorId = "Test",
                Inputs = new[] { "A", "B" },
                Outputs = new[] { "C" }
            };

            TestRunner.Check(spec.MatchesMaterials(new List<string> { "A", "B" }, new List<string> { "C" }),
                "顺序一致时匹配");
            TestRunner.Check(spec.MatchesMaterials(new List<string> { "B", "A" }, new List<string> { "C" }),
                "输入顺序不同也匹配");
            TestRunner.Check(!spec.MatchesMaterials(new List<string> { "A" }, new List<string> { "C" }),
                "输入数量不同不匹配");
            TestRunner.Check(!spec.MatchesMaterials(new List<string> { "A", "B" }, new List<string> { "D" }),
                "输出不同不匹配");
            TestRunner.Check(!spec.MatchesMaterials(new List<string> { "A", "B", "B" }, new List<string> { "C" }),
                "同材料重复出现时按数量比较");

            RecipeZoneSpec aluminum = Find("MetalRefinery", "AluminumOre");
            if (aluminum != null)
            {
                TestRunner.Check(aluminum.MatchesMaterials(new List<string> { "AluminumOre" }, new List<string> { "Aluminum" }),
                    "铝矿条目用原版材料标识可匹配");
                TestRunner.Check(!aluminum.MatchesMaterials(new List<string> { "CopperOre" }, new List<string> { "Copper" }),
                    "铜矿配方不匹配铝矿条目");
            }
        }

        private static RecipeZoneSpec Find(string fabricatorId, string inputId)
        {
            foreach (RecipeZoneSpec spec in RecipeZoneCatalogue.All)
            {
                if (spec.FabricatorId == fabricatorId)
                {
                    foreach (string input in spec.Inputs)
                    {
                        if (input == inputId)
                        {
                            return spec;
                        }
                    }
                }
            }

            return null;
        }
    }
}
