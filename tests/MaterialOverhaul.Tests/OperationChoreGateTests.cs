using MaterialOverhaul.Core;
using MaterialOverhaul.Game;
using MaterialOverhaul.Heating;
using MaterialOverhaul.Zones;

namespace MaterialOverhaul.Tests
{
    public static class OperationChoreGateTests
    {
        private static float C(float celsius) { return Temperatures.FromCelsius(celsius); }

        // 游戏状态读取替身使用真实温控判断层给出许可，实际任务门控／清理源码参与编译。
        // V0.1.2 温区：建筑下限 80 ℃、默认原启动目标 117 ℃、配方最佳温区 450–650 ℃。
        private static ProductionDecision Evaluate(float temp, RunPolicy policy, RunState state)
        {
            RunDecision decision = RunController.Update(C(temp), RunPolicyAndHeatingTests.AluminumZone, policy, state);
            return new ProductionDecision
            {
                State = decision.State, Efficiency = decision.Efficiency,
                AllowsProduction = decision.AllowsProduction, ZoneConfigured = decision.ZoneConfigured
            };
        }

        public static void Run()
        {
            ColdBatchAndRecreatedChore();
            TemperatureTransitions();
            PreserveVanillaScope();
        }

        private static void ColdBatchAndRecreatedChore()
        {
            TestRunner.Section("材料到齐开始批次：原版创建入口立即拦截，无需等待 Sim200ms");
            ModLog.Clear();
            ComplexFabricator machine = new ComplexFabricator();
            MaterialOverhaulFab mod = new MaterialOverhaulFab(machine,
                recipe => Evaluate(20f, RunPolicy.DefaultMode, RunState.Fresh));
            machine.UpdateChore();
            TestRunner.Check(machine.CurrentChore == null && !mod.Paused, "无批次不制造温度暂停");

            ComplexRecipe batch = new ComplexRecipe { id = "Aluminum" };
            machine.CurrentWorkingOrder = batch; // 原版 StartWorkingOrder 已投料，再调用 UpdateChore。
            machine.UpdateChore();
            TestRunner.CheckEqual(0, machine.Created, "20 ℃ 材料到齐也不创建操作任务");
            TestRunner.Check(machine.CurrentChore == null && mod.Paused, "已有锁定批次但没有操作任务");
            TestRunner.Check(ReferenceEquals(batch, machine.CurrentWorkingOrder), "保留已锁定配方");
            TestRunner.CheckClose(0.42f, machine.OrderProgress, 0.0001f, "暂停不重置当前进度");
            TestRunner.CheckClose(100f, machine.BatchMaterialMass, 0.0001f, "暂停不退料或重复投料");

            for (int i = 0; i < 5; i++) machine.UpdateChore(); // 模拟原版事件重复刷新。
            TestRunner.CheckEqual(0, machine.Created, "持续暂停期间刷新不能重新派工");
            TestRunner.CheckEqual(0, machine.Cancelled, "没有实际任务时不反复调用撤销");
            TestRunner.CheckEqual(1, ModLog.CountOf("INFO"), "持续暂停只记录一次转换日志");

            TestRunner.Section("已暂停后出现残留任务：按实际 chore 清理，不只看暂停标记");
            machine.CurrentChore = new Chore();
            machine.WorkerAssigned = true;
            mod.Active = true;
            machine.UpdateChore();
            TestRunner.CheckEqual(1, machine.Cancelled, "已暂停时仍撤销后来出现的实际任务");
            TestRunner.Check(machine.CurrentChore == null && !machine.WorkerAssigned, "撤销任务释放操作工");
            TestRunner.Check(!mod.Active, "关闭原版生产活动状态");
            TestRunner.CheckEqual(0, machine.Created, "清理后不执行原版创建正文");
            TestRunner.CheckClose(0.42f, machine.OrderProgress, 0.0001f, "清理残留任务不取消批次");
            TestRunner.CheckEqual(1, ModLog.CountOf("INFO"), "清理残留任务不重复刷暂停日志");

            mod.Evaluation = recipe => Evaluate(117f, RunPolicy.DefaultMode, RunState.Fresh);
            machine.UpdateChore();
            TestRunner.CheckEqual(1, machine.Created, "达到默认启动线后沿用原版创建任务");
            machine.UpdateChore();
            TestRunner.CheckEqual(1, machine.Created, "连续刷新不重复创建已有任务");
        }

        private static void TemperatureTransitions()
        {
            TestRunner.Section("创建入口沿用启动线、高温回差与强制运行");
            ComplexFabricator machine = new ComplexFabricator { CurrentWorkingOrder = new ComplexRecipe { id = "Aluminum" } };
            MaterialOverhaulFab mod = new MaterialOverhaulFab(machine,
                recipe => Evaluate(100f, RunPolicy.DefaultMode, RunState.Fresh));
            machine.UpdateChore();
            TestRunner.Check(machine.CurrentChore == null, "冷机在 100 ℃（未到 117 ℃ 启动目标）不能派工");
            mod.Evaluation = recipe => Evaluate(350f, RunPolicy.ControlModePolicy, RunState.Fresh);
            machine.UpdateChore();
            TestRunner.Check(machine.CurrentChore == null, "控制模式 350 ℃ 不能创建操作任务");
            mod.Evaluation = recipe => Evaluate(455f, RunPolicy.ControlModePolicy, RunState.Fresh);
            machine.UpdateChore();
            TestRunner.Check(machine.CurrentChore == null, "控制模式冷机 455 ℃ 仍需等 470 ℃");
            mod.Evaluation = recipe => Evaluate(470f, RunPolicy.ControlModePolicy, RunState.Fresh);
            machine.UpdateChore();
            TestRunner.Check(machine.CurrentChore != null, "控制模式到达 470 ℃ 可派工");
            mod.Evaluation = recipe => Evaluate(700f, RunPolicy.ControlModePolicy, new RunState { Started = true });
            machine.UpdateChore();
            TestRunner.Check(machine.CurrentChore == null, "控制模式 700 ℃ 撤销操作任务并等待降温");
            mod.Evaluation = recipe => Evaluate(980f, RunPolicy.DefaultMode, new RunState { Started = true, HighTempWaiting = true });
            machine.UpdateChore();
            TestRunner.Check(machine.CurrentChore == null, "默认模式高温回差内不提前派工（980 ℃ 高于 974.1 ℃ 恢复线）");
            mod.Evaluation = recipe => Evaluate(974f, RunPolicy.DefaultMode, new RunState { Started = true, HighTempWaiting = true });
            machine.UpdateChore();
            TestRunner.Check(machine.CurrentChore != null, "回落到默认恢复线后可派工");
            mod.Evaluation = recipe => Evaluate(1000f, RunPolicy.DefaultMode, new RunState { Started = true });
            machine.UpdateChore();
            TestRunner.Check(machine.CurrentChore == null, "默认模式超过 989.1 ℃ 建筑上限也不派工");
            mod.Evaluation = recipe => Evaluate(1000f, RunPolicy.ControlModePolicy.WithForced(true), RunState.Fresh);
            machine.UpdateChore();
            TestRunner.Check(machine.CurrentChore != null, "强制运行仍允许越界操作");
            machine.Operational = false;
            machine.UpdateChore();
            TestRunner.Check(machine.CurrentChore == null, "强制不绕过原版断电／禁用／红输入等停机条件");
            machine.Operational = true;
            mod.Evaluation = recipe => new ProductionDecision { ZoneConfigured = true, AllowsProduction = false };
            machine.UpdateChore();
            TestRunner.Check(machine.CurrentChore == null, "有温区但网络尚未就绪的许可同样阻止派工");
        }

        private static void PreserveVanillaScope()
        {
            TestRunner.Section("白名单与无温区配方保持原版，取料／批次不受创建门控影响");
            ComplexFabricator vanilla = new ComplexFabricator { CurrentWorkingOrder = new ComplexRecipe { id = "Vanilla" } };
            vanilla.UpdateChore();
            TestRunner.Check(vanilla.CurrentChore != null, "无 Mod 组件的建筑不受补丁影响");

            ComplexFabricator machine = new ComplexFabricator { CurrentWorkingOrder = new ComplexRecipe { id = "Copper" } };
            MaterialOverhaulFab mod = new MaterialOverhaulFab(machine, recipe => ProductionDecision.Passthrough);
            machine.UpdateChore();
            TestRunner.Check(machine.CurrentChore != null && !mod.Paused, "未配置温区的铜配方沿用原版");
            machine.CurrentWorkingOrder = null;
            machine.UpdateChore();
            TestRunner.Check(machine.CurrentChore == null, "没有批次时原版正常清理任务");
            machine.CurrentWorkingOrder = new ComplexRecipe { id = "Aluminum" };
            machine.duplicantOperated = false;
            mod.Evaluation = recipe => Evaluate(20f, RunPolicy.DefaultMode, RunState.Fresh);
            machine.UpdateChore();
            TestRunner.Check(!mod.Paused && machine.CurrentChore == null, "非复制人建筑不走操作任务门控");
            machine.duplicantOperated = true;
            mod.Spawned = false;
            machine.UpdateChore();
            TestRunner.Check(machine.CurrentChore != null && !mod.Paused, "未生成组件不读取未初始化游戏状态");
        }
    }
}
