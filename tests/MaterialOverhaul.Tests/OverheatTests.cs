using MaterialOverhaul.Core;
using MaterialOverhaul.Heating;

namespace MaterialOverhaul.Tests
{
    /// <summary>
    /// 过热阈值规则（V0.1.2 开发计划 §1.1、§2.1.5）：
    /// **最高工作温度 = 过热阈值 = 主建造材料熔点(K) × 0.75**。
    ///
    /// 熔点数据取自本地游戏元素表 Solid 元素定义（SandStone 1200 K、Granite 942 K 等）。
    /// 摄氏显示是换算后的结果，不是「摄氏熔点 × 0.75」。
    /// </summary>
    public static class OverheatTests
    {
        private const float Tol = 0.01f;

        public static void Run()
        {
            CelsiusConversion();
            MeltingPointRule();
            CoverageAgainstBestZone();
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
            TestRunner.Section("最高工作温度 / 过热阈值取熔点 75%（按 K 计算）");

            float overheat;
            TestRunner.Check(OverheatRule.TryCompute(1357f, out overheat), "铝矿熔点可用");
            TestRunner.CheckClose(1017.75f, overheat, Tol, "1357 K × 0.75 = 1017.75 K");

            TestRunner.CheckClose(706.5f, Compute(942f), Tol, "花岗岩 942 K × 0.75 = 706.5 K（433.35 ℃）");
            TestRunner.CheckClose(900f, Compute(1200f), Tol, "砂岩 1200 K × 0.75 = 900 K（626.85 ℃）");

            TestRunner.Check(!OverheatRule.TryCompute(0f, out overheat), "熔点为 0 时不计算");
            TestRunner.Check(!OverheatRule.TryCompute(-100f, out overheat), "熔点为负时不计算");
            TestRunner.Check(!OverheatRule.TryCompute(float.NaN, out overheat), "熔点为 NaN 时不计算");
            TestRunner.Check(!OverheatRule.TryCompute(float.PositiveInfinity, out overheat), "熔点为无穷时不计算");
            TestRunner.Check(!OverheatRule.TryCompute(1000f, 0f, out overheat), "比例为 0 时不计算");
            TestRunner.CheckClose(750f, OverheatRule.TryCompute(1000f, 0.75f, out overheat) ? overheat : -1f, Tol,
                "显式传入比例同样生效");
            TestRunner.CheckClose(0.75f, OverheatRule.MeltingPointFraction, 0.00001f, "已确认系数为 0.75");
        }

        private static float Compute(float meltingPoint)
        {
            float value;
            OverheatRule.TryCompute(meltingPoint, out value);
            return value;
        }

        /// <summary>
        /// 材料上限与炼铝最佳温区（450–650 ℃ = 723.15–923.15 K）的关系。
        /// V0.1.2 起「包不住最佳区」不是配置错误，而是玩法分支：
        /// B₂ &gt; U 是最佳温区受限，B₁ &gt; U 是最佳温区不可达。
        /// </summary>
        private static void CoverageAgainstBestZone()
        {
            TestRunner.Section("建筑上限与炼铝最佳区的关系（V0.1.2 §2.1.5）");
            float bestMin = Temperatures.FromCelsius(450f);
            float bestMax = Temperatures.FromCelsius(650f);

            // 原版建筑可用的矿物材料（Solid 元素定义）与它们对 450–650 ℃ 的覆盖情况。
            CheckCoverage("Granite", 942f, bestMin, bestMax, false, false);
            CheckCoverage("SandStone", 1200f, bestMin, bestMax, true, false);
            CheckCoverage("IgneousRock", 1683f, bestMin, bestMax, true, true);
            CheckCoverage("Obsidian", 3000f, bestMin, bestMax, true, true);
            CheckCoverage("Ceramic", 2123f, bestMin, bestMax, true, true);

            TestRunner.Check(OverheatRule.CoversBestMax(0f, bestMax) == false, "阈值不可用时视为不覆盖");
        }

        private static void CheckCoverage(
            string materialId, float meltingPoint, float bestMin, float bestMax,
            bool reachable, bool coversWhole)
        {
            float overheat = Compute(meltingPoint);

            TestRunner.CheckEqual(coversWhole, OverheatRule.CoversBestMax(overheat, bestMax),
                materialId + " 上限 " + overheat.ToString("F2") + " K 是否覆盖整个最佳区");
            TestRunner.CheckEqual(reachable, overheat >= bestMin,
                materialId + " 上限是否不低于最佳下限（控制模式可用性）");
        }

        private static void RuleTableValidation()
        {
            TestRunner.Section("过热规则侧表登记与校验");

            OverheatRuleTable.Register("MetalRefinery", OverheatRule.MeltingPointFraction);
            float fraction;
            TestRunner.Check(OverheatRuleTable.TryGet("MetalRefinery", out fraction), "已登记建筑可查到比例");
            TestRunner.CheckClose(0.75f, fraction, Tol, "比例为 0.75");
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
            TestRunner.Check(OverheatRuleTable.TryGet("MetalRefinery", out fraction) && fraction > 0.7f,
                "重复登记保留先登记的值");

            ModLog.Clear();
        }
    }
}
