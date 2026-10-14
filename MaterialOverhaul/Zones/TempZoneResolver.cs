using MaterialOverhaul.Core;

namespace MaterialOverhaul.Zones
{
    /// <summary>
    /// 把「建筑最低工作温度 L」「配方最佳温区 [B₁, B₂]」「主建造材料决定的上限 U」
    /// 三份来源合成一份可运算的温区（V0.1.2 开发计划 §2.1、§2.3）。
    ///
    /// 三条来源的归属（已确认设计）：
    ///   - **L**：建筑类型，见 <see cref="BuildingZoneTable"/>；
    ///   - **B₁、B₂**：配方，见 <see cref="TempZone"/>；
    ///   - **U**：主建造材料熔点 × 过热系数，见 <see cref="HeatCeilingResolver"/>。
    ///
    /// 有效范围与配方最佳区的关系（不再要求 B₂ ≤ U）：
    ///   - `B₁ ≤ U`：控制模式可用区间为 `[B₁, min(B₂, U)]`，预热目标在该区间内；
    ///   - `B₂ &gt; U`：显示「最佳温区受限」，控制模式只能用到 U；
    ///   - `B₁ &gt; U`：显示「最佳温区不可达」，控制模式没有可用温区，预热到 U 后停止并暂停生产；
    ///   - `B₁ = U`：交集是单点，按受限处理，不误报完全无交集。
    ///
    /// **不在这里做的是**：把 U 裁剪进配方最佳区再当成配方数据保存。U 每次都由当前主建造材料重算，
    /// 换材料立即改变建筑上限，配方最佳区始终不变。
    /// </summary>
    public struct ResolvedZone
    {
        /// <summary>建筑最低工作温度 L。低于它普通模式停止生产并进入预热判断。</summary>
        public float Min;

        /// <summary>配方最佳温区下限 B₁（未做上限裁剪，保留配方原值）。</summary>
        public float BestMin;

        /// <summary>配方最佳温区上限 B₂（未做上限裁剪，保留配方原值）。</summary>
        public float BestMax;

        /// <summary>建筑最高工作温度 U，等于过热阈值。</summary>
        public float Max;

        /// <summary>本温区是否可参与运算；false 表示退化为原版行为。</summary>
        public bool Configured;

        public float LowBoundaryEfficiency;
        public float HighBoundaryEfficiency;
        public bool NoBuiltInHeating;

        public static ResolvedZone Unconfigured
        {
            get { return default(ResolvedZone); }
        }

        /// <summary>普通模式的有效工作范围 `[L, U]` 是否有效。有效范围内恒为 L ≤ T ≤ U。</summary>
        public bool HasWorkRange
        {
            get { return Configured && IsFinite(Min) && IsFinite(Max) && Max > Min; }
        }

        /// <summary>控制模式可用区间上限：`min(B₂, U)`。B₁ &gt; U 时它低于 B₁，表示没有可用交集。</summary>
        public float ControlMax
        {
            get { return BestMax < Max ? BestMax : Max; }
        }

        /// <summary>控制模式是否存在可用温区：要求 `B₁ ≤ U`（B₁ = U 时是单点交集，仍算可用）。</summary>
        public bool HasControlRange
        {
            get { return HasWorkRange && BestMin <= Max; }
        }

        /// <summary>属于 V0.1.2 §3 的 B 情况：最佳区高侧被建筑上限截断，但仍有交集。</summary>
        public bool ControlRangeLimited
        {
            get { return HasControlRange && BestMax > Max && BestMin < Max; }
        }

        /// <summary>属于 V0.1.2 §3 的 D 情况：配方最佳下限高于建筑上限，控制模式无可用温区。</summary>
        public bool ControlRangeUnreachable
        {
            get { return HasWorkRange && BestMin > Max; }
        }

        /// <summary>最佳区是否完整可用（B₂ ≤ U），即 V0.1.2 §3 的 A 情况。</summary>
        public bool ControlRangeFull
        {
            get { return HasControlRange && BestMax <= Max; }
        }

        public override string ToString()
        {
            if (!Configured)
            {
                return "ResolvedZone(Unconfigured)";
            }

            return "ResolvedZone[work " + Min.ToString("F2") + ".." + Max.ToString("F2")
                + ", best " + BestMin.ToString("F2") + ".." + BestMax.ToString("F2")
                + ", control " + BestMin.ToString("F2") + ".." + ControlMax.ToString("F2") + "]";
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    /// <summary>
    /// 温区合成与校验。纯逻辑，不依赖游戏对象，可脱离游戏单独检查。
    ///
    /// 校验分工（V0.1.2 开发计划 §2.1、§7 第 2、5 项）：
    ///   - `B₁ &lt; L` 是**设计阶段的配置错误**，不作为正常玩法分支：这里记录日志并把下限抬到 L，
    ///     使运行时至少不会出现「预热目标低于建筑下限」的倒置目标；
    ///   - `B₁ ≥ B₂` 或非有限值：该配方退化为未配置并记录日志，游戏退回原版行为；
    ///   - L 不可用、U 不可用或 `U ≤ L`：该建筑本次不启用本 Mod 温控，退回原版行为，**不产生无限上限**。
    /// </summary>
    public static class TempZoneResolver
    {
        /// <summary>
        /// 配方层校验：只检查配方自己声明的最佳温区。建筑下限不在这里校验——
        /// 同一配方可能被多栋建筑使用，而 L 属于建筑。
        /// </summary>
        public static TempZone Validate(string recipeId, TempZone zone)
        {
            if (!zone.Configured)
            {
                return zone;
            }

            if (!zone.IsOrdered)
            {
                ModLog.Error("配方温区校验失败：'" + recipeId + "' 的最佳温区 " + zone
                    + " 不是两个有限值且下限小于上限，已退化为未配置（该配方按原版行为运行）。");
                return TempZone.Unconfigured;
            }

            return zone;
        }

        /// <summary>
        /// 合成可运算温区。任何一项来源不可用都返回未配置，调用方据此退回原版行为。
        /// </summary>
        /// <param name="id">用于日志定位的标识（配方标识或“建筑 + 配方”）。</param>
        /// <param name="floorKelvin">建筑最低工作温度 L。</param>
        /// <param name="zone">配方最佳温区。</param>
        /// <param name="ceiling">主建造材料上限取值委托；不可用时返回 false。</param>
        public static ResolvedZone Resolve(string id, float floorKelvin, TempZone zone, HeatCeilingResolver ceiling, float lowEfficiency = Balance.LowBoundaryEfficiency,
            float highEfficiency = Balance.HighBoundaryEfficiency, bool noBuiltInHeating = false)
        {
            if (!zone.Configured)
            {
                return ResolvedZone.Unconfigured;
            }

            if (float.IsNaN(floorKelvin) || float.IsInfinity(floorKelvin) || floorKelvin <= 0f)
            {
                ModLog.WarnOnce("zone|bad-floor|" + id,
                    "温区：'" + id + "' 的建筑最低工作温度 " + floorKelvin
                    + " K 不可用，该建筑本次按原版行为运行。");
                return ResolvedZone.Unconfigured;
            }

            float ceilingKelvin = 0f;
            if (ceiling == null || !ceiling(out ceilingKelvin)
                || float.IsNaN(ceilingKelvin) || float.IsInfinity(ceilingKelvin))
            {
                // 熔点缺失或不可用：不覆盖原版阈值，也不启用本 Mod 温控（V0.1.2 §7 第 5 项）。
                ModLog.WarnOnce("zone|bad-ceiling|" + id,
                    "温区：'" + id + "' 的主建造材料上限不可用，该建筑本次按原版行为运行（仍保留原版过热保护）。");
                return ResolvedZone.Unconfigured;
            }

            if (ceilingKelvin <= floorKelvin)
            {
                // 特殊建材组合：上限不高于建筑下限，普通范围为空。
                // V0.1.2 §7 第 4 项尚未定案，本版取「退回原版行为」而不是引入无限上限或新玩法分支。
                ModLog.WarnOnce("zone|empty-range|" + id,
                    "温区：'" + id + "' 的建筑上限 " + ceilingKelvin.ToString("F1")
                    + " K 不高于建筑下限 " + floorKelvin.ToString("F1")
                    + " K，本建筑本次按原版行为运行。");
                return ResolvedZone.Unconfigured;
            }

            float bestMin = zone.BestMin;
            if (bestMin < floorKelvin)
            {
                // 合法配置不会出现；出现即为配置错误。抬到 L，避免预热目标低于建筑下限。
                ModLog.WarnOnce("zone|best-below-floor|" + id,
                    "温区：'" + id + "' 的配方最佳下限 " + bestMin.ToString("F1")
                    + " K 低于建筑最低工作温度 " + floorKelvin.ToString("F1")
                    + " K，这是不允许的配置；运行时按建筑下限取值，请修正配置。");
                bestMin = floorKelvin;
            }

            ResolvedZone resolved = default(ResolvedZone);
            resolved.Min = floorKelvin;
            resolved.BestMin = bestMin;
            resolved.BestMax = zone.BestMax;
            resolved.Max = ceilingKelvin;
            resolved.Configured = true;
            resolved.LowBoundaryEfficiency = lowEfficiency;
            resolved.HighBoundaryEfficiency = highEfficiency;
            resolved.NoBuiltInHeating = noBuiltInHeating;

            if (resolved.ControlRangeUnreachable)
            {
                ModLog.WarnOnce("zone|control-unreachable|" + id,
                    "温区：'" + id + "' 的配方最佳下限 " + resolved.BestMin.ToString("F1")
                    + " K 高于建筑上限 " + resolved.Max.ToString("F1")
                    + " K，普通控制模式无可用温区（最佳温区不可达）。");
            }
            else if (resolved.ControlRangeLimited)
            {
                ModLog.WarnOnce("zone|control-limited|" + id,
                    "温区：'" + id + "' 的最佳区高侧被建筑上限截断，控制模式可用区间 "
                    + resolved.BestMin.ToString("F1") + "~" + resolved.ControlMax.ToString("F1") + " K。");
            }

            return resolved;
        }
    }
}
