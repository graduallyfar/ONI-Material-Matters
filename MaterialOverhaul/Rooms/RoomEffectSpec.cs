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

        /// <summary>压力缓解百分比（正数；50 表示 −50%），0 表示不修改。工作口径为实现为压力变化乘数。</summary>
        public float StressReductionPercent;

        /// <summary>生命值回复速度提高百分比（正数；50 表示 +50%），0 表示不修改。</summary>
        public float HealthRecoveryPercent;

        /// <summary>是否直接挂到房间类型上显示；由特殊触发路径发放的效果为 false。</summary>
        public bool AttachedToRoom = true;

        /// <summary>效果持续时间（秒）。</summary>
        public float DurationSeconds;

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
    }
}
