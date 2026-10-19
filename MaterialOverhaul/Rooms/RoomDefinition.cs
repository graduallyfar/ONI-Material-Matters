using System;

namespace MaterialOverhaul.Rooms
{
    /// <summary>
    /// 一个新增房间或一个对原版房间的改造定义。
    ///
    /// 只描述构成与环境条件，不引用游戏类型；游戏接入层据此构造 <c>RoomType</c>
    /// 与原版附加条件。效果（士气、压力、效率等）在对应模块实现后单独接入，
    /// 未实装的效果不写入显示文本，避免把设计目标显示成已生效。
    /// </summary>
    public sealed class RoomDefinition
    {
        /// <summary>稳定房间 id；新增房间使用 <c>MO_</c> 前缀。</summary>
        public string Id;

        public RoomTier Tier;

        /// <summary>原版 <c>RoomTypeCategory</c> 字段名，如 Bathroom、Park、Industrial。</summary>
        public string CategoryKey;

        public string NameKey;

        public string DescriptionKey;

        public string TooltipKey;

        /// <summary>专属设施 prefab id 列表（满足其一即可作为主建筑）。</summary>
        public string[] PrimaryPrefabIds;

        public RoomRequirement[] Additional;

        /// <summary>需求的 DLC id；为空表示本体可用。</summary>
        public string[] RequiredDlcIds;

        /// <summary>本房间优先于哪些原版房间 id（例如舞厅优先于原版娱乐室）。</summary>
        public string[] Beats;

        public int Priority;

        public int SortKey;

        /// <summary>若不为空，表示这是对既有原版房间的附加条件改造，不新建房间类型。</summary>
        public string ExistingRoomId;

        /// <summary>已实装的原版 Effect id；留空表示效果尚未接入。</summary>
        public string[] EffectIds;

        public bool IsModification
        {
            get { return !string.IsNullOrEmpty(ExistingRoomId); }
        }

        public bool UsesEnvironment
        {
            get
            {
                if (Additional == null)
                {
                    return false;
                }

                for (int i = 0; i < Additional.Length; i++)
                {
                    if (Additional[i].UsesEnvironment)
                    {
                        return true;
                    }
                }

                return false;
            }
        }
    }
}
