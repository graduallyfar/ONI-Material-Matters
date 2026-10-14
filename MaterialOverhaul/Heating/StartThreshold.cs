using MaterialOverhaul.Core;
using MaterialOverhaul.Zones;

namespace MaterialOverhaul.Heating
{
    /// <summary>
    /// 各模式下的温控边界线（V0.1.2 开发计划 §2.3）。
    ///
    /// **原目标**沿用旧版已确认公式（比例 0.1），只把最低温度来源改成建筑配置：
    ///   - 默认原目标 `S₀ = L + 0.1 × (B₁ − L)`
    ///   - 控制原目标 `S₁ = B₁ + 0.1 × (B₂ − B₁)`
    ///
    /// **实际启动目标**再按建筑上限封顶（建筑范围优先于配方相关加热目标）：
    ///   - `S = min(对应原启动目标, U)`
    ///
    /// 其余边界：
    ///   - 生产下限（**已开工**后仍可生产的线）：默认模式 L，控制模式 B₁。开工后再跌到这条线以下才重新停机。
    ///   - 高温暂停线：默认模式 U，控制模式 `min(B₂, U)`。
    ///   - 高温恢复线：暂停线减去暂定回差，并**钳制在实际启动目标以下**且不低于生产下限，
    ///     使窄交集与单点交集下不会出现倒置的恢复线（取法仍属 V0.1.2 §7 第 3 项待定项）。
    ///
    /// 封顶不会绕过 B₁：控制模式达到 S 之后仍必须满足最佳区才生产，否则就是「最佳温区不可达」，
    /// 由 <see cref="RunController"/> 给出「温度受限 · 生产暂停」。
    ///
    /// 铝矿配方的验算点（L = 80 ℃、最佳区 450–650 ℃、U 随建材）：
    ///   - 默认原目标 `80 + 0.1 × 370 = 117 ℃`；控制原目标 `450 + 0.1 × 200 = 470 ℃`；
    ///   - 砂岩 U = 626.85 ℃：默认实际目标 117 ℃，控制实际目标 470 ℃（未被封顶）；
    ///   - 火成岩 U = 989.1 ℃：两者都未被封顶。
    /// </summary>
    public static class StartThreshold
    {
        /// <summary>十分之一系数，来自已确认的阈值公式。</summary>
        public const float Fraction = Balance.StartFraction;

        /// <summary>默认模式的**原**启动目标（未按建筑上限封顶）。</summary>
        public static float OriginalDefault(ResolvedZone zone)
        {
            if (!zone.Configured)
            {
                return 0f;
            }

            return zone.Min + Fraction * (zone.BestMin - zone.Min);
        }

        /// <summary>控制模式的**原**启动目标（未按建筑上限封顶）。</summary>
        public static float OriginalControl(ResolvedZone zone)
        {
            if (!zone.Configured)
            {
                return 0f;
            }

            return zone.BestMin + Fraction * (zone.BestMax - zone.BestMin);
        }

        /// <summary>当前模式的**原**启动目标；仅用于显示「原目标 → 实际目标」。</summary>
        public static float Original(ResolvedZone zone, RunPolicy policy)
        {
            return policy.ControlMode ? OriginalControl(zone) : OriginalDefault(zone);
        }

        /// <summary>
        /// 当前模式的实际启动目标：原目标按建筑上限封顶，且不低于建筑下限
        /// （合法配置下原目标本身不会低于下限，这里只是防止异常数据造出倒置目标）。
        /// 加热与开工判断必须使用同一个值，避免「加热停在 U，生产仍等待原先更高启动线」。
        /// </summary>
        public static float Start(ResolvedZone zone, RunPolicy policy)
        {
            float target = Original(zone, policy);

            if (!zone.Configured)
            {
                return target;
            }

            if (target > zone.Max)
            {
                return zone.Max;
            }

            return target < zone.Min ? zone.Min : target;
        }

        /// <summary>当前模式的生产下限：开工后跌破它才重新进入低温停机。</summary>
        public static float WorkLine(ResolvedZone zone, RunPolicy policy)
        {
            return policy.ControlMode ? zone.BestMin : zone.Min;
        }

        /// <summary>当前模式的高温暂停线：超过它暂停生产并关闭普通加热。</summary>
        public static float PauseLine(ResolvedZone zone, RunPolicy policy)
        {
            return policy.ControlMode ? zone.ControlMax : zone.Max;
        }

        /// <summary>
        /// 高温暂停后的恢复线：暂停线减去本版暂定回差。
        ///
        /// 回差的取法属 V0.1.2 §7 第 3 项待定项，本版按「不产生倒置目标、也不越过实际启动目标」的口径处理：
        ///   - 回差最多取「实际启动目标 − 生产下限」，因此在窄交集与单点交集下不会出现
        ///     「恢复线高于暂停线」或「恢复后仍然无法开工」的倒置目标；
        ///   - 单点交集（`B₁ = U`）时回差退化为 0，恢复条件就是该点本身；
        ///   - 不暗中新增安全余量、保温模式或百分比。
        ///
        /// 标准炼铝口径（默认 1262.25 / 117 ℃；控制 650 / 470 / 450 ℃）下回差就是
        /// 默认 15 K、控制 10 K，与旧实现读数一致。
        /// </summary>
        public static float ResumeLine(ResolvedZone zone, RunPolicy policy)
        {
            if (!zone.Configured)
            {
                return 0f;
            }

            float floor = WorkLine(zone, policy);
            float target = Start(zone, policy);

            float margin = policy.ControlMode
                ? Balance.ControlHighRecoveryKelvin
                : Balance.DefaultHighRecoveryKelvin;

            float available = target - floor;
            if (available < 0f)
            {
                available = 0f;
            }

            if (margin > available)
            {
                margin = available;
            }

            float line = PauseLine(zone, policy) - margin;
            return line < floor ? floor : line;
        }
    }
}
