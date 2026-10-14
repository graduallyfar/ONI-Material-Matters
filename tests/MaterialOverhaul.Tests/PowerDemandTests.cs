using MaterialOverhaul.Core;
using MaterialOverhaul.Heating;

namespace MaterialOverhaul.Tests
{
    /// <summary>
    /// 耗电矩阵与电力口径（V0.0.3 开发计划 §4.1、§9.1「加热与功耗」）。
    /// v0.1.1：原版生产功率 1200 W、三种加热共用默认 1200 W、倍率 1000。
    /// </summary>
    public static class PowerDemandTests
    {
        private const float Tol = 0.0001f;

        public static void Run()
        {
            Matrix();
            DemandSurvivesOutage();
            AutomationInput();
        }

        private static void AutomationInput()
        {
            TestRunner.Section("「允许／暂停生产」自动化输入（§2.2.3）");
            TestRunner.Check(Heating.AutomationGate.AllowsProduction(false, false, false), "没有输入端口 → 不受自动化约束");
            TestRunner.Check(Heating.AutomationGate.AllowsProduction(true, false, false), "端口标记尚未初始化 → 默认允许（未接线放行）");
            TestRunner.Check(Heating.AutomationGate.AllowsProduction(true, true, true), "绿色 → 允许生产");
            TestRunner.Check(!Heating.AutomationGate.AllowsProduction(true, true, false), "红色 → 暂停生产");

            TestRunner.Check(
                Heating.AutomationGate.IsAutomationOnlyBlock(true, true, false, true, false, false, true),
                "红信号且机器本身可用 → 只由自动化造成");
            TestRunner.Check(
                !Heating.AutomationGate.IsAutomationOnlyBlock(true, true, false, false, false, false, true),
                "红信号但断电 → 不算只由自动化造成（走原版取消取料路径）");
            TestRunner.Check(
                !Heating.AutomationGate.IsAutomationOnlyBlock(true, true, false, true, true, false, true),
                "手动禁用 → 不算只由自动化造成");
            TestRunner.Check(
                !Heating.AutomationGate.IsAutomationOnlyBlock(true, true, false, true, false, true, true),
                "损坏停机 → 不算只由自动化造成");
            TestRunner.Check(
                !Heating.AutomationGate.IsAutomationOnlyBlock(true, true, true, true, false, false, true),
                "绿信号 → 不是红信号阻塞");
            TestRunner.Check(
                !Heating.AutomationGate.IsAutomationOnlyBlock(false, false, false, true, false, false, true),
                "没有端口 → 不参与自动化阻塞判定");
        }

        private static PowerDemandContext Base()
        {
            PowerDemandContext context = default(PowerDemandContext);
            context.HasLockedBatch = true;
            context.PowerAvailable = true;
            context.TemperatureAllowsProduction = true;
            context.AutomationAllowsProduction = true;
            context.HasWorker = true;
            context.CommandedHeating = HeatingIntent.None;
            return context;
        }

        private static void Matrix()
        {
            TestRunner.Section("§4.1 耗电矩阵");

            PowerDemand idle = PowerDemand.Evaluate(default(PowerDemandContext));
            Check(idle, 0f, 0f, "空闲／无有效批次");

            PowerDemand preheat = PowerDemand.Evaluate(With(Base(), heating: HeatingIntent.Preheat, temperature: false));
            Check(preheat, 0f, 1200f, "单独预热");
            TestRunner.CheckClose(1200f, HeatingPower.KilowattsFor(HeatingIntent.Preheat), Tol, "单独预热注热 1200 kW");
            TestRunner.Check(!preheat.ProductionRunning, "单独预热不算真实生产");

            PowerDemand producing = PowerDemand.Evaluate(Base());
            Check(producing, 1200f, 0f, "生产，无加热");
            TestRunner.CheckClose(0f, HeatingPower.KilowattsFor(HeatingIntent.None), Tol, "生产无加热不注热");

            PowerDemand topUp = PowerDemand.Evaluate(With(Base(), heating: HeatingIntent.TopUp));
            Check(topUp, 1200f, 1200f, "控制生产并补热");
            TestRunner.CheckClose(1200f, HeatingPower.KilowattsFor(HeatingIntent.TopUp), Tol, "补热默认注热 1200 kW");

            PowerDemand redTopUp = PowerDemand.Evaluate(With(Base(), heating: HeatingIntent.TopUp, automation: false));
            Check(redTopUp, 0f, 1200f, "红信号停产仍需补热");

            PowerDemand waitingTopUp = PowerDemand.Evaluate(With(Base(), heating: HeatingIntent.TopUp, worker: false));
            Check(waitingTopUp, 0f, 1200f, "等操作工仍需补热");

            PowerDemand forced = PowerDemand.Evaluate(With(Base(), heating: HeatingIntent.Sustained));
            Check(forced, 1200f, 1200f, "强制生产并持续加热");
            TestRunner.CheckClose(1200f, HeatingPower.KilowattsFor(HeatingIntent.Sustained), Tol, "持续加热注热 1200 kW");

            PowerDemand forcedPaused = PowerDemand.Evaluate(With(Base(), heating: HeatingIntent.Sustained, automation: false));
            Check(forcedPaused, 0f, 1200f, "强制但生产被红信号暂停");

            PowerDemand highWait = PowerDemand.Evaluate(With(Base(), heating: HeatingIntent.None, temperature: false));
            Check(highWait, 0f, 0f, "高温等待，无强制");

            Check(PowerDemand.Evaluate(With(Base(), heating: HeatingIntent.Preheat, powered: false)), 0f, 0f, "断电");
            Check(PowerDemand.Evaluate(With(Base(), heating: HeatingIntent.Preheat, disabled: true)), 0f, 0f, "手动禁用");
            Check(PowerDemand.Evaluate(With(Base(), heating: HeatingIntent.Sustained, broken: true)), 0f, 0f, "损坏停机");
        }

        private static void DemandSurvivesOutage()
        {
            TestRunner.Section("断电时保留需求");
            PowerDemandContext context = With(Base(), heating: HeatingIntent.TopUp, powered: false);
            PowerDemand demand = PowerDemand.Evaluate(context);
            TestRunner.CheckClose(2400f, demand.RequestedTotalWatts, Tol, "断电时仍请求 2400 W");
            TestRunner.CheckClose(0f, demand.UsedTotalWatts, Tol, "断电时不消费、不注热");
            TestRunner.CheckEqual(HeatingIntent.None, demand.AppliedHeating, "断电时不注入热量");

            context.PowerAvailable = true;
            PowerDemand restored = PowerDemand.Evaluate(context);
            TestRunner.CheckClose(2400f, restored.UsedTotalWatts, Tol, "恢复供电后立即按需求消费");
            TestRunner.CheckEqual(HeatingIntent.TopUp, restored.AppliedHeating, "恢复供电后立即注热");
        }

        private static PowerDemandContext With(
            PowerDemandContext context,
            HeatingIntent? heating = null,
            bool? temperature = null,
            bool? automation = null,
            bool? worker = null,
            bool? powered = null,
            bool? disabled = null,
            bool? broken = null)
        {
            if (heating.HasValue)
            {
                context.CommandedHeating = heating.Value;
            }

            if (temperature.HasValue)
            {
                context.TemperatureAllowsProduction = temperature.Value;
            }

            if (automation.HasValue)
            {
                context.AutomationAllowsProduction = automation.Value;
            }

            if (worker.HasValue)
            {
                context.HasWorker = worker.Value;
            }

            if (powered.HasValue)
            {
                context.PowerAvailable = powered.Value;
            }

            if (disabled.HasValue)
            {
                context.ManuallyDisabled = disabled.Value;
            }

            if (broken.HasValue)
            {
                context.BrokenDown = broken.Value;
            }

            return context;
        }

        private static void Check(PowerDemand demand, float productionWatts, float heatingWatts, string name)
        {
            TestRunner.CheckClose(productionWatts, demand.UsedProductionWatts, Tol, name + "：生产用电 " + productionWatts + " W");
            TestRunner.CheckClose(heatingWatts, demand.UsedHeatingWatts, Tol, name + "：加热用电 " + heatingWatts + " W");
            TestRunner.CheckClose(productionWatts + heatingWatts, demand.UsedTotalWatts, Tol, name + "：合计 " + (productionWatts + heatingWatts) + " W");
        }
    }
}
