using MaterialOverhaul.Core;
using MaterialOverhaul.Zones;

namespace MaterialOverhaul.Tests
{
    /// <summary>
    /// 温区合成、六态边界与温度效率（V0.1.2 开发计划 §2.1–§2.3）。
    ///
    /// V0.1.2 起温区由三份来源合成：建筑下限 L、配方最佳区 [B₁, B₂]、主建造材料上限 U。
    /// 本文件用一组「测试构造值」验证合成与边界：L = 5 ℃、最佳区 35–55 ℃、U = 100 ℃。
    /// 这些数字只用于构造边界条件，**不是任何建筑的正式数值**。
    /// </summary>
    public static class ZoneTests
    {
        private const float FloorCelsius = 5f;
        private const float BestMinCelsius = 35f;
        private const float BestMaxCelsius = 55f;
        private const float CeilingCelsius = 100f;

        private static readonly TempZone Zone = TempZone.Celsius(BestMinCelsius, BestMaxCelsius);

        private const float Tol = 0.0001f;

        private static float C(float celsius)
        {
            return Temperatures.FromCelsius(celsius);
        }

        /// <summary>按固定上限合成温区（构造测试用；正式上限由主建造材料熔点推出）。</summary>
        public static ResolvedZone WithCeiling(float floorCelsius, float bestMinCelsius, float bestMaxCelsius, float ceilingCelsius)
        {
            float ceiling = C(ceilingCelsius);
            return TempZoneResolver.Resolve(
                "test.zone",
                C(floorCelsius),
                TempZone.Celsius(bestMinCelsius, bestMaxCelsius),
                delegate (out float value)
                {
                    value = ceiling;
                    return true;
                });
        }

        private static ResolvedZone Sample
        {
            get { return WithCeiling(FloorCelsius, BestMinCelsius, BestMaxCelsius, CeilingCelsius); }
        }

        public static void Run()
        {
            SixStates();
            AllowedStates();
            EfficiencyCurve();
            Unconfigured();
            Resolution();
            CelsiusConversion();
        }

        private static void SixStates()
        {
            TestRunner.Section("六态边界（区间开闭）");
            ResolvedZone zone = Sample;

            TestRunner.CheckEqual(TempState.BelowMin, TempZoneEvaluator.Evaluate(C(4.9f), zone), "低于建筑下限 → 停止");
            TestRunner.CheckEqual(TempState.LowEfficiency, TempZoneEvaluator.Evaluate(C(5f), zone), "等于建筑下限 → 低温低效（左闭）");
            TestRunner.CheckEqual(TempState.LowEfficiency, TempZoneEvaluator.Evaluate(C(34.9f), zone), "略低于最佳下限 → 低温低效");
            TestRunner.CheckEqual(TempState.Normal, TempZoneEvaluator.Evaluate(C(35f), zone), "等于最佳下限 → 正常（左闭）");
            TestRunner.CheckEqual(TempState.Normal, TempZoneEvaluator.Evaluate(C(55f), zone), "等于最佳上限 → 正常（右闭）");
            TestRunner.CheckEqual(TempState.HighEfficiency, TempZoneEvaluator.Evaluate(C(55.1f), zone), "略高于最佳上限 → 高温低效");
            TestRunner.CheckEqual(TempState.HighEfficiency, TempZoneEvaluator.Evaluate(C(100f), zone), "等于建筑上限 → 仍在范围内（右闭，不因超限停机）");
            TestRunner.CheckEqual(TempState.AboveMax, TempZoneEvaluator.Evaluate(C(100.1f), zone), "高于建筑上限 → 停止");
        }

        private static void AllowedStates()
        {
            TestRunner.Section("允许生产 / 停止加热（两种模式的差别）");

            // 默认模式：整个 [L, U] 都允许生产。
            TestRunner.Check(TempState.Normal.AllowsProduction(), "默认模式：最佳区允许生产");
            TestRunner.Check(TempState.LowEfficiency.AllowsProduction(), "默认模式：低温低效允许生产");
            TestRunner.Check(TempState.HighEfficiency.AllowsProduction(), "默认模式：高温低效允许生产");
            TestRunner.Check(!TempState.BelowMin.AllowsProduction(), "默认模式：低于建筑下限停止生产");
            TestRunner.Check(!TempState.AboveMax.AllowsProduction(), "默认模式：高于建筑上限停止生产");

            // 控制模式：只有最佳区允许生产。
            TestRunner.Check(TempState.Normal.AllowsControlProduction(), "控制模式：最佳区允许生产");
            TestRunner.Check(!TempState.LowEfficiency.AllowsControlProduction(), "控制模式：低温段不允许生产");
            TestRunner.Check(!TempState.HighEfficiency.AllowsControlProduction(), "控制模式：高温段不允许生产");

            TestRunner.Check(TempState.AboveMax.StopsHeating(), "超过建筑上限停止内置加热");
            TestRunner.Check(!TempState.BelowMin.StopsHeating(), "低温侧不停止加热");
            TestRunner.Check(TempState.HighEfficiency.IsHighEfficiencyBand(), "高温低效属于高温段");
            TestRunner.Check(TempState.BelowMin.IsLowEfficiencyBand(), "低于建筑下限仍算低温段");
        }

        private static void EfficiencyCurve()
        {
            TestRunner.Section("温度效率（最佳区 100%，两侧按建筑范围衰减）");
            ResolvedZone zone = Sample;

            TestRunner.CheckClose(1f, TempZoneEvaluator.Efficiency(TempState.Normal, C(45f), zone), Tol, "最佳温区效率 100%");
            TestRunner.CheckClose(1f, TempZoneEvaluator.Efficiency(C(35f), zone), Tol, "最佳下限处效率 100%");
            TestRunner.CheckClose(1f, TempZoneEvaluator.Efficiency(C(55f), zone), Tol, "最佳上限处效率 100%");

            TestRunner.CheckClose(TempZoneEvaluator.LowBoundaryEfficiency, TempZoneEvaluator.Efficiency(C(5f), zone), 0.00001f,
                "建筑下限处取低温边界效率 0.3");
            TestRunner.CheckClose(TempZoneEvaluator.HighBoundaryEfficiency, TempZoneEvaluator.Efficiency(C(100f), zone), 0.00001f,
                "建筑上限处取高温边界效率 0.8");

            TestRunner.CheckClose(TempZoneEvaluator.LowBoundaryEfficiency, TempZoneEvaluator.Efficiency(C(0f), zone), Tol,
                "越低温不继续下降、不归零（仅强制运行可达）");
            TestRunner.CheckClose(TempZoneEvaluator.HighBoundaryEfficiency, TempZoneEvaluator.Efficiency(C(150f), zone), Tol,
                "越高温保持高温侧边界效率（仅强制运行可达）");

            // 低温段单点验算：5→35 ℃ 区间，20 ℃ 处的归一化比例为 0.5。
            float lowMid = TempZoneEvaluator.Efficiency(C(20f), zone);
            TestRunner.CheckClose(0.5477226f, lowMid, 0.00001f, "低温段 20 ℃ ≈ 0.3^0.5 = 0.547723");
            TestRunner.Check(lowMid > TempZoneEvaluator.LowBoundaryEfficiency && lowMid < 1f, "低温段效率介于边界值与 1 之间");

            // 高温段单点验算：55→100 ℃ 区间，77.5 ℃ 处的归一化比例为 0.5。
            float highMid = TempZoneEvaluator.Efficiency(C(77.5f), zone);
            TestRunner.CheckClose(0.8944272f, highMid, 0.00001f, "高温段 77.5 ℃ ≈ 0.8^0.5 = 0.894427");
            TestRunner.Check(highMid < 1f && highMid > TempZoneEvaluator.HighBoundaryEfficiency, "高温段效率介于 1 与边界值之间");

            TestRunner.Check(TempZoneEvaluator.Efficiency(C(30f), zone) > TempZoneEvaluator.Efficiency(C(15f), zone),
                "低温段随温度上升递增");
            TestRunner.Check(TempZoneEvaluator.Efficiency(C(60f), zone) > TempZoneEvaluator.Efficiency(C(90f), zone),
                "高温段随温度上升递减");

            // 建材只改变高温段终点，不改变最佳区内的效率（V0.1.2 §2.1.5）。
            ResolvedZone tighter = WithCeiling(FloorCelsius, BestMinCelsius, BestMaxCelsius, 65f);
            TestRunner.CheckClose(1f, TempZoneEvaluator.Efficiency(C(45f), tighter), Tol,
                "上限更低时最佳区内效率仍为 100%（建材不降低最佳区效率）");
        }

        private static void Unconfigured()
        {
            TestRunner.Section("未配置温区");
            TestRunner.CheckEqual(TempState.Disabled, TempZoneEvaluator.Evaluate(100f, ResolvedZone.Unconfigured), "未配置温区判断恒通过");
            TestRunner.CheckClose(1f, TempZoneEvaluator.Efficiency(TempState.Disabled, 100f, ResolvedZone.Unconfigured), Tol, "未配置温区效率恒为 1");
        }

        private static void Resolution()
        {
            TestRunner.Section("配方最佳区校验（B₁ < B₂ 且为有限值）");
            ModLog.Clear();

            TempZone crossed = TempZoneResolver.Validate("test.crossed", TempZone.Celsius(55f, 35f));
            TestRunner.Check(!crossed.Configured, "最佳下限不低于上限时退化为未配置");
            TestRunner.Check(ModLog.CountOf("ERROR") == 1, "最佳区倒置记录错误日志");

            TempZone nan = TempZoneResolver.Validate("test.nan", new TempZone(float.NaN, 328.15f));
            TestRunner.Check(!nan.Configured, "含非有限值退化为未配置");

            TempZone equal = TempZoneResolver.Validate("test.equal", new TempZone(300f, 300f));
            TestRunner.Check(!equal.Configured, "最佳上下限相等退化为未配置（最佳段不能退化为一点）");

            TempZone ok = TempZoneResolver.Validate("test.ok", Zone);
            TestRunner.Check(ok.Configured && ok.IsOrdered, "合法最佳区原样保留");
            TestRunner.CheckClose(Zone.BestMin, ok.BestMin, Tol, "合法最佳区数值不变");

            TestRunner.Section("三份来源合成温区");
            ModLog.Clear();

            ResolvedZone sample = Sample;
            TestRunner.Check(sample.Configured, "三份来源齐备时可运算");
            TestRunner.CheckClose(C(FloorCelsius), sample.Min, Tol, "建筑下限取自建筑配置");
            TestRunner.CheckClose(C(BestMinCelsius), sample.BestMin, Tol, "配方最佳下限原样保留");
            TestRunner.CheckClose(C(BestMaxCelsius), sample.BestMax, Tol, "配方最佳上限原样保留");
            TestRunner.CheckClose(C(CeilingCelsius), sample.Max, Tol, "建筑上限取自材料上限");
            TestRunner.Check(sample.ControlRangeFull, "最佳区完整可用（A 情况）");
            TestRunner.CheckClose(C(BestMaxCelsius), sample.ControlMax, Tol, "完整可用时控制上限取 min(B₂, U) = B₂");

            // B 情况：最佳区部分可用，高侧被建筑上限截断。
            ResolvedZone limited = WithCeiling(FloorCelsius, 35f, 80f, 60f);
            TestRunner.Check(limited.Configured && limited.ControlRangeLimited, "高侧被截断属于最佳温区受限");
            TestRunner.CheckClose(C(60f), limited.ControlMax, Tol, "受限时控制上限取建筑上限");
            TestRunner.Check(!limited.ControlRangeUnreachable, "仍有交集时不报不可达");

            // C 情况：最佳区部分可用，且启动目标会被封顶。
            ResolvedZone capped = WithCeiling(FloorCelsius, 35f, 80f, 55f);
            TestRunner.Check(capped.ControlRangeLimited, "交集很窄时仍属于最佳温区受限");
            TestRunner.CheckClose(C(55f), capped.ControlMax, Tol, "窄交集的控制上限取建筑上限");

            // D 情况：最佳区整体高于建筑上限。
            ResolvedZone unreachable = WithCeiling(FloorCelsius, 40f, 60f, 38f);
            TestRunner.Check(unreachable.Configured && unreachable.ControlRangeUnreachable, "B₁ 高于建筑上限时最佳温区不可达");
            TestRunner.Check(!unreachable.HasControlRange, "不可达时没有可用控制区间");
            TestRunner.Check(!unreachable.ControlRangeLimited, "不可达不与受限同时成立");

            TestRunner.Section("配置错误与安全回退");
            ModLog.Clear();

            // B₁ < L 是设计阶段错误：运行时抬到建筑下限，不出现低于下限的预热目标。
            ResolvedZone raised = WithCeiling(30f, 20f, 60f, 100f);
            TestRunner.Check(raised.Configured, "B₁ < L 时仍可运算");
            TestRunner.CheckClose(C(30f), raised.BestMin, Tol, "B₁ < L 时下限被抬到建筑下限");
            TestRunner.Check(ModLog.CountOf("WARN") >= 1, "B₁ < L 记录提示日志");

            ModLog.Clear();

            // 熔点不可用：整体退回原版行为，不产生无限上限。
            ResolvedZone noCeiling = TempZoneResolver.Resolve(
                "test.no-ceiling", C(FloorCelsius), Zone,
                delegate (out float value) { value = 0f; return false; });
            TestRunner.Check(!noCeiling.Configured, "材料上限不可用时退化为未配置");
            TestRunner.Check(ModLog.CountOf("WARN") == 1, "材料上限不可用记录提示日志");

            // 上限不高于下限：特殊建材组合，本版退回原版行为（V0.1.2 §7 第 4 项未定案）。
            ModLog.Clear();
            ResolvedZone emptyRange = WithCeiling(80f, 100f, 140f, 70f);
            TestRunner.Check(!emptyRange.Configured, "上限不高于下限时退化为未配置");
            TestRunner.Check(ModLog.CountOf("WARN") == 1, "空工作范围记录提示日志");

            ModLog.Clear();
        }

        private static void CelsiusConversion()
        {
            TestRunner.Section("温区数值的摄氏度书写");
            TempZone zone = TempZone.Celsius(450f, 650f);
            TestRunner.CheckClose(723.15f, zone.BestMin, 0.01f, "450 ℃ → 723.15 K");
            TestRunner.CheckClose(923.15f, zone.BestMax, 0.01f, "650 ℃ → 923.15 K");
            TestRunner.Check(zone.Configured, "按摄氏度书写即为已配置");
        }
    }
}
