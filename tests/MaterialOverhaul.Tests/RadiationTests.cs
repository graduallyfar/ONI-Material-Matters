using System;
using System.IO;
using System.Text.RegularExpressions;
using MaterialOverhaul.Radiation;

namespace MaterialOverhaul.Tests
{
    /// <summary>
    /// 辐射独立验证版 0.0.1：感生放射性的纯计算与接入结构（辐射系统设计方案 §8、§9）。
    /// 只验证公式、优先规则和源码结构；原生 Sim 的辐射、病菌行为与界面需游戏内验收。
    /// </summary>
    public static class RadiationTests
    {
        public static void Run()
        {
            Parameters();
            Materials();
            ActivationSteps();
            InducedFeedback();
            Contamination();
            ContaminantPriority();
            SourceIndex();
            ProfileIsolation();
        }

        private static void Parameters()
        {
            TestRunner.Section("感生放射参数（工作初值）");
            TestRunner.CheckClose(500f, RadiationBalance.ActivationThresholdRads, 0.0001f, "活化门槛 500 rads/周期");
            TestRunner.CheckClose(10f, RadiationBalance.BuildingHalfLifeCycles, 0.0001f, "建筑活化半衰期 10 周期");
            TestRunner.CheckClose(10f, RadiationBalance.EmitMinRads, 0.0001f, "自身放射下限 10 rads/周期");
            TestRunner.CheckEqual(2, RadiationBalance.EmitRadiusCells, "自身放射半径 2 格");
            TestRunner.CheckClose(0.035f, RadiationBalance.ContaminantsPerTonRadSecond, 0.00001f, "污染物系数 0.035");
            TestRunner.CheckClose(50f, RadiationBalance.GasMassMultiplier, 0.0001f, "气体按质量 50 倍计算");
            TestRunner.CheckEqual(5, RadiationBalance.ScanSlicesPerPass, "每 5 秒扫完一遍");
            TestRunner.CheckClose(RadiationBalance.ScanPassSeconds, RadiationBalance.ScanSlicesPerPass * 1f, 0.0001f, "扫描份数与 1 秒更新对应");
        }

        private static void Materials()
        {
            TestRunner.Section("材料活化系数");
            TestRunner.CheckClose(0.05f, ActivationMaterial.FractionFor("Steel", ActivationCategory.Metal), 0.0001f, "钢 5%");
            TestRunner.CheckClose(0.05f, ActivationMaterial.FractionFor("Iron", ActivationCategory.Metal), 0.0001f, "铁 5%");
            TestRunner.CheckClose(0.04f, ActivationMaterial.FractionFor("Copper", ActivationCategory.Metal), 0.0001f, "铜 4%");
            TestRunner.CheckClose(0.025f, ActivationMaterial.FractionFor("Aluminum", ActivationCategory.Metal), 0.0001f, "铝 2.5%");
            TestRunner.CheckClose(0.01f, ActivationMaterial.FractionFor("Lead", ActivationCategory.Metal), 0.0001f, "铅 1%");
            TestRunner.CheckClose(0.01f, ActivationMaterial.FractionFor("DepletedUranium", ActivationCategory.Metal), 0.0001f, "贫铀 1%");
            TestRunner.CheckClose(0.03f, ActivationMaterial.FractionFor("Tungsten", ActivationCategory.Metal), 0.0001f, "未列出金属按 3%");
            TestRunner.CheckClose(0.015f, ActivationMaterial.FractionFor("Granite", ActivationCategory.Mineral), 0.0001f, "矿物按 1.5%");
            TestRunner.CheckClose(0.005f, ActivationMaterial.FractionFor("Polypropylene", ActivationCategory.Organic), 0.0001f, "塑料按 0.5%");
            TestRunner.CheckClose(0.015f, ActivationMaterial.FractionFor(null, ActivationCategory.Mineral), 0.0001f, "缺元素按分类默认");
            TestRunner.Check(ActivationMaterial.FractionFor("Lead", ActivationCategory.Metal)
                < ActivationMaterial.FractionFor("Steel", ActivationCategory.Metal), "铅比钢难活化");
        }

        private static void ActivationSteps()
        {
            TestRunner.Section("建筑活化度步进");
            float lambda = ActivationModel.BuildingDecayPerSecond;
            TestRunner.CheckClose((float)(Math.Log(2.0) / 6000.0), lambda, 1e-9f, "半衰期 10 周期 = 6000 s");

            TestRunner.CheckClose(0f, ActivationModel.EffectiveExcess(500f, 0f), 0.0001f, "500 rads 恰在门槛，不活化");
            TestRunner.CheckClose(5500f, ActivationModel.EffectiveExcess(6000f, 0f), 0.0001f, "6000 rads 超额 5500");
            TestRunner.CheckClose(0f, ActivationModel.EffectiveExcess(float.NaN, 0f), 0.0001f, "缺失辐射值不活化");

            // 钢墙贴反应堆（6000 rads/周期）：稳态 275 rads/周期；一个半衰期达到一半。
            TestRunner.CheckClose(275f, ActivationModel.SteadyState(5500f, 0.05f), 0.01f, "钢稳态 275");
            TestRunner.CheckClose(55f, ActivationModel.SteadyState(5500f, 0.01f), 0.01f, "铅稳态 55");
            float halfLife = 10f * 600f;
            TestRunner.CheckClose(137.5f, ActivationModel.Step(0f, 5500f, 0.05f, halfLife), 0.05f, "10 周期达到稳态一半");
            TestRunner.CheckClose(206.25f, ActivationModel.Step(0f, 5500f, 0.05f, 2f * halfLife), 0.05f, "20 周期达到 75%");

            // 与步长无关：一次走 100 s 与分 20 次走 5 s 结果一致。
            float stepped = 0f;
            for (int i = 0; i < 20; i++) stepped = ActivationModel.Step(stepped, 5500f, 0.05f, 5f);
            TestRunner.CheckClose(ActivationModel.Step(0f, 5500f, 0.05f, 100f), stepped, 0.0005f, "积分与更新间隔无关");

            // 离开辐射源后按半衰期衰减。
            TestRunner.CheckClose(100f, ActivationModel.Step(200f, 0f, 0.05f, halfLife), 0.05f, "离开后 10 周期减半");
            TestRunner.CheckClose(200f, ActivationModel.Step(200f, 0f, 0.05f, 0f), 0.0001f, "dt 为 0 不变");
            TestRunner.CheckClose(0f, ActivationModel.Step(-5f, 0f, 0.05f, 5f), 0.0001f, "负活化度按 0 处理");

            TestRunner.CheckClose(0f, ActivationModel.EmitRads(9.9f), 0.0001f, "低于 10 不放射");
            TestRunner.CheckClose(10f, ActivationModel.EmitRads(10f), 0.0001f, "达到 10 开始放射");
            TestRunner.Check(ActivationModel.NeedsRefresh(0f, 12f), "开始放射需要通知 Sim");
            TestRunner.Check(ActivationModel.NeedsRefresh(12f, 0f), "停止放射需要通知 Sim");
            TestRunner.Check(!ActivationModel.NeedsRefresh(100f, 104f), "变化不足 5% 不通知");
            TestRunner.Check(ActivationModel.NeedsRefresh(100f, 106f), "变化超过 5% 通知");
            TestRunner.Check(!ActivationModel.NeedsRefresh(0f, 0f), "一直不放射不通知");
            TestRunner.Check(ActivationModel.CanDrop(0.4f, 0f), "未受照且活化度很低时移除");
            TestRunner.Check(!ActivationModel.CanDrop(0.4f, 1f), "仍在受照不移除");
            TestRunner.Check(!ActivationModel.CanDrop(50f, 0f), "仍有活化度不移除");
        }

        private static void InducedFeedback()
        {
            TestRunner.Section("活化物不能互相维持");
            // 5×5 钢砖块阵列，每块 300 rads/周期，按上界全部叠加到中心格：7500。
            // 反应堆移除后实测辐射全部来自活化物，扣掉后没有超额辐射，只会衰减。
            float measured = 25f * 300f;
            float induced = 25f * 300f;
            TestRunner.CheckClose(0f, ActivationModel.EffectiveExcess(measured, induced), 0.0001f, "只有活化物照射时超额为 0");
            float after = ActivationModel.Step(300f, ActivationModel.EffectiveExcess(measured, induced), 0.05f, 6000f);
            TestRunner.Check(after < 300f, "无真实辐射源时活化度下降");
            // 真实辐射源仍在时，扣掉活化物贡献后剩下真实部分。
            TestRunner.CheckClose(5500f, ActivationModel.EffectiveExcess(6000f + 275f, 275f), 0.01f, "扣掉自身贡献后只剩真实辐射");
        }

        private static void Contamination()
        {
            TestRunner.Section("液体、气体、天然格的污染物添加量");
            TestRunner.CheckClose(1000f, ContaminationModel.EffectiveMassKg(1000f, CellPhase.Liquid), 0.001f, "液体按真实质量");
            TestRunner.CheckClose(100f, ContaminationModel.EffectiveMassKg(2f, CellPhase.Gas), 0.001f, "2 kg 气体按 100 kg");
            TestRunner.CheckClose(0f, ContaminationModel.EffectiveMassKg(0f, CellPhase.Vacuum), 0.001f, "真空不污染");
            TestRunner.CheckClose(0f, ContaminationModel.EffectiveMassKg(5f, CellPhase.Vacuum), 0.001f, "真空格即使有质量记录也不污染");

            // 1 吨水在 6000 rads/周期下，每 5 秒添加 0.035 × 1 × 5500 × 5 ≈ 963 个。
            TestRunner.CheckEqual(963, ContaminationModel.ContaminantsToAdd(1000f, 5500f, 5f), "1 吨水每 5 秒约 963 个");
            TestRunner.CheckEqual(0, ContaminationModel.ContaminantsToAdd(1000f, 0f, 5f), "无超额不添加");
            TestRunner.CheckEqual(0, ContaminationModel.ContaminantsToAdd(0f, 5500f, 5f), "无质量不添加");
            TestRunner.CheckEqual(RadiationBalance.MaxCellContaminants,
                ContaminationModel.ContaminantsToAdd(1e9f, 1e9f, 5f), "添加量不超过单格上限");

            // 稳态：添加速率 = 衰减速率（原版污染物半衰期 1 周期）。
            double lambda = Math.Log(2.0) / 600.0;
            double steady = 0.035 * 1.0 * 5500.0 / lambda;
            TestRunner.Check(steady > 160000 && steady < 170000, "1 吨水贴反应堆稳态约 16.5 万个（约 165 rads/周期）");

            TestRunner.CheckClose(165f, ContaminationModel.SelfRads(7, 165000, 7), 0.001f, "16.5 万个污染物自身 165 rads/周期");
            TestRunner.CheckClose(0f, ContaminationModel.SelfRads(3, 165000, 7), 0.001f, "其他病菌不计入自身放射");
            TestRunner.CheckClose(0f, ContaminationModel.SelfRads(ContaminationModel.NoDisease, 0, 7), 0.001f, "无病菌不计入");
        }

        private static void ContaminantPriority()
        {
            TestRunner.Section("放射性污染物优先");
            const byte ours = 7;
            const byte foodPoisoning = 2;
            byte none = ContaminationModel.NoDisease;

            ContaminationPlan empty = ContaminationModel.Plan(none, 0, ours, 500);
            TestRunner.CheckEqual(none, empty.ClearIdx, "空格不清除");
            TestRunner.CheckEqual(500, empty.AddCount, "空格直接添加");

            ContaminationPlan same = ContaminationModel.Plan(ours, 1000, ours, 500);
            TestRunner.CheckEqual(none, same.ClearIdx, "已有污染物不清除");
            TestRunner.CheckEqual(500, same.AddCount, "已有污染物继续累加");

            ContaminationPlan other = ContaminationModel.Plan(foodPoisoning, 1000000, ours, 500);
            TestRunner.CheckEqual(foodPoisoning, other.ClearIdx, "其他病菌先清除");
            TestRunner.CheckEqual(1000000, other.ClearCount, "清除全部其他病菌");
            TestRunner.CheckEqual(500, other.AddCount, "清除后写入污染物");

            ContaminationPlan nothing = ContaminationModel.Plan(foodPoisoning, 1000, ours, 0);
            TestRunner.Check(!nothing.HasWork, "没有可添加量时不动作");
            TestRunner.CheckEqual(none, nothing.ClearIdx, "没有可添加量时不清除其他病菌");

            ContaminationPlan unknown = ContaminationModel.Plan(none, 0, none, 500);
            TestRunner.Check(!unknown.HasWork, "找不到污染物病菌时不动作");

            ContaminationPlan full = ContaminationModel.Plan(ours, RadiationBalance.MaxCellContaminants, ours, 500);
            TestRunner.Check(!full.HasWork, "达到单格上限后不再添加");
            ContaminationPlan nearFull = ContaminationModel.Plan(ours, RadiationBalance.MaxCellContaminants - 100, ours, 500);
            TestRunner.CheckEqual(100, nearFull.AddCount, "接近上限时只补到上限");
        }

        private static void SourceIndex()
        {
            TestRunner.Section("活化放射源位置索引");
            const int width = 100;
            InducedSourceIndex index = new InducedSourceIndex();
            int center = 50 * width + 50;
            index.Add(center, 100f);
            index.Add(center + 2, 50f);
            index.Add(center + 4 * width, 30f);
            index.Add(center + 5, 999f);
            TestRunner.CheckClose(180f, index.UpperBound(center, width, 4), 0.001f, "半径 4 内三个源相加");
            TestRunner.CheckClose(150f, index.UpperBound(center, width, 2), 0.001f, "半径 2 只含两个源");
            TestRunner.CheckClose(1179f, index.UpperBound(center, width, 5), 0.001f, "半径 5 含全部");

            index.Add(center, -100f);
            TestRunner.CheckClose(0f, index.At(center), 0.001f, "减为 0 后移除");
            TestRunner.CheckEqual(3, index.Count, "移除后剩 3 格");

            // 不跨越世界左右边界：最右一列不应算到下一行最左一列。
            InducedSourceIndex edge = new InducedSourceIndex();
            edge.Add(10 * width, 40f);
            TestRunner.CheckClose(0f, edge.UpperBound(9 * width + width - 1, width, 1), 0.001f, "不跨左右边界");

            // 遍历分支与逐格分支结果一致。
            InducedSourceIndex dense = new InducedSourceIndex();
            for (int i = 0; i < 200; i++) dense.Add(i * 37 % 5000, 1f + i % 7);
            float total = 0f;
            int c = 25 * width + 25;
            for (int dy = -3; dy <= 3; dy++)
                for (int dx = -3; dx <= 3; dx++)
                    total += dense.At(c + dy * width + dx);
            TestRunner.CheckClose(total, dense.UpperBound(c, width, 3), 0.001f, "大量源时逐格求和一致");
        }

        private static void ProfileIsolation()
        {
            TestRunner.Section("辐射并入整合版，其他独立版不含辐射（源码结构）");
            string root = FindRepoRoot();
            TestRunner.Check(root != null, "能找到仓库根目录");
            if (root == null) return;

            string csproj = File.ReadAllText(Path.Combine(root, "MaterialOverhaul", "MaterialOverhaul.csproj"));
            TestRunner.Check(csproj.Contains("MATERIAL_OVERHAUL_RADIATION_ONLY"), "定义辐射独立版编译符号");
            TestRunner.Check(!Regex.IsMatch(csproj, "SystemProfile\\)' == 'Combined'\">\\s*<Compile Remove="), "整合版不移除任何源码（含辐射）");
            foreach (string mode in new[] { "Power", "Industrial", "Room" })
            {
                Match group = Regex.Match(csproj, "SystemProfile\\)' == '" + mode + "'\">\\s*<Compile Remove=\"([^\"]*)\"");
                TestRunner.Check(group.Success && group.Groups[1].Value.Contains("Radiation\\**\\*.cs"), mode + " 独立版移除辐射判断层");
            }
            TestRunner.Check(csproj.Contains("<Version Condition=\"'$(SystemProfile)' == 'Radiation'\">0.0.1</Version>"), "辐射独立版版本 0.0.1");

            string profile = File.ReadAllText(Path.Combine(root, "MaterialOverhaul", "Core", "BuildProfile.cs"));
            int combined = profile.IndexOf("Name = \"Combined\"", StringComparison.Ordinal);
            TestRunner.Check(combined > 0 && profile.IndexOf("RadiationEnabled = true", combined, StringComparison.Ordinal) > 0,
                "整合版辐射开关为 true");
            int radiation = profile.IndexOf("Name = \"Radiation\"", StringComparison.Ordinal);
            TestRunner.CheckEqual(2, Regex.Matches(profile, "RadiationEnabled = true").Count, "只有辐射独立版与整合版开启辐射");
            TestRunner.Check(radiation > 0, "存在辐射独立版构建身份");

            string script = File.ReadAllText(Path.Combine(root, "MaterialOverhaul", "tools", "build-profiles.ps1"));
            TestRunner.Check(script.Contains("[string[]]$Profile = @('Industrial', 'Power', 'Room', 'Radiation', 'Combined')"),
                "默认构建四个独立版与整合版");
            TestRunner.Check(script.Contains("if ($mode -eq 'Combined' -or"), "整合包本地化包含全部键（含辐射）");
            TestRunner.Check(script.Contains("($mode -eq 'Industrial' -and !$isPower -and !$isRoom -and !$isRadiation)"), "工业包本地化不含辐射键");

            string entry = File.ReadAllText(Path.Combine(root, "MaterialOverhaul", "Game", "MaterialOverhaulMod.cs"));
            TestRunner.Check(entry.Contains("#if !MATERIAL_OVERHAUL_POWER_ONLY && !MATERIAL_OVERHAUL_INDUSTRIAL_ONLY && !MATERIAL_OVERHAUL_ROOM_ONLY\n            LocString.CreateLocStringKeys(typeof(Game.RadiationStrings.RADIATION)")
                || entry.Contains("#if !MATERIAL_OVERHAUL_POWER_ONLY && !MATERIAL_OVERHAUL_INDUSTRIAL_ONLY && !MATERIAL_OVERHAUL_ROOM_ONLY\r\n            LocString.CreateLocStringKeys(typeof(Game.RadiationStrings.RADIATION)"),
                "整合版与辐射版都登记辐射文本键");

            string patches = File.ReadAllText(Path.Combine(root, "MaterialOverhaul", "Game", "Patches", "RadiationPatches.cs"));
            TestRunner.CheckEqual(4, Regex.Matches(patches, "\\[HarmonyPatch\\(").Count, "辐射独立版共 4 个补丁");
            TestRunner.Check(patches.Contains("typeof(BuildingLoader), \"CreateBuildingComplete\""), "活化组件挂在完成建筑上");

            string monitor = File.ReadAllText(Path.Combine(root, "MaterialOverhaul", "Game", "RadiationInducedMonitor.cs"));
            TestRunner.Check(monitor.Contains("Sim.IsRadiationEnabled()"), "未开启辐射时不运行");
            int clear = monitor.IndexOf("ModifyDiseaseOnCell(cell, plan.ClearIdx, -plan.ClearCount)", StringComparison.Ordinal);
            int add = monitor.IndexOf("ModifyDiseaseOnCell(cell, contaminantIdx, plan.AddCount)", StringComparison.Ordinal);
            TestRunner.Check(clear > 0 && add > clear, "先清除其他病菌，再写入污染物");
            TestRunner.Check(monitor.Contains("if (!Grid.Foundation[cell])"), "砖块格子不重复添加污染物");
        }

        private static string FindRepoRoot()
        {
            DirectoryInfo dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "MaterialOverhaul", "MaterialOverhaul.csproj")))
                    return dir.FullName;
                dir = dir.Parent;
            }
            return null;
        }
    }
}
