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
            TestRunner.Section("启动阈值");
            TempZone zone = new TempZone(278.15f, 308.15f, 328.15f, 348.15f);
            TestRunner.CheckClose(281.15f, StartThreshold.Default(zone), Tol, "默认模式 = WorkMin + 10% 的低温段宽度");
            TestRunner.CheckClose(310.15f, StartThreshold.Control(zone), Tol, "控制模式 = BestMin + 10% 的最佳温区宽度");
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
            TestRunner.Section("加热功率映射（v0.0.2 测试值）");
            TestRunner.CheckClose(1.2f, HeatingPower.KilowattsFor(HeatingIntent.Preheat), Tol, "预热功率 1.2 kW（用户给定 1200 W 按 1:1 换算）");
            TestRunner.CheckClose(0.2f, HeatingPower.KilowattsFor(HeatingIntent.TopUp), Tol, "补热功率 0.2 kW（设计 200 W 按 1:1 换算）");
            TestRunner.CheckClose(1.2f, HeatingPower.KilowattsFor(HeatingIntent.Sustained), Tol, "持续加热沿用预热值");
            TestRunner.CheckClose(0f, HeatingPower.KilowattsFor(HeatingIntent.None), Tol, "None 意图不注热");
            TestRunner.Check(HeatingPower.IsConfigured, "默认已配置测试值");

            HeatingPower.PreheatKilowatts = 12f;
            HeatingPower.TopUpKilowatts = 0f;
            TestRunner.CheckClose(12f, HeatingPower.KilowattsFor(HeatingIntent.Preheat), Tol, "改写后取新值");
            TestRunner.CheckClose(0f, HeatingPower.KilowattsFor(HeatingIntent.TopUp), Tol, "改写为 0 的项不注热");

            // 复位为测试值，避免影响其他用例。
            HeatingPower.PreheatKilowatts = 1.2f;
            HeatingPower.TopUpKilowatts = 0.2f;
            HeatingPower.SustainedKilowatts = 1.2f;
        }

        private static void Wear()
        {
            TestRunner.Section("磨损累计");
            WearAccumulator wear = new WearAccumulator();
            TestRunner.CheckClose(0f, wear.Wear, Tol, "初始为零");

            // 只有实际生产推进才调用；预热、待机、暂停、维修都不调用。
            wear.AccumulateProduction(0f, 0.5f);
            TestRunner.CheckClose(0f, wear.Wear, Tol, "零生产时间不累计");
            wear.AccumulateProduction(10f, 0f);
            TestRunner.CheckClose(0f, wear.Wear, Tol, "零速率不累计");
            wear.AccumulateProduction(-1f, 1f);
            TestRunner.CheckClose(0f, wear.Wear, Tol, "负时间不累计");

            wear.AccumulateProduction(1f, 1.5f);
            TestRunner.CheckClose(1.5f, wear.Wear, Tol, "按生产时间累计");
            TestRunner.CheckEqual(1, wear.TakeWholeWear(), "取走已成形部分");
            TestRunner.CheckClose(0.5f, wear.Wear, Tol, "保留尚未成形的累计部分");

            wear.AccumulateProduction(0.25f, 1f);
            TestRunner.CheckClose(0.75f, wear.Wear, Tol, "小数部分继续累加");
            TestRunner.CheckEqual(0, wear.TakeWholeWear(), "不足 1 时不产生伤害单位");

            wear.Restore(2.5f);
            TestRunner.CheckClose(2.5f, wear.Wear, Tol, "读档恢复累计值");
            wear.Restore(-5f);
            TestRunner.CheckClose(0f, wear.Wear, Tol, "负值恢复被钳制为 0");
        }
    }
}
