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

        /// <summary>压力上升速度的降低百分比（正数；50 表示 −50%），0 表示不修改。</summary>
        public float StressReductionPercent;

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
    }
}
