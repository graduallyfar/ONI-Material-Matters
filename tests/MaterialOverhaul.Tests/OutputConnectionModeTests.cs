using MaterialOverhaul.Core;
using MaterialOverhaul.Heating;
using MaterialOverhaul.Zones;

namespace MaterialOverhaul.Tests
{
    public static class OutputConnectionModeTests
    {
        private static TempZone Zone { get { return RunPolicyAndHeatingTests.Aluminum; } }
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
            TestRunner.Check(!low.AllowsProduction, "350 ℃ 接线后暂停");
            TestRunner.CheckEqual(HeatingIntent.Preheat, low.Heating, "接线后按控制启动线预热");
            policy = policy.WithOutputConnection(false);
            state = RunController.OnPolicyChanged(C(350), Zone, policy, low.Next);
            TestRunner.Check(RunController.Update(C(350), Zone, policy, state).AllowsProduction, "350 ℃ 拆线后按默认启动线恢复");

            policy = policy.WithOutputConnection(true);
            state = RunController.OnPolicyChanged(C(700), Zone, policy, state);
            RunDecision high = RunController.Update(C(700), Zone, policy, state);
            TestRunner.Check(!high.AllowsProduction && high.Next.HighTempWaiting, "700 ℃ 控制模式等待降温");
            TestRunner.CheckEqual(HeatingIntent.None, high.Heating, "高温等待不注热");
            policy = policy.WithOutputConnection(false);
            state = RunController.OnPolicyChanged(C(700), Zone, policy, high.Next);
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
            foreach (RunPolicy mode in new[] { RunPolicy.DefaultMode, RunPolicy.ControlModePolicy })
            {
                float resume = StartThreshold.HighResumeLine(Zone, mode);
                float pause = StartThreshold.HighPauseLine(Zone, mode);
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
            TestRunner.CheckClose(200f, red.UsedHeatingWatts, 0.001f, "红输入补热用电仍为 200 W");
            context.AutomationAllowsProduction = true;
            TestRunner.Check(PowerDemand.Evaluate(context).ProductionRunning && policy.ControlMode, "输入恢复绿不改变控制模式并恢复生产");
            context.HasLockedBatch = false;
            TestRunner.CheckClose(0f, PowerDemand.Evaluate(context).UsedTotalWatts, 0.001f, "输出接线控制模式无批次不加热");
        }
    }
}
