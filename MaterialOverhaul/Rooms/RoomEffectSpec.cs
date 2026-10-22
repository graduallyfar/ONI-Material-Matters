namespace MaterialOverhaul.Rooms
{
    /// <summary>
    /// 一个房间效果要施加的修正。纯数据，不含游戏类型，便于离线核对数值。
    /// </summary>
    public sealed class RoomEffectSpec
    {
        /// <summary>稳定效果 id，与 <see cref="RoomEffectIds"/> 一致。</summary>
        public string Id;

        /// <summary>本地化键片段，游戏接入层按 <c>STRINGS.ROOMS.MATERIALOVERHAUL.EFFECTS.&lt;Token&gt;.*</c> 读取名称与提示。</summary>
        public string StringToken;

        /// <summary>士气修正（<c>QualityOfLife</c>），0 表示不修改。</summary>
        public float Morale;

        /// <summary>压力缓解（正数；30 表示每周期压力 −30 个百分点），0 表示不修改。按加减法结算。</summary>
        public float StressReductionPercent;

        /// <summary>生命值回复速度提高百分比（正数；50 表示 +50%），0 表示不修改。</summary>
        public float HealthRecoveryPercent;

        /// <summary>
        /// 实际写入原版乘数之和的生命回复百分比；0 表示与 <see cref="HealthRecoveryPercent"/> 相同。
        /// 护理员加成接在房间加成之后连乘时，用它补足连乘与相加的差。
        /// </summary>
        public float HealthModifierPercent;

        /// <summary>属性加成 id（例如 Athletics）；为空表示不修改属性。</summary>
        public string AttributeId;

        /// <summary>属性加成数值。</summary>
        public float AttributeBonus;

        /// <summary>是否直接挂到房间类型上显示；由特殊触发路径发放的效果为 false。</summary>
        public bool AttachedToRoom = true;

        /// <summary>效果持续时间（秒）。</summary>
        public float DurationSeconds;

        /// <summary>永久效果：持续时间为 0 且不随状态移除，发放时按存档保存。</summary>
        public bool Permanent;

        /// <summary>负面效果，界面按原版负面效果显示。</summary>
        public bool IsBad;

        public bool HasMorale
        {
            get { return Morale != 0f; }
        }

        public bool HasStressReduction
        {
            get { return StressReductionPercent != 0f; }
        }

        public bool HasHealthRecovery
        {
            get { return HealthRecoveryPercent != 0f; }
        }

        public float EffectiveHealthModifierPercent
        {
            get { return HealthModifierPercent != 0f ? HealthModifierPercent : HealthRecoveryPercent; }
        }

        public bool HasAttributeBonus
        {
            get { return !string.IsNullOrEmpty(AttributeId) && AttributeBonus != 0f; }
        }
    }
}
