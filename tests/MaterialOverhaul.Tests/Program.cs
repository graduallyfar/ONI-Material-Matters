using System;

namespace MaterialOverhaul.Tests
{
    public static class Program
    {
        public static int Main()
        {
            Console.WriteLine("MaterialOverhaul 判断层检查");
            Console.WriteLine();

            Run("Zones", ZoneTests.Run);
            Run("Orders", OrderTests.Run);
            Run("订单流程（v0.0.2）", OrderFlowTests.Run);
            Run("订单身份与逐笔编辑（v0.0.3）", OrderIdentityTests.Run);
            Run("配方温区目录（v0.0.2）", RecipeCatalogueTests.Run);
            Run("过热阈值规则（v0.0.2）", OverheatTests.Run);
            Run("温区许可与加热意图（v0.0.3）", RunPolicyAndHeatingTests.Run);
            Run("温控运行状态机（v0.0.3）", RunControllerTests.Run);
            Run("默认模式冷机启动（v0.0.4）", DefaultModeStartupTests.Run);
            Run("输出接线模式与读档回差（v0.1.0）", OutputConnectionModeTests.Run);
            Run("操作任务温度门控（实际补丁源码＋游戏替身）", OperationChoreGateTests.Run);
            Run("耗电矩阵（v0.0.3）", PowerDemandTests.Run);
            Run("Heating / Hardness", HeatingAndWearTests.Run);
            Run("损坏原因与维修", DamageCauseTests.Run);

            Console.WriteLine();
            Console.WriteLine("断言 " + TestRunner.Checks + " 项，失败 " + TestRunner.Failures + " 项。");
            return TestRunner.Failures == 0 ? 0 : 1;
        }

        private static void Run(string name, Action body)
        {
            Console.WriteLine("== " + name);
            try
            {
                body();
            }
            catch (Exception ex)
            {
                TestRunner.Check(false, name + " 抛出异常：" + ex.GetType().Name + " " + ex.Message);
            }
        }
    }
}
