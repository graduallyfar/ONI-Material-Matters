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
        MechanicalAndOverheat = 3
    }

    /// <summary>
    /// 损坏原因判定与维修方式选择。纯逻辑，不依赖游戏对象。
    ///
    /// 已确认规则（AGENTS 已确认约束、建筑细节文档 §2.4）：
    ///   - 只有机械损坏时，维修**不重置温度**；包含过热损坏时，按原版过热维修的时机重置温度，
    ///     不是在刚超过过热阈值时立即降温。
    ///   - 普通运行的机械损坏停机后，环境继续升温仍可**追加**过热原因，
    ///     不能假设普通运行永远只有一种原因。
    ///   - 强制运行发生过热损坏时，**无论是否实际产生机械磨损**，都显示两种原因并按过热方式维修；
    ///     双重显示不代表额外制造一份机械扣血。
    /// </summary>
    public static class DamageCauseEvaluator
    {
        /// <summary>
        /// 判定当前损坏原因。
        /// </summary>
        /// <param name="mechanicalDamaged">是否已经存在机械损坏（因硬度不足磨损导致）。</param>
        /// <param name="overheated">当前是否处于过热损坏状态（含损坏停机后由环境升温追加的情形）。</param>
        /// <param name="forcedRun">是否开启了强制运行。</param>
        public static DamageCause Evaluate(bool mechanicalDamaged, bool overheated, bool forcedRun)
        {
            // 强制运行下发生过热损坏即视为双重原因，不要求实际产生机械磨损。
            bool mechanical = mechanicalDamaged || (forcedRun && overheated);

            if (mechanical && overheated)
            {
                return DamageCause.MechanicalAndOverheat;
            }

            if (mechanical)
            {
                return DamageCause.Mechanical;
            }

            return overheated ? DamageCause.Overheat : DamageCause.None;
        }

        /// <summary>显示两种原因。</summary>
        public static bool ShowsBothCauses(DamageCause cause)
        {
            return cause == DamageCause.MechanicalAndOverheat;
        }

        /// <summary>维修方式：只要当前状态包含过热，就采用原版过热维修。</summary>
        public static bool UsesOverheatRepair(DamageCause cause)
        {
            return cause == DamageCause.Overheat || cause == DamageCause.MechanicalAndOverheat;
        }

        /// <summary>
        /// 维修是否重置温度。只有机械损坏时不重置；含过热时按原版过热维修时机重置。
        /// 与 <see cref="UsesOverheatRepair"/> 同义，单独命名是为了让调用点的意图清楚。
        /// </summary>
        public static bool ResetsTemperatureOnRepair(DamageCause cause)
        {
            return UsesOverheatRepair(cause);
        }
    }
}
