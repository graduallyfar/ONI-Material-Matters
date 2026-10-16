using System;
using System.Collections.Generic;
using System.Linq;
using MaterialOverhaul.Core;
using MaterialOverhaul.Game;
using MaterialOverhaul.Hardness;
using MaterialOverhaul.Heating;
using MaterialOverhaul.Zones;

namespace MaterialOverhaul.Tests
{
    public static class FirstReleaseBalanceTests
    {
        private const float Tol = 0.001f;
        private static float C(float c) { return Temperatures.FromCelsius(c); }
        private static ResolvedZone Zone(string building, float b1, float b2, float upper)
        {
            BuildingProfile p = BuildingProfiles.All[building];
            return TempZoneResolver.Resolve(building, C(p.WorkMinCelsius), TempZone.Celsius(b1, b2),
                (out float ceiling) => { ceiling = C(upper); return true; }, p.LowEfficiency, p.HighEfficiency, !p.BuiltInHeating);
        }
        private static float Efficiency(float c, ResolvedZone z)
        {
            return TempZoneEvaluator.Efficiency(TempZoneEvaluator.Evaluate(C(c), z), C(c), z);
        }
        public static void Run()
        {
            ProfilesAndCurves();
            RunAndPower();
            RecoveryAndHeatBudget();
            Wear();
            Registration();
        }
        private static void ProfilesAndCurves()
        {
            TestRunner.Section("独立建筑参数与效率端点");
            // 第三批开发后白名单共 29 栋；数量本身不是结论，随后逐栋核对参数（见 ThirdWaveBuildingTests）。
            TestRunner.CheckEqual(29, BuildingProfiles.All.Count, "首发三栋加第二批三栋加第三批23栋");
            TestRunner.CheckEqual(29, BuildingWhitelist.Count, "白名单与参数表同步");
            TestRunner.Check(BuildingWhitelist.Contains("Kiln") && BuildingWhitelist.Contains("RubberMaker"), "第二批与第三批建筑均已纳入名单");
            TestRunner.Check(!BuildingWhitelist.Contains("CraftingTable") && !BuildingWhitelist.Contains("SuitFabricator")
                && !BuildingWhitelist.Contains("CookingStation") && !BuildingWhitelist.Contains("Compost"),
                "工业制造三栋与生活类建筑不扩散");
            string[] buildings = { "MetalRefinery", "RockCrusher", "GlassForge" };
            float[] floors = { 80f, -20f, 80f };
            float[] lows = { .30f, .80f, .25f };
            float[] highs = { .80f, .90f, .75f };
            float[] b1 = { 450f, 15f, 900f };
            float[] b2 = { 650f, 60f, 1200f };
            for (int i = 0; i < buildings.Length; i++)
            {
                ResolvedZone z = Zone(buildings[i], b1[i], b2[i], 1600f);
                TestRunner.CheckClose(C(floors[i]), z.Min, Tol, buildings[i] + " 下限");
                TestRunner.CheckClose(lows[i], Efficiency(floors[i], z), Tol, "低温端点");
                TestRunner.CheckClose(lows[i], Efficiency(floors[i] - 20f, z), Tol, "强制低于下限取底值");
                TestRunner.CheckClose((float)Math.Sqrt(lows[i]), Efficiency((floors[i] + b1[i]) / 2f, z), Tol, "低侧指数中点");
                TestRunner.CheckClose(1f, Efficiency(b1[i], z), Tol, "最佳下限 100%");
                TestRunner.CheckClose(1f, Efficiency(b2[i], z), Tol, "最佳上限 100%");
                TestRunner.CheckClose((float)Math.Sqrt(highs[i]), Efficiency((1600f + b2[i]) / 2f, z), Tol, "高侧指数中点");
                TestRunner.CheckClose(highs[i], Efficiency(1600f, z), Tol, "高温端点");
                TestRunner.CheckClose(highs[i], Efficiency(1700f, z), Tol, "强制超过上限取底值");
                TestRunner.Check(HeatingScope.IsEnabled(buildings[i]) == (i != 1), "内置加热能力独立");
            }
            ResolvedZone limited = Zone("GlassForge", 900f, 1200f, 989.1f);
            TestRunner.CheckClose(1f, Efficiency(1100f, limited), Tol, "强制超过 U 仍在最佳区，效率 100%");
            TestRunner.CheckClose(.75f, Efficiency(1201f, limited), Tol, "无高温段时越过最佳上限取高侧底值");
            ResolvedZone unreachable = Zone("GlassForge", 900f, 1200f, 626.85f);
            TestRunner.Check(Efficiency(800f, unreachable) > .25f && Efficiency(800f, unreachable) < 1f,
                "强制超过 U 但仍低于最佳区，沿低侧曲线");
        }
        private static void RunAndPower()
        {
            TestRunner.Section("粉碎机无热、供电与强制");
            ResolvedZone rock = Zone("RockCrusher", 15f, 60f, 626.85f);
            RunDecision cold = RunController.Update(C(-30f), rock, RunPolicy.ControlModePolicy, RunState.Fresh);
            TestRunner.Check(!cold.AllowsProduction && cold.Heating == HeatingIntent.None, "冷机停产但不虚构加热");
            TestRunner.CheckClose(C(-16.5f), StartThreshold.Start(rock, RunPolicy.DefaultMode), Tol, "粉碎机默认启动线");
            TestRunner.CheckClose(C(19.5f), StartThreshold.Start(rock, RunPolicy.ControlModePolicy), Tol, "粉碎机控制启动线");
            RunDecision ready = RunController.Update(C(20f), rock, RunPolicy.ControlModePolicy, RunState.Fresh);
            TestRunner.Check(ready.AllowsProduction && ready.Heating == HeatingIntent.None, "外热达到启动线后正常生产");
            RunPolicy forced = RunPolicy.ControlModePolicy;
            forced.Forced = true;
            RunDecision forcedCold = RunController.Update(C(-30f), rock, forced, RunState.Fresh);
            TestRunner.Check(forcedCold.AllowsProduction && forcedCold.Heating == HeatingIntent.None, "强制冷机生产但无热");
            TestRunner.CheckEqual(HeatingIntent.None, HeatingPlanner.Decide(C(-30f), rock, forced, false), "简化加热入口同样无热");
            RunState stale = ready.Next;
            stale.TopUpActive = true;
            RunState loaded = RunController.OnLoaded(C(20f), rock, RunPolicy.ControlModePolicy, stale);
            TestRunner.Check(loaded.Started && !loaded.TopUpActive, "粉碎机读档保留启动资格并清理旧补热");
            ComplexFabricator machine = new ComplexFabricator { CurrentWorkingOrder = new ComplexRecipe { id = "rock" } };
            MaterialOverhaulFab fab = new MaterialOverhaulFab(machine, r => ProductionDecision.Passthrough) { PrefabId = "RockCrusher", Forced = true };
            fab.RefreshSafety();
            TestRunner.CheckClose(240f, fab.SafetyPower.UsedProductionWatts, Tol, "实际参数 partial 接入 240 W");
            TestRunner.CheckClose(0f, fab.SafetyPower.RequestedHeatingWatts, Tol, "即使残留持续加热意图也不申请加热电力");
            fab.IsPowered = false;
            fab.RefreshSafety();
            TestRunner.CheckClose(240f, fab.SafetyPower.RequestedTotalWatts, Tol, "断电保留生产需求");
            TestRunner.CheckClose(0f, fab.SafetyPower.UsedTotalWatts, Tol, "断电无消费");
            fab.PrefabId = "GlassForge";
            fab.IsPowered = true;
            fab.RefreshSafety();
            TestRunner.CheckClose(2400f, fab.SafetyPower.UsedTotalWatts, Tol, "玻璃熔炉生产加默认内置热 2400 W");
            PowerDemand demand = PowerDemand.Evaluate(new PowerDemandContext {
                HasLockedBatch = true, PowerAvailable = true, ProductionWatts = 240f,
                TemperatureAllowsProduction = true, HasWorker = true, AutomationAllowsProduction = false,
                CommandedHeating = HeatingIntent.Sustained, DisallowBuiltInHeating = true });
            TestRunner.CheckClose(0f, demand.UsedTotalWatts, Tol, "粉碎机红信号停产也无热");
        }
        private static void RecoveryAndHeatBudget()
        {
            TestRunner.Section("窄交集、单点与普通注热预算");
            ResolvedZone aluminium = Zone("MetalRefinery", 450f, 650f, 626.85f);
            TestRunner.CheckClose(8.8425f, StartThreshold.ControlHysteresis(aluminium), Tol, "砂岩炼铝控制回差");
            TestRunner.CheckClose(C(618.0075f), StartThreshold.ResumeLine(aluminium, RunPolicy.ControlModePolicy), Tol, "砂岩炼铝高温恢复线");
            ResolvedZone glass = Zone("GlassForge", 900f, 1200f, 989.1f);
            TestRunner.CheckClose(4.455f, HeatingPlanner.Hysteresis(glass), Tol, "火成岩玻璃回差");
            TestRunner.CheckClose(C(925.545f), HeatingPlanner.TopUpLine(glass, RunPolicy.ControlModePolicy), Tol, "火成岩玻璃补热启动线");
            ResolvedZone point = Zone("GlassForge", 900f, 1200f, 900f);
            TestRunner.CheckClose(0f, StartThreshold.ControlHysteresis(point), Tol, "单点回差零");
            TestRunner.Check(RunController.Update(C(900f), point, RunPolicy.ControlModePolicy, RunState.Fresh).AllowsProduction, "单点恰好允许生产");
            ResolvedZone empty = Zone("GlassForge", 900f, 1200f, 626.85f);
            RunDecision blocked = RunController.Update(C(626.85f), empty, RunPolicy.ControlModePolicy, RunState.Fresh);
            TestRunner.Check(!blocked.AllowsProduction && blocked.Heating == HeatingIntent.None && blocked.Next.ZoneUnreachableLatched,
                "无交集到上限停热并保持批次暂停");
            TestRunner.CheckClose(32f, HeatingPower.CapKilojoules(80f, C(449f), C(450f), 32f), Tol, "末步只注入剩余32 kJ");
            TestRunner.CheckClose(0f, HeatingPower.CapKilojoules(80f, C(450f), C(450f), 32f), Tol, "已到目标不继续注热");
            TestRunner.CheckClose(20f, HeatingPower.CapKilojoules(20f, C(449f), C(450f), 32f), Tol, "功率不足仍只注20 kJ");
            TestRunner.CheckClose(100f, HeatingPower.TestHeatMultiplier, Tol, "倍率100");
            TestRunner.CheckClose(120f, HeatingPower.KilowattsFor(HeatingIntent.Preheat, 1200f), Tol, "1200 W 对应120 kW内置热");
        }
        private static void Wear()
        {
            TestRunner.Section("每栋独立磨损与既有HP余额");
            string[] buildings = { "MetalRefinery", "RockCrusher", "GlassForge" };
            float[] damage = { 2f, .8f, 1f };
            for (int i = 0; i < buildings.Length; i++)
            {
                WearAccumulator accumulator = new WearAccumulator();
                float rate = BuildingProfiles.All[buildings[i]].DamagePerWearUnit;
                for (int step = 0; step < 200; step++) DamageConversion.AccumulateProduction(accumulator, .2f, rate);
                TestRunner.CheckClose(damage[i], accumulator.DamageBalance, Tol, buildings[i] + " 40生产秒损伤");
                float balance = accumulator.DamageBalance;
                WearAccumulator loaded = new WearAccumulator();
                loaded.Restore(balance);
                DamageConversion.AccumulateProduction(loaded, 0f, rate);
                TestRunner.CheckClose(balance, loaded.DamageBalance, Tol, "读档暂停不重算或清除余额");
            }
        }
        private static ComplexRecipe.RecipeElement Element(string id, float amount)
        {
            return new ComplexRecipe.RecipeElement { material = new Tag(id), amount = amount };
        }
        private static void Registration()
        {
            TestRunner.Section("实际登记源码、配方数量与候选材料集合");
            ComplexRecipeManager.Instance.recipes.Clear();
            ContentRegistration.Apply();
            TestRunner.Check(!ContentRegistration.ZonesResolved, "空配方表不误报登记完成");
            TestRunner.CheckEqual(60, RecipeZoneCatalogue.Count, "首发40条、第二批11条、第三批9条");
            TestRunner.CheckEqual(13, RecipeZoneCatalogue.All.Count(s => s.FabricatorId == "MetalRefinery"), "精炼13条");
            TestRunner.CheckEqual(26, RecipeZoneCatalogue.All.Count(s => s.FabricatorId == "RockCrusher"), "粉碎26条");
            TestRunner.CheckEqual(1, RecipeZoneCatalogue.All.Count(s => s.FabricatorId == "GlassForge"), "玻璃1条");
            foreach (RecipeZoneSpec spec in RecipeZoneCatalogue.All)
            {
                string id = "first-release-" + ComplexRecipeManager.Instance.recipes.Count;
                ComplexRecipe recipe = new ComplexRecipe { id = id,
                    consumedHEP = spec.ConsumedParticles.HasValue ? spec.ConsumedParticles.Value : 0,
                    fabricators = new List<Tag> { new Tag(spec.FabricatorId) },
                    ingredients = spec.Inputs.Select((s, i) => Element(s, spec.InputAmounts[i])).ToArray(),
                    results = spec.Outputs.Select((s, i) => Element(s, spec.OutputAmounts[i])).ToArray() };
                if (spec.InputOptions != null) { recipe.ingredients[0].material = null; recipe.ingredients[0].possibleMaterials = spec.InputOptions.Select(s => new Tag(s)).ToArray(); }
                if (spec.InputAlternatives != null)
                    recipe.ingredients = spec.InputAlternatives.Select(s => new ComplexRecipe.RecipeElement {
                        material = s.Materials.Length == 1 ? new Tag(s.Materials[0]) : null,
                        possibleMaterials = s.Materials.Select(m => new Tag(m)).ToArray(),
                        possibleMaterialAmounts = s.Amounts, amount = s.Amount }).ToArray();
                ComplexRecipeManager.Instance.recipes.Add(recipe);
            }
            ComplexRecipe altered = new ComplexRecipe { id = "unknown-yield", fabricators = new List<Tag> { new Tag("GlassForge") },
                ingredients = new[] { Element("Sand", 100f) }, results = new[] { Element("MoltenGlass", 50f) } };
            ComplexRecipeManager.Instance.recipes.Add(altered);
            ContentRegistration.RegisterRecipeZones();
            TestRunner.Check(ContentRegistration.ZonesResolved, "真实表处理完成");
            foreach (ComplexRecipe recipe in ComplexRecipeManager.Instance.recipes.Take(60))
                TestRunner.Check(RecipeZoneTable.GetOrUnconfigured(recipe.id).Configured, recipe.id + " 按完整材料和质量登记");
            TestRunner.Check(!RecipeZoneTable.GetOrUnconfigured(altered.id).Configured, "不同产量的其他Mod配方保持原版");
            ContentRegistration.RegisterRecipeZones();
            TestRunner.CheckEqual(50f, altered.results[0].amount, "登记不修改原版产量");
            foreach (string building in BuildingProfiles.All.Keys)
            {
                byte hardness; float fraction;
                TestRunner.Check(HardnessRequirementTable.TryGet(building, out hardness) && hardness == 25, building + " 硬度25");
                TestRunner.Check(OverheatRuleTable.TryGet(building, out fraction) && fraction == .75f, building + " 材料上限规则");
            }
            RecipeZoneSpec sand = RecipeZoneCatalogue.All.First(s => s.InputOptions != null);
            RecipeMaterial[] output = { new RecipeMaterial("Sand", 100f) };
            TestRunner.Check(sand.MatchesRecipe(new[] { new RecipeMaterial(new[] { "Granite", "Basalt" }, 100f) }, output), "制砂允许当前DLC候选子集");
            TestRunner.Check(!sand.MatchesRecipe(new[] { new RecipeMaterial(new[] { "Granite", "AlienRock" }, 100f) }, output), "未知候选不擅自扩散");
            TestRunner.Check(!sand.MatchesRecipe(new[] { new RecipeMaterial("Granite", float.NaN) }, output), "非法质量不匹配");
            TestRunner.Check(!sand.MatchesRecipe(new[] { new RecipeMaterial(Array.Empty<string>(), 100f) }, output), "空候选不匹配");
            RecipeZoneSpec galena = RecipeZoneCatalogue.All.First(s => s.FabricatorId == "RockCrusher" && s.Inputs[0] == "Galena");
            TestRunner.Check(galena.MatchesRecipe(new[] { new RecipeMaterial("Galena", 100f) }, new[] {
                new RecipeMaterial("Sand", 50f), new RecipeMaterial("Sulfur", 6.5f), new RecipeMaterial("Lead", 43.5f) }), "副产物倒序仍匹配");
            TestRunner.Check(!galena.MatchesRecipe(new[] { new RecipeMaterial("Galena", 100f) }, new[] {
                new RecipeMaterial("Sand", 50f), new RecipeMaterial("Lead", 43.5f) }), "缺少副产物不匹配");
            // DLC未启用时只登记实际存在的配方，无需构造缺失建筑或配方。
            ComplexRecipeManager.Instance.recipes.Clear();
            ComplexRecipeManager.Instance.recipes.Add(altered);
            ContentRegistration.RegisterRecipeZones();
            TestRunner.Check(ContentRegistration.ZonesResolved, "DLC候选缺失也完成登记，不反复全表重试");
        }
    }
}
