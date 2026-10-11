using MaterialOverhaul.Core;
using MaterialOverhaul.Heating;
using MaterialOverhaul.Zones;

namespace MaterialOverhaul.Tests
{
    /// <summary>
    /// 温区许可、效率与加热意图（V0.0.3 开发计划 §2.2、§3.2）。
    /// 使用铝配方四边界：300／450／650／800 ℃。
    /// </summary>
    public static class RunPolicyAndHeatingTests
    {
        public static readonly TempZone Aluminum = new TempZone(
            Temperatures.FromCelsius(300f),
            Temperatures.FromCelsius(450f),
            Temperatures.FromCelsius(650f),
            Temperatures.FromCelsius(800f));

        private const float Tol = 0.0001f;

        public static void Run()
        {
            ZoneBoundaries();
            DefaultModeDecisions();
            ControlModeDecisions();
            ForcedDecisions();
            HeatingIntents();
            ExponentialCurve();
        }

        private static float C(float celsius)
        {
            return Temperatures.FromCelsius(celsius);
        }

        private static void ZoneBoundaries()
        {
            TestRunner.Section("铝配方四边界");
            TestRunner.CheckClose(C(300f), Aluminum.WorkMin, Tol, "最低可工作温度 300 ℃");
            TestRunner.CheckClose(C(450f), Aluminum.BestMin, Tol, "最佳温区下限 450 ℃");
            TestRunner.CheckClose(C(650f), Aluminum.BestMax, Tol, "最佳温区上限 650 ℃");
            TestRunner.CheckClose(C(800f), Aluminum.WorkMax, Tol, "最高可工作温度 800 ℃");

            TestRunner.CheckEqual(TempState.BelowWorkMin, TempZoneEvaluator.Evaluate(C(299.9f), Aluminum), "低于 300 ℃ 停止");
            TestRunner.CheckEqual(TempState.LowEfficiency, TempZoneEvaluator.Evaluate(C(300f), Aluminum), "300 ℃ 低温低效");
            TestRunner.CheckEqual(TempState.Normal, TempZoneEvaluator.Evaluate(C(450f), Aluminum), "450 ℃ 进入最佳温区");
            TestRunner.CheckEqual(TempState.Normal, TempZoneEvaluator.Evaluate(C(650f), Aluminum), "650 ℃ 仍在最佳温区");
            TestRunner.CheckEqual(TempState.HighEfficiency, TempZoneEvaluator.Evaluate(C(650.1f), Aluminum), "650 ℃ 以上高温低效");
            TestRunner.CheckEqual(TempState.AboveWorkMax, TempZoneEvaluator.Evaluate(C(800.1f), Aluminum), "800 ℃ 以上停止");
        }

        private static void ExponentialCurve()
        {
            TestRunner.Section("指数温度效率（§2.2）");
            TestRunner.CheckClose(0.3f, TempZoneEvaluator.Efficiency(C(300f), Aluminum), 0.00001f, "300 ℃ = 0.3");
            TestRunner.CheckClose(1f, TempZoneEvaluator.Efficiency(C(450f), Aluminum), 0.00001f, "450 ℃ = 1");
            TestRunner.CheckClose(1f, TempZoneEvaluator.Efficiency(C(650f), Aluminum), 0.00001f, "650 ℃ = 1");
            TestRunner.CheckClose(0.8f, TempZoneEvaluator.Efficiency(C(800f), Aluminum), 0.00001f, "800 ℃ = 0.8");
            TestRunner.CheckClose(0.5477226f, TempZoneEvaluator.Efficiency(C(375f), Aluminum), 0.00001f, "375 ℃ ≈ 0.547723");
            TestRunner.CheckClose(0.8944272f, TempZoneEvaluator.Efficiency(C(725f), Aluminum), 0.00001f, "725 ℃ ≈ 0.894427");
        }

        private static void DefaultModeDecisions()
        {
            TestRunner.Section("默认模式生产许可（未开工时看启动线，开工后看生产下限）");
            RunState started = new RunState { Started = true };

            ProductionDecision below = TempZoneEvaluator.Decide(C(290f), Aluminum, RunPolicy.DefaultMode);
            TestRunner.Check(!below.AllowsProduction, "低于最低可工作温度不允许生产");
            TestRunner.CheckClose(0.3f, below.Efficiency, Tol, "越低温保持低温侧边界效率，不归零");

            ProductionDecision low = TempZoneEvaluator.Decide(C(400f), Aluminum, RunPolicy.DefaultMode);
            TestRunner.Check(low.AllowsProduction, "低温段允许生产");
            TestRunner.Check(low.Efficiency > 0.3f && low.Efficiency < 1f, "低温段效率下降");

            TestRunner.Check(TempZoneEvaluator.Decide(C(550f), Aluminum, RunPolicy.DefaultMode).AllowsProduction, "最佳温区允许生产");
            TestRunner.CheckClose(1f, TempZoneEvaluator.Decide(C(550f), Aluminum, RunPolicy.DefaultMode).Efficiency, Tol, "最佳温区效率 100%");
            TestRunner.Check(TempZoneEvaluator.Decide(C(700f), Aluminum, RunPolicy.DefaultMode).AllowsProduction, "高温段允许生产");

            ProductionDecision above = TempZoneEvaluator.Decide(C(810f), Aluminum, RunPolicy.DefaultMode);
            TestRunner.Check(!above.AllowsProduction, "高于最高可工作温度不允许生产");
            TestRunner.CheckClose(0.8f, above.Efficiency, Tol, "越高温保持高温侧边界效率");

            // 启动线本身只影响状态机；这里顺带确认边界读数。
            TestRunner.CheckClose(C(315f), StartThreshold.Default(Aluminum), Tol, "默认低温启动线 315 ℃");
            TestRunner.CheckClose(C(470f), StartThreshold.Control(Aluminum), Tol, "控制低温启动线 470 ℃");
            TestRunner.CheckClose(C(800f), StartThreshold.HighPauseLine(Aluminum, RunPolicy.DefaultMode), Tol, "默认高温暂停线 800 ℃");
            TestRunner.CheckClose(C(785f), StartThreshold.HighResumeLine(Aluminum, RunPolicy.DefaultMode), Tol, "默认高温恢复线 785 ℃");

            RunState check = started;
            TestRunner.Check(check.Started, "默认模式已开工状态可携带");
        }

        private static void ControlModeDecisions()
        {
            TestRunner.Section("控制模式生产许可");
            TestRunner.Check(!TempZoneEvaluator.Decide(C(400f), Aluminum, RunPolicy.ControlModePolicy).AllowsProduction, "低温段不允许生产");
            TestRunner.Check(TempZoneEvaluator.Decide(C(550f), Aluminum, RunPolicy.ControlModePolicy).AllowsProduction, "最佳温区允许生产");
            TestRunner.Check(!TempZoneEvaluator.Decide(C(700f), Aluminum, RunPolicy.ControlModePolicy).AllowsProduction, "高温段不允许生产");
            TestRunner.Check(!TempZoneEvaluator.Decide(C(810f), Aluminum, RunPolicy.ControlModePolicy).AllowsProduction, "高于最高可工作温度不允许生产");

            TestRunner.CheckClose(C(650f), StartThreshold.HighPauseLine(Aluminum, RunPolicy.ControlModePolicy), Tol, "控制高温暂停线 650 ℃");
            TestRunner.CheckClose(C(640f), StartThreshold.HighResumeLine(Aluminum, RunPolicy.ControlModePolicy), Tol, "控制高温恢复线 640 ℃");
        }

        private static void ForcedDecisions()
        {
            TestRunner.Section("强制运行");
            RunPolicy forced = RunPolicy.ControlModePolicy.WithForced(true);
            TestRunner.Check(forced.Forced && forced.ControlMode, "强制开关独立于模式");

            ProductionDecision cold = TempZoneEvaluator.Decide(C(250f), Aluminum, forced);
            TestRunner.Check(cold.AllowsProduction, "强制运行越过低温边界仍生产");
            TestRunner.CheckClose(0.3f, cold.Efficiency, Tol, "越界沿用低温侧边界效率，不归零");

            ProductionDecision hot = TempZoneEvaluator.Decide(C(900f), Aluminum, forced);
            TestRunner.Check(hot.AllowsProduction, "强制运行越过高温边界仍生产");
            TestRunner.CheckClose(0.8f, hot.Efficiency, Tol, "越界保持高温侧边界效率，不取两侧最低值");

            ProductionDecision passthrough = TempZoneEvaluator.Decide(C(500f), TempZone.Disabled, RunPolicy.DefaultMode);
            TestRunner.Check(passthrough.AllowsProduction, "未配置温区时判断恒通过");
            TestRunner.CheckClose(1f, passthrough.Efficiency, Tol, "未配置温区时效率恒为 1");
            TestRunner.Check(!passthrough.ZoneConfigured, "未配置温区标记为未匹配");
        }

        private static void HeatingIntents()
        {
            TestRunner.Section("预热与补热意图");
            TestRunner.CheckClose(C(470f), HeatingPlanner.PreheatTarget(Aluminum, RunPolicy.ControlModePolicy), Tol, "控制模式预热到 470 ℃");
            TestRunner.CheckClose(C(460f), HeatingPlanner.TopUpLine(Aluminum, RunPolicy.ControlModePolicy), Tol, "控制补热启动线 460 ℃");
            TestRunner.CheckClose(C(470f), HeatingPlanner.TopUpStopLine(Aluminum, RunPolicy.ControlModePolicy), Tol, "控制补热停止线 470 ℃");

            TestRunner.CheckEqual(HeatingIntent.Preheat,
                HeatingPlanner.Decide(C(310f), Aluminum, RunPolicy.DefaultMode, false), "默认模式未开工且低于 315 ℃ → 预热");
            TestRunner.CheckEqual(HeatingIntent.None,
                HeatingPlanner.Decide(C(320f), Aluminum, RunPolicy.DefaultMode, false), "达到 315 ℃ → 不加热");
            TestRunner.CheckEqual(HeatingIntent.None,
                HeatingPlanner.Decide(C(305f), Aluminum, RunPolicy.DefaultMode, true, false), "默认模式开工后不补热");

            TestRunner.CheckEqual(HeatingIntent.Preheat,
                HeatingPlanner.Decide(C(465f), Aluminum, RunPolicy.ControlModePolicy, false), "控制模式预热到 470 ℃");
            TestRunner.CheckEqual(HeatingIntent.TopUp,
                HeatingPlanner.Decide(C(455f), Aluminum, RunPolicy.ControlModePolicy, true, false), "控制模式生产中低于 460 ℃ → 启动补热");
            TestRunner.CheckEqual(HeatingIntent.None,
                HeatingPlanner.Decide(C(465f), Aluminum, RunPolicy.ControlModePolicy, true, false), "回差区间内且未在补热 → 不启动补热");
            TestRunner.CheckEqual(HeatingIntent.TopUp,
                HeatingPlanner.Decide(C(465f), Aluminum, RunPolicy.ControlModePolicy, true, true), "回差区间内且正在补热 → 继续补热");
            TestRunner.CheckEqual(HeatingIntent.None,
                HeatingPlanner.Decide(C(471f), Aluminum, RunPolicy.ControlModePolicy, true, true), "达到补热停止线 → 停止补热");

            TestRunner.CheckEqual(HeatingIntent.None,
                HeatingPlanner.Decide(C(810f), Aluminum, RunPolicy.DefaultMode, false), "超过最高可工作温度 → 停止内置加热");
            TestRunner.CheckEqual(HeatingIntent.Sustained,
                HeatingPlanner.Decide(C(810f), Aluminum, RunPolicy.DefaultMode.WithForced(true), false), "强制运行 → 持续加热");
            TestRunner.CheckEqual(HeatingIntent.Sustained,
                HeatingPlanner.Decide(C(550f), Aluminum, RunPolicy.ControlModePolicy.WithForced(true), true), "强制运行在最佳温区内仍持续加热");
            TestRunner.CheckEqual(HeatingIntent.None,
                HeatingPlanner.Decide(C(550f), TempZone.Disabled, RunPolicy.DefaultMode, false), "未配置温区不加热");

            TestRunner.Check(HeatingPlanner.TopUpLine(Aluminum, RunPolicy.ControlModePolicy) < HeatingPlanner.TopUpStopLine(Aluminum, RunPolicy.ControlModePolicy), "补热停止线高于启动线（回差）");
            TestRunner.Check(HeatingPlanner.TopUpLine(Aluminum, RunPolicy.ControlModePolicy) > Aluminum.BestMin, "补热启动线仍位于最佳温区内部");
        }
    }
}
