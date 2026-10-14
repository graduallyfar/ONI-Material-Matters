using MaterialOverhaul.Hardness;
using MaterialOverhaul.Heating;
using MaterialOverhaul.Zones;

namespace MaterialOverhaul.Tests
{
    public static class HeatingAndWearTests
    {
        private const float Tol = 0.0001f;

        public static void Run()
        {
            StartThresholds();
            HeatingGate();
            HeatingPowerMapping();
            Wear();
        }

        private static void StartThresholds()
        {
            TestRunner.Section("启动阈值（V0.1.2：原目标公式不变，来源改为建筑下限）");
            ResolvedZone zone = RunPolicyAndHeatingTests.Resolve(5f, 35f, 55f, 75f);
            TestRunner.CheckClose(278.15f + 0.1f * 30f, StartThreshold.OriginalDefault(zone), Tol,
                "默认原目标 = L + 10% 的低温段宽度");
            TestRunner.CheckClose(308.15f + 0.1f * 20f, StartThreshold.OriginalControl(zone), Tol,
                "控制原目标 = B₁ + 10% 的最佳温区宽度");
            TestRunner.CheckClose(281.15f, StartThreshold.Start(zone, RunPolicy.DefaultMode), Tol,
                "上限高于原目标时不封顶");
            TestRunner.CheckClose(310.15f, StartThreshold.Start(zone, RunPolicy.ControlModePolicy), Tol,
                "控制模式实际目标等于原目标");
        }

        private static void HeatingGate()
        {
            TestRunner.Section("加热条件");
            HeatingContext ok = new HeatingContext
            {
                HasLockedBatch = true,
                BatchMaterialsReady = true,
                Powered = true,
                ManuallyDisabled = false,
                BrokenDown = false
            };
            TestRunner.Check(HeatingConditions.CanHeat(ok), "条件齐备允许加热");

            HeatingContext noBatch = ok;
            noBatch.HasLockedBatch = false;
            TestRunner.Check(!HeatingConditions.CanHeat(noBatch), "无有效批次不加热（预告不算）");

            HeatingContext unpowered = ok;
            unpowered.Powered = false;
            TestRunner.Check(!HeatingConditions.CanHeat(unpowered), "断电不加热");

            HeatingContext disabled = ok;
            disabled.ManuallyDisabled = true;
            TestRunner.Check(!HeatingConditions.CanHeat(disabled), "手动禁用不加热");

            HeatingContext broken = ok;
            broken.BrokenDown = true;
            TestRunner.Check(!HeatingConditions.CanHeat(broken), "损坏停机不加热");

            HeatingContext notReady = ok;
            notReady.BatchMaterialsReady = false;
            TestRunner.Check(!HeatingConditions.CanHeat(notReady), "材料未齐备不加热");
        }

        private static void HeatingPowerMapping()
        {
            TestRunner.Section("加热功率映射（v0.1.1 共用滑条，暂定倍率 1000）");
            TestRunner.CheckClose(1000f, HeatingPower.TestHeatMultiplier, Tol, "测试倍率为 1000");
            TestRunner.CheckClose(1200f, HeatingPower.KilowattsFor(HeatingIntent.Preheat), Tol, "预热 1200 W 用电 × 1000 = 1200 kW");
            TestRunner.CheckClose(1200f, HeatingPower.KilowattsFor(HeatingIntent.TopUp), Tol, "补热沿用共用滑条默认 1200 W");
            TestRunner.CheckClose(1200f, HeatingPower.KilowattsFor(HeatingIntent.Sustained), Tol, "持续加热沿用预热值");
            TestRunner.CheckClose(0f, HeatingPower.KilowattsFor(HeatingIntent.None), Tol, "None 意图不注热");
            TestRunner.CheckClose(20f, HeatingPower.KilowattsFor(HeatingIntent.Preheat, 20f), Tol, "最低功率对应注热");
            TestRunner.CheckClose(4000f, HeatingPower.KilowattsFor(HeatingIntent.TopUp, 4000f), Tol, "最高功率对应补热注热");
        }

        private static void Wear()
        {
            TestRunner.Section("磨损与伤害余额（v0.0.3：0.05 HP／磨损单位）");
            WearAccumulator wear = new WearAccumulator();
            TestRunner.CheckClose(0f, wear.DamageBalance, Tol, "初始为零");

            // 只有实际生产推进才调用；预热、待机、暂停、维修都不调用。
            DamageConversion.AccumulateProduction(wear, 0f);
            TestRunner.CheckClose(0f, wear.DamageBalance, Tol, "零生产时间不累计");
            DamageConversion.AccumulateProduction(wear, -1f);
            TestRunner.CheckClose(0f, wear.DamageBalance, Tol, "负时间不累计");

            // 20 s 实际生产 = 1 HP。
            DamageConversion.AccumulateProduction(wear, 19f);
            TestRunner.CheckEqual(0, wear.TakeWholeDamage(), "19 s 不扣整点生命");
            TestRunner.CheckClose(0.95f, wear.DamageBalance, Tol, "余额为 0.95 HP");
            DamageConversion.AccumulateProduction(wear, 1f);
            TestRunner.CheckEqual(1, wear.TakeWholeDamage(), "再 1 s 扣 1 HP");
            TestRunner.CheckClose(0f, wear.DamageBalance, Tol, "扣血后余额归零");

            wear.Restore(0f);
            TestRunner.CheckClose(0f, wear.DamageBalance, Tol, "读档恢复前先归零");
            wear.Restore(2.5f);
            TestRunner.CheckClose(2.5f, wear.DamageBalance, Tol, "读档恢复伤害余额");
            TestRunner.CheckEqual(2, wear.TakeWholeDamage(), "恢复后取走整点");
            TestRunner.CheckClose(0.5f, wear.DamageBalance, Tol, "小数余额保留");
            wear.Restore(-5f);
            TestRunner.CheckClose(0f, wear.DamageBalance, Tol, "负值恢复被钳制为 0");
        }
    }
}
