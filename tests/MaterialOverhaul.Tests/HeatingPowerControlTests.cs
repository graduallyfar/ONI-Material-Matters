using System.Reflection;
using MaterialOverhaul.Core;
using MaterialOverhaul.Game;
using MaterialOverhaul.Heating;
using MaterialOverhaul.Zones;

namespace MaterialOverhaul.Tests
{
    public static class HeatingPowerControlTests
    {
        private const float Tol = 0.001f;

        public static void Run()
        {
            SliderAndRestore();
            HeatingMatrix();
        }

        private static MaterialOverhaulFab NewFab()
        {
            return new MaterialOverhaulFab(new ComplexFabricator(), recipe => ProductionDecision.Passthrough);
        }

        private static void SliderAndRestore()
        {
            TestRunner.Section("实际滑条接口、实例隔离与恢复字段校验");
            MaterialOverhaulFab first = NewFab();
            MaterialOverhaulFab second = NewFab();
            TestRunner.CheckClose(1200f, first.HeatingWatts, Tol, "新实例默认功率");
            TestRunner.CheckClose(20f, first.GetSliderMin(0), Tol, "滑条最小 20 W");
            TestRunner.CheckClose(4000f, first.GetSliderMax(0), Tol, "滑条最大 4000 W");
            TestRunner.CheckEqual(0, first.SliderDecimalPlaces(0), "整数 W 显示");
            TestRunner.CheckEqual(" W", first.SliderUnits, "显示电功率单位");
            TestRunner.CheckEqual("STRINGS.UI.UISIDESCREENS.MATERIALOVERHAUL.HEATING_POWER.TITLE", first.SliderTitleKey, "标题本地化键");
            first.SetSliderValue(4000f, 0);
            TestRunner.CheckClose(4000f, first.HeatingWatts, Tol, "设置最高值");
            TestRunner.CheckClose(1200f, second.HeatingWatts, Tol, "另一个实例独立");
            TestRunner.Check(first.GetSliderTooltip(0).Contains("4000"), "提示反映当前设置");
            first.SetSliderValue(20f, 0);
            TestRunner.CheckClose(20f, first.GetSliderValue(0), Tol, "设置最低值");
            first.SetSliderValue(1234.6f, 0);
            TestRunner.CheckClose(1235f, first.HeatingWatts, Tol, "设置按 1 W 精度保存");
            first.PrefabId = "RockCrusher";
            TestRunner.CheckClose(first.GetSliderMin(0), first.GetSliderMax(0), Tol, "无内置加热建筑隐藏滑条");
            first.SetSliderValue(4000f, 0);
            TestRunner.CheckClose(1235f, first.HeatingWatts, Tol, "不适用建筑不修改设置");

            // 模拟字段装载后调用实际恢复方法；不宣称验证了游戏序列化流程。
            FieldInfo field = typeof(MaterialOverhaulFab).GetField("configuredHeatingWatts", BindingFlags.NonPublic | BindingFlags.Instance);
            MethodInfo restore = typeof(MaterialOverhaulFab).GetMethod("RestoreHeatingPower", BindingFlags.NonPublic | BindingFlags.Instance);
            TestRunner.Check(field.GetCustomAttribute<KSerialization.SerializeAttribute>() != null, "功率字段标记随存档保存");
            float[] input = { 20f, 4000f, 567f, 0f, -10f, 5000f, float.NaN, float.PositiveInfinity, float.NegativeInfinity };
            float[] expected = { 20f, 4000f, 567f, 1200f, 20f, 4000f, 1200f, 1200f, 1200f };
            for (int i = 0; i < input.Length; i++)
            {
                field.SetValue(second, input[i]);
                restore.Invoke(second, null);
                TestRunner.CheckEqual(expected[i], (float)field.GetValue(second), "恢复设置 " + input[i]);
                TestRunner.CheckEqual(expected[i], second.HeatingWatts, "恢复后读取一致 " + input[i]);
            }
        }

        private static void HeatingMatrix()
        {
            TestRunner.Section("三种加热在各功率下的需求、供电与停止条件");
            HeatingIntent[] intents = { HeatingIntent.Preheat, HeatingIntent.TopUp, HeatingIntent.Sustained };
            foreach (float watts in new[] { 20f, 567f, 1200f, 4000f })
            {
                foreach (HeatingIntent intent in intents)
                {
                    string name = intent + " / " + watts + " W";
                    PowerDemandContext context = new PowerDemandContext
                    {
                        HasLockedBatch = true, PowerAvailable = true,
                        TemperatureAllowsProduction = intent != HeatingIntent.Preheat,
                        AutomationAllowsProduction = true, HasWorker = true,
                        CommandedHeating = intent, ConfiguredHeatingWatts = watts
                    };
                    PowerDemand demand = PowerDemand.Evaluate(context);
                    float production = intent == HeatingIntent.Preheat ? 0f : Balance.ProductionWatts;
                    TestRunner.CheckClose(watts, demand.UsedHeatingWatts, Tol, name + " 消费设置功率");
                    TestRunner.CheckClose(production + watts, demand.RequestedTotalWatts, Tol, name + " 与生产用电相加");
                    TestRunner.CheckEqual(intent, demand.AppliedHeating, name + " 实际加热意图");
                    TestRunner.CheckClose(Balance.KilowattsForWatts(watts), HeatingPower.KilowattsFor(intent, watts), Tol, name + " 注热由相同设置推导");

                    context.AutomationAllowsProduction = false;
                    demand = PowerDemand.Evaluate(context);
                    TestRunner.CheckClose(watts, demand.UsedTotalWatts, Tol, name + " 自动化红仅停止生产");
                    context.PowerAvailable = false;
                    demand = PowerDemand.Evaluate(context);
                    TestRunner.CheckClose(watts, demand.RequestedTotalWatts, Tol, name + " 断电保留新需求");
                    TestRunner.CheckClose(0f, demand.UsedTotalWatts, Tol, name + " 断电零消费");
                    TestRunner.CheckEqual(HeatingIntent.None, demand.AppliedHeating, name + " 断电不注热");
                    context.ConfiguredHeatingWatts = watts == 20f ? 4000f : 20f;
                    demand = PowerDemand.Evaluate(context);
                    TestRunner.CheckClose(context.ConfiguredHeatingWatts, demand.RequestedHeatingWatts, Tol, name + " 断电调滑条更新需求");
                    context.PowerAvailable = true;
                    demand = PowerDemand.Evaluate(context);
                    TestRunner.CheckClose(context.ConfiguredHeatingWatts, demand.UsedHeatingWatts, Tol, name + " 来电按新设置恢复");

                    PowerDemandContext blocked = context;
                    blocked.HasLockedBatch = false;
                    CheckStopped(blocked, name + " 无批次");
                    blocked = context;
                    blocked.ManuallyDisabled = true;
                    CheckStopped(blocked, name + " 手动禁用");
                    blocked = context;
                    blocked.BrokenDown = true;
                    CheckStopped(blocked, name + " 损坏停机");
                }
            }
            TestRunner.CheckClose(0f, HeatingPower.HeatingWattsFor(HeatingIntent.None, 4000f), Tol, "无加热意图不耗电");
            TestRunner.CheckClose(0f, HeatingPower.KilowattsFor((HeatingIntent)999, 4000f), Tol, "未知意图不注热");
        }

        private static void CheckStopped(PowerDemandContext context, string name)
        {
            PowerDemand demand = PowerDemand.Evaluate(context);
            TestRunner.CheckClose(0f, demand.RequestedTotalWatts, Tol, name + " 不请求电力");
            TestRunner.CheckEqual(HeatingIntent.None, demand.AppliedHeating, name + " 不注热");
        }
    }
}
