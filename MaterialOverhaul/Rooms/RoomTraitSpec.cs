namespace MaterialOverhaul.Rooms
{
    /// <summary>一项目标特质与对应房间加成的映射。</summary>
    public sealed class RoomTraitSpec
    {
        /// <summary>稳定特质 id，创建为原版 <c>Trait</c> 资源。</summary>
        public string TraitId;

        /// <summary>本地化键片段，游戏接入层读取 <c>STRINGS.ROOMS.MATERIALOVERHAUL.TRAITS.&lt;Token&gt;.*</c>。</summary>
        public string StringToken;

        /// <summary>触发该加成的房间类型 id。</summary>
        public string RoomId;

        /// <summary>特质持有者在房间内获得的特殊效果 id。</summary>
        public string EffectId;
    }
}
