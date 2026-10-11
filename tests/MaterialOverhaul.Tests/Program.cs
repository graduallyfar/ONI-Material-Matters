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
            Run("配方温区目录（v0.0.2）", RecipeCatalogueTests.Run);
            Run("过热阈值规则（v0.0.2）", OverheatTests.Run);
            Run("强制运行与加热意图", RunPolicyAndHeatingTests.Run);
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
