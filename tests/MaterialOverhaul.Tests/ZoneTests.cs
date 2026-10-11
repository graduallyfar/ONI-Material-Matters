using MaterialOverhaul.Core;
using MaterialOverhaul.Zones;

namespace MaterialOverhaul.Tests
{
    public static class ZoneTests
    {
        // 5 ℃ / 35 ℃ / 55 ℃ / 75 ℃
        private static readonly TempZone Zone = new TempZone(278.15f, 308.15f, 328.15f, 348.15f);

        private const float Tol = 0.0001f;

        public static void Run()
        {
            TestRunner.Section("五态边界（区间开闭）");
            TestRunner.CheckEqual(TempState.BelowWorkMin, TempZoneEvaluator.Evaluate(278.14f, Zone), "低于最低可工作温度 → 停止");
            TestRunner.CheckEqual(TempState.LowEfficiency, TempZoneEvaluator.Evaluate(278.15f, Zone), "等于最低可工作温度 → 低温低效（左闭）");
            TestRunner.CheckEqual(TempState.LowEfficiency, TempZoneEvaluator.Evaluate(308.14f, Zone), "略低于最佳下限 → 低温低效");
            TestRunner.CheckEqual(TempState.Normal, TempZoneEvaluator.Evaluate(308.15f, Zone), "等于最佳下限 → 正常（左闭）");
            TestRunner.CheckEqual(TempState.Normal, TempZoneEvaluator.Evaluate(328.15f, Zone), "等于最佳上限 → 正常（右闭）");
            TestRunner.CheckEqual(TempState.HighEfficiency, TempZoneEvaluator.Evaluate(328.16f, Zone), "略高于最佳上限 → 高温低效");
            TestRunner.CheckEqual(TempState.HighEfficiency, TempZoneEvaluator.Evaluate(348.15f, Zone), "等于最高可工作温度 → 高温低效（右闭）");
            TestRunner.CheckEqual(TempState.AboveWorkMax, TempZoneEvaluator.Evaluate(348.16f, Zone), "高于最高可工作温度 → 停止");

            TestRunner.Section("允许生产 / 停止加热");
            TestRunner.Check(TempState.Normal.AllowsProduction(), "正常允许生产");
            TestRunner.Check(TempState.LowEfficiency.AllowsProduction(), "低温低效允许生产");
            TestRunner.Check(TempState.HighEfficiency.AllowsProduction(), "高温低效允许生产");
            TestRunner.Check(!TempState.BelowWorkMin.AllowsProduction(), "低于最低可工作温度停止生产");
            TestRunner.Check(!TempState.AboveWorkMax.AllowsProduction(), "高于最高可工作温度停止生产");
            TestRunner.Check(TempState.AboveWorkMax.StopsHeating(), "高温保护停止内置加热");
            TestRunner.Check(!TempState.BelowWorkMin.StopsHeating(), "低温侧不停止加热");

            TestRunner.Section("温度效率");
            TestRunner.CheckClose(1f, TempZoneEvaluator.Efficiency(TempState.Normal, 318.15f, Zone), Tol, "最佳温区效率 100%");
            TestRunner.CheckClose(TempZoneEvaluator.LowBoundaryEfficiency, TempZoneEvaluator.Efficiency(278.15f, Zone), Tol, "最低可工作温度处取低温边界效率");
            TestRunner.CheckClose(TempZoneEvaluator.HighBoundaryEfficiency, TempZoneEvaluator.Efficiency(348.15f, Zone), Tol, "最高可工作温度处取高温边界效率");
            TestRunner.CheckClose(TempZoneEvaluator.LowBoundaryEfficiency, TempZoneEvaluator.Efficiency(200f, Zone), Tol, "越低温不继续下降、不归零");
            TestRunner.CheckClose(TempZoneEvaluator.HighBoundaryEfficiency, TempZoneEvaluator.Efficiency(500f, Zone), Tol, "越高温保持高温侧边界效率");

            float lowMid = TempZoneEvaluator.Efficiency(293.15f, Zone);
            TestRunner.Check(lowMid > TempZoneEvaluator.LowBoundaryEfficiency && lowMid < 1f, "低温段效率介于边界值与 1 之间");

            float highMid = TempZoneEvaluator.Efficiency(338.15f, Zone);
            TestRunner.Check(highMid < 1f && highMid > TempZoneEvaluator.HighBoundaryEfficiency, "高温段效率介于 1 与边界值之间");

            float lowNear = TempZoneEvaluator.Efficiency(305f, Zone);
            TestRunner.Check(lowNear > lowMid, "低温段随温度上升递增");

            float highNear = TempZoneEvaluator.Efficiency(331f, Zone);
            TestRunner.Check(highNear > highMid, "高温段随温度上升递减");

            TestRunner.Section("退化温区");
            TestRunner.CheckEqual(TempState.Disabled, TempZoneEvaluator.Evaluate(100f, TempZone.Disabled), "退化温区判断恒通过");
            TestRunner.CheckClose(1f, TempZoneEvaluator.Efficiency(TempState.Disabled, 100f, TempZone.Disabled), Tol, "退化温区效率恒为 1");

            TestRunner.Section("边界校验");
            ModLog.Clear();
            TempZone crossed = TempZoneEvaluator.Validate("test.crossed", new TempZone(348.15f, 328.15f, 308.15f, 278.15f));
            TestRunner.Check(crossed.IsDisabled, "边界交叉退化为 Disabled");
            TestRunner.Check(ModLog.CountOf("ERROR") == 1, "边界交叉记录错误日志");

            TempZone nan = TempZoneEvaluator.Validate("test.nan", new TempZone(float.NaN, 308.15f, 328.15f, 348.15f));
            TestRunner.Check(nan.IsDisabled, "含非有限值退化为 Disabled");

            ModLog.Clear();
            TempZone equal = TempZoneEvaluator.Validate("test.equal", new TempZone(300f, 300f, 300f, 300f));
            TestRunner.Check(!equal.IsDisabled && equal.IsOrdered, "边界相等视为合法");
            TestRunner.Check(ModLog.CountOf("WARN") == 1, "边界相等记录提示日志");

            TempZone ok = TempZoneEvaluator.Validate("test.ok", Zone);
            TestRunner.CheckClose(Zone.WorkMin, ok.WorkMin, Tol, "合法温区原样保留");
        }
    }
}
