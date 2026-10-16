using System;
using System.Collections.Generic;
using System.Reflection;
using MaterialOverhaul.Game;
using MaterialOverhaul.Hardness;
using MaterialOverhaul.Heating;
using MaterialOverhaul.Zones;

namespace MaterialOverhaul.Tests
{
    public static class StatusItemRegressionTests
    {
        public static void Run()
        {
            TestRunner.Section("实际状态注册：完整键、构造重载和世界图标");
            LocString.CreateLocStringKeys(typeof(ModStrings.UI), "STRINGS.");
            LocString.CreateLocStringKeys(typeof(ModStrings.BUILDINGS), "STRINGS.");
            LocString.CreateLocStringKeys(typeof(ModStrings.BUILDING), "STRINGS.");
            TestRunner.CheckEqual(81, Strings.Values.Count, "真实文本树登记81条完整键");
            DamageStatusItems.Register();
            MaterialRiskStatusItems.Register();
            RunModeStatusItem.Register();
            RunStatusItem.Register();
            StatusItem[] statuses = { DamageStatusItems.Mechanical, DamageStatusItems.Overheat,
                DamageStatusItems.Both, DamageStatusItems.Unknown, MaterialRiskStatusItems.Wear,
                MaterialRiskStatusItems.Heat, MaterialRiskStatusItems.ZoneLimit };
            string[] names = { "损坏：机械磨损", "损坏：过热", "损坏：机械磨损、过热", "损坏：原因不明",
                "硬度不足", "过热", "温区受限" };
            for (int i = 0; i < statuses.Length; i++)
            {
                TestRunner.CheckEqual(names[i], statuses[i].Name, "实际构造结果 " + names[i]);
                TestRunner.Check(!statuses[i].Tooltip.Contains("MISSING."), "说明键未被再次拼接 " + names[i]);
                TestRunner.Check(statuses[i].ShowWorldIcon, "真实风险和损坏保留世界警告图标 " + names[i]);
            }
            TestRunner.Check(!RunModeStatusItem.Item.ShowWorldIcon, "模式只显示状态栏，不伪造无制作队列图标");
            TestRunner.Check(!RunStatusItem.Item.ShowWorldIcon, "温控运行只显示状态栏，不伪造无制作队列图标");
            StatusItem nativeIdle = new StatusItem("FabricatorIdle", "无制作队列", "选择一个配方以开始制造",
                "status_item_fabricator_select", StatusItem.IconType.Custom, NotificationType.Neutral, false, OverlayModes.None.ID);
            TestRunner.Check(nativeIdle.ShowWorldIcon, "原版真正无队列的提示不被关闭");

            TestRunner.Section("三栋实际名称／悬停回调：预热、生产、强制和损坏");
            foreach (string building in new[] { "MetalRefinery", "RockCrusher", "GlassForge" })
            {
                ComplexFabricator machine = new ComplexFabricator { CurrentWorkingOrder = new ComplexRecipe { id = "test-" + building } };
                MaterialOverhaulFab fab = new MaterialOverhaulFab(machine, r => ProductionDecision.Passthrough) { PrefabId = building };
                foreach (StatusItem item in statuses)
                {
                    TestRunner.Check(!item.ResolvedName(fab).Contains("MISSING."), building + " " + item.Id + " 名称");
                    TestRunner.Check(!item.ResolvedTooltip(fab).Contains("MISSING."), building + " " + item.Id + " 完整悬停");
                }
                TestRunner.CheckEqual("默认模式", RunModeStatusItem.Item.ResolvedName(fab), building + " 默认模式名称");
                fab.ControlMode = true;
                TestRunner.CheckEqual("控制模式", RunModeStatusItem.Item.ResolvedName(fab), building + " 控制模式名称");
                fab.Forced = true;
                TestRunner.CheckEqual("控制模式 · 强制运行", RunModeStatusItem.Item.ResolvedName(fab), building + " 强制不遮住基础模式");
                string modeTooltip = RunModeStatusItem.Item.ResolvedTooltip(fab);
                TestRunner.Check(!modeTooltip.Contains("MISSING."), building + " 模式悬停有本地化文本");
                TestRunner.Check(modeTooltip.Contains("没有内置加热") == (building == "RockCrusher"), building + " 内置加热文案区分");
                fab.HP.HitPoints = 99;
                fab.LatchOverheatDamage();
                string damageTooltip = DamageStatusItems.Both.ResolvedTooltip(fab);
                TestRunner.Check(damageTooltip.Contains("内置加热已停止") == (building != "RockCrusher"), building + " 受损停热句按真实能力显示");
                TestRunner.CheckEqual(DamageStatusItems.Both, DamageStatusItems.For(DamageCause.MechanicalAndOverheat), "双重原因仍用合并状态项");

                fab.Forced = false;
                fab.ControlMode = false;
                fab.SafetyPower = new PowerDemand { AppliedHeating = building == "RockCrusher" ? HeatingIntent.None : HeatingIntent.Preheat };
                if (building != "RockCrusher")
                {
                    TestRunner.CheckEqual("预热中", RunStatusItem.Item.ResolvedName(fab), building + " 实际运行回调预热中文");
                    string tooltip = RunStatusItem.Item.ResolvedTooltip(fab);
                    TestRunner.Check(!tooltip.Contains("MISSING.") && tooltip.Contains("内置加热：预热中"), building + " 完整运行悬停");
                }
                machine.OrderProgress = .61f;
                fab.SafetyPower = new PowerDemand { ProductionRunning = true };
                fab.CurrentState = TempState.Normal;
                TestRunner.CheckEqual("生产中", RunStatusItem.Item.ResolvedName(fab), building + " 正在生产时显示真实状态");
                TestRunner.Check(!RunModeStatusItem.Item.ShowWorldIcon && !RunStatusItem.Item.ShowWorldIcon, building + " 有安排且生产中没有额外队列图标");
                TestRunner.CheckClose(.61f, machine.OrderProgress, .0001f, "渲染状态不改生产进度");
                TestRunner.CheckClose(100f, machine.BatchMaterialMass, .0001f, "渲染状态不动已投料材料");
                fab.HardnessDeficit = true;
                fab.IsAboveOverheatThreshold = true;
                fab.RefreshRisks();
                TestRunner.Check(!fab.Selection.Items.ContainsKey(MaterialRiskStatusItems.WearCategory), building + " 已有双重损坏不重复显示尚未受损的磨损风险");
                TestRunner.Check(!fab.Selection.Items.ContainsKey(MaterialRiskStatusItems.HeatCategory), building + " 已有损坏不重复显示尚未受损的过热风险");
                fab.RestoreDamage(DamageCauseState.Fresh);
                fab.RefreshRisks();
                TestRunner.Check(fab.Selection.Items.ContainsKey(MaterialRiskStatusItems.WearCategory), building + " 尚未受损时显示真实硬度风险");
                TestRunner.Check(fab.Selection.Items.ContainsKey(MaterialRiskStatusItems.HeatCategory), building + " 尚未受损时显示真实过热风险");
            }
            TestRunner.CheckEqual(9, Db.Get().BuildingStatusItems.Count, "实际注册四种损坏、三种风险、模式和运行项");
            InfoCards();
        }

        private static void InfoCards()
        {
            TestRunner.Section("三栋实际信息卡补丁：工作温度、配方详情和范围隔离");
            Assembly assembly = typeof(MaterialOverhaul.Patches.Patch_ComplexFabricator_UpdateChore).Assembly;
            MethodInfo buildingPostfix = assembly.GetType("MaterialOverhaul.Patches.Patch_Overheatable_GetDescriptors")
                .GetMethod("Postfix", BindingFlags.NonPublic | BindingFlags.Static);
            MethodInfo recipePostfix = assembly.GetType("MaterialOverhaul.Patches.Patch_ComplexFabricator_AdditionalEffectsForRecipe")
                .GetMethod("Postfix", BindingFlags.NonPublic | BindingFlags.Static);
            string[] buildings = { "MetalRefinery", "RockCrusher", "GlassForge" };
            string[] recipeIds = { "first-release-4", "first-release-25", "first-release-39" };
            string[] floors = { "80.0", "-20.0", "80.0" };
            string[] bestMins = { "450.0", "15.0", "900.0" };
            for (int i = 0; i < buildings.Length; i++)
            {
                MaterialOverhaulFab fab = new MaterialOverhaulFab(new ComplexFabricator(), r => ProductionDecision.Passthrough) { PrefabId = buildings[i] };
                List<Descriptor> native = new List<Descriptor> { new Descriptor("原版过热温度", "原版属性来源", Descriptor.DescriptorType.Effect) };
                Overheatable heat = new Overheatable { Mod = fab, OverheatTemperature = 1000f };
                object[] args = { heat, native };
                buildingPostfix.Invoke(null, args);
                List<Descriptor> result = (List<Descriptor>)args[1];
                TestRunner.CheckEqual(2, result.Count, buildings[i] + " 追加工作温度，保留原版条目");
                TestRunner.CheckEqual("原版属性来源", result[0].Tooltip, "不重写原版过热说明");
                TestRunner.Check(result[1].Text.Contains(floors[i]) && !result[1].Text.Contains("MISSING."), buildings[i] + " 显示本栋真实工作下限");
                TestRunner.Check(result[1].Tooltip.Contains("最高温度") && !result[1].Tooltip.Contains("MISSING."), buildings[i] + " 工作温度悬停正确");
                args = new object[] { new ComplexRecipe { id = recipeIds[i] }, new List<Descriptor>() };
                recipePostfix.Invoke(null, args);
                result = (List<Descriptor>)args[1];
                TestRunner.CheckEqual(1, result.Count, buildings[i] + " 已注册配方追加最佳温度");
                TestRunner.Check(result[0].Text.Contains(bestMins[i]) && !result[0].Text.Contains("MISSING."), buildings[i] + " 配方最佳温度中文数值");
                TestRunner.Check(result[0].Tooltip.Contains("100%") && !result[0].Tooltip.Contains("MISSING."), buildings[i] + " 配方详情使用独立说明");
            }
            MaterialOverhaulFab outside = new MaterialOverhaulFab(new ComplexFabricator(), r => ProductionDecision.Passthrough) { PrefabId = "MicrobeMusher" };
            object[] unchanged = { new Overheatable { Mod = outside, OverheatTemperature = 1000f }, new List<Descriptor>() };
            buildingPostfix.Invoke(null, unchanged);
            TestRunner.CheckEqual(0, ((List<Descriptor>)unchanged[1]).Count, "名单外没有工作温度文案");
            unchanged = new object[] { new ComplexRecipe { id = "foreign-recipe" }, new List<Descriptor>() };
            recipePostfix.Invoke(null, unchanged);
            TestRunner.CheckEqual(0, ((List<Descriptor>)unchanged[1]).Count, "未配置配方不伪造最佳温度");
        }
    }
}
