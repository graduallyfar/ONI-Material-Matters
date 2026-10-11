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
