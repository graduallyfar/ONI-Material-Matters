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
        /// <summary>最低可工作温度处的效率。暂定值，数值阶段可能调整。</summary>
        public const float LowBoundaryEfficiency = 0.3f;

        /// <summary>最高可工作温度处的效率。暂定值，数值阶段可能调整。</summary>
        public const float HighBoundaryEfficiency = 0.8f;

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
        /// 温度效率 η_T。
        ///
        /// 分段结构已确认：低温段随温度上升递增、最佳段恒为 1、高温段随温度上升递减；
        /// 边界条件为暂定值：最低可工作温度处 LowBoundaryEfficiency(0.3)、
        /// 最高可工作温度处 HighBoundaryEfficiency(0.8)。
        ///
        /// **占位说明：** 总体设计给出的是指数形式，各段系数与转换关系尚未定案。
        /// 这里先用线性插值把分段结构固定下来，属于**占位实现**，不是最终曲线，
        /// 数值阶段确定后只改本函数，不影响判断层与调用方。
        /// </summary>
        public static float Efficiency(TempState state, float bodyTemp, TempZone zone)
        {
            switch (state)
            {
                case TempState.Disabled:
                case TempState.Normal:
                    return 1f;

                case TempState.BelowWorkMin:
                    // 强制运行越界时保持低温侧边界效率，不归零、不取两侧最低值。
                    return LowBoundaryEfficiency;

                case TempState.AboveWorkMax:
                    return HighBoundaryEfficiency;

                case TempState.LowEfficiency:
                    return Lerp(LowBoundaryEfficiency, 1f, Ratio(bodyTemp, zone.WorkMin, zone.BestMin));

                case TempState.HighEfficiency:
                    return Lerp(HighBoundaryEfficiency, 1f, 1f - Ratio(bodyTemp, zone.BestMax, zone.WorkMax));

                default:
                    return 1f;
            }
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

        private static float Lerp(float from, float to, float t)
        {
            return from + (to - from) * t;
        }
    }
}
