using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using MaterialOverhaul.Game;
using MaterialOverhaul.Radiation;

namespace MaterialOverhaul.Tests
{
    /// <summary>
    /// 辐射「累积 → 显现」：纯计算、每格累积表、粒子与发生器结算、复制人三条剂量途径与接入结构。
    /// 只验证公式、规则和源码结构；原生 Sim 的辐射、病菌行为、存档读写与界面需游戏内验收。
    /// </summary>
    public static class RadiationTests
    {
        public static void Run()
        {
            Parameters();
            Materials();
            AccumulationSteps();
            Manifestation();
            SelfExclusionAndChains();
            Contamination();
            ContaminantPriority();
            Table();
            Particles();
            GeneratorDrain();
            Dose();
            Decommission();
            Localization();
            SourceStructure();
            ProfileIsolation();
        }

        private static void Parameters()
        {
            TestRunner.Section("累积与显现参数（工作初值）");
            TestRunner.CheckClose(10f, RadiationBalance.AccumulationHalfLifeCycles, 0.0001f, "累积量半衰期 10 周期");
            TestRunner.CheckClose(1f, RadiationBalance.EmitMinRads, 0.0001f, "自身放射不到 1 rads/周期 忽略不计");
            TestRunner.CheckEqual(2, RadiationBalance.EmitRadiusCells, "自身放射半径 2 格");
            TestRunner.CheckClose(3000f, RadiationBalance.FluidLimitRads, 0.0001f, "液体、气体显现限度 3000");
            TestRunner.CheckClose(0.035f, RadiationBalance.ContaminantsPerTonRadSecond, 0.00001f, "污染物系数 0.035");
            TestRunner.CheckClose(50f, RadiationBalance.GasMassMultiplier, 0.0001f, "气体按质量 50 倍计算");
            TestRunner.CheckClose(10f, RadiationBalance.RadsPerParticle, 0.0001f, "1 点粒子 = 10 rads");
            TestRunner.CheckClose(0.06f, RadiationBalance.InhaleRadsPerThousand, 0.00001f, "吸入每 1000 个 0.06 rads");
            TestRunner.CheckClose(0.25f, RadiationBalance.ContactRadsPerThousandPerCycle, 0.00001f, "接触每 1000 个每周期 0.25 rads");
            TestRunner.CheckClose(10f, RadiationBalance.DecommissionMinRads, 0.0001f, "拆除转换下限 10 rads/周期");
            TestRunner.CheckEqual(25, RadiationBalance.ScanSlicesPerPass, "每 0.2 秒一份、每遍 25 份");
            TestRunner.CheckClose(RadiationBalance.ScanPassSeconds, RadiationBalance.ScanSlicesPerPass * 0.2f, 0.0001f, "扫描份数与 0.2 秒更新对应，五秒一遍");
            TestRunner.CheckClose(0.0693f, AccumulationModel.DecayPerCycle, 0.0001f, "λ = ln2/10 ≈ 0.069 每周期");
        }

        private static void Materials()
        {
            TestRunner.Section("材料显现限度与放射比例");
            CheckProfile("Steel", MaterialCategory.Metal, 2000f, 0.05f, "钢");
            CheckProfile("Iron", MaterialCategory.Metal, 2000f, 0.05f, "铁");
            CheckProfile("Cobalt", MaterialCategory.Metal, 2000f, 0.05f, "钴");
            CheckProfile("Copper", MaterialCategory.Metal, 2500f, 0.04f, "铜");
            CheckProfile("Gold", MaterialCategory.Metal, 2500f, 0.04f, "金");
            CheckProfile("Tungsten", MaterialCategory.Metal, 3000f, 0.03f, "未列出的金属");
            CheckProfile("IronOre", MaterialCategory.Metal, 3000f, 0.03f, "金属矿石");
            CheckProfile("Aluminum", MaterialCategory.Metal, 4000f, 0.025f, "铝");
            CheckProfile("Granite", MaterialCategory.Mineral, 5000f, 0.015f, "矿物、岩石");
            CheckProfile("Glass", MaterialCategory.Mineral, 5000f, 0.015f, "玻璃");
            CheckProfile("Polypropylene", MaterialCategory.Organic, 6000f, 0.005f, "塑料");
            CheckProfile("WoodLog", MaterialCategory.Organic, 6000f, 0.005f, "木材");
            CheckProfile("Lead", MaterialCategory.Metal, 10000f, 0.01f, "铅");
            CheckProfile("DepletedUranium", MaterialCategory.Metal, 10000f, 0.01f, "贫铀");
            CheckProfile("Water", MaterialCategory.Fluid, 3000f, RadiationBalance.CellDrainFraction, "液体");
            CheckProfile("Steel", MaterialCategory.Fluid, 3000f, RadiationBalance.CellDrainFraction, "流体不套用固体元素表");
            CheckProfile(null, MaterialCategory.Mineral, 5000f, 0.015f, "缺元素按分类默认");
            TestRunner.CheckEqual(8, ManifestMaterial.ListedCount, "单独列出 8 种元素");
            TestRunner.Check(ManifestMaterial.For("Lead", MaterialCategory.Metal).Limit
                > ManifestMaterial.For("Steel", MaterialCategory.Metal).Limit, "铅比钢难显现");
        }

        private static void CheckProfile(string id, MaterialCategory category, float limit, float fraction, string name)
        {
            ManifestProfile profile = ManifestMaterial.For(id, category);
            TestRunner.CheckClose(limit, profile.Limit, 0.001f, name + "显现限度 " + limit);
            TestRunner.CheckClose(fraction, profile.Fraction, 0.00001f, name + "放射比例 " + fraction);
        }

        private static void AccumulationSteps()
        {
            TestRunner.Section("累积量步进：再弱的照射也会累积，按半衰期衰减");
            float lambda = AccumulationModel.DefaultDecayPerSecond;
            TestRunner.CheckClose((float)(Math.Log(2.0) / 6000.0), lambda, 1e-9f, "半衰期 10 周期 = 6000 s");
            TestRunner.CheckClose(6000f / 0.0693147f, AccumulationModel.SteadyState(6000f), 5f, "稳态 = R/λ ≈ 14.4 R");
            TestRunner.CheckClose(0f, AccumulationModel.SteadyState(0f), 0.0001f, "无照射稳态为 0");

            float halfLife = 6000f;
            float steady = AccumulationModel.SteadyState(200f);
            TestRunner.CheckClose(steady / 2f, AccumulationModel.Step(0f, 200f, halfLife), 0.5f, "10 周期达到稳态一半");
            TestRunner.CheckClose(steady * 0.75f, AccumulationModel.Step(0f, 200f, 2f * halfLife), 0.5f, "20 周期达到 75%");
            TestRunner.Check(AccumulationModel.Step(0f, 0.5f, 5f) > 0f, "0.5 rads/周期 也会累积");

            float stepped = 0f;
            for (int i = 0; i < 20; i++) stepped = AccumulationModel.Step(stepped, 6000f, 5f);
            TestRunner.CheckClose(AccumulationModel.Step(0f, 6000f, 100f), stepped, 0.01f, "积分与更新间隔无关");

            TestRunner.CheckClose(5000f, AccumulationModel.Step(10000f, 0f, halfLife), 0.5f, "离开辐射后 10 周期减半");
            TestRunner.CheckClose(300f, AccumulationModel.Step(300f, 0f, 0f), 0.0001f, "dt 为 0 不变");
            TestRunner.CheckClose(0f, AccumulationModel.Step(-5f, 0f, 5f), 0.0001f, "负累积量按 0 处理");
            TestRunner.CheckClose(0f, AccumulationModel.Step(float.NaN, 0f, 5f), 0.0001f, "无效累积量按 0 处理");
            TestRunner.CheckClose(RadiationBalance.MaxAccumulationRads, AccumulationModel.Step(0f, 1e9f, 1e6f), 0.5f, "累积量有数值上限");
            TestRunner.Check(AccumulationModel.CanDrop(0.5f, 0f), "未受照且累积量很低时清零");
            TestRunner.Check(!AccumulationModel.CanDrop(0.5f, 1f), "仍在受照不清零");
            TestRunner.Check(!AccumulationModel.CanDrop(50f, 0f), "仍有累积量不清零");
        }

        private static void Manifestation()
        {
            TestRunner.Section("显现：超过限度后放射，强度随超出部分增长");
            TestRunner.CheckClose(0f, AccumulationModel.EmitRads(2000f, 2000f, 0.05f), 0.0001f, "恰在限度不放射");
            TestRunner.CheckClose(0.05f * 0.0693147f * 3000f, AccumulationModel.EmitRads(5000f, 2000f, 0.05f), 0.01f, "E = f·λ·(D − L)");
            TestRunner.CheckClose(0f, AccumulationModel.EmitRads(2100f, 2000f, 0.05f), 0.0001f, "不到 1 rads/周期 忽略不计");
            TestRunner.Check(AccumulationModel.EmitRads(2300f, 2000f, 0.05f) >= 1f, "超过 1 rads/周期 开始放射");
            TestRunner.CheckClose(0f, AccumulationModel.EmitRads(1e5f, 2000f, 0f), 0.0001f, "放射比例为 0 不放射");
            TestRunner.CheckClose(0.0693147f * 3000f, AccumulationModel.ExcessRate(5000f, 2000f), 0.01f, "超出部分的衰减速率 λ·(D − L)");

            TestRunner.CheckClose(139f, AccumulationModel.MinSustainedRads(2000f), 0.5f, "钢的最低持续照射 139");
            TestRunner.CheckClose(347f, AccumulationModel.MinSustainedRads(5000f), 0.5f, "矿物的最低持续照射 347");
            TestRunner.CheckClose(693f, AccumulationModel.MinSustainedRads(10000f), 0.5f, "铅的最低持续照射 693");

            // 文档表：贴着研究反应堆（6000）与太空辐射最强处（375）的稳态自身放射。
            float[] reactor = { 293f, 233f, 174f, 143f, 85f, 28f, 53f };
            float[] space = { 12f, 8f, 5f, 2f, 0.4f, 0f, 0f };
            ManifestProfile[] rows =
            {
                ManifestMaterial.For("Steel", MaterialCategory.Metal), ManifestMaterial.For("Copper", MaterialCategory.Metal),
                ManifestMaterial.MetalDefault, ManifestMaterial.For("Aluminum", MaterialCategory.Metal),
                ManifestMaterial.MineralDefault, ManifestMaterial.OrganicDefault, ManifestMaterial.For("Lead", MaterialCategory.Metal)
            };
            string[] names = { "钢", "铜", "其他金属", "铝", "矿物", "塑料", "铅" };
            for (int i = 0; i < rows.Length; i++)
            {
                TestRunner.CheckClose(reactor[i], AccumulationModel.SteadyEmitRads(6000f, rows[i].Limit, rows[i].Fraction), 0.6f, names[i] + "贴反应堆稳态 " + reactor[i]);
                TestRunner.CheckClose(space[i], AccumulationModel.SteadyEmitRads(375f, rows[i].Limit, rows[i].Fraction), 0.5f, names[i] + "太空辐射稳态约 " + space[i]);
            }

            float steadyD = AccumulationModel.SteadyState(375f);
            TestRunner.CheckClose(0f, AccumulationModel.EmitRads(steadyD, 5000f, 0.015f), 0.0001f, "地表岩石建筑放射不到 1，忽略");
            TestRunner.CheckClose(0f, AccumulationModel.EmitRads(steadyD, 6000f, 0.005f), 0.0001f, "地表塑料建筑不显现");

            // 贴着研究反应堆的钢墙约三分之一个周期显现；铀矿旁 200 rads/周期 的钢墙约 17 个周期显现，稳态约 3 rads/周期。
            TestRunner.CheckClose(0.34f, CyclesToReach(6000f, 2000f), 0.02f, "贴反应堆的钢墙约 1/3 周期显现");
            TestRunner.CheckClose(17f, CyclesToReach(200f, 2000f), 0.2f, "铀矿旁 200 rads/周期 的钢墙约 17 周期显现");
            TestRunner.CheckClose(3f, AccumulationModel.SteadyEmitRads(200f, 2000f, 0.05f), 0.1f, "铀矿旁钢墙稳态约 3 rads/周期");
            TestRunner.Check(float.IsInfinity(CyclesToReach(100f, 2000f)), "低于最低持续照射永远不显现");

            // 旧存档的活化度（自身放射）折算成累积量后，自身放射不变。
            float migrated = AccumulationModel.AccumulationForEmission(275f, 2000f, 0.05f);
            TestRunner.CheckClose(275f, AccumulationModel.EmitRads(migrated, 2000f, 0.05f), 0.05f, "旧存档活化度 275 折算后仍放射 275");
            TestRunner.CheckClose(0f, AccumulationModel.AccumulationForEmission(0f, 2000f, 0.05f), 0.0001f, "旧存档无活化度时为 0");
        }

        private static float CyclesToReach(float rads, float limit)
        {
            float steady = AccumulationModel.SteadyState(rads);
            if (!(steady > limit))
            {
                return float.PositiveInfinity;
            }

            return (float)(-Math.Log(1.0 - limit / steady) / AccumulationModel.DecayPerCycle);
        }

        private static void SelfExclusionAndChains()
        {
            TestRunner.Section("自己不累积自己；显现物之间的链条一级比一级弱，拆掉来源后全部衰减");
            // A 贴着反应堆，B 只受 A 照射（按 A 的全部放射计，上界）；A 也受 B 照射。
            float a = 0f, b = 0f;
            ManifestProfile steel = ManifestMaterial.For("Steel", MaterialCategory.Metal);
            for (int i = 0; i < 2000; i++)
            {
                float ea = AccumulationModel.EmitRads(a, steel.Limit, steel.Fraction);
                float eb = AccumulationModel.EmitRads(b, steel.Limit, steel.Fraction);
                a = AccumulationModel.Step(a, 6000f + eb, 60f);
                b = AccumulationModel.Step(b, ea, 60f);
            }

            float emitA = AccumulationModel.EmitRads(a, steel.Limit, steel.Fraction);
            float emitB = AccumulationModel.EmitRads(b, steel.Limit, steel.Fraction);
            TestRunner.Check(emitA > 280f && emitA < 310f, "第一级约 293 rads/周期");
            TestRunner.Check(emitB > 0f && emitB < emitA * 0.06f, "第二级只有第一级的约 5%");

            for (int i = 0; i < 20000; i++)
            {
                float ea = AccumulationModel.EmitRads(a, steel.Limit, steel.Fraction);
                float eb = AccumulationModel.EmitRads(b, steel.Limit, steel.Fraction);
                a = AccumulationModel.Step(a, eb, 60f);
                b = AccumulationModel.Step(b, ea, 60f);
            }

            TestRunner.CheckClose(0f, AccumulationModel.EmitRads(a, steel.Limit, steel.Fraction), 0.0001f, "拆掉反应堆后 A 不再放射");
            TestRunner.CheckClose(0f, AccumulationModel.EmitRads(b, steel.Limit, steel.Fraction), 0.0001f, "拆掉反应堆后 B 不再放射");
            TestRunner.Check(a < 1f && b < 1f, "累积量最终衰减殆尽");
        }

        private static void Contamination()
        {
            TestRunner.Section("格子、管道、物品显现后写入污染物");
            TestRunner.CheckClose(1000f, ContaminationModel.EffectiveMassKg(1000f, CellPhase.Liquid), 0.001f, "液体按真实质量");
            TestRunner.CheckClose(100f, ContaminationModel.EffectiveMassKg(2f, CellPhase.Gas), 0.001f, "2 kg 气体按 100 kg");
            TestRunner.CheckClose(0f, ContaminationModel.EffectiveMassKg(5f, CellPhase.Vacuum), 0.001f, "真空不污染");

            // 1 吨水贴反应堆：稳态 D ≈ 86580，超出限度部分的衰减速率 ≈ 5793 rads/周期，自身放射约 170。
            float d = AccumulationModel.SteadyState(6000f);
            float excess = AccumulationModel.ExcessRate(d, RadiationBalance.FluidLimitRads);
            TestRunner.CheckClose(5792f, excess, 2f, "1 吨水贴反应堆的超出速率约 5793");
            TestRunner.CheckClose(175f, ContaminationModel.SteadySelfRads(1000f, excess), 3f, "1 吨水贴反应堆稳态自身放射约 170");
            TestRunner.CheckEqual(1014, ContaminationModel.ContaminantsToAdd(1000f, excess, 5f), "每 5 秒约添加 1014 个");
            TestRunner.CheckEqual(0, ContaminationModel.ContaminantsToAdd(1000f, 0f, 5f), "未超出限度不添加");
            TestRunner.CheckEqual(0, ContaminationModel.ContaminantsToAdd(0f, 5000f, 5f), "无质量不添加");
            TestRunner.CheckEqual(RadiationBalance.MaxCellContaminants, ContaminationModel.ContaminantsToAdd(1e9f, 1e9f, 5f), "添加量不超过单格上限");

            // 太空辐射下 1 吨水：稳态 D ≈ 5410，超出速率约 167，自身放射约 5 rads/周期；气体格放射不到 1 时忽略。
            float spaceExcess = AccumulationModel.ExcessRate(AccumulationModel.SteadyState(375f), RadiationBalance.FluidLimitRads);
            TestRunner.Check(ContaminationModel.SteadySelfRads(1000f, spaceExcess) >= 1f, "太空辐射下的静止水池会显现");
            TestRunner.Check(ContaminationModel.SteadySelfRads(ContaminationModel.EffectiveMassKg(1.8f, CellPhase.Gas), spaceExcess) < 1f,
                "太空辐射下的氧气格放射不到 1，忽略");

            TestRunner.CheckClose(165f, ContaminationModel.SelfRads(7, 165000, 7), 0.001f, "16.5 万个污染物自身 165 rads/周期");
            TestRunner.CheckClose(0f, ContaminationModel.SelfRads(3, 165000, 7), 0.001f, "其他病菌不计入自身放射");

            TestRunner.Check(ContaminationModel.CanContaminate(0f), "不放射的元素可以累积");
            TestRunner.Check(!ContaminationModel.CanContaminate(250f), "原版放射物质本身就是来源，不累积");
            TestRunner.Check(ContaminationModel.CanContaminate(float.NaN), "无效放射值按不放射处理");
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

        private static void Table()
        {
            TestRunner.Section("每格累积表：四份累积量、物质一换就清零、存档往返");
            AccumulationTable table = new AccumulationTable(1000);
            const int water = 101;
            const int oil = 202;
            table.Set(10, AccumulationLayer.Cell, water, 500f);
            TestRunner.CheckClose(500f, table.Reconcile(10, AccumulationLayer.Cell, water), 0.001f, "同一物质保留累积量");
            TestRunner.CheckClose(0f, table.Reconcile(10, AccumulationLayer.Cell, oil), 0.001f, "物质被替换后清零");
            TestRunner.CheckEqual(oil, table.IdentityOf(10, AccumulationLayer.Cell), "清零后记下新物质");
            table.Set(10, AccumulationLayer.Cell, oil, 300f);
            TestRunner.CheckClose(0f, table.Reconcile(10, AccumulationLayer.Cell, 0), 0.001f, "变成真空后清零");
            TestRunner.CheckEqual(0, table.IdentityOf(10, AccumulationLayer.Cell), "真空不记物质");

            table.Set(20, AccumulationLayer.LiquidPipe, water, 100f);
            table.Set(20, AccumulationLayer.GasPipe, oil, 200f);
            table.Set(20, AccumulationLayer.Items, 7, 300f);
            TestRunner.CheckClose(100f, table.Get(20, AccumulationLayer.LiquidPipe), 0.001f, "液体管一份");
            TestRunner.CheckClose(200f, table.Get(20, AccumulationLayer.GasPipe), 0.001f, "气体管一份");
            TestRunner.CheckClose(300f, table.Get(20, AccumulationLayer.Items), 0.001f, "地上物品一份");
            TestRunner.CheckClose(0f, table.Get(20, AccumulationLayer.Cell), 0.001f, "四份互不影响");
            TestRunner.Check(table.HasAny(20) && !table.HasAny(21), "按格判断有无累积量");

            TestRunner.CheckClose(5000f, table.Add(30, AccumulationLayer.Cell, water, 5000f), 0.001f, "粒子撞击一次性存入");
            TestRunner.CheckClose(5000f, table.Add(30, AccumulationLayer.Cell, oil, 5000f), 0.001f, "存入时物质不同先清零");
            TestRunner.CheckClose(0f, table.Add(-1, AccumulationLayer.Cell, water, 10f), 0.001f, "越界格子忽略");
            table.Set(40, AccumulationLayer.Cell, water, 0.5f);

            int[] keys;
            float[] values;
            int[] ids;
            table.Pack(RadiationBalance.SaveMinRads, out keys, out values, out ids);
            TestRunner.CheckEqual(4, keys.Length, "存档只写出不低于 1 rads 的份");
            TestRunner.CheckEqual(keys.Length, values.Length, "存档数组长度一致");
            TestRunner.Check(keys.Contains(20 * 4 + 1) && keys.Contains(20 * 4 + 3), "存档键 = 格子 × 4 + 层");

            AccumulationTable loaded = new AccumulationTable(1000);
            TestRunner.CheckEqual(4, loaded.Unpack(keys, values, ids), "读档写回全部条目");
            TestRunner.CheckClose(200f, loaded.Get(20, AccumulationLayer.GasPipe), 0.001f, "读档后累积量一致");
            TestRunner.CheckEqual(oil, loaded.IdentityOf(30, AccumulationLayer.Cell), "读档后物质身份一致");

            AccumulationTable small = new AccumulationTable(25);
            TestRunner.CheckEqual(3, small.Unpack(keys, values, ids), "超出格子范围的条目跳过");
            TestRunner.CheckEqual(0, small.Unpack(new[] { 4, -8 }, new[] { -1f }, new[] { 1, 1 }), "长度不一致时只读共同部分、跳过无效值");
            TestRunner.CheckEqual(0, small.Unpack(null, null, null), "没有存档数据时不读");

            TestRunner.CheckEqual(0, AccumulationTable.KindIdentity(new List<int>()), "没有物品时身份为 0");
            int ab = AccumulationTable.KindIdentity(new List<int> { 11, 22 });
            TestRunner.CheckEqual(ab, AccumulationTable.KindIdentity(new List<int> { 22, 11 }), "物品身份与顺序无关");
            TestRunner.Check(ab != AccumulationTable.KindIdentity(new List<int> { 11 }), "物品种类变了身份就变");
            TestRunner.Check(AccumulationTable.ElementIdentity(0) != 0, "元素哈希为 0 时身份不为 0");
        }

        private static void Particles()
        {
            TestRunner.Section("辐射粒子：只在撞到固体时结算，1 点 = 10 rads");
            TestRunner.CheckClose(5000f, AccumulationModel.ParticleRads(500f), 0.001f, "500 点粒子存入 5000 rads");
            TestRunner.CheckClose(0f, AccumulationModel.ParticleRads(-5f), 0.001f, "无效能量不存入");
            float d = AccumulationModel.Deposit(0f, AccumulationModel.ParticleRads(500f));
            float emit = AccumulationModel.EmitRads(d, 2000f, 0.05f);
            TestRunner.CheckClose(10.4f, emit, 0.1f, "500 点粒子打在钢墙上，钢墙立刻显现，约 10 rads/周期");
            TestRunner.CheckClose(RadiationBalance.MaxAccumulationRads, AccumulationModel.Deposit(RadiationBalance.MaxAccumulationRads, 5000f), 0.5f, "存入不超过上限");
        }

        private static void GeneratorDrain()
        {
            TestRunner.Section("辐射粒子发生器消耗辐射：由周围显现物按份额付账");
            // 原版每 0.2 秒采样：每秒速率 = R/600，存入 R/600 × 0.2 × 0.1 点粒子，即 10 rads = 1 点。
            float perSecond = 600f / 600f;
            TestRunner.CheckClose(0.2f, GeneratorDrainModel.AbsorbedRads(perSecond, 0.2f, 1000f), 0.0001f, "600 rads/周期 下每次采样吸收 0.2 rads");
            TestRunner.CheckClose(0.1f, GeneratorDrainModel.AbsorbedRads(perSecond, 0.2f, 0.01f), 0.0001f, "粒子仓快满时按实际存入量记账");
            TestRunner.CheckClose(0f, GeneratorDrainModel.AbsorbedRads(perSecond, 0.2f, 0f), 0.0001f, "粒子仓已满不吸收");

            TestRunner.CheckClose(90f, GeneratorDrainModel.EstimatedContribution(90f, 0f, 2), 0.001f, "同格按全部放射估计");
            TestRunner.CheckClose(60f, GeneratorDrainModel.EstimatedContribution(90f, 1f, 2), 0.001f, "1 格外按 2/3");
            TestRunner.CheckClose(30f, GeneratorDrainModel.EstimatedContribution(90f, 2f, 2), 0.001f, "2 格外按 1/3");
            TestRunner.CheckClose(0f, GeneratorDrainModel.EstimatedContribution(90f, 3f, 2), 0.001f, "放射半径外不付账");

            TestRunner.CheckClose(10f, GeneratorDrainModel.Charge(10f, 60f, 60f, 60f), 0.001f, "唯一来源付全部");
            TestRunner.CheckClose(5f, GeneratorDrainModel.Charge(10f, 60f, 60f, 120f), 0.001f, "估不到来源的一半不扣");
            TestRunner.CheckClose(2.5f, GeneratorDrainModel.Charge(10f, 30f, 120f, 60f), 0.001f, "估计之和大于实测时按估计分摊");
            TestRunner.CheckClose(0f, GeneratorDrainModel.Charge(10f, 0f, 60f, 60f), 0.001f, "不放射的不付账");

            TestRunner.CheckClose(1000f, AccumulationModel.Drain(2000f, 50f, 0.05f), 0.001f, "累积量按「吸收量 ÷ f」扣");
            TestRunner.CheckClose(0f, AccumulationModel.Drain(100f, 50f, 0.05f), 0.001f, "扣到 0 为止");

            // 发生器贴着一面显现的钢墙（1 格外）：墙比自然衰减快约 1.6 倍变干净。
            ManifestProfile steel = ManifestMaterial.For("Steel", MaterialCategory.Metal);
            float wall = 20000f;
            float emitRads = AccumulationModel.EmitRads(wall, steel.Limit, steel.Fraction);
            float atGenerator = GeneratorDrainModel.EstimatedContribution(emitRads, 1f, RadiationBalance.EmitRadiusCells);
            float absorbedPerSecond = GeneratorDrainModel.AbsorbedRads(atGenerator / 600f, 1f, 1000f);
            float charge = GeneratorDrainModel.Charge(absorbedPerSecond, atGenerator, atGenerator, atGenerator);
            double natural = wall * (1.0 - Math.Exp(-AccumulationModel.DefaultDecayPerSecond));
            double drained = charge / steel.Fraction;
            double ratio = (natural + drained) / natural;
            TestRunner.Check(ratio > 1.5 && ratio < 1.7, "贴着发生器的显现钢墙约快 1.6 倍变干净（实际 " + ratio.ToString("0.00") + "）");
        }

        private static void Dose()
        {
            TestRunner.Section("复制人：吸入 > 照射 > 接触");
            // 一格 1.8 kg 氧气含 1 万个污染物，待一个周期约吸入 33 万个。
            int inhaled = (int)(60f / 1.8f * 10000f);
            TestRunner.CheckClose(20f, DoseModel.InhaledRads(inhaled), 0.1f, "吸入约 20 rads");
            TestRunner.CheckClose(10f, DoseModel.ExposureRadsPerCycle(10000), 0.001f, "同一格照射约 10 rads/周期");
            TestRunner.CheckClose(2.5f, DoseModel.ContactRads(10000, 600f), 0.001f, "身上 1 万个污染物，接触每周期 2.5 rads");
            TestRunner.CheckClose(DoseModel.ExposureRadsPerCycle(10000) / 4f, DoseModel.ContactRads(10000, 600f), 0.001f, "接触约为照射的四分之一");
            TestRunner.Check(DoseModel.InhaledRads(inhaled) > DoseModel.ExposureRadsPerCycle(10000), "吸入累积最快");
            TestRunner.CheckClose(0f, DoseModel.ContactRads(0, 1f), 0.0001f, "身上没有污染物不累积");
            TestRunner.CheckClose(0f, DoseModel.InhaledRads(-5), 0.0001f, "无效个数不累积");
        }

        private static void Decommission()
        {
            TestRunner.Section("拆除显现建筑转成核废料（规则不变）");
            TestRunner.CheckClose(0f, DecommissionModel.WasteFraction(5f), 0.0001f, "自身放射低于 10 不转换");
            TestRunner.CheckClose(0f, DecommissionModel.WasteFraction(float.NaN), 0.0001f, "无效值不转换");
            TestRunner.CheckClose(0.01f, DecommissionModel.WasteFraction(10f), 0.00001f, "10 rads 转 1%");
            TestRunner.CheckClose(0.275f, DecommissionModel.WasteFraction(275f), 0.00001f, "275 rads 转 27.5%");
            TestRunner.CheckClose(0.5f, DecommissionModel.WasteFraction(800f), 0.00001f, "转换比例最多 50%");
            TestRunner.CheckClose(27.5f, DecommissionModel.WasteMass(100f, 275f), 0.0001f, "100 kg 主材在 275 rads 时转出 27.5 kg");
            TestRunner.CheckClose(0f, DecommissionModel.WasteMass(0f, 275f), 0.0001f, "没有主材不产生核废料");
            TestRunner.CheckEqual(275000, DecommissionModel.WasteContaminants(275f), "污染物按每 1000 个 = 1 rads/周期");
            TestRunner.CheckEqual(0, DecommissionModel.WasteContaminants(5f), "不转换时不带污染物");
            TestRunner.CheckEqual(RadiationBalance.MaxCellContaminants, DecommissionModel.WasteContaminants(1e9f), "污染物不超过单格上限");
        }

        private static void Localization()
        {
            TestRunner.Section("辐射文本：字段树键与中文 PO 一致");
            const string prefix = "STRINGS.RADIATION.MATERIALOVERHAUL.";
            LocString.CreateLocStringKeys(typeof(RadiationStrings.RADIATION), "STRINGS.");
            Dictionary<string, string> keys = Strings.Values
                .Where(pair => pair.Key.StartsWith(prefix, StringComparison.Ordinal))
                .ToDictionary(pair => pair.Key, pair => pair.Value);
            TestRunner.CheckEqual(4, keys.Count, "辐射字段树登记 4 条");

            string root = FindRepoRoot();
            if (root == null)
            {
                return;
            }

            Dictionary<string, string> po = ReadPo(Path.Combine(root, "MaterialOverhaul", "mod", "strings", "zh.po"))
                .Where(pair => pair.Key.StartsWith(prefix, StringComparison.Ordinal))
                .ToDictionary(pair => pair.Key, pair => pair.Value);
            TestRunner.Check(keys.Keys.OrderBy(k => k).SequenceEqual(po.Keys.OrderBy(k => k)), "辐射字段树键与 PO 键集合一致");
            foreach (KeyValuePair<string, string> entry in keys)
            {
                string translated;
                if (po.TryGetValue(entry.Key, out translated))
                {
                    TestRunner.CheckEqual(entry.Value, translated, entry.Key + " 中文 PO 与字段默认文本一致");
                }
            }
        }

        private static void SourceStructure()
        {
            TestRunner.Section("接入结构（源码）");
            string root = FindRepoRoot();
            TestRunner.Check(root != null, "能找到仓库根目录");
            if (root == null) return;

            string game = Path.Combine(root, "MaterialOverhaul", "Game");
            string monitor = File.ReadAllText(Path.Combine(game, "RadiationInducedMonitor.cs"));
            TestRunner.Check(monitor.Contains("Sim.IsRadiationEnabled()"), "未开启辐射时不运行");
            int clear = monitor.IndexOf("ModifyDiseaseOnCell(cell, plan.ClearIdx, -plan.ClearCount)", StringComparison.Ordinal);
            int add = monitor.IndexOf("ModifyDiseaseOnCell(cell, contaminantIdx, plan.AddCount)", StringComparison.Ordinal);
            TestRunner.Check(clear > 0 && add > clear, "格子先清除其他病菌，再写入污染物");
            int itemClear = monitor.IndexOf("primary.ModifyDiseaseCount(-plan.ClearCount", StringComparison.Ordinal);
            int itemAdd = monitor.IndexOf("primary.AddDisease(contaminantIdx, plan.AddCount", StringComparison.Ordinal);
            TestRunner.Check(itemClear > 0 && itemAdd > itemClear, "物品先清除其他病菌，再写入污染物");
            TestRunner.Check(monitor.Contains("if (Grid.Foundation[cell])"), "砖块格子由砖块自己累积");
            TestRunner.Check(monitor.Contains("rads - ContaminationModel.SelfRads(idx, count, contaminantIdx)"), "格子物质扣掉自身污染物的放射");
            TestRunner.Check(monitor.Contains("rads - activation.CurrentEmitRads"), "建筑扣掉自身放射");
            TestRunner.Check(monitor.Contains("flow.SetContents(cell, contents)"), "管道显现后写回管道内容");
            TestRunner.Check(monitor.Contains("id.HasTag(GameTags.Creature)"), "动物不累积");
            TestRunner.Check(monitor.Contains("GameTags.HasSuitTank") && monitor.Contains("GameTags.HasAirtightSuit"), "穿防护服时不计接触剂量");
            TestRunner.Check(monitor.Contains("Db.Get().Amounts.RadiationBalance.Lookup(minion)"), "剂量写入原版辐射剂量");
            TestRunner.Check(monitor.Contains("ContaminationModel.SteadySelfRads(effective, excess) < RadiationBalance.EmitMinRads"),
                "格子物质放射不到 1 rads/周期 时不写入");

            string materials = File.ReadAllText(Path.Combine(game, "RadiationMaterials.cs"));
            TestRunner.Check(materials.Contains("ContaminationModel.CanContaminate(element.radiationPer1000Mass)"), "原版放射物质不累积");

            string store = File.ReadAllText(Path.Combine(game, "RadiationAccumulationStore.cs"));
            TestRunner.Check(store.Contains("[OnSerializing]") && store.Contains("RadiationInducedMonitor.TryPack"), "存档时写出累积表");
            TestRunner.Check(Regex.IsMatch(store, @"OnSerializing\(\)\s*\{\s*if \(!handedOver\)\s*\{\s*return;"), "交出之前存档原样写回");
            TestRunner.Check(store.Contains("ISim200ms") && store.Contains("RadiationInducedMonitor.Tick(dt)"), "每 0.2 秒驱动扫描");

            string activation = File.ReadAllText(Path.Combine(game, "RadiationActivation.cs"));
            TestRunner.Check(activation.Contains("[Serialize] private float accumulation;"), "建筑累积量随存档保存");
            TestRunner.Check(activation.Contains("AccumulationModel.AccumulationForEmission(activation, Limit, Fraction)"), "旧存档活化度折算成累积量");

            string patches = File.ReadAllText(Path.Combine(game, "Patches", "RadiationPatches.cs"));
            TestRunner.CheckEqual(9, Regex.Matches(patches, "\\[HarmonyPatch\\(").Count, "辐射共 9 个补丁");
            TestRunner.Check(patches.Contains("typeof(BuildingLoader), \"CreateBuildingComplete\""), "累积组件挂在完成建筑上");
            TestRunner.Check(patches.Contains("typeof(SaveGame), \"OnPrefabInit\""), "累积表组件挂在 SaveGame 上");
            TestRunner.Check(!patches.Contains("RoomProber"), "不再借用房间探测的 1 秒更新");
            TestRunner.Check(patches.Contains("collisionType != HighEnergyParticle.CollisionType.Solid"), "粒子只在撞到固体时结算");
            TestRunner.Check(patches.Contains("nameof(HighEnergyParticleSpawner.LauncherUpdate)") && patches.Contains("___particleStorage.RemainingCapacity()"),
                "发生器按实际存入量记账");
            TestRunner.Check(patches.Contains("typeof(GermExposureMonitor.Instance), nameof(GermExposureMonitor.Instance.OnAirConsumed)")
                && patches.Contains("byte disseaseIDX, int disseaseCount"), "吸入挂在原版呼吸回调，参数名与原版一致");
            TestRunner.Check(patches.Contains("new[] { typeof(float), typeof(byte), typeof(int), typeof(WorkerBase) }"), "拆除结算挂在原版私有生成建材方法上");
            TestRunner.Check(patches.Contains("ref float src_mass"), "从主建材里扣出核废料质量");
            TestRunner.Check(patches.Contains("SimHashes.NuclearWaste.CreateTag()"), "扣出的部分以原版核废料掉落");
            TestRunner.Check(patches.Contains("DecommissionModel.WasteFraction(activation.CurrentEmitRads)"), "拆除按当前自身放射结算");
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

            string gameFiles = "Game\\RadiationStrings.cs;Game\\RadiationStatusItems.cs;Game\\RadiationActivation.cs;Game\\RadiationMaterials.cs;Game\\RadiationAccumulationStore.cs;Game\\RadiationInducedMonitor.cs;Game\\Patches\\RadiationPatches.cs";
            TestRunner.CheckEqual(2, Regex.Matches(csproj, Regex.Escape(gameFiles)).Count, "工业版移除、辐射版编入同一组 7 个辐射接入文件");
            foreach (string file in Directory.GetFiles(Path.Combine(root, "MaterialOverhaul", "Game"), "Radiation*.cs"))
            {
                TestRunner.Check(gameFiles.Contains("Game\\" + Path.GetFileName(file)), Path.GetFileName(file) + " 已列入独立版文件清单");
            }

            TestRunner.Check(csproj.Contains("<Version Condition=\"'$(SystemProfile)' == 'Radiation'\">0.0.1</Version>"), "辐射独立版版本 0.0.1");

            string profile = File.ReadAllText(Path.Combine(root, "MaterialOverhaul", "Core", "BuildProfile.cs"));
            int combined = profile.IndexOf("Name = \"Combined\"", StringComparison.Ordinal);
            TestRunner.Check(combined > 0 && profile.IndexOf("RadiationEnabled = true", combined, StringComparison.Ordinal) > 0,
                "整合版辐射开关为 true");
            TestRunner.CheckEqual(2, Regex.Matches(profile, "RadiationEnabled = true").Count, "只有辐射独立版与整合版开启辐射");
            TestRunner.Check(profile.IndexOf("Name = \"Radiation\"", StringComparison.Ordinal) > 0, "存在辐射独立版构建身份");

            string script = File.ReadAllText(Path.Combine(root, "MaterialOverhaul", "tools", "build-profiles.ps1"));
            TestRunner.Check(script.Contains("[string[]]$Profile = @('Industrial', 'Power', 'Room', 'Radiation', 'Combined')"),
                "默认构建四个独立版与整合版");
            TestRunner.Check(script.Contains("if ($mode -eq 'Combined' -or"), "整合包本地化包含全部键（含辐射）");
            TestRunner.Check(script.Contains("($mode -eq 'Industrial' -and !$isPower -and !$isRoom -and !$isRadiation -and !$isOptions)"), "工业包本地化不含辐射键");

            string entry = File.ReadAllText(Path.Combine(root, "MaterialOverhaul", "Game", "MaterialOverhaulMod.cs"));
            TestRunner.Check(entry.Contains("#if !MATERIAL_OVERHAUL_POWER_ONLY && !MATERIAL_OVERHAUL_INDUSTRIAL_ONLY && !MATERIAL_OVERHAUL_ROOM_ONLY\n            LocString.CreateLocStringKeys(typeof(Game.RadiationStrings.RADIATION)")
                || entry.Contains("#if !MATERIAL_OVERHAUL_POWER_ONLY && !MATERIAL_OVERHAUL_INDUSTRIAL_ONLY && !MATERIAL_OVERHAUL_ROOM_ONLY\r\n            LocString.CreateLocStringKeys(typeof(Game.RadiationStrings.RADIATION)"),
                "整合版与辐射版都登记辐射文本键");
        }

        private static Dictionary<string, string> ReadPo(string path)
        {
            Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.Ordinal);
            string key = null;
            foreach (string line in File.ReadAllLines(path))
            {
                Match context = Regex.Match(line, "^msgctxt \"(.*)\"$");
                if (context.Success)
                {
                    key = context.Groups[1].Value;
                    continue;
                }

                Match str = Regex.Match(line, "^msgstr \"(.*)\"$");
                if (str.Success && key != null)
                {
                    values[key] = str.Groups[1].Value.Replace("\\n", "\n").Replace("\\\"", "\"");
                    key = null;
                }
            }

            return values;
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
