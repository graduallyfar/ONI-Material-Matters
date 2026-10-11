using MaterialOverhaul.Core;
using MaterialOverhaul.Hardness;

namespace MaterialOverhaul.Tests
{
    public static class DamageCauseTests
    {
        public static void Run()
        {
            CauseSelection();
            LatchState();
            RepairRules();
            Conversion();
            RealProductionSeconds();
            WearRate();
        }

        private static void LatchState()
        {
            TestRunner.Section("损坏原因锁存（v0.0.3）");
            DamageCauseState state = DamageCauseState.Fresh;
            TestRunner.Check(state.IsClean, "初始无未修复原因");

            state.MechanicalLatched = true;
            TestRunner.CheckEqual(DamageCause.Mechanical, DamageCauseEvaluator.Evaluate(state), "首次机械扣血后记录机械原因");
            TestRunner.Check(!DamageCauseEvaluator.ResetsTemperatureOnRepair(DamageCauseEvaluator.Evaluate(state)), "纯机械维修不重置温度");

            state.OverheatLatched = true;
            TestRunner.CheckEqual(DamageCause.MechanicalAndOverheat, DamageCauseEvaluator.Evaluate(state), "机械停机后追加过热 → 双重原因");
            TestRunner.Check(DamageCauseEvaluator.ResetsTemperatureOnRepair(DamageCauseEvaluator.Evaluate(state)), "含过热按原版维修时机重置温度");

            // 强制运行发生过热：即使硬度合格也要显示两种原因，但不额外扣机械血。
            DamageCauseState forced = DamageCauseState.Fresh;
            forced.OverheatLatched = true;
            forced.ForcedOverheatLatched = true;
            TestRunner.CheckEqual(DamageCause.MechanicalAndOverheat, DamageCauseEvaluator.Evaluate(forced), "强制过热单独存在也显示双重原因");
            TestRunner.Check(DamageCauseEvaluator.ShowsBothCauses(DamageCauseEvaluator.Evaluate(forced)), "强制过热标记为同时显示两种原因");

            // 锁存与当前强制开关无关：关闭开关不能丢原因。
            DamageCause afterSwitchOff = DamageCauseEvaluator.Evaluate(forced);
            TestRunner.CheckEqual(DamageCause.MechanicalAndOverheat, afterSwitchOff, "关闭强制开关后原因不丢");

            // 迁移未知：保留原版维修温度行为，不猜成纯机械。
            DamageCauseState legacy = DamageCauseState.Fresh;
            legacy.LegacyUnknown = true;
            TestRunner.CheckEqual(DamageCause.Unknown, DamageCauseEvaluator.Evaluate(legacy), "旧损伤标为来源未知");
            TestRunner.Check(DamageCauseEvaluator.UsesOverheatRepair(DamageCause.Unknown), "来源未知保留原版维修温度行为");
            TestRunner.Check(!DamageCauseEvaluator.ResetsTemperatureOnRepair(DamageCause.Mechanical), "纯机械不重置温度");

            legacy.Clear();
            TestRunner.Check(legacy.IsClean, "完全修复后清除全部原因");
        }

        private static void RealProductionSeconds()
        {
            TestRunner.Section("真实生产秒数还原（效率 0.3 的时间口径）");
            TestRunner.CheckClose(0.3f, DamageConversion.RealProductionSeconds(0.3f, 1f, 1f), 0.0001f, "效率 1 时原样返回");
            TestRunner.CheckClose(0.6666f, DamageConversion.RealProductionSeconds(0.2f, 0.3f, 5f), 0.001f, "效率 0.3 时 0.2 s 已缩放时间对应约 0.667 s 真实时间");
            TestRunner.CheckClose(0f, DamageConversion.RealProductionSeconds(0f, 0.3f, 5f), 0.0001f, "零时间不还原");
            TestRunner.CheckClose(0f, DamageConversion.RealProductionSeconds(0.2f, 0f, 5f), 0.0001f, "效率为零只可能来自不推进，不累计");
            TestRunner.CheckClose(1f, DamageConversion.RealProductionSeconds(5f, 0.3f, 1f), 0.0001f, "单步结果受上限保护");
        }

        private static void WearRate()
        {
            TestRunner.Section("效率 0.3 下完成同一批的磨损");
            // 同一批 20 s 的「等效满效率工作量」：效率 1 时真实时间 20 s；
            // 效率 0.3 时真实时间约 66.67 s，因此磨损更多。
            WearAccumulator full = new WearAccumulator();
            DamageConversion.AccumulateProduction(full, 20f);
            TestRunner.CheckEqual(1, full.TakeWholeDamage(), "效率 1：20 s 真实时间扣 1 HP");

            WearAccumulator low = new WearAccumulator();
            float scaledPerTick = 0.2f;
            float realTotal = 0f;
            while (realTotal < 66.666f)
            {
                float real = DamageConversion.RealProductionSeconds(scaledPerTick, 0.3f, 5f);
                DamageConversion.AccumulateProduction(low, real);
                realTotal += real;
            }

            TestRunner.Check(low.DamageBalance > 3f, "效率 0.3：完成同一批磨损明显更多（" + low.DamageBalance.ToString("F2") + " HP）");

            // 0.2 s 分步与一次 20 s 必须得到同样的扣血，不能因为浮点累计差一点。
            WearAccumulator stepped = new WearAccumulator();
            for (int i = 0; i < 100; i++)
            {
                DamageConversion.AccumulateProduction(stepped, 0.2f);
            }

            TestRunner.CheckEqual(1, stepped.TakeWholeDamage(), "0.2 s × 100 与一次 20 s 都扣 1 HP");
            TestRunner.CheckClose(0f, stepped.DamageBalance, 0.0001f, "分步累计后余额归零，没有浮点尾巴");

            // 19 s + 1 s 与 20 s 一致，且 19 s 时不提前扣血。
            WearAccumulator split = new WearAccumulator();
            DamageConversion.AccumulateProduction(split, 19f);
            TestRunner.CheckEqual(0, split.TakeWholeDamage(), "19 s 不扣整点");
            DamageConversion.AccumulateProduction(split, 1f);
            TestRunner.CheckEqual(1, split.TakeWholeDamage(), "再 1 s 扣 1 HP");
        }

        private static void Conversion()
        {
            TestRunner.Section("磨损到伤害的转换（v0.0.3 常规配置）");
            TestRunner.CheckClose(0.05f, DamageConversion.DamagePerWearUnit, 0.00001f, "默认 0.05 HP／磨损单位");
            TestRunner.CheckClose(1f, DamageConversion.WearPerProductionSecond, 0.00001f, "默认 1 磨损单位／生产秒");
            TestRunner.Check(DamageConversion.IsConfigured, "默认已配置转换系数");
            TestRunner.CheckClose(1f, DamageConversion.WearUnitsOf(DamageConversion.DamagePerWearUnit), 0.00001f, "余额换算回磨损单位");

            TestRunner.CheckEqual(0, DamageConversion.DamageFor(0), "零磨损不产生伤害");
            TestRunner.CheckEqual(0, DamageConversion.DamageFor(-3), "负磨损不产生伤害");
            TestRunner.CheckEqual(0, DamageConversion.DamageFor(10), "10 个磨损单位不足 1 HP");

            float original = DamageConversion.DamagePerWearUnit;
            DamageConversion.DamagePerWearUnit = 0.5f;
            TestRunner.CheckEqual(5, DamageConversion.DamageFor(10), "按系数折算并向下取整");
            TestRunner.CheckEqual(0, DamageConversion.DamageFor(1), "不足 1 点伤害时返回 0");

            DamageConversion.DamagePerWearUnit = 0f;
            TestRunner.Check(!DamageConversion.IsConfigured, "复位后回到未配置");
            DamageConversion.DamagePerWearUnit = original;
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
