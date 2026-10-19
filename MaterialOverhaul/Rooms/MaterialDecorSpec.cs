namespace MaterialOverhaul.Rooms
{
    /// <summary>一项材料装饰度倍率配置。</summary>
    public sealed class MaterialDecorSpec
    {
        /// <summary><c>SimHashes</c> 成员名；不是元素的项目不进入本目录。</summary>
        public string SimHash;

        /// <summary>装饰度倍率百分比（25 表示 +25%，-50 表示 −50%）。</summary>
        public float Percent;
    }
}
