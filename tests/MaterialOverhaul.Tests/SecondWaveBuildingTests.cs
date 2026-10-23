using System;
using System.Collections.Generic;
using System.Linq;
using MaterialOverhaul.Core;
using MaterialOverhaul.Game;
using MaterialOverhaul.Hardness;
using MaterialOverhaul.Heating;
using MaterialOverhaul.Patches;
using MaterialOverhaul.Zones;

namespace MaterialOverhaul.Tests
{
    public static class SecondWaveBuildingTests
    {
        private static float C(float c) { return Temperatures.FromCelsius(c); }
        public static void Run()
        {
            string[] buildings = { "SupermaterialRefinery", "ChemicalRefinery", "Kiln" };
            int[] counts = { 5, 4, 2 };
            float[] watts = { 1600f, 480f, 0f };
            float[] wearRates = { .03f, .01f, .02f };
            for (int i = 0; i < buildings.Length; i++)
            {
                string id = buildings[i];
                BuildingProfile p = BuildingProfiles.All[id];
                TestRunner.Check(BuildingWhitelist.Contains(id), id + " 已列入接入名单");
                TestRunner.CheckEqual(counts[i], RecipeZoneCatalogue.All.Count(s => s.FabricatorId == id), id + " 原版配方候选数");
                TestRunner.CheckClose(watts[i], p.ProductionWatts, .001f, id + " 原版生产功率保留");
                TestRunner.Check(p.BuiltInHeating == (i != 2) && HeatingScope.IsEnabled(id) == p.BuiltInHeating, id + " 内置加热范围一致");
                TestRunner.Check(p.PreserveOverheatImmunity == (i == 2), id + " 仅窑炉保留过热豁免");
                var wear = new WearAccumulator();
                DamageConversion.AccumulateProduction(wear, 40f, p.DamagePerWearUnit);
                TestRunner.CheckClose(40f * wearRates[i], wear.DamageBalance, .001f, id + " 真实生产计磨损");
                foreach (RecipeZoneSpec spec in RecipeZoneCatalogue.All.Where(s => s.FabricatorId == id))
                {
                    TestRunner.Check(spec.Zone.BestMin >= C(p.WorkMinCelsius), id + " 最佳下限不低于建筑下限");
                    ResolvedZone zone = TempZoneResolver.Resolve(id, C(p.WorkMinCelsius), spec.Zone,
                        (out float upper) => { upper = C(2498.1f); return true; }, p.LowEfficiency, p.HighEfficiency, !p.BuiltInHeating);
                    float mid = (zone.BestMin + zone.BestMax) / 2f;
                    RunDecision atBest = RunController.Update(mid, zone, RunPolicy.ControlModePolicy, RunState.Fresh);
                    TestRunner.Check(atBest.AllowsProduction && atBest.Efficiency == 1f, id + " 最佳区100%并允许控制生产");
                    RunDecision tooHot = RunController.Update(zone.Max + 1f, zone, RunPolicy.DefaultMode, RunState.Fresh);
                    TestRunner.Check(!tooHot.AllowsProduction && tooHot.Heating == HeatingIntent.None, id + " 普通模式超过工作上限停产停热");
                    RunDecision forced = RunController.Update(zone.Max + 1f, zone, new RunPolicy { Forced = true }, RunState.Fresh);
                    TestRunner.Check(forced.AllowsProduction && forced.Heating == (p.BuiltInHeating ? HeatingIntent.Sustained : HeatingIntent.None), id + " 强制遵守加热能力");
                }
            }
            KilnColdStartAndPower();
            AlternativeMaterials();
            ImmuneDisplay();
            Ports();
        }

        private static void KilnColdStartAndPower()
        {
            BuildingProfile p = BuildingProfiles.All["Kiln"];
            foreach (RecipeZoneSpec spec in RecipeZoneCatalogue.All.Where(s => s.FabricatorId == "Kiln"))
            {
                ResolvedZone z = TempZoneResolver.Resolve("kiln-cold", C(p.WorkMinCelsius), spec.Zone,
                    (out float upper) => { upper = C(857.25f); return true; }, p.LowEfficiency, p.HighEfficiency, true);
                RunDecision cold = RunController.Update(C(20f), z, RunPolicy.DefaultMode, RunState.Fresh);
                TestRunner.Check(cold.AllowsProduction && cold.Efficiency < 1f && cold.Heating == HeatingIntent.None,
                    "20℃窑炉默认模式能低效冷启动，不依赖外部电热");
                TestRunner.Check(!RunController.Update(C(20f), z, RunPolicy.ControlModePolicy, RunState.Fresh).AllowsProduction,
                    "控制模式温度不足等待外部升温");
            }
            var context = new PowerDemandContext { HasLockedBatch = true, NoPowerRequired = true,
                TemperatureAllowsProduction = true, AutomationAllowsProduction = true, HasWorker = true,
                CommandedHeating = HeatingIntent.Sustained, PowerAvailable = false };
            PowerDemand demand = PowerDemand.Evaluate(context);
            TestRunner.Check(demand.ProductionRunning, "窑炉无电网仍生产，0W不代表未生产");
            TestRunner.CheckClose(0f, demand.RequestedTotalWatts, .001f, "无电建筑不回退到1200W，强制不产生加热需求");
            TestRunner.Check(demand.AppliedHeating == HeatingIntent.None, "无电建筑不会免费注热");
            context.AutomationAllowsProduction = false;
            TestRunner.Check(!PowerDemand.Evaluate(context).ProductionRunning, "无电不绕过自动化许可");
            context.AutomationAllowsProduction = true; context.BrokenDown = true;
            TestRunner.Check(!PowerDemand.Evaluate(context).ProductionRunning, "无电不绕过损坏停机");
        }

        private static void AlternativeMaterials()
        {
            RecipeZoneSpec carbon = RecipeZoneCatalogue.All.First(s => s.FabricatorId == "Kiln" && s.Outputs[0] == "RefinedCarbon");
            RecipeMaterial[] output = { new RecipeMaterial("RefinedCarbon", 100f) };
            string[] fuel = { "WoodLog", "FabricatedWood", "PalmWood", "Carbon", "Peat" };
            float[] amounts = { 200f, 200f, 200f, 125f, 300f };
            for (int i = 0; i < fuel.Length; i++)
            {
                TestRunner.Check(carbon.MatchesRecipe(new[] { new RecipeMaterial(fuel[i], amounts[i]) }, output), fuel[i] + " 按独立用量登记");
                TestRunner.Check(!carbon.MatchesRecipe(new[] { new RecipeMaterial(fuel[i], amounts[i] + 1f) }, output), fuel[i] + " 改变用量的第三方配方不接管");
            }
            TestRunner.Check(carbon.MatchesRecipe(new[] { new RecipeMaterial(new[] { "Peat", "Carbon" }, new[] { 300f, 125f }) }, output), "替代材料列表顺序改变，用量仍按材料关联");
            TestRunner.Check(!carbon.MatchesRecipe(new[] { new RecipeMaterial(new[] { "Peat", "Carbon" }, new[] { 125f, 300f }) }, output), "同一组选项但用量错配不接管");
            TestRunner.Check(!carbon.MatchesRecipe(new[] { new RecipeMaterial(new[] { "Peat", "Unknown" }, new[] { 300f, 125f }) }, output), "未知燃料不接管");
            TestRunner.Check(!carbon.MatchesRecipe(new[] { new RecipeMaterial(fuel, new[] { 200f }) }, output), "替代材料与用量长度不符拒绝");
            RecipeZoneSpec ceramic = RecipeZoneCatalogue.All.First(s => s.FabricatorId == "Kiln" && s.Outputs[0] == "Ceramic");
            TestRunner.Check(ceramic.MatchesRecipe(new[] { new RecipeMaterial("Peat", 25f), new RecipeMaterial("Clay", 100f) }, new[] { new RecipeMaterial("Ceramic", 100f) }), "多输入颠倒顺序仍匹配陶瓷");
            RecipeZoneSpec insulation = RecipeZoneCatalogue.All.First(s => s.Outputs[0] == "SuperInsulator");
            foreach (string fabric in new[] { "BasicFabric", "FeatherFabric" })
                TestRunner.Check(insulation.MatchesRecipe(new[] { new RecipeMaterial("Katairite", 80f), new RecipeMaterial(fabric, 5f), new RecipeMaterial("Isoresin", 15f) }, new[] { new RecipeMaterial("SuperInsulator", 100f) }), fabric + " 织物均能登记，DLC子集正常匹配");
            TestRunner.Check(!insulation.MatchesRecipe(new[] { new RecipeMaterial("Katairite", 80f), new RecipeMaterial("OtherFabric", 5f), new RecipeMaterial("Isoresin", 15f) }, new[] { new RecipeMaterial("SuperInsulator", 100f) }), "未知织物不静默接管");
        }

        private static void ImmuneDisplay()
        {
            var kiln = new MaterialOverhaulFab(new ComplexFabricator(), recipe => ProductionDecision.Passthrough)
                { PrefabId = "Kiln", DisplayWorkingCeiling = C(857.25f) };
            var descriptors = kiln.GetDescriptors(null);
            TestRunner.CheckEqual(1, descriptors.Count, "窑炉没有Overheatable也能生成工作温度行");
            TestRunner.Check(descriptors[0].Tooltip.Contains("过热豁免") && descriptors[0].Tooltip.Contains("没有内置加热"), "窑炉说明明确过热豁免与无加热");
            TestRunner.Check(!Strings.Get("STRINGS.UI.USERMENUACTIONS.MATERIALOVERHAUL.FORCEDRUN.TOOLTIP_NO_HEATING").Contains("仍需要材料、电力和复制人操作"), "无电无人操作窑炉的强制提示不虚构要求");
            var other = new MaterialOverhaulFab(new ComplexFabricator(), recipe => ProductionDecision.Passthrough)
                { PrefabId = "ChemicalRefinery", DisplayWorkingCeiling = C(857.25f) };
            TestRunner.CheckEqual(0, other.GetDescriptors(null).Count, "有Overheatable的建筑不重复增加工作温度行");
        }

        private static void Ports()
        {
            string[] ids = { "MetalRefinery", "RockCrusher", "GlassForge", "SupermaterialRefinery", "ChemicalRefinery", "Kiln" };
            int[] widths = { 3, 4, 5, 4, 4, 2 }, heights = { 4, 4, 4, 5, 3, 2 };
            // 前四栋需要电力，原版默认在 (0,0) 有逻辑控制输入；后两栋在 Config 写明。
            int[] inputX = { 0, 0, 0, 0, 0, 0 }, inputY = { 0, 0, 0, 0, 0, 1 };
            for (int i = 0; i < ids.Length; i++)
            {
                CellOffset input, output;
                TestRunner.Check(AutomationControlScope.TryGetOffset(ids[i], out input), ids[i] + " 用原版输入");
                TestRunner.Check(input.x == inputX[i] && input.y == inputY[i], ids[i] + " 输入为原版位置");
                TestRunner.Check(AutomationControlScope.TryGetOutputOffset(ids[i], out output), ids[i] + " 输出存在");
                int left = widths[i] / 2 - widths[i] + 1, right = widths[i] / 2;
                TestRunner.Check(output.x >= left && output.x <= right && output.y >= 0 && output.y < heights[i], ids[i] + " 输出在占格内");
                TestRunner.Check(input.x != output.x || input.y != output.y, ids[i] + " 输入输出不重叠");
            }
            CellOffset chemical, kiln;
            AutomationControlScope.TryGetOffset("ChemicalRefinery", out chemical);
            AutomationControlScope.TryGetOffset("Kiln", out kiln);
            TestRunner.Check(chemical.x == 0 && chemical.y == 0 && kiln.x == 0 && kiln.y == 1, "化学精炼厂与窑炉复用原版输入位置");
        }
    }
}
