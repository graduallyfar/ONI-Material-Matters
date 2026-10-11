using MaterialOverhaul.Hardness;

namespace MaterialOverhaul.Tests
{
    public static class DamageCauseTests
    {
        public static void Run()
        {
            CauseSelection();
            RepairRules();
        }

        private static void CauseSelection()
        {
            TestRunner.Section("损坏原因判定");
            TestRunner.CheckEqual(DamageCause.None,
                DamageCauseEvaluator.Evaluate(false, false, false), "无机械损坏也未过热 → 无损坏");
            TestRunner.CheckEqual(DamageCause.Mechanical,
                DamageCauseEvaluator.Evaluate(true, false, false), "只有机械损坏");
            TestRunner.CheckEqual(DamageCause.Overheat,
                DamageCauseEvaluator.Evaluate(false, true, false), "只有过热损坏");

            TestRunner.CheckEqual(DamageCause.MechanicalAndOverheat,
                DamageCauseEvaluator.Evaluate(true, true, false), "机械损坏停机后追加过热 → 双重原因");
            TestRunner.CheckEqual(DamageCause.MechanicalAndOverheat,
                DamageCauseEvaluator.Evaluate(false, true, true), "强制运行发生过热 → 双重原因，不要求实际机械磨损");
            TestRunner.CheckEqual(DamageCause.MechanicalAndOverheat,
                DamageCauseEvaluator.Evaluate(true, true, true), "强制运行且机械与过热同时存在");

            TestRunner.CheckEqual(DamageCause.Mechanical,
                DamageCauseEvaluator.Evaluate(true, false, true), "强制运行但未过热 → 只有机械损坏");
            TestRunner.CheckEqual(DamageCause.None,
                DamageCauseEvaluator.Evaluate(false, false, true), "强制运行但既无磨损也未过热 → 无损坏");

            TestRunner.Check(DamageCauseEvaluator.ShowsBothCauses(DamageCause.MechanicalAndOverheat), "双重原因标记为同时显示");
            TestRunner.Check(!DamageCauseEvaluator.ShowsBothCauses(DamageCause.Overheat), "单一过热不标记双重显示");
        }

        private static void RepairRules()
        {
            TestRunner.Section("维修方式与温度重置");
            TestRunner.Check(!DamageCauseEvaluator.UsesOverheatRepair(DamageCause.Mechanical), "只有机械损坏 → 普通维修");
            TestRunner.Check(!DamageCauseEvaluator.ResetsTemperatureOnRepair(DamageCause.Mechanical), "只有机械损坏 → 维修不重置温度");

            TestRunner.Check(DamageCauseEvaluator.UsesOverheatRepair(DamageCause.Overheat), "只有过热损坏 → 过热维修");
            TestRunner.Check(DamageCauseEvaluator.ResetsTemperatureOnRepair(DamageCause.Overheat), "过热损坏 → 按原版过热维修重置温度");

            TestRunner.Check(DamageCauseEvaluator.UsesOverheatRepair(DamageCause.MechanicalAndOverheat), "双重原因 → 过热维修");
            TestRunner.Check(DamageCauseEvaluator.ResetsTemperatureOnRepair(DamageCause.MechanicalAndOverheat), "双重原因 → 重置温度");

            TestRunner.Check(!DamageCauseEvaluator.UsesOverheatRepair(DamageCause.None), "无损坏不需过热维修");
        }
    }
}
