using MaterialOverhaul.Core;
using MaterialOverhaul.Heating;
using MaterialOverhaul.Zones;

namespace MaterialOverhaul.Tests
{
    /// <summary>
    /// 温区许可、效率、启动线与加热意图（V0.1.2 开发计划 §2.2–§2.4）。
    ///
    /// 使用已确认的炼铝口径：建筑下限 80 ℃、配方最佳温区 450–650 ℃；
    /// 主建造材料上限取火成岩的 1262.25 K（1683 K × 0.75 = 989.1 ℃），即最佳区完整可用。
    /// 建材上限只用于构造边界条件，不代表用户已经定案所有材料。
    /// </summary>
    public static class RunPolicyAndHeatingTests
    {
        /// <summary>建筑最低工作温度 L = 80 ℃。</summary>
        public const float FloorCelsius = 80f;

        /// <summary>火成岩作为主建造材料时的建筑上限：1683 K × 0.75 = 1262.25 K。</summary>
        public const float IgneousCeilingKelvin = 1683f * 0.75f;

        /// <summary>配方最佳温区 450–650 ℃。</summary>
        public static readonly TempZone Aluminum = TempZone.Celsius(450f, 650f);

        /// <summary>合成后的炼铝温区（默认口径）。</summary>
        public static readonly ResolvedZone AluminumZone = TempZoneResolver.Resolve(
            "test.aluminum",
            Temperatures.FromCelsius(FloorCelsius),
            Aluminum,
            ConstantCeiling(IgneousCeilingKelvin));

        private const float Tol = 0.0001f;

        /// <summary>构造固定上限的取值委托。</summary>
        private static HeatCeilingResolver ConstantCeiling(float ceilingKelvin)
        {
            float captured = ceilingKelvin;
            return delegate (out float value)
            {
                value = captured;
                return true;
            };
        }

        public static void Run()
        {
            ZoneBoundaries();
            ExponentialCurve();
            DefaultModeDecisions();
            ControlModeDecisions();
            CeilingCapped();
            UnreachableControl();
            ForcedDecisions();
            HeatingIntents();
            CoolingRecovery();
            LoadAndRebuild();
        }

        private static float C(float celsius)
        {
            return Temperatures.FromCelsius(celsius);
        }

        /// <summary>按固定主材料上限合成温区；上限以**摄氏度**给出。</summary>
        public static ResolvedZone Resolve(float floorCelsius, float bestMinCelsius, float bestMaxCelsius, float ceilingCelsius)
        {
            return TempZoneResolver.Resolve(
                "test.aluminum",
                C(floorCelsius),
                TempZone.Celsius(bestMinCelsius, bestMaxCelsius),
                ConstantCeiling(C(ceilingCelsius)));
        }

        /// <summary>用默认模式在给定温度下走一步状态机。</summary>
        public static RunDecision Step(float celsius, RunPolicy policy)
        {
            return RunController.Update(C(celsius), AluminumZone, policy, new RunState { Started = true });
        }

        /// <summary>用默认模式在给定温度下走一步状态机（未开工）。</summary>
        public static RunDecision StepCold(float celsius, RunPolicy policy)
        {
            return RunController.Update(C(celsius), AluminumZone, policy, RunState.Fresh);
        }

        private static void ZoneBoundaries()
        {
            TestRunner.Section("炼铝温区（建筑下限 + 配方最佳区 + 材料上限）");
            TestRunner.CheckClose(C(80f), AluminumZone.Min, Tol, "建筑最低工作温度 80 ℃");
            TestRunner.CheckClose(C(450f), AluminumZone.BestMin, Tol, "配方最佳温区下限 450 ℃");
            TestRunner.CheckClose(C(650f), AluminumZone.BestMax, Tol, "配方最佳温区上限 650 ℃");
            TestRunner.CheckClose(IgneousCeilingKelvin, AluminumZone.Max, 0.01f, "火成岩上限 = 1683 K × 0.75");
            TestRunner.Check(AluminumZone.ControlRangeFull, "火成岩可覆盖整个最佳区");

            TestRunner.CheckEqual(TempState.BelowMin, TempZoneEvaluator.Evaluate(C(79.9f), AluminumZone), "低于 80 ℃ 停止");
            TestRunner.CheckEqual(TempState.LowEfficiency, TempZoneEvaluator.Evaluate(C(80f), AluminumZone), "80 ℃ 低温低效");
            TestRunner.CheckEqual(TempState.Normal, TempZoneEvaluator.Evaluate(C(450f), AluminumZone), "450 ℃ 进入最佳温区");
            TestRunner.CheckEqual(TempState.Normal, TempZoneEvaluator.Evaluate(C(650f), AluminumZone), "650 ℃ 仍在最佳温区");
            TestRunner.CheckEqual(TempState.HighEfficiency, TempZoneEvaluator.Evaluate(C(650.1f), AluminumZone), "650 ℃ 以上高温低效");
            TestRunner.CheckEqual(TempState.HighEfficiency, TempZoneEvaluator.Evaluate(C(989.1f), AluminumZone), "989.1 ℃ 等于建筑上限，仍在范围内");
            TestRunner.CheckEqual(TempState.AboveMax, TempZoneEvaluator.Evaluate(C(989.2f), AluminumZone), "超过建筑上限停止");
        }

        private static void ExponentialCurve()
        {
            TestRunner.Section("温度效率（最佳区 100%，低温段到 80 ℃ 衰减到 0.3）");
            TestRunner.CheckClose(0.3f, TempZoneEvaluator.Efficiency(C(80f), AluminumZone), 0.00001f, "80 ℃ = 0.3");
            TestRunner.CheckClose(1f, TempZoneEvaluator.Efficiency(C(450f), AluminumZone), 0.00001f, "450 ℃ = 1");
            TestRunner.CheckClose(1f, TempZoneEvaluator.Efficiency(C(650f), AluminumZone), 0.00001f, "650 ℃ = 1");
            TestRunner.CheckClose(0.8f, TempZoneEvaluator.Efficiency(C(989.1f), AluminumZone), 0.0001f, "建筑上限处 = 0.8");

            // 低温段中点（265 ℃）：比例 0.5 → 0.3^0.5。
            TestRunner.CheckClose(0.5477226f, TempZoneEvaluator.Efficiency(C(265f), AluminumZone), 0.00001f, "265 ℃ ≈ 0.547723");
            // 高温段中点：(650 + 989.1) / 2 = 819.55 ℃。
            TestRunner.CheckClose(0.8944272f, TempZoneEvaluator.Efficiency(C(819.55f), AluminumZone), 0.00001f, "819.55 ℃ ≈ 0.894427");
        }

        private static void DefaultModeDecisions()
        {
            TestRunner.Section("默认模式生产许可（未开工时看启动线，开工后看生产下限）");
            RunPolicy policy = RunPolicy.DefaultMode;

            RunDecision below = Step(70f, policy);
            TestRunner.Check(!below.AllowsProduction, "低于建筑下限不允许生产");
            TestRunner.CheckClose(0.3f, below.Efficiency, Tol, "越低温保持低温侧边界效率，不归零");

            RunDecision low = Step(300f, policy);
            TestRunner.Check(low.AllowsProduction, "低温段允许生产");
            TestRunner.Check(low.Efficiency > 0.3f && low.Efficiency < 1f, "低温段效率下降");

            TestRunner.Check(Step(550f, policy).AllowsProduction, "最佳温区允许生产");
            TestRunner.CheckClose(1f, Step(550f, policy).Efficiency, Tol, "最佳温区效率 100%");
            TestRunner.Check(Step(700f, policy).AllowsProduction, "高于最佳上限、仍在建筑范围内允许生产");
            TestRunner.CheckEqual(TempState.HighEfficiency, Step(700f, policy).State, "高于最佳上限属于高温段");

            RunDecision above = Step(1000f, policy);
            TestRunner.Check(!above.AllowsProduction, "高于建筑上限不允许生产");
            TestRunner.CheckClose(0.8f, above.Efficiency, Tol, "越高温保持高温侧边界效率");

            // 启动线与暂停线读数：原公式比例 0.1，按建筑上限封顶。
            TestRunner.CheckClose(C(117f), StartThreshold.OriginalDefault(AluminumZone), Tol, "默认原启动目标 = 80 + 0.1 × 370 = 117 ℃");
            TestRunner.CheckClose(C(470f), StartThreshold.OriginalControl(AluminumZone), Tol, "控制原启动目标 = 450 + 0.1 × 200 = 470 ℃");
            TestRunner.CheckClose(C(117f), StartThreshold.Start(AluminumZone, policy), Tol, "默认实际启动目标 117 ℃");
            TestRunner.CheckClose(C(80f), StartThreshold.WorkLine(AluminumZone, policy), Tol, "默认生产下限 80 ℃");
            TestRunner.CheckClose(IgneousCeilingKelvin, StartThreshold.PauseLine(AluminumZone, policy), 0.01f, "默认高温暂停线 = 建筑上限");
        }

        private static void ControlModeDecisions()
        {
            TestRunner.Section("控制模式生产许可（只在配方最佳区 ∩ 建筑范围内生产）");
            RunPolicy policy = RunPolicy.ControlModePolicy;

            TestRunner.Check(!Step(300f, policy).AllowsProduction, "低温段不允许生产");
            TestRunner.Check(Step(550f, policy).AllowsProduction, "最佳温区允许生产");
            TestRunner.Check(!Step(700f, policy).AllowsProduction, "高于最佳上限不允许生产");
            TestRunner.Check(!Step(1000f, policy).AllowsProduction, "高于建筑上限不允许生产");

            TestRunner.CheckClose(C(450f), StartThreshold.WorkLine(AluminumZone, policy), Tol, "控制生产下限 = B₁ = 450 ℃");
            TestRunner.CheckClose(C(650f), StartThreshold.PauseLine(AluminumZone, policy), Tol, "控制高温暂停线 = B₂ = 650 ℃");
            TestRunner.CheckClose(C(470f), StartThreshold.Start(AluminumZone, policy), Tol, "控制实际启动目标 470 ℃");
        }

        private static void CeilingCapped()
        {
            TestRunner.Section("建材上限优先限制加热目标（V0.1.2 §2.3）");

            // 砂岩：1200 K × 0.75 = 900 K = 626.85 ℃，落在最佳区内部（B 情况）。
            ResolvedZone sandstone = Resolve(80f, 450f, 650f, 626.85f);
            TestRunner.Check(sandstone.ControlRangeLimited, "砂岩上限落在最佳区内部，属于最佳温区受限");
            TestRunner.CheckClose(C(470f), StartThreshold.OriginalControl(sandstone), Tol, "控制原目标仍是 470 ℃");
            TestRunner.CheckClose(C(470f), StartThreshold.Start(sandstone, RunPolicy.ControlModePolicy), Tol, "原目标低于上限时不封顶");
            TestRunner.CheckClose(C(626.85f), sandstone.ControlMax, 0.01f, "控制可用上限取建筑上限");
            TestRunner.Check(!StartThreshold.Start(sandstone, RunPolicy.ControlModePolicy).Equals(0f), "实际目标有效");

            // 原目标高于上限：必须封顶，且加热与开工判断使用同一个受限目标。
            ResolvedZone capped = Resolve(80f, 450f, 650f, 460f);
            TestRunner.CheckClose(C(470f), StartThreshold.OriginalControl(capped), Tol, "控制原目标 470 ℃");
            TestRunner.CheckClose(C(460f), StartThreshold.Start(capped, RunPolicy.ControlModePolicy), Tol, "实际目标被封顶到 460 ℃");
            TestRunner.CheckClose(C(460f), HeatingPlanner.PreheatTarget(capped, RunPolicy.ControlModePolicy), Tol, "预热目标使用同一个受限目标");
            TestRunner.CheckClose(C(460f), HeatingPlanner.TopUpStopLine(capped, RunPolicy.ControlModePolicy), Tol, "补热停止线同样封顶");

            // 默认模式的原目标也可能超过上限（低熔点建材）。
            ResolvedZone lowCeiling = Resolve(80f, 450f, 650f, 100f);
            TestRunner.CheckClose(C(100f), StartThreshold.Start(lowCeiling, RunPolicy.DefaultMode), Tol, "默认实际目标也按上限封顶");

            // 高恢复线必须落在暂停线以下、生产下限以上，不能倒置。
            TestRunner.Check(StartThreshold.ResumeLine(capped, RunPolicy.ControlModePolicy) <= StartThreshold.PauseLine(capped, RunPolicy.ControlModePolicy),
                "窄交集时恢复线不高于暂停线");
            TestRunner.Check(StartThreshold.ResumeLine(capped, RunPolicy.ControlModePolicy) >= StartThreshold.WorkLine(capped, RunPolicy.ControlModePolicy),
                "窄交集时恢复线不低于生产下限");
            TestRunner.Check(StartThreshold.ResumeLine(capped, RunPolicy.ControlModePolicy) <= StartThreshold.Start(capped, RunPolicy.ControlModePolicy),
                "窄交集时恢复线不高于实际启动目标（恢复后能开工）");

            // 单点交集：B₁ = U，交集只有一个温度点。
            ResolvedZone point = Resolve(80f, 450f, 650f, 450f);
            TestRunner.Check(point.HasControlRange, "单点交集仍算有可用交集");
            TestRunner.Check(!point.ControlRangeUnreachable, "单点交集不得误报完全无交集");
            TestRunner.Check(!point.ControlRangeLimited, "B₁ = U 只在一点相交，不作为「受限」区间提示");
            TestRunner.CheckClose(C(450f), point.ControlMax, Tol, "单点交集的控制上限就是该点");
            TestRunner.CheckClose(C(450f), StartThreshold.Start(point, RunPolicy.ControlModePolicy), Tol, "单点交集的实际目标就是该点");
            TestRunner.CheckClose(C(450f), HeatingPlanner.TopUpLine(point, RunPolicy.ControlModePolicy), Tol, "单点交集补热启动线不越出区间");
            TestRunner.CheckClose(C(450f), StartThreshold.ResumeLine(point, RunPolicy.ControlModePolicy), Tol, "单点交集的恢复线就是该点（回差为 0）");

            // 在该点本身允许控制模式生产；略低一点就只预热不生产。
            RunDecision atPoint = RunController.Update(C(450f), point, RunPolicy.ControlModePolicy, RunState.Fresh);
            TestRunner.Check(atPoint.AllowsProduction, "单点交集处允许控制模式生产");
            TestRunner.CheckEqual(TempState.Normal, atPoint.State, "单点交集处属于最佳温区");
            RunDecision belowPoint = RunController.Update(C(440f), point, RunPolicy.ControlModePolicy, atPoint.Next);
            TestRunner.Check(!belowPoint.AllowsProduction, "低于单点交集不生产");
        }

        private static void UnreachableControl()
        {
            TestRunner.Section("最佳温区不可达（B₁ > U，V0.1.2 §2.4、§3 D 情况）");

            // 花岗岩：942 K × 0.75 = 706.5 K = 433.35 ℃，低于炼铝最佳下限 450 ℃。
            ResolvedZone granite = Resolve(80f, 450f, 650f, 433.35f);
            TestRunner.Check(granite.ControlRangeUnreachable, "花岗岩上限低于最佳下限，控制模式不可达");
            TestRunner.CheckClose(C(433.35f), StartThreshold.Start(granite, RunPolicy.ControlModePolicy), 0.01f, "实际目标封顶到建筑上限");

            RunState state = RunState.Fresh;
            RunDecision heating = RunController.Update(C(300f), granite, RunPolicy.ControlModePolicy, state);
            TestRunner.Check(!heating.AllowsProduction, "不可达时控制模式不生产");
            TestRunner.CheckEqual(HeatingIntent.Preheat, heating.Heating, "仍按既有许可预热到上限");
            TestRunner.Check(!heating.Next.ZoneUnreachableLatched, "尚未到达受限目标时不标记不可达锁定");

            // 超过材料上限：停止注热并进入温度故障；控制交集为空，因此是「温度受限」而不是普通等待。
            RunDecision atCeiling = RunController.Update(C(435f), granite, RunPolicy.ControlModePolicy, heating.Next);
            TestRunner.CheckEqual(HeatingIntent.None, atCeiling.Heating, "超过上限后停止加热");
            TestRunner.Check(!atCeiling.AllowsProduction, "超过上限后生产保持暂停");
            TestRunner.Check(atCeiling.State != TempState.Normal, "超过材料上限不可能落在最佳温区");
            TestRunner.Check(atCeiling.Next.ZoneUnreachableLatched, "状态机标记最佳温区不可达（供状态栏区分受限与等待）");

            // 回落到上限及以下：解除高温等待，重新按控制交集判断——交集为空，仍然不生产。
            RunDecision belowCeiling = RunController.Update(C(430f), granite, RunPolicy.ControlModePolicy, atCeiling.Next);
            TestRunner.Check(!belowCeiling.AllowsProduction, "回落仍不生产（控制无可用温区）");
            TestRunner.Check(belowCeiling.Next.ZoneUnreachableLatched, "不可达锁定在交集为空时持续成立");

            // 默认模式仍可在建筑范围内按低温效率生产，不会被不可达拖住。
            RunDecision defaultLow = RunController.Update(C(300f), granite, RunPolicy.DefaultMode, new RunState { Started = true });
            TestRunner.Check(defaultLow.AllowsProduction, "默认模式仍可在建筑范围内生产");
            TestRunner.CheckEqual(TempState.LowEfficiency, defaultLow.State, "默认模式此时属于低温低效段");
        }

        private static void ForcedDecisions()
        {
            TestRunner.Section("强制运行");
            RunPolicy forced = RunPolicy.ControlModePolicy.WithForced(true);
            TestRunner.Check(forced.Forced && forced.ControlMode, "强制开关独立于模式");

            RunDecision cold = Step(50f, forced);
            TestRunner.Check(cold.AllowsProduction, "强制运行越过低温边界仍生产");
            TestRunner.CheckClose(0.3f, cold.Efficiency, Tol, "越界沿用低温侧边界效率，不归零");

            RunDecision hot = Step(1000f, forced);
            TestRunner.Check(hot.AllowsProduction, "强制运行越过建筑上限仍生产");
            TestRunner.CheckClose(0.8f, hot.Efficiency, Tol, "越界保持高温侧边界效率，不取两侧最低值");

            // 越过建筑上限但仍在配方最佳区内：不能误报高温低效。
            ResolvedZone capped = Resolve(80f, 450f, 650f, 500f);
            RunDecision inBest = RunController.Update(C(520f), capped, RunPolicy.DefaultMode.WithForced(true), RunState.Fresh);
            TestRunner.Check(inBest.AllowsProduction, "强制运行越过建筑上限仍生产");
            TestRunner.CheckEqual(TempState.Normal, inBest.State, "仍在配方最佳区内");
            TestRunner.CheckClose(1f, inBest.Efficiency, Tol, "最佳区内效率 100%，不因超过建筑上限降为高温低效");

            RunDecision passthrough = RunController.Update(C(500f), ResolvedZone.Unconfigured, RunPolicy.DefaultMode, RunState.Fresh);
            TestRunner.Check(passthrough.AllowsProduction, "未配置温区时判断恒通过");
            TestRunner.CheckClose(1f, passthrough.Efficiency, Tol, "未配置温区时效率恒为 1");
            TestRunner.Check(!passthrough.ZoneConfigured, "未配置温区标记为未匹配");
        }

        private static void HeatingIntents()
        {
            TestRunner.Section("预热与补热意图");
            RunPolicy control = RunPolicy.ControlModePolicy;

            TestRunner.CheckClose(C(470f), HeatingPlanner.PreheatTarget(AluminumZone, control), Tol, "控制模式预热到 470 ℃");
            TestRunner.CheckClose(C(460f), HeatingPlanner.TopUpLine(AluminumZone, control), Tol, "控制补热启动线 460 ℃");
            TestRunner.CheckClose(C(470f), HeatingPlanner.TopUpStopLine(AluminumZone, control), Tol, "控制补热停止线 470 ℃");

            TestRunner.CheckEqual(HeatingIntent.Preheat,
                HeatingPlanner.Decide(C(100f), AluminumZone, RunPolicy.DefaultMode, false), "默认模式未开工且低于 117 ℃ → 预热");
            TestRunner.CheckEqual(HeatingIntent.None,
                HeatingPlanner.Decide(C(120f), AluminumZone, RunPolicy.DefaultMode, false), "达到 117 ℃ → 不加热");
            TestRunner.CheckEqual(HeatingIntent.None,
                HeatingPlanner.Decide(C(100f), AluminumZone, RunPolicy.DefaultMode, true, false), "默认模式开工后不补热");

            TestRunner.CheckEqual(HeatingIntent.Preheat,
                HeatingPlanner.Decide(C(465f), AluminumZone, control, false), "控制模式预热到 470 ℃");
            TestRunner.CheckEqual(HeatingIntent.TopUp,
                HeatingPlanner.Decide(C(455f), AluminumZone, control, true, false), "控制模式生产中低于 460 ℃ → 启动补热");
            TestRunner.CheckEqual(HeatingIntent.None,
                HeatingPlanner.Decide(C(465f), AluminumZone, control, true, false), "回差区间内且未在补热 → 不启动补热");
            TestRunner.CheckEqual(HeatingIntent.TopUp,
                HeatingPlanner.Decide(C(465f), AluminumZone, control, true, true), "回差区间内且正在补热 → 继续补热");
            TestRunner.CheckEqual(HeatingIntent.None,
                HeatingPlanner.Decide(C(471f), AluminumZone, control, true, true), "达到补热停止线 → 停止补热");

            TestRunner.CheckEqual(HeatingIntent.None,
                HeatingPlanner.Decide(C(1000f), AluminumZone, RunPolicy.DefaultMode, false), "超过建筑上限 → 停止内置加热");
            TestRunner.CheckEqual(HeatingIntent.Sustained,
                HeatingPlanner.Decide(C(1000f), AluminumZone, RunPolicy.DefaultMode.WithForced(true), false), "强制运行 → 持续加热");
            TestRunner.CheckEqual(HeatingIntent.Sustained,
                HeatingPlanner.Decide(C(550f), AluminumZone, control.WithForced(true), true), "强制运行在最佳温区内仍持续加热");
            TestRunner.CheckEqual(HeatingIntent.None,
                HeatingPlanner.Decide(C(550f), ResolvedZone.Unconfigured, RunPolicy.DefaultMode, false), "未配置温区不加热");

            TestRunner.Check(HeatingPlanner.TopUpLine(AluminumZone, control) < HeatingPlanner.TopUpStopLine(AluminumZone, control),
                "补热停止线高于启动线（回差）");
            TestRunner.Check(HeatingPlanner.TopUpLine(AluminumZone, control) >= AluminumZone.BestMin,
                "补热启动线不低于控制生产下限");
        }

        private static void CoolingRecovery()
        {
            TestRunner.Section("高温保护与恢复回差");

            // 默认模式超过建筑上限 → 高温等待，生产与加热都关。
            RunDecision over = RunController.Update(C(1000f), AluminumZone, RunPolicy.DefaultMode, new RunState { Started = true });
            TestRunner.Check(!over.AllowsProduction && over.Next.HighTempWaiting, "默认模式超过建筑上限进入高温等待");
            TestRunner.CheckEqual(HeatingIntent.None, over.Heating, "高温等待停止内置加热");

            // 回落到恢复线以上仍等待。
            RunDecision stillHot = RunController.Update(C(980f), AluminumZone, RunPolicy.DefaultMode, over.Next);
            TestRunner.Check(stillHot.Next.HighTempWaiting && !stillHot.AllowsProduction, "仍在恢复线以上保持等待");

            // 回落到恢复线以下解除等待。
            RunDecision recovered = RunController.Update(C(950f), AluminumZone, RunPolicy.DefaultMode, over.Next);
            TestRunner.Check(!recovered.Next.HighTempWaiting, "回落到恢复线以下解除高温等待");
            TestRunner.Check(recovered.AllowsProduction, "解除后按默认模式恢复生产");

            // 控制模式越过最佳上限：等待降温而不是高温保护。
            RunDecision controlHot = RunController.Update(C(700f), AluminumZone, RunPolicy.ControlModePolicy, new RunState { Started = true });
            TestRunner.Check(controlHot.Next.HighTempWaiting, "控制模式越过最佳上限进入等待");
            TestRunner.Check(!controlHot.Next.ZoneUnreachableLatched, "只是越过最佳上限不算不可达");
            TestRunner.CheckClose(C(640f), StartThreshold.ResumeLine(AluminumZone, RunPolicy.ControlModePolicy), Tol, "控制恢复线 = 650 − 10 = 640 ℃");
        }

        private static void LoadAndRebuild()
        {
            TestRunner.Section("读档与模式切换重算（V0.1.2 §7 第 5 项口径）");

            // 存档里已开工且正在补热：温度仍在停止线以下时保留补热状态。
            RunState saved = new RunState { Started = true, TopUpActive = true };
            RunState loaded = RunController.OnLoaded(C(455f), AluminumZone, RunPolicy.ControlModePolicy, saved);
            TestRunner.Check(loaded.TopUpActive, "读档时温度仍低于补热停止线，保留补热状态");
            TestRunner.Check(loaded.Started, "读档保留开工资格");

            // 新上限更低时不能抢跑生产：温度高于新上限则进入等待。
            ResolvedZone capped = Resolve(80f, 450f, 650f, 460f);
            RunState reloaded = RunController.OnLoaded(C(470f), capped, RunPolicy.ControlModePolicy, new RunState { Started = true });
            TestRunner.Check(reloaded.HighTempWaiting, "读档后按新材料上限重判，超过上限进入等待");
            TestRunner.Check(!reloaded.Started || reloaded.HighTempWaiting, "上限变低时不抢跑生产");

            // 模式切换：保留批次与进度，只重建启动资格与加热意图。
            RunState switched = RunController.OnPolicyChanged(C(470f), capped, RunPolicy.DefaultMode, new RunState { Started = true, TopUpActive = true });
            TestRunner.Check(!switched.TopUpActive, "切到默认模式后清除补热状态（默认模式没有补热阶段）");
            TestRunner.Check(switched.Started, "温度高于默认启动目标时直接取得开工资格");

            RunState toControl = RunController.OnPolicyChanged(C(470f), capped, RunPolicy.ControlModePolicy, new RunState { Started = true });
            TestRunner.Check(toControl.Started, "切到控制模式时温度达到受限实际目标，保留开工资格");

            RunState lowTemp = RunController.OnPolicyChanged(C(300f), capped, RunPolicy.ControlModePolicy, RunState.Fresh);
            TestRunner.Check(!lowTemp.Started, "低于受限实际目标时不具备开工资格（需要先预热）");
            TestRunner.Check(!lowTemp.ZoneUnreachableLatched, "有交集时切模式不标记不可达");
        }
    }
}
