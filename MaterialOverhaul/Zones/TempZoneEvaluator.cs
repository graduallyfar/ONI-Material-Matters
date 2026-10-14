using System;
using MaterialOverhaul.Core;

namespace MaterialOverhaul.Zones
{
    /// <summary>
    /// 温区判断与温度效率（V0.1.2 开发计划 §2.1–§2.3）。机制层，只依赖 float 与
    /// <see cref="ResolvedZone"/>，不依赖任何游戏对象，因此可以脱离游戏单独检查。
    ///
    /// 不复用原版温度组件的状态机：原版越界状态会执行角色死亡逻辑，
    /// 建筑的越界后果是停止生产，不是销毁对象。
    ///
    /// **效率曲线不再以「最高可工作温度」为配方边界**（建筑细节文档 §2.1.4）：
    /// 旧实现用 `[WorkMin, BestMin]`／`[BestMax, WorkMax]` 两段的固定边界效率，
    /// 但 V0.1.2 里材料上限可能落在配方最佳区内部，固定边界效率会与最佳区 100% 冲突。
    /// 新曲线两侧都从 100% 连续衰减到各自的边界底值：
    ///
    /// <code>
    /// η_T(T) = 1                                                   B₁ ≤ T ≤ B₂
    ///        = 0.30 ^ ((B₁ − T) / (B₁ − L))                        L ≤ T &lt; B₁
    ///        = 0.80 ^ ((T − B₂) / (U − B₂))                        B₂ &lt; T ≤ U
    ///        = 0.30（T &lt; L，仅强制运行可达）／0.80（T &gt; U，仅强制运行可达）
    /// </code>
    ///
    /// - 两侧都**不归零**：普通模式靠生产许可停机，强制运行沿用边界底值继续生产。
    /// - **效率只由配方温度关系与建筑范围决定，不随建材变化**——建材只通过 U 改变高温段的终点，
    ///   最佳区内始终 100%，这正是 V0.1.2 「不能误报为高温低效」的要求。
    /// - 温度效率只乘到进度速度上，不改变投入与产出，也不改变产物质量。
    /// </summary>
    public static class TempZoneEvaluator
    {
        /// <summary>低温侧边界效率（在建筑最低工作温度处）。本版暂定值。</summary>
        public const float LowBoundaryEfficiency = Balance.LowBoundaryEfficiency;

        /// <summary>高温侧边界效率（在建筑最高工作温度处）。本版暂定值。</summary>
        public const float HighBoundaryEfficiency = Balance.HighBoundaryEfficiency;

        /// <summary>
        /// 按建筑本体温度判断六态。区间开闭：`T &lt; L` 为 BelowMin；
        /// `B₁ ≤ T ≤ B₂` 为最佳区；`T &gt; U` 为 AboveMax（U 本身仍算在范围内，
        /// 「T = U 不因超过上限规则停机」是 V0.1.2 §2.2 的明确要求）。
        /// </summary>
        public static TempState Evaluate(float bodyTemp, ResolvedZone zone)
        {
            if (!zone.Configured)
            {
                return TempState.Disabled;
            }

            if (bodyTemp < zone.Min)
            {
                return TempState.BelowMin;
            }

            if (bodyTemp < zone.BestMin)
            {
                return TempState.LowEfficiency;
            }

            if (bodyTemp <= zone.BestMax)
            {
                return TempState.Normal;
            }

            if (bodyTemp <= zone.Max)
            {
                return TempState.HighEfficiency;
            }

            return TempState.AboveMax;
        }

        /// <summary>温度效率 η_T。分段与边界见类注释。</summary>
        public static float Efficiency(TempState state, float bodyTemp, ResolvedZone zone)
        {
            switch (state)
            {
                case TempState.Disabled:
                case TempState.Normal:
                    return 1f;

                case TempState.BelowMin:
                    // 只有强制运行会走到这里：保持低温侧底值，不继续下降、不归零。
                    return LowBoundaryEfficiency;

                case TempState.AboveMax:
                    // 只有强制运行会走到这里：保持高温侧底值。
                    return HighBoundaryEfficiency;

                case TempState.LowEfficiency:
                    // 0.3 ^ ratio，ratio 从 1（在 L）降到 0（在 B₁）。
                    return Power(LowBoundaryEfficiency, 1f - Ratio(bodyTemp, zone.Min, zone.BestMin), LowBoundaryEfficiency);

                case TempState.HighEfficiency:
                    // 0.8 ^ ratio，ratio 从 0（在 B₂）升到 1（在 U）。
                    return Power(HighBoundaryEfficiency, Ratio(bodyTemp, zone.BestMax, zone.Max), HighBoundaryEfficiency);

                default:
                    return 1f;
            }
        }

        /// <summary>效率直接取值的便捷重载。</summary>
        public static float Efficiency(float bodyTemp, ResolvedZone zone)
        {
            return Efficiency(Evaluate(bodyTemp, zone), bodyTemp, zone);
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
