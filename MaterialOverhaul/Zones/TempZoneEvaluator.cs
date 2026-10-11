using System;
using MaterialOverhaul.Core;

namespace MaterialOverhaul.Zones
{
    /// <summary>
    /// 温区判断与温度效率。机制层，只依赖 float 与 TempZone，不依赖任何游戏对象
    /// （V0.0.1 开发计划 §2.1、§3.5），因此可以脱离游戏单独检查。
    ///
    /// 不复用原版温度组件的状态机：原版越界状态会执行角色死亡逻辑，
    /// 建筑的越界后果是停止生产，不是销毁对象（§3.4）。
    /// </summary>
    public static class TempZoneEvaluator
    {
        /// <summary>最低可工作温度处的效率。本版暂定值（见 <see cref="Balance.LowBoundaryEfficiency"/>）。</summary>
        public const float LowBoundaryEfficiency = Balance.LowBoundaryEfficiency;

        /// <summary>最高可工作温度处的效率。本版暂定值（见 <see cref="Balance.HighBoundaryEfficiency"/>）。</summary>
        public const float HighBoundaryEfficiency = Balance.HighBoundaryEfficiency;

        /// <summary>
        /// 按建筑本体温度判断五态。区间开闭严格按 §3.1 五态表，不使用周围气体温度。
        /// </summary>
        public static TempState Evaluate(float bodyTemp, TempZone zone)
        {
            if (zone.IsDisabled)
            {
                return TempState.Disabled;
            }

            if (bodyTemp < zone.WorkMin)
            {
                return TempState.BelowWorkMin;
            }

            if (bodyTemp < zone.BestMin)
            {
                return TempState.LowEfficiency;
            }

            if (bodyTemp <= zone.BestMax)
            {
                return TempState.Normal;
            }

            if (bodyTemp <= zone.WorkMax)
            {
                return TempState.HighEfficiency;
            }

            return TempState.AboveWorkMax;
        }

        /// <summary>
        /// 温度效率 η_T：已确认的指数分段（V0.0.3 开发计划 §2.2）。
        ///
        /// <code>
        /// η_T(T) = 0.3 ^ ((BestMin − T) / (BestMin − WorkMin))    WorkMin ≤ T &lt; BestMin
        ///        = 1                                              BestMin ≤ T ≤ BestMax
        ///        = 0.8 ^ ((T − BestMax) / (WorkMax − BestMax))    BestMax &lt; T ≤ WorkMax
        /// </code>
        ///
        /// 写成归一化温差而不是绝对温度，是为了避免绝对开尔文参与大指数。
        /// 验算点：375 ℃≈0.547723、725 ℃≈0.894427；300／450／650／800 ℃分别为 0.3／1／1／0.8。
        ///
        /// 越界取值：低于最低可工作温度保持低温侧边界效率、高于最高可工作温度保持高温侧边界效率，
        /// 两者都**不归零**——普通运行靠生产许可停机，强制运行沿用边界效率继续生产。
        /// 温度效率只乘到进度速度上，不改变投入与产出，也不改变产物质量。
        /// </summary>
        public static float Efficiency(TempState state, float bodyTemp, TempZone zone)
        {
            switch (state)
            {
                case TempState.Disabled:
                case TempState.Normal:
                    return 1f;

                case TempState.BelowWorkMin:
                    return LowBoundaryEfficiency;

                case TempState.AboveWorkMax:
                    return HighBoundaryEfficiency;

                case TempState.LowEfficiency:
                    // 0.3 ^ ratio，ratio 从 1（在 WorkMin）降到 0（在 BestMin）。
                    return Power(LowBoundaryEfficiency, 1f - Ratio(bodyTemp, zone.WorkMin, zone.BestMin), LowBoundaryEfficiency);

                case TempState.HighEfficiency:
                    // 0.8 ^ ratio，ratio 从 0（在 BestMax）升到 1（在 WorkMax）。
                    return Power(HighBoundaryEfficiency, Ratio(bodyTemp, zone.BestMax, zone.WorkMax), HighBoundaryEfficiency);

                default:
                    return 1f;
            }
        }

        /// <summary>指数效率：base 的 exponent 次方。段宽退化（exponent 不可用）时返回 fallback。</summary>
        private static float Power(float baseValue, float exponent, float fallback)
        {
            if (float.IsNaN(exponent) || float.IsInfinity(exponent))
            {
                return fallback;
            }

            double value = Math.Pow(baseValue, exponent);
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                return fallback;
            }

            return (float)value;
        }

        /// <summary>
        /// 结合运行模式与强制运行开关给出完整结论。
        ///
        /// 模式只改变「是否允许生产」，不改变效率曲线：
        ///   默认模式 —— 整个可工作温区内允许生产；
        ///   控制模式 —— 只在最佳温区内允许生产；
        ///   强制运行 —— 无论是否越界都允许生产，效率仍按五态取值，越界保持对应侧边界效率。
        /// </summary>
        public static ProductionDecision Decide(float bodyTemp, TempZone zone, RunPolicy policy)
        {
            if (zone.IsDisabled)
            {
                return ProductionDecision.Passthrough;
            }

            TempState state = Evaluate(bodyTemp, zone);

            ProductionDecision decision = default(ProductionDecision);
            decision.State = state;
            decision.Efficiency = Efficiency(state, bodyTemp, zone);
            decision.ZoneConfigured = true;
            decision.AllowsProduction = policy.Forced
                || (policy.ControlMode ? state == TempState.Normal : state.AllowsProduction());
            return decision;
        }

        /// <summary>效率直接取值的便捷重载。</summary>
        public static float Efficiency(float bodyTemp, TempZone zone)
        {
            return Efficiency(Evaluate(bodyTemp, zone), bodyTemp, zone);
        }

        /// <summary>
        /// 校验四边界；不合法时退化为 Disabled 并记录日志，绝不抛异常
        /// （§3.7：宁可该建筑回到原版行为，也不能因它影响其他建筑）。
        /// </summary>
        public static TempZone Validate(string id, TempZone zone)
        {
            if (zone.IsDisabled)
            {
                return zone;
            }

            if (!zone.IsOrdered)
            {
                ModLog.Error("温区校验失败：'" + id + "' 的四边界不满足升序或含非有限值 " + zone + "，已退化为 Disabled。");
                return TempZone.Disabled;
            }

            if (zone.HasDegenerateSegment)
            {
                ModLog.WarnOnce("zone|degenerate|" + id, "温区提示：'" + id + "' 存在相等的相邻边界 " + zone + "，最佳段退化为一点。");
            }

            return zone;
        }

        private static float Ratio(float value, float min, float max)
        {
            float span = max - min;
            if (span <= 0f)
            {
                return 0f;
            }

            float t = (value - min) / span;
            if (t < 0f)
            {
                return 0f;
            }

            return t > 1f ? 1f : t;
        }
    }
}
