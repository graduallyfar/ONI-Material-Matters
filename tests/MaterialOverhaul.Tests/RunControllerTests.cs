using MaterialOverhaul.Core;
using MaterialOverhaul.Heating;
using MaterialOverhaul.Zones;

namespace MaterialOverhaul.Tests
{
    /// <summary>
    /// 温控运行状态机（V0.0.3 开发计划 §3.2、§3.3；V0.1.2 开发计划 §2.2–§2.4）。
    ///
    /// V0.1.2 数值：建筑下限 80 ℃、默认原启动目标 117 ℃、配方最佳温区 450–650 ℃、
    /// 控制原启动目标 470 ℃、主建造材料取火成岩（建筑上限 989.1 ℃，最佳区完整可用）。
    /// </summary>
    public static class RunControllerTests
    {
        private const float Tol = 0.0001f;

        /// <summary>火成岩作为主建造材料时的建筑上限：1683 K × 0.75 = 1262.25 K。</summary>
        private const float CeilingKelvin = 1683f * 0.75f;

        /// <summary>上限的摄氏读数，仅用于构造测试点。</summary>
        private const float CeilingCelsius = CeilingKelvin - 273.15f;

        public static void Run()
        {
            DefaultLowSide();
            DefaultHighSide();
            ControlLowSide();
            ControlHighSide();
            ForcedRun();
            PolicySwitch();
            SaveLoad();
            BatchLocked();
        }

        private static float C(float celsius)
        {
            return Temperatures.FromCelsius(celsius);
        }

        private static ResolvedZone Zone
        {
            get { return RunPolicyAndHeatingTests.AluminumZone; }
        }

        private static RunDecision Step(float celsius, RunPolicy policy, ref RunState state)
        {
            RunDecision decision = RunController.Update(C(celsius), Zone, policy, state);
            state = decision.Next;
            return decision;
        }

        private static void DefaultLowSide()
        {
            TestRunner.Section("默认模式低温启动与停机");
            RunState state = RunState.Fresh;

            RunDecision at100 = Step(100f, RunPolicy.DefaultMode, ref state);
            TestRunner.Check(!at100.AllowsProduction, "冷机 100 ℃ 仍禁止生产（未到 117 ℃）");
            TestRunner.CheckEqual(HeatingIntent.Preheat, at100.Heating, "冷机 100 ℃ 预热");
            TestRunner.Check(!state.Started, "100 ℃ 未取得开工资格");

            RunDecision at116_9 = Step(116.9f, RunPolicy.DefaultMode, ref state);
            TestRunner.Check(!at116_9.AllowsProduction, "冷机 116.9 ℃ 仍等 117 ℃");
            TestRunner.CheckEqual(HeatingIntent.Preheat, at116_9.Heating, "116.9 ℃ 继续预热");

            RunDecision at117 = Step(117f, RunPolicy.DefaultMode, ref state);
            TestRunner.Check(at117.AllowsProduction, "到达 117 ℃ 取得开工资格");
            TestRunner.CheckEqual(HeatingIntent.None, at117.Heating, "默认模式开工后不补热");
            TestRunner.Check(state.Started, "117 ℃ 记录开工资格");

            RunDecision at100b = Step(100f, RunPolicy.DefaultMode, ref state);
            TestRunner.Check(at100b.AllowsProduction, "已经开工后 100 ℃ 仍能生产（高于建筑下限 80 ℃）");
            TestRunner.Check(state.Started, "100 ℃ 不撤销开工资格");

            RunDecision at79 = Step(79f, RunPolicy.DefaultMode, ref state);
            TestRunner.Check(!at79.AllowsProduction, "跌破 80 ℃ 建筑下限暂停生产");
            TestRunner.CheckEqual(HeatingIntent.Preheat, at79.Heating, "跌破 80 ℃ 重新预热");
            TestRunner.Check(!state.Started, "跌破生产下限后开工资格复位");

            RunDecision at110 = Step(110f, RunPolicy.DefaultMode, ref state);
            TestRunner.Check(!at110.AllowsProduction, "低温停机后 110 ℃ 仍不足 117 ℃，继续等待");
        }

        private static void DefaultHighSide()
        {
            TestRunner.Section("默认模式高温暂停与恢复回差");
            RunState state = new RunState { Started = true };

            RunDecision at990 = Step(CeilingCelsius + 1f, RunPolicy.DefaultMode, ref state);
            TestRunner.Check(!at990.AllowsProduction, "超过建筑上限 989.1 ℃ 高温暂停");
            TestRunner.CheckEqual(HeatingIntent.None, at990.Heating, "高温暂停关闭普通加热");
            TestRunner.Check(state.HighTempWaiting, "记录高温等待");

            RunDecision at980 = Step(CeilingCelsius - 9.1f, RunPolicy.DefaultMode, ref state);
            TestRunner.Check(!at980.AllowsProduction, "980 ℃ 高于恢复线 974.1 ℃，仍等待");
            TestRunner.Check(state.HighTempWaiting, "980 ℃ 仍处于高温等待");

            RunDecision at974 = Step(CeilingCelsius - 15f, RunPolicy.DefaultMode, ref state);
            TestRunner.Check(at974.AllowsProduction, "回落到 974.1 ℃ 恢复生产");
            TestRunner.Check(!state.HighTempWaiting, "974.1 ℃ 解除高温等待");

            // 外部骤冷：即使低于恢复线，也不能绕过低温侧校验。
            RunState hot = new RunState { Started = true, HighTempWaiting = true };
            RunDecision cold = RunController.Update(C(70f), Zone, RunPolicy.DefaultMode, hot);
            TestRunner.Check(!cold.AllowsProduction, "989 ℃ 直接骤冷到 70 ℃ 不恢复生产");
            TestRunner.CheckEqual(HeatingIntent.Preheat, cold.Heating, "骤冷后重新预热");
            TestRunner.Check(!cold.Next.HighTempWaiting, "骤冷解除高温等待");
            TestRunner.Check(!cold.Next.Started, "骤冷同时撤销开工资格");
        }

        private static void ControlLowSide()
        {
            TestRunner.Section("控制模式低温启动与补热回差");
            RunState state = RunState.Fresh;

            RunDecision at469_9 = Step(469.9f, RunPolicy.ControlModePolicy, ref state);
            TestRunner.Check(!at469_9.AllowsProduction, "控制模式 469.9 ℃ 仍等 470 ℃");
            TestRunner.CheckEqual(HeatingIntent.Preheat, at469_9.Heating, "控制模式未开工时预热");

            RunDecision at470 = Step(470f, RunPolicy.ControlModePolicy, ref state);
            TestRunner.Check(at470.AllowsProduction, "到达 470 ℃ 取得开工资格");

            RunDecision at455 = Step(455f, RunPolicy.ControlModePolicy, ref state);
            TestRunner.Check(at455.AllowsProduction, "已开工后 455 ℃ 仍能生产");
            TestRunner.CheckEqual(HeatingIntent.TopUp, at455.Heating, "455 ℃ 低于 460 ℃，补热");

            RunDecision at465 = Step(465f, RunPolicy.ControlModePolicy, ref state);
            TestRunner.CheckEqual(HeatingIntent.TopUp, at465.Heating, "回差区间内继续补热到 470 ℃");

            RunDecision at470b = Step(470f, RunPolicy.ControlModePolicy, ref state);
            TestRunner.CheckEqual(HeatingIntent.None, at470b.Heating, "到达补热停止线 470 ℃ 停止补热");
            TestRunner.Check(!state.TopUpActive, "停止补热后清除补热状态");

            RunDecision at456 = Step(456f, RunPolicy.ControlModePolicy, ref state);
            TestRunner.CheckEqual(HeatingIntent.TopUp, at456.Heating, "跌破 460 ℃ 重新启动补热");

            RunDecision at449 = Step(449f, RunPolicy.ControlModePolicy, ref state);
            TestRunner.Check(!at449.AllowsProduction, "跌破 450 ℃ 暂停生产");
            TestRunner.CheckEqual(HeatingIntent.Preheat, at449.Heating, "跌破 450 ℃ 重新预热到 470 ℃");
            TestRunner.Check(!state.TopUpActive, "低温停机清除补热状态");
        }

        private static void ControlHighSide()
        {
            TestRunner.Section("控制模式高温暂停与恢复回差");
            RunState state = new RunState { Started = true };

            RunDecision at651 = Step(651f, RunPolicy.ControlModePolicy, ref state);
            TestRunner.Check(!at651.AllowsProduction, "超过 650 ℃ 高温暂停");
            TestRunner.Check(state.HighTempWaiting, "记录高温等待");

            RunDecision at645 = Step(645f, RunPolicy.ControlModePolicy, ref state);
            TestRunner.Check(!at645.AllowsProduction, "645 ℃ 高于恢复线 640 ℃，仍等待");

            RunDecision at640 = Step(640f, RunPolicy.ControlModePolicy, ref state);
            TestRunner.Check(at640.AllowsProduction, "回落到 640 ℃（等于恢复线）恢复生产");
            TestRunner.Check(!state.HighTempWaiting, "640 ℃ 解除高温等待");
        }

        private static void ForcedRun()
        {
            TestRunner.Section("强制运行与关闭后的恢复判断");
            RunState state = RunState.Fresh;

            RunDecision cold = Step(50f, RunPolicy.DefaultMode.WithForced(true), ref state);
            TestRunner.Check(cold.AllowsProduction, "强制运行低于 80 ℃ 仍生产");
            TestRunner.CheckEqual(HeatingIntent.Sustained, cold.Heating, "强制运行持续加热");
            TestRunner.CheckClose(0.3f, cold.Efficiency, Tol, "越低温取低温侧边界效率");
            TestRunner.Check(state.Started, "强制运行视为已开工");

            RunDecision hot = Step(1000f, RunPolicy.DefaultMode.WithForced(true), ref state);
            TestRunner.Check(hot.AllowsProduction, "强制运行超过建筑上限仍生产");
            TestRunner.CheckClose(0.8f, hot.Efficiency, Tol, "越高温取高温侧边界效率");

            // 关闭强制：立即恢复普通判断，低温先预热、高温进入等待。
            RunState closing = RunState.Fresh;
            RunState forcedState = RunController.Update(C(500f), Zone, RunPolicy.DefaultMode.WithForced(true), closing).Next;
            RunState restored = RunController.OnPolicyChanged(C(70f), Zone, RunPolicy.DefaultMode, forcedState);
            TestRunner.Check(!restored.Started, "关闭强制后 70 ℃ 需要重新预热");
            RunDecision afterClose = RunController.Update(C(70f), Zone, RunPolicy.DefaultMode, restored);
            TestRunner.Check(!afterClose.AllowsProduction, "关闭强制后按默认模式禁止生产");
            TestRunner.CheckEqual(HeatingIntent.Preheat, afterClose.Heating, "关闭强制后低温预热");

            RunState hotRestored = RunController.OnPolicyChanged(C(1000f), Zone, RunPolicy.DefaultMode, forcedState);
            TestRunner.Check(hotRestored.HighTempWaiting, "关闭强制后高温进入等待");
            RunDecision hotAfterClose = RunController.Update(C(1000f), Zone, RunPolicy.DefaultMode, hotRestored);
            TestRunner.Check(!hotAfterClose.AllowsProduction, "关闭强制后高温不生产");
            TestRunner.CheckEqual(HeatingIntent.None, hotAfterClose.Heating, "关闭强制后高温不加热");
        }

        private static void PolicySwitch()
        {
            TestRunner.Section("模式切换重建启动条件");
            RunState started = new RunState { Started = true };

            RunState toControl = RunController.OnPolicyChanged(C(455f), Zone, RunPolicy.ControlModePolicy, started);
            TestRunner.Check(!toControl.Started, "从默认切到控制：455 ℃ 不足 470 ℃，重新预热");
            RunDecision control = RunController.Update(C(455f), Zone, RunPolicy.ControlModePolicy, toControl);
            TestRunner.Check(!control.AllowsProduction, "切换后 455 ℃ 不允许生产");

            RunState toDefault = RunController.OnPolicyChanged(C(120f), Zone, RunPolicy.DefaultMode, new RunState { Started = true });
            TestRunner.Check(toDefault.Started, "从控制切到默认：120 ℃ 已过 117 ℃，直接取得资格");
        }

        private static void SaveLoad()
        {
            TestRunner.Section("读档校验（保留回差状态）");
            RunState saved = new RunState { Started = true, TopUpActive = true };

            RunState loaded = RunController.OnLoaded(C(465f), Zone, RunPolicy.ControlModePolicy, saved);
            TestRunner.Check(loaded.Started, "读档保留开工资格");
            TestRunner.Check(loaded.TopUpActive, "读档保留 465 ℃ 的补热回差状态");

            RunState cooled = RunController.OnLoaded(C(475f), Zone, RunPolicy.ControlModePolicy, saved);
            TestRunner.Check(!cooled.TopUpActive, "读档时已高于补热停止线，清除补热状态");

            RunState hot = RunController.OnLoaded(C(1000f), Zone, RunPolicy.DefaultMode, new RunState { Started = true });
            TestRunner.Check(hot.HighTempWaiting, "读档时已超过暂停线，进入高温等待");

            RunState recovered = RunController.OnLoaded(C(500f), Zone, RunPolicy.DefaultMode, new RunState { Started = true, HighTempWaiting = true });
            TestRunner.Check(!recovered.HighTempWaiting, "读档时已低于恢复线，解除高温等待");

            RunState legacy = RunController.OnLoaded(C(200f), Zone, RunPolicy.DefaultMode, RunState.Fresh);
            TestRunner.Check(legacy.Started, "旧存档未保存启动资格时按实际温度重建");
        }

        private static void BatchLocked()
        {
            TestRunner.Section("新批次锁定时的状态重建");
            RunState warm = RunController.OnBatchLocked(C(120f), Zone, RunPolicy.DefaultMode, RunState.Fresh);
            TestRunner.Check(warm.Started, "新批次在 120 ℃ 锁定：已过 117 ℃，直接开工");
            TestRunner.Check(!warm.TopUpActive, "新批次不继承上一批的补热状态");

            RunState cold = RunController.OnBatchLocked(C(110f), Zone, RunPolicy.DefaultMode, new RunState { Started = true });
            TestRunner.Check(!cold.Started, "新批次在 110 ℃ 锁定：重新预热到 117 ℃");

            RunState high = RunController.OnBatchLocked(C(1000f), Zone, RunPolicy.DefaultMode, RunState.Fresh);
            TestRunner.Check(high.HighTempWaiting, "新批次在 1000 ℃ 锁定：高温等待");
            TestRunner.Check(high.Started, "高温等待不算未预热完成");
        }
    }
}
