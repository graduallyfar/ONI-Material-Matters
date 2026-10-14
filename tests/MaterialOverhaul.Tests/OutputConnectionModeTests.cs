using MaterialOverhaul.Core;
using MaterialOverhaul.Heating;
using MaterialOverhaul.Zones;

namespace MaterialOverhaul.Tests
{
    public static class OutputConnectionModeTests
    {
        private static ResolvedZone Zone { get { return RunPolicyAndHeatingTests.AluminumZone; } }
        private static float C(float value) { return Temperatures.FromCelsius(value); }

        public static void Run()
        {
            TestRunner.Section("输出接拆线：低温暂停预热与高温等待");
            RunPolicy policy = RunPolicy.DefaultMode;
            RunState state = new RunState { Started = true };
            TestRunner.Check(RunController.Update(C(350), Zone, policy, state).AllowsProduction, "350 ℃ 默认模式已开工可生产");
            policy = policy.WithOutputConnection(true);
            TestRunner.Check(policy.ControlMode && !policy.Forced, "接输出线选择控制模式");
            state = RunController.OnPolicyChanged(C(350), Zone, policy, state);
            RunDecision low = RunController.Update(C(350), Zone, policy, state);
            TestRunner.Check(!low.AllowsProduction, "350 ℃ 接线后暂停（低于配方最佳下限）");
            TestRunner.CheckEqual(HeatingIntent.Preheat, low.Heating, "接线后按控制启动线预热");
            policy = policy.WithOutputConnection(false);
            state = RunController.OnPolicyChanged(C(350), Zone, policy, low.Next);
            TestRunner.Check(RunController.Update(C(350), Zone, policy, state).AllowsProduction, "350 ℃ 拆线后按默认启动线恢复");

            policy = policy.WithOutputConnection(true);
            state = RunController.OnPolicyChanged(C(700), Zone, policy, state);
            RunDecision high = RunController.Update(C(700), Zone, policy, state);
            TestRunner.Check(!high.AllowsProduction && high.Next.HighTempWaiting, "700 ℃ 控制模式等待降温");
            TestRunner.CheckEqual(HeatingIntent.None, high.Heating, "高温等待不注热");
            TestRunner.Check(!high.Next.ZoneUnreachableLatched, "越过最佳上限不等于最佳温区不可达");

            // 回落到恢复线 640 ℃ 以下即恢复控制模式生产。
            RunDecision stillWaiting = RunController.Update(C(645), Zone, policy, high.Next);
            TestRunner.Check(!stillWaiting.AllowsProduction && stillWaiting.Next.HighTempWaiting,
                "645 ℃ 仍高于恢复线 640 ℃，继续等待降温");
            RunDecision back = RunController.Update(C(640), Zone, policy, high.Next);
            TestRunner.Check(back.AllowsProduction, "回落到 640 ℃（等于恢复线）解除等待并恢复控制生产");

            policy = policy.WithOutputConnection(false);
            state = RunController.OnPolicyChanged(C(700), Zone, policy, back.Next);
            TestRunner.Check(RunController.Update(C(700), Zone, policy, state).AllowsProduction, "700 ℃ 拆线后默认模式允许高温低效生产");

            TestRunner.Section("强制期间接拆线及关闭强制");
            policy = RunPolicy.DefaultMode.WithForced(true).WithOutputConnection(true);
            state = RunController.OnPolicyChanged(C(700), Zone, policy, state);
            RunDecision forced = RunController.Update(C(700), Zone, policy, state);
            TestRunner.Check(policy.ControlMode && policy.Forced && forced.AllowsProduction, "强制接线保留强制许可并更新基础模式");
            TestRunner.CheckEqual(HeatingIntent.Sustained, forced.Heating, "强制接线持续加热");
            policy = policy.WithForced(false);
            state = RunController.OnPolicyChanged(C(700), Zone, policy, forced.Next);
            TestRunner.Check(!RunController.Update(C(700), Zone, policy, state).AllowsProduction, "关闭强制按最新控制模式停产");
            policy = policy.WithForced(true).WithOutputConnection(false);
            TestRunner.Check(policy.Forced && !policy.ControlMode, "强制拆线不关闭强制开关");
            policy = policy.WithForced(false);
            state = RunController.OnPolicyChanged(C(700), Zone, policy, state);
            TestRunner.Check(RunController.Update(C(700), Zone, policy, state).AllowsProduction, "关闭强制按最新默认模式恢复");

            TestRunner.Section("同模式读档：高温与补热回差边界");
            // 默认模式：暂停线 U = 1262.25 K，恢复线 U − 15 K。控制模式：650 / 640 ℃。
            float[] resumeLines = { 1262.25f - 15f, C(640f) };
            float[] pauseLines = { 1262.25f, C(650f) };
            RunPolicy[] modes = { RunPolicy.DefaultMode, RunPolicy.ControlModePolicy };

            for (int i = 0; i < modes.Length; i++)
            {
                RunPolicy mode = modes[i];
                float resume = StartThreshold.ResumeLine(Zone, mode);
                float pause = StartThreshold.PauseLine(Zone, mode);
                TestRunner.CheckClose(resumeLines[i], resume, 0.01f, "恢复线读数 " + mode);
                TestRunner.CheckClose(pauseLines[i], pause, 0.01f, "暂停线读数 " + mode);

                RunState saved = new RunState { Started = true, HighTempWaiting = true };
                RunState loaded = RunController.OnLoaded((resume + pause) / 2, Zone, mode, saved);
                TestRunner.Check(loaded.HighTempWaiting, "已等待：读档在恢复线与暂停线之间仍等待 " + mode);
                TestRunner.Check(!RunController.Update((resume + pause) / 2, Zone, mode, loaded).AllowsProduction, "读档不提前恢复生产 " + mode);
                TestRunner.Check(!RunController.OnLoaded(resume, Zone, mode, saved).HighTempWaiting, "恢复线相等时解除等待 " + mode);
                TestRunner.Check(!RunController.OnLoaded(pause, Zone, mode, new RunState { Started = true }).HighTempWaiting, "未等待：暂停线相等不制造等待 " + mode);
                TestRunner.Check(RunController.OnLoaded(pause + 0.1f, Zone, mode, RunState.Fresh).HighTempWaiting, "超过暂停线建立等待 " + mode);
            }

            RunState topup = new RunState { Started = true, TopUpActive = true };
            RunState same = RunController.OnLoaded(C(465), Zone, RunPolicy.ControlModePolicy.WithOutputConnection(true), topup);
            TestRunner.Check(same.Started && same.TopUpActive, "相同接线读档保留 465 ℃ 补热回差");
            TestRunner.CheckEqual(HeatingIntent.TopUp, RunController.Update(C(465), Zone, RunPolicy.ControlModePolicy, same).Heating, "保留回差后继续补热到 470 ℃");
            RunState switched = RunController.OnPolicyChanged(C(465), Zone, RunPolicy.DefaultMode, same);
            TestRunner.Check(switched.Started && !switched.TopUpActive, "读档接线变默认时重建并清除控制补热目标");
            TestRunner.Check(!RunController.OnLoaded(C(440), Zone, RunPolicy.ControlModePolicy, topup).TopUpActive, "读档低于生产下限不保留非法补热状态");
            TestRunner.Check(!RunController.OnLoaded(C(660), Zone, RunPolicy.ControlModePolicy, topup).TopUpActive, "读档高温等待不保留补热状态");
            RunState preheat = RunController.OnLoaded(C(455), Zone, RunPolicy.ControlModePolicy, RunState.Fresh);
            TestRunner.Check(!preheat.Started, "冷机读档在最佳区内但低于 470 ℃ 仍不提前开工");

            TestRunner.Section("输出接线与红输入：补热独立于生产许可");
            policy = RunPolicy.DefaultMode.WithOutputConnection(true);
            RunDecision warming = RunController.Update(C(465), Zone, policy, topup);
            PowerDemandContext context = new PowerDemandContext
            {
                HasLockedBatch = true, PowerAvailable = true, HasWorker = true,
                TemperatureAllowsProduction = warming.AllowsProduction,
                CommandedHeating = warming.Heating, AutomationAllowsProduction = false
            };
            PowerDemand red = PowerDemand.Evaluate(context);
            TestRunner.Check(!red.ProductionRunning && red.UsedProductionWatts == 0f, "红输入暂停生产");
            TestRunner.CheckEqual(HeatingIntent.TopUp, red.AppliedHeating, "红输入控制模式仍补热");
            TestRunner.CheckClose(1200f, red.UsedHeatingWatts, 0.001f, "红输入补热仍用共用滑条默认功率");
            context.AutomationAllowsProduction = true;
            TestRunner.Check(PowerDemand.Evaluate(context).ProductionRunning && policy.ControlMode, "输入恢复绿不改变控制模式并恢复生产");
            context.HasLockedBatch = false;
            TestRunner.CheckClose(0f, PowerDemand.Evaluate(context).UsedTotalWatts, 0.001f, "输出接线控制模式无批次不加热");
        }
    }
}
