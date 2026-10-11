namespace MaterialOverhaul.Hardness
{
    /// <summary>
    /// 建筑当前的损坏原因。只维护**尚未修复**的状态，用于显示与选择维修方式，
    /// 不增加长期历史伤害统计（AGENTS 已确认约束）。
    /// </summary>
    public enum DamageCause
    {
        None = 0,

        /// <summary>只有机械损坏。</summary>
        Mechanical = 1,

        /// <summary>只有过热损坏。</summary>
        Overheat = 2,

        /// <summary>机械与过热两种原因同时存在。</summary>
        MechanicalAndOverheat = 3,

        /// <summary>
        /// 从旧版本存档迁移过来、无法判定来源的旧损伤。
        ///
        /// 处理方式（V0.0.3 开发计划 §5.3）：不能猜成纯机械（那会阻止原版过热维修），
        /// 因此保留原版维修温度行为，并在界面上如实标为「来源未知」。
        /// </summary>
        Unknown = 4
    }

    /// <summary>
    /// 尚未修复的损坏原因锁存状态。随存档持久化（§5.3：原因不能读档重置）。
    ///
    /// 为什么需要锁存而不是每帧重新判断（§6.2）：
    ///   - 机械原因在**首次实际扣血**时就记录，不等到生命值耗尽；
    ///   - 过热原因一旦成立就保持到完全修复，温度回落不撤销已发生的损坏；
    ///   - 强制运行期间发生过热损坏时，另外锁存一条「附加机械显示原因」，
    ///     即使材料硬度合格也要同时显示两种原因——这只是显示，不额外制造机械伤害；
    ///   - 不能用「当前强制开关 && 曾经过热」每次临时计算历史原因：
    ///     关闭强制开关不能丢原因，也不能伪造一次强制过热事件。
    /// </summary>
    public struct DamageCauseState
    {
        /// <summary>已发生过机械磨损扣血。</summary>
        public bool MechanicalLatched;

        /// <summary>已发生过热损坏。</summary>
        public bool OverheatLatched;

        /// <summary>强制运行期间发生过热损坏 → 附加机械显示原因。</summary>
        public bool ForcedOverheatLatched;

        /// <summary>来自旧存档、无法判定来源的旧损伤。</summary>
        public bool LegacyUnknown;

        /// <summary>没有任何尚未修复的原因。</summary>
        public bool IsClean
        {
            get { return !MechanicalLatched && !OverheatLatched && !ForcedOverheatLatched && !LegacyUnknown; }
        }

        public static DamageCauseState Fresh
        {
            get { return default(DamageCauseState); }
        }

        /// <summary>完全修复后清除全部原因（不清机械磨损小数余额，那在 <see cref="WearAccumulator"/> 里）。</summary>
        public void Clear()
        {
            MechanicalLatched = false;
            OverheatLatched = false;
            ForcedOverheatLatched = false;
            LegacyUnknown = false;
        }

        public override string ToString()
        {
            return "DamageCauseState[机械 " + MechanicalLatched + "，过热 " + OverheatLatched
                + "，强制过热 " + ForcedOverheatLatched + "，迁移未知 " + LegacyUnknown + "]";
        }
    }

    /// <summary>
    /// 损坏原因判定与维修方式选择。纯逻辑，不依赖游戏对象。
    ///
    /// 已确认规则（AGENTS 已确认约束、建筑细节文档 §2.4、V0.0.3 开发计划 §6.2、§6.3）：
    ///   - 只有机械损坏时，维修**不重置温度**；包含过热损坏时，按原版过热维修的时机重置温度，
    ///     不是在刚超过过热阈值时立即降温。
    ///   - 普通运行的机械损坏停机后，环境继续升温仍可**追加**过热原因，
    ///     不能假设普通运行永远只有一种原因。
    ///   - 强制运行发生过热损坏时，**无论是否实际产生机械磨损**，都显示两种原因并按过热方式维修；
    ///     双重显示不代表额外制造一份机械扣血。
    ///   - 来源未知的旧损伤保留原版维修温度行为，不猜成纯机械。
    /// </summary>
    public static class DamageCauseEvaluator
    {
        /// <summary>按锁存状态判定当前损坏原因。</summary>
        public static DamageCause Evaluate(DamageCauseState state)
        {
            // 强制运行下的过热损坏同时锁存了机械显示原因，所以它一并计入机械侧。
            bool mechanical = state.MechanicalLatched || state.ForcedOverheatLatched;
            bool overheated = state.OverheatLatched || state.ForcedOverheatLatched;

            if (mechanical && overheated)
            {
                return DamageCause.MechanicalAndOverheat;
            }

            if (mechanical)
            {
                return DamageCause.Mechanical;
            }

            if (overheated)
            {
                return DamageCause.Overheat;
            }

            return state.LegacyUnknown ? DamageCause.Unknown : DamageCause.None;
        }

        /// <summary>
        /// 便捷重载：<paramref name="forcedOverheat"/> 表示「强制运行期间发生过热损坏」
        /// （与 §6.2 的锁存口径一致，传当前强制开关会把原因算成历史状态，不要那样用）。
        /// </summary>
        public static DamageCause Evaluate(bool mechanicalDamaged, bool overheated, bool forcedOverheat)
        {
            DamageCauseState state = default(DamageCauseState);
            state.MechanicalLatched = mechanicalDamaged;
            state.OverheatLatched = overheated && !forcedOverheat;
            state.ForcedOverheatLatched = overheated && forcedOverheat;
            return Evaluate(state);
        }

        /// <summary>显示两种原因。</summary>
        public static bool ShowsBothCauses(DamageCause cause)
        {
            return cause == DamageCause.MechanicalAndOverheat;
        }

        /// <summary>维修方式：只要当前状态包含过热（或来源未知），就采用原版过热维修。</summary>
        public static bool UsesOverheatRepair(DamageCause cause)
        {
            return cause == DamageCause.Overheat
                || cause == DamageCause.MechanicalAndOverheat
                || cause == DamageCause.Unknown;
        }

        /// <summary>
        /// 维修是否重置温度。只有机械损坏时不重置；含过热时按原版过热维修时机重置；
        /// 来源未知的旧损伤放行原版行为（不猜成纯机械）。
        /// </summary>
        public static bool ResetsTemperatureOnRepair(DamageCause cause)
        {
            return UsesOverheatRepair(cause);
        }
    }
}
