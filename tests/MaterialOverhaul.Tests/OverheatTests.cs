using MaterialOverhaul.Core;
using MaterialOverhaul.Heating;

namespace MaterialOverhaul.Tests
{
    /// <summary>
    /// v0.0.2 过热阈值规则：过热阈值 = 主建造材料熔点 × 85%
    /// （建筑细节文档 §2.1.5，V0.0.2 开发计划 §4）。
    /// 熔点数据取自游戏元素表 Solid 元素定义（AluminumOre 1357 K 等）。
    /// </summary>
    public static class OverheatTests
    {
        private const float Tol = 0.01f;

        public static void Run()
        {
            CelsiusConversion();
            MeltingPointRule();
            RuleCoversWorkMax();
            RuleTableValidation();
        }

        private static void CelsiusConversion()
        {
            TestRunner.Section("摄氏度与开尔文换算");
            TestRunner.CheckClose(573.15f, Temperatures.FromCelsius(300f), Tol, "300 ℃ = 573.15 K");
            TestRunner.CheckClose(1073.15f, Temperatures.FromCelsius(800f), Tol, "800 ℃ = 1073.15 K");
            TestRunner.CheckClose(800f, Temperatures.ToCelsius(1073.15f), Tol, "开尔文转回摄氏度");
            TestRunner.CheckClose(25f, Temperatures.ToCelsius(Temperatures.FromCelsius(25f)), Tol, "换算可往返");
        }

        private static void MeltingPointRule()
        {
            TestRunner.Section("过热阈值取熔点 85%");

            float overheat;
            TestRunner.Check(OverheatRule.TryCompute(1357f, out overheat), "铝矿熔点可用");
            TestRunner.CheckClose(1153.45f, overheat, Tol, "1357 K × 0.85 = 1153.45 K");

            TestRunner.Check(!OverheatRule.TryCompute(0f, out overheat), "熔点为 0 时不计算");
            TestRunner.Check(!OverheatRule.TryCompute(-100f, out overheat), "熔点为负时不计算");
            TestRunner.Check(!OverheatRule.TryCompute(float.NaN, out overheat), "熔点为 NaN 时不计算");
            TestRunner.Check(!OverheatRule.TryCompute(float.PositiveInfinity, out overheat), "熔点为无穷时不计算");
            TestRunner.Check(!OverheatRule.TryCompute(1000f, 0f, out overheat), "比例为 0 时不计算");
            TestRunner.CheckClose(850f, OverheatRule.TryCompute(1000f, 0.85f, out overheat) ? overheat : -1f, Tol,
                "显式传入比例同样生效");
        }

        private static void RuleCoversWorkMax()
        {
            TestRunner.Section("材料熔点与铝矿配方最高可工作温度的关系");
            float workMax = Temperatures.FromCelsius(800f);   // 1073.15 K

            // 原版建筑可用的矿物材料（Solid 元素定义）与它们能否覆盖铝矿配方的 800 ℃。
            CheckCoverage("SandStone", 1200f, workMax, false);
            CheckCoverage("Granite", 942f, workMax, false);
            CheckCoverage("IgneousRock", 1683f, workMax, true);
            CheckCoverage("Obsidian", 3000f, workMax, true);
            CheckCoverage("Ceramic", 2123f, workMax, true);

            TestRunner.Check(OverheatRule.CoversWorkMax(0f, workMax) == false, "阈值不可用时视为不覆盖");
        }

        private static void CheckCoverage(string materialId, float meltingPoint, float workMax, bool expected)
        {
            float overheat;
            OverheatRule.TryCompute(meltingPoint, out overheat);
            TestRunner.CheckEqual(expected, OverheatRule.CoversWorkMax(overheat, workMax),
                materialId + " 过热阈值 " + overheat.ToString("F2") + " K 对铝矿配方 800 ℃ 的覆盖结论");
        }

        private static void RuleTableValidation()
        {
            TestRunner.Section("过热规则侧表登记与校验");

            OverheatRuleTable.Register("MetalRefinery", OverheatRule.MeltingPointFraction);
            float fraction;
            TestRunner.Check(OverheatRuleTable.TryGet("MetalRefinery", out fraction), "已登记建筑可查到比例");
            TestRunner.CheckClose(0.85f, fraction, Tol, "比例为 0.85");
            TestRunner.Check(!OverheatRuleTable.TryGet("GlassForge", out fraction), "未登记建筑查不到（保持原版阈值）");

            ModLog.Clear();
            OverheatRuleTable.Register("BadZero", 0f);
            OverheatRuleTable.Register("BadLarge", 1.5f);
            OverheatRuleTable.Register("BadNaN", float.NaN);
            TestRunner.CheckEqual(3, ModLog.CountOf("ERROR"), "非法比例逐条记录错误日志");
            TestRunner.Check(!OverheatRuleTable.TryGet("BadZero", out fraction), "非法条目不入表");
            TestRunner.Check(!OverheatRuleTable.TryGet("BadLarge", out fraction), "超出 (0,1] 的条目不入表");

            ModLog.Clear();
            OverheatRuleTable.Register("MetalRefinery", 0.5f);
            TestRunner.CheckEqual(1, ModLog.CountOf("ERROR"), "重复登记记录错误日志");
            TestRunner.Check(OverheatRuleTable.TryGet("MetalRefinery", out fraction) && fraction > 0.8f,
                "重复登记保留先登记的值");

            ModLog.Clear();
        }
    }
}
