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
    ///
    /// V0.1.2 数值：建筑下限 80 ℃，默认原启动目标 = 80 + 0.1 × (450 − 80) = 117 ℃；
    /// 主建造材料取火成岩，建筑上限 989.1 ℃，远高于启动目标（不被封顶）。
    /// </summary>
    public static class DefaultModeStartupTests
    {
        private static ResolvedZone Zone
        {
            get { return RunPolicyAndHeatingTests.AluminumZone; }
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
        /// 从常温缓慢升温到 117 ℃ 的完整链路：每一步都必须继续预热，不允许中途停止注热。
        /// </summary>
        private static void ColdStartRamp()
        {
            TestRunner.Section("默认模式冷机缓慢升温（v0.0.4）");
            RunState state = RunState.Fresh;
            bool stoppedPreheating = false;

            for (float temp = 20f; temp < 115f; temp += 5f)
            {
                RunDecision decision = Step(temp, ref state);
                if (decision.Heating != HeatingIntent.Preheat || decision.AllowsProduction)
                {
                    stoppedPreheating = true;
                }
            }

            TestRunner.Check(!stoppedPreheating, "20→115 ℃ 全程保持预热意图且不提前放行生产");

            RunDecision at117 = Step(117f, ref state);
            TestRunner.Check(at117.AllowsProduction, "到达 117 ℃ 后取得开工资格并允许生产");
            TestRunner.CheckEqual(HeatingIntent.None, at117.Heating, "取得资格后停止预热（默认模式不补热）");
        }

        /// <summary>
        /// 温度在启动线下方小幅回摆（模拟注热与环境散热打平）：仍应一直预热，
        /// 且不得把这种回摆当成「已经到位」而给出空加热意图。
        /// </summary>
        private static void JitterBelowStartLine()
        {
            TestRunner.Section("启动线下方回摆（v0.0.4）");
            RunState state = RunState.Fresh;
            float[] samples = { 100.8f, 110.5f, 101.1f, 112.9f, 100.9f, 113.2f };
            bool allPreheat = true;

            foreach (float sample in samples)
            {
                RunDecision decision = Step(sample, ref state);
                if (decision.Heating != HeatingIntent.Preheat || decision.AllowsProduction || state.Started)
                {
                    allPreheat = false;
                }
            }

            TestRunner.Check(allPreheat, "100.8↔113.2 ℃ 回摆期间始终预热、不放行生产、不误置开工资格");
        }

        /// <summary>已经取得开工资格后的小幅波动：保持生产许可，不退回预热（生产下限是建筑下限 80 ℃）。</summary>
        private static void StartedSurvivesJitter()
        {
            TestRunner.Section("已开工后的小幅波动（v0.0.4）");
            RunState state = new RunState { Started = true };

            RunDecision at100 = Step(100f, ref state);
            TestRunner.Check(at100.AllowsProduction, "已开工 100 ℃ 仍生产（高于建筑下限 80 ℃）");
            TestRunner.CheckEqual(HeatingIntent.None, at100.Heating, "已开工不退回预热");

            RunDecision at79_5 = Step(79.5f, ref state);
            TestRunner.Check(!at79_5.AllowsProduction, "跌破 80 ℃ 建筑下限暂停生产");
            TestRunner.CheckEqual(HeatingIntent.Preheat, at79_5.Heating, "跌破建筑下限后重新预热");
            TestRunner.Check(!state.Started, "重新预热前先复位开工资格");

            RunDecision at90 = Step(90f, ref state);
            RunDecision at113 = Step(113f, ref state);
            TestRunner.Check(!at113.AllowsProduction, "复位后 113 ℃ 不足 117 ℃，继续等待");
            TestRunner.CheckEqual(HeatingIntent.Preheat, at113.Heating, "复位后 113 ℃ 仍在预热");

            RunDecision at118 = Step(118f, ref state);
            TestRunner.Check(at118.AllowsProduction, "重新到达 117 ℃ 以上后恢复生产资格");
        }
    }
}
