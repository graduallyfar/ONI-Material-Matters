using System;
using MaterialOverhaul.Core;
using MaterialOverhaul.Heating;
using MaterialOverhaul.Zones;

namespace MaterialOverhaul.Tests
{
    /// <summary>
    /// v0.0.4 覆盖：本版把界面模式固定为**默认模式**（移除默认／控制切换按钮），
    /// 因此默认模式的冷机启动链路必须有回归保护，避免以后改界面时顺带改坏玩法判定。
    ///
    /// 同时固化 V0.0.4 开发计划 §3 的核实结论：预热与启动线判定**不因温度在启动线附近
    /// 小幅回摆而丢失开工资格**——模拟加热与环境散热打平时那种「缓慢、带回落」的升温过程。
    /// </summary>
    public static class DefaultModeStartupTests
    {
        private static TempZone Zone
        {
            get { return RunPolicyAndHeatingTests.Aluminum; }
        }

        private static float C(float celsius)
        {
            return Temperatures.FromCelsius(celsius);
        }

        private static RunDecision Step(float celsius, ref RunState state)
        {
            RunDecision decision = RunController.Update(C(celsius), Zone, RunPolicy.DefaultMode, state);
            state = decision.Next;
            return decision;
        }

        public static void Run()
        {
            ColdStartRamp();
            JitterBelowStartLine();
            StartedSurvivesJitter();
        }

        /// <summary>
        /// 从常温缓慢升温到 315 ℃ 的完整链路：每一步都必须继续预热，不允许中途停止注热。
        /// </summary>
        private static void ColdStartRamp()
        {
            TestRunner.Section("默认模式冷机缓慢升温（v0.0.4）");
            RunState state = RunState.Fresh;
            bool stoppedPreheating = false;

            for (float temp = 20f; temp < 315f; temp += 5f)
            {
                RunDecision decision = Step(temp, ref state);
                if (decision.Heating != HeatingIntent.Preheat || decision.AllowsProduction)
                {
                    stoppedPreheating = true;
                }
            }

            TestRunner.Check(!stoppedPreheating, "20→310 ℃ 全程保持预热意图且不提前放行生产");

            RunDecision at315 = Step(315f, ref state);
            TestRunner.Check(at315.AllowsProduction, "到达 315 ℃ 后取得开工资格并允许生产");
            TestRunner.CheckEqual(HeatingIntent.None, at315.Heating, "取得资格后停止预热（默认模式不补热）");
        }

        /// <summary>
        /// 温度在启动线下方小幅回摆（模拟注热与环境散热打平）：仍应一直预热，
        /// 且不得把这种回摆当成「已经到位」而给出空加热意图。
        /// </summary>
        private static void JitterBelowStartLine()
        {
            TestRunner.Section("启动线下方回摆（v0.0.4）");
            RunState state = RunState.Fresh;
            float[] samples = { 299.8f, 305.5f, 300.1f, 304.9f, 299.9f, 305.2f };
            bool allPreheat = true;

            foreach (float sample in samples)
            {
                RunDecision decision = Step(sample, ref state);
                if (decision.Heating != HeatingIntent.Preheat || decision.AllowsProduction || state.Started)
                {
                    allPreheat = false;
                }
            }

            TestRunner.Check(allPreheat, "299.8↔305.5 ℃ 回摆期间始终预热、不放行生产、不误置开工资格");
        }

        /// <summary>已经取得开工资格后的小幅波动：保持生产许可，不退回预热（生产下限仍是 300 ℃）。</summary>
        private static void StartedSurvivesJitter()
        {
            TestRunner.Section("已开工后的小幅波动（v0.0.4）");
            RunState state = new RunState { Started = true };

            RunDecision at303 = Step(303f, ref state);
            TestRunner.Check(at303.AllowsProduction, "已开工 303 ℃ 仍生产");
            TestRunner.CheckEqual(HeatingIntent.None, at303.Heating, "已开工不退回预热");

            RunDecision at299_5 = Step(299.5f, ref state);
            TestRunner.Check(!at299_5.AllowsProduction, "跌破 300 ℃ 暂停生产");
            TestRunner.CheckEqual(HeatingIntent.Preheat, at299_5.Heating, "跌破生产下限后重新预热");
            TestRunner.Check(!state.Started, "重新预热前先复位开工资格");

            RunDecision at298 = Step(298f, ref state);
            RunDecision at311 = Step(311f, ref state);
            TestRunner.Check(!at311.AllowsProduction, "复位后 311 ℃ 不足 315 ℃，继续等待");
            TestRunner.CheckEqual(HeatingIntent.Preheat, at311.Heating, "复位后 311 ℃ 仍在预热");

            RunDecision at316 = Step(316f, ref state);
            TestRunner.Check(at316.AllowsProduction, "重新到达 315 ℃ 以上后恢复生产资格");
        }
    }
}
