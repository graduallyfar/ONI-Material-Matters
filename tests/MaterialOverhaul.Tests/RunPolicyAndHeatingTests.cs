using MaterialOverhaul.Heating;
using MaterialOverhaul.Zones;

namespace MaterialOverhaul.Tests
{
    public static class RunPolicyAndHeatingTests
    {
        // 5 ℃ / 35 ℃ / 55 ℃ / 75 ℃；T_work,start = 281.15 K，T_best,start = 310.15 K
        private static readonly TempZone Zone = new TempZone(278.15f, 308.15f, 328.15f, 348.15f);

        private const float Tol = 0.0001f;

        public static void Run()
        {
            DefaultModeDecisions();
            ControlModeDecisions();
            ForcedDecisions();
            HeatingIntents();
        }

        private static void DefaultModeDecisions()
        {
            TestRunner.Section("默认模式生产许可");
            ProductionDecision below = TempZoneEvaluator.Decide(275f, Zone, RunPolicy.DefaultMode);
            TestRunner.Check(!below.AllowsProduction, "低于最低可工作温度不允许生产");
            TestRunner.CheckClose(TempZoneEvaluator.LowBoundaryEfficiency, below.Efficiency, Tol, "越低温保持低温侧边界效率");

            ProductionDecision low = TempZoneEvaluator.Decide(290f, Zone, RunPolicy.DefaultMode);
            TestRunner.Check(low.AllowsProduction, "低温段允许生产");
            TestRunner.Check(low.Efficiency > TempZoneEvaluator.LowBoundaryEfficiency && low.Efficiency < 1f, "低温段效率下降");

            ProductionDecision normal = TempZoneEvaluator.Decide(318f, Zone, RunPolicy.DefaultMode);
            TestRunner.Check(normal.AllowsProduction, "最佳温区允许生产");
            TestRunner.CheckClose(1f, normal.Efficiency, Tol, "最佳温区效率 100%");

            ProductionDecision high = TempZoneEvaluator.Decide(340f, Zone, RunPolicy.DefaultMode);
            TestRunner.Check(high.AllowsProduction, "高温段允许生产");

            ProductionDecision above = TempZoneEvaluator.Decide(350f, Zone, RunPolicy.DefaultMode);
            TestRunner.Check(!above.AllowsProduction, "高于最高可工作温度不允许生产");
            TestRunner.CheckClose(TempZoneEvaluator.HighBoundaryEfficiency, above.Efficiency, Tol, "越高温保持高温侧边界效率");
        }

        private static void ControlModeDecisions()
        {
            TestRunner.Section("控制模式生产许可");
            TestRunner.Check(!TempZoneEvaluator.Decide(290f, Zone, RunPolicy.ControlModePolicy).AllowsProduction, "低温段不允许生产");
            TestRunner.Check(TempZoneEvaluator.Decide(318f, Zone, RunPolicy.ControlModePolicy).AllowsProduction, "最佳温区允许生产");
            TestRunner.Check(!TempZoneEvaluator.Decide(340f, Zone, RunPolicy.ControlModePolicy).AllowsProduction, "高温段不允许生产");
            TestRunner.Check(!TempZoneEvaluator.Decide(350f, Zone, RunPolicy.ControlModePolicy).AllowsProduction, "高于最高可工作温度不允许生产");
        }

        private static void ForcedDecisions()
        {
            TestRunner.Section("强制运行");
            RunPolicy forced = RunPolicy.ControlModePolicy.WithForced(true);
            TestRunner.Check(forced.Forced && forced.ControlMode, "强制开关独立于模式");

            ProductionDecision cold = TempZoneEvaluator.Decide(275f, Zone, forced);
            TestRunner.Check(cold.AllowsProduction, "强制运行越过低温边界仍生产");
            TestRunner.CheckClose(TempZoneEvaluator.LowBoundaryEfficiency, cold.Efficiency, Tol, "越界沿用低温侧边界效率，不归零");

            ProductionDecision hot = TempZoneEvaluator.Decide(350f, Zone, forced);
            TestRunner.Check(hot.AllowsProduction, "强制运行越过高温边界仍生产");
            TestRunner.CheckClose(TempZoneEvaluator.HighBoundaryEfficiency, hot.Efficiency, Tol, "越界保持高温侧边界效率，不取两侧最低值");

            TestRunner.Check(TempZoneEvaluator.Decide(290f, Zone, forced).AllowsProduction, "强制运行下低温段仍生产");
            TestRunner.Check(TempZoneEvaluator.Decide(340f, Zone, forced).AllowsProduction, "强制运行下高温段仍生产");

            ProductionDecision passthrough = TempZoneEvaluator.Decide(500f, TempZone.Disabled, RunPolicy.DefaultMode);
            TestRunner.Check(passthrough.AllowsProduction, "未配置温区时判断恒通过");
            TestRunner.CheckClose(1f, passthrough.Efficiency, Tol, "未配置温区时效率恒为 1");
            TestRunner.Check(!passthrough.ZoneConfigured, "未配置温区标记为未匹配");
        }

        private static void HeatingIntents()
        {
            TestRunner.Section("预热与补热意图");

            TestRunner.CheckEqual(HeatingIntent.Preheat,
                HeatingPlanner.Decide(280f, Zone, RunPolicy.DefaultMode, false), "默认模式未开工且低于启动阈值 → 预热");
            TestRunner.CheckEqual(HeatingIntent.None,
                HeatingPlanner.Decide(282f, Zone, RunPolicy.DefaultMode, false), "达到启动阈值 → 不加热");
            TestRunner.CheckEqual(HeatingIntent.None,
                HeatingPlanner.Decide(279f, Zone, RunPolicy.DefaultMode, true), "默认模式开工后不补热");

            TestRunner.CheckEqual(HeatingIntent.Preheat,
                HeatingPlanner.Decide(309f, Zone, RunPolicy.ControlModePolicy, false), "控制模式预热到最佳启动阈值");
            TestRunner.CheckEqual(HeatingIntent.TopUp,
                HeatingPlanner.Decide(309f, Zone, RunPolicy.ControlModePolicy, true), "控制模式生产中低于补热线 → 补热");
            TestRunner.CheckEqual(HeatingIntent.None,
                HeatingPlanner.Decide(311f, Zone, RunPolicy.ControlModePolicy, true), "达到补热停止线 → 停止补热");

            TestRunner.CheckEqual(HeatingIntent.None,
                HeatingPlanner.Decide(350f, Zone, RunPolicy.DefaultMode, false), "超过最高可工作温度 → 停止内置加热");
            TestRunner.CheckEqual(HeatingIntent.Sustained,
                HeatingPlanner.Decide(350f, Zone, RunPolicy.DefaultMode.WithForced(true), false), "强制运行 → 持续加热");
            TestRunner.CheckEqual(HeatingIntent.Sustained,
                HeatingPlanner.Decide(318f, Zone, RunPolicy.ControlModePolicy.WithForced(true), true), "强制运行在最佳温区内仍持续加热");

            TestRunner.CheckEqual(HeatingIntent.None,
                HeatingPlanner.Decide(318f, TempZone.Disabled, RunPolicy.DefaultMode, false), "未配置温区不加热");

            TestRunner.CheckClose(StartThreshold.Control(Zone), HeatingPlanner.TopUpLine(Zone, RunPolicy.ControlModePolicy), Tol, "补热线当前取预热停止线");
            TestRunner.CheckClose(HeatingPlanner.TopUpLine(Zone, RunPolicy.ControlModePolicy),
                HeatingPlanner.TopUpStopLine(Zone, RunPolicy.ControlModePolicy), Tol, "补热启停线当前同点（待确认的文档冲突）");
        }
    }
}
