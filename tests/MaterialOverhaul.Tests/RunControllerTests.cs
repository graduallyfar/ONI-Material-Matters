using MaterialOverhaul.Core;
using MaterialOverhaul.Heating;
using MaterialOverhaul.Zones;

namespace MaterialOverhaul.Tests
{
    /// <summary>
    /// 温控运行状态机（V0.0.3 开发计划 §3.2、§3.3、§9.1「启动与回差」）。
    /// 使用铝配方四边界：300／450／650／800 ℃；启动线 315／470 ℃；恢复线 785／640 ℃。
    /// </summary>
    public static class RunControllerTests
    {
        private const float Tol = 0.0001f;

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

        private static TempZone Zone
        {
            get { return RunPolicyAndHeatingTests.Aluminum; }
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

            RunDecision at300 = Step(300f, RunPolicy.DefaultMode, ref state);
            TestRunner.Check(!at300.AllowsProduction, "冷机 300 ℃ 仍禁止生产（未到 315 ℃）");
            TestRunner.CheckEqual(HeatingIntent.Preheat, at300.Heating, "冷机 300 ℃ 预热");
            TestRunner.Check(!state.Started, "300 ℃ 未取得开工资格");

            RunDecision at314_9 = Step(314.9f, RunPolicy.DefaultMode, ref state);
            TestRunner.Check(!at314_9.AllowsProduction, "冷机 314.9 ℃ 仍等 315 ℃");
            TestRunner.CheckEqual(HeatingIntent.Preheat, at314_9.Heating, "314.9 ℃ 继续预热");

            RunDecision at315 = Step(315f, RunPolicy.DefaultMode, ref state);
            TestRunner.Check(at315.AllowsProduction, "到达 315 ℃ 取得开工资格");
            TestRunner.CheckEqual(HeatingIntent.None, at315.Heating, "默认模式开工后不补热");
            TestRunner.Check(state.Started, "315 ℃ 记录开工资格");

            RunDecision at305 = Step(305f, RunPolicy.DefaultMode, ref state);
            TestRunner.Check(at305.AllowsProduction, "已经开工后 305 ℃ 仍能生产");
            TestRunner.Check(state.Started, "305 ℃ 不撤销开工资格");

            RunDecision at299 = Step(299f, RunPolicy.DefaultMode, ref state);
            TestRunner.Check(!at299.AllowsProduction, "跌破 300 ℃ 暂停生产");
            TestRunner.CheckEqual(HeatingIntent.Preheat, at299.Heating, "跌破 300 ℃ 重新预热");
            TestRunner.Check(!state.Started, "跌破生产下限后开工资格复位");

            RunDecision at310 = Step(310f, RunPolicy.DefaultMode, ref state);
            TestRunner.Check(!at310.AllowsProduction, "低温停机后 310 ℃ 仍不足 315 ℃，继续等待");
        }

        private static void DefaultHighSide()
        {
            TestRunner.Section("默认模式高温暂停与恢复回差");
            RunState state = new RunState { Started = true };

            RunDecision at801 = Step(801f, RunPolicy.DefaultMode, ref state);
            TestRunner.Check(!at801.AllowsProduction, "超过 800 ℃ 高温暂停");
            TestRunner.CheckEqual(HeatingIntent.None, at801.Heating, "高温暂停关闭普通加热");
            TestRunner.Check(state.HighTempWaiting, "记录高温等待");

            RunDecision at790 = Step(790f, RunPolicy.DefaultMode, ref state);
            TestRunner.Check(!at790.AllowsProduction, "790 ℃ 高于恢复线 785 ℃，仍等待");
            TestRunner.Check(state.HighTempWaiting, "790 ℃ 仍处于高温等待");

            RunDecision at785 = Step(785f, RunPolicy.DefaultMode, ref state);
            TestRunner.Check(at785.AllowsProduction, "回落到 785 ℃ 恢复生产");
            TestRunner.Check(!state.HighTempWaiting, "785 ℃ 解除高温等待");

            // 外部骤冷：即使低于恢复线，也不能绕过低温侧校验。
            RunState hot = new RunState { Started = true, HighTempWaiting = true };
            RunDecision cold = RunController.Update(C(290f), Zone, RunPolicy.DefaultMode, hot);
            TestRunner.Check(!cold.AllowsProduction, "810 ℃ 直接骤冷到 290 ℃ 不恢复生产");
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
            TestRunner.Check(at640.AllowsProduction, "回落到 640 ℃ 恢复生产");
        }

        private static void ForcedRun()
        {
            TestRunner.Section("强制运行与关闭后的恢复判断");
            RunState state = RunState.Fresh;

            RunDecision cold = Step(250f, RunPolicy.DefaultMode.WithForced(true), ref state);
            TestRunner.Check(cold.AllowsProduction, "强制运行低于 300 ℃ 仍生产");
            TestRunner.CheckEqual(HeatingIntent.Sustained, cold.Heating, "强制运行持续加热");
            TestRunner.CheckClose(0.3f, cold.Efficiency, Tol, "越低温取低温侧边界效率");
            TestRunner.Check(state.Started, "强制运行视为已开工");

            RunDecision hot = Step(900f, RunPolicy.DefaultMode.WithForced(true), ref state);
            TestRunner.Check(hot.AllowsProduction, "强制运行超过 800 ℃ 仍生产");
            TestRunner.CheckClose(0.8f, hot.Efficiency, Tol, "越高温取高温侧边界效率");

            // 关闭强制：立即恢复普通判断，低温先预热、高温进入等待。
            RunState closing = RunState.Fresh;
            RunState forcedState = RunController.Update(C(500f), Zone, RunPolicy.DefaultMode.WithForced(true), closing).Next;
            RunState restored = RunController.OnPolicyChanged(C(290f), Zone, RunPolicy.DefaultMode, forcedState);
            TestRunner.Check(!restored.Started, "关闭强制后 290 ℃ 需要重新预热");
            RunDecision afterClose = RunController.Update(C(290f), Zone, RunPolicy.DefaultMode, restored);
            TestRunner.Check(!afterClose.AllowsProduction, "关闭强制后按默认模式禁止生产");
            TestRunner.CheckEqual(HeatingIntent.Preheat, afterClose.Heating, "关闭强制后低温预热");

            RunState hotRestored = RunController.OnPolicyChanged(C(820f), Zone, RunPolicy.DefaultMode, forcedState);
            TestRunner.Check(hotRestored.HighTempWaiting, "关闭强制后高温进入等待");
            RunDecision hotAfterClose = RunController.Update(C(820f), Zone, RunPolicy.DefaultMode, hotRestored);
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

            RunState toDefault = RunController.OnPolicyChanged(C(320f), Zone, RunPolicy.DefaultMode, new RunState { Started = true });
            TestRunner.Check(toDefault.Started, "从控制切到默认：320 ℃ 已过 315 ℃，直接取得资格");
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

            RunState hot = RunController.OnLoaded(C(900f), Zone, RunPolicy.DefaultMode, new RunState { Started = true });
            TestRunner.Check(hot.HighTempWaiting, "读档时已超过暂停线，进入高温等待");

            RunState recovered = RunController.OnLoaded(C(700f), Zone, RunPolicy.DefaultMode, new RunState { Started = true, HighTempWaiting = true });
            TestRunner.Check(!recovered.HighTempWaiting, "读档时已低于恢复线，解除高温等待");

            RunState legacy = RunController.OnLoaded(C(350f), Zone, RunPolicy.DefaultMode, RunState.Fresh);
            TestRunner.Check(legacy.Started, "旧存档未保存启动资格时按实际温度重建");
        }

        private static void BatchLocked()
        {
            TestRunner.Section("新批次锁定时的状态重建");
            RunState warm = RunController.OnBatchLocked(C(320f), Zone, RunPolicy.DefaultMode, RunState.Fresh);
            TestRunner.Check(warm.Started, "新批次在 320 ℃ 锁定：已过 315 ℃，直接开工");
            TestRunner.Check(!warm.TopUpActive, "新批次不继承上一批的补热状态");

            RunState cold = RunController.OnBatchLocked(C(305f), Zone, RunPolicy.DefaultMode, new RunState { Started = true });
            TestRunner.Check(!cold.Started, "新批次在 305 ℃ 锁定：重新预热到 315 ℃");

            RunState high = RunController.OnBatchLocked(C(820f), Zone, RunPolicy.DefaultMode, RunState.Fresh);
            TestRunner.Check(high.HighTempWaiting, "新批次在 820 ℃ 锁定：高温等待");
            TestRunner.Check(high.Started, "高温等待不算未预热完成");
        }
    }
}
