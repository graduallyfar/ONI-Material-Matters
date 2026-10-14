using System.Collections.Generic;
using MaterialOverhaul.Core;
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
            AluminumBuildingZone();
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
            TestRunner.CheckClose(723.15f, spec.Zone.BestMin, Tol, "配方最佳温区下限 450 ℃");
            TestRunner.CheckClose(923.15f, spec.Zone.BestMax, Tol, "配方最佳温区上限 650 ℃");
            TestRunner.Check(spec.Zone.Configured && spec.Zone.IsOrdered, "最佳温区已配置且升序、有限");
            TestRunner.Check(!string.IsNullOrEmpty(spec.Source), "记录数值来源");

            // 建筑下限由建筑类型决定，不再是配方属性。
            float floor;
            TestRunner.Check(BuildingZoneCatalogue.TryGetFloorKelvin("MetalRefinery", out floor),
                "建筑下限目录里有金属精炼器");
            TestRunner.CheckClose(Temperatures.FromCelsius(80f), floor, Tol, "金属精炼器建筑下限 80 ℃");

            // 登记到侧表后应能被查询（游戏侧再由 ContentRegistration 调用同一条）。
            BuildingZoneTable.Register("MetalRefinery", floor);
            float registered;
            TestRunner.Check(BuildingZoneTable.TryGet("MetalRefinery", out registered), "登记后侧表可查到建筑下限");
            TestRunner.CheckClose(floor, registered, Tol, "侧表数值与目录一致");

            // 建筑材料上限：火成岩 1683 K × 0.75 = 1262.25 K，覆盖整个最佳区。
            float overheat;
            OverheatRule.TryCompute(1683f, out overheat);
            TestRunner.Check(overheat > spec.Zone.BestMax, "火成岩熔点推出的建筑上限覆盖该配方最佳区");

            // 花岗岩 942 K × 0.75 = 706.5 K = 433.35 ℃，低于最佳下限：控制模式不可达。
            float granite;
            OverheatRule.TryCompute(942f, out granite);
            TestRunner.Check(granite < spec.Zone.BestMin, "花岗岩上限低于最佳下限（最佳温区不可达）");
        }

        private static void AluminumBuildingZone()
        {
            TestRunner.Section("建筑下限与配方最佳区互相独立");
            RecipeZoneSpec spec = Find("MetalRefinery", "AluminumOre");
            float floor;
            BuildingZoneCatalogue.TryGetFloorKelvin("MetalRefinery", out floor);

            // 同一栋建筑的 L 不随配方改变：换配方只换最佳区。
            TestRunner.Check(floor > 0f, "建筑下限是建筑级数值");
            TestRunner.Check(spec.Zone.BestMin > floor, "配方最佳下限不低于建筑下限");

            // 目录里不应再有四边界：配方只携带两个边界。
            TestRunner.CheckClose(723.15f, spec.Zone.BestMin, 0.01f, "配方条目只提供最佳下限");
            TestRunner.CheckClose(923.15f, spec.Zone.BestMax, 0.01f, "配方条目只提供最佳上限");
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
                TestRunner.Check(spec.Zone.IsOrdered && spec.Zone.Configured,
                    spec.FabricatorId + " 的最佳温区数值合法");
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
