using System;

namespace MaterialOverhaul.Rooms
{
    /// <summary>
    /// 六项娱乐偏好特质与对应房间加成的映射（房间系统设计 §4.1.3）。
    ///
    /// 这些特质在目标游戏中没有同名定义，由本 Mod 创建为正面特质并加入原版正面特质池
    /// （2026-10-27 确认），与原版正面特质一起随机抽取；特质持有者在对应房间内获得专用效果。
    /// 同时拥有两项相关特质时取映射表中的第一项，不叠加。
    /// </summary>
    public static class RoomTraitCatalogue
    {
        /// <summary>
        /// 加入正面特质池时的稀有度，对应原版 <c>DUPLICANTSTATS.RARITY_COMMON</c>（1）。
        /// 暂定（2026-10-27 确认）：特质只在对应房间生效、不提供永久属性，弱于原版正面特质，因此取最低档，
        /// 与较轻的负面特质配对。原版按稀有度先选档、档内均分。
        /// </summary>
        public const int PoolRarity = 1;

        private static readonly RoomTraitSpec[] AllValue =
        {
            new RoomTraitSpec
            {
                TraitId = "MO_Superstar",
                StringToken = "SUPERSTAR",
                RoomId = "MO_DanceHall",
                EffectId = RoomEffectIds.TraitSuperstar
            },
            new RoomTraitSpec
            {
                TraitId = "MO_EsportsPro",
                StringToken = "ESPORTS",
                RoomId = "MO_ArcadeHall",
                EffectId = RoomEffectIds.TraitEsports
            },
            new RoomTraitSpec
            {
                TraitId = "MO_BeachBum",
                StringToken = "BEACH",
                RoomId = "MO_BeachParadise",
                EffectId = RoomEffectIds.TraitBeach
            },
            new RoomTraitSpec
            {
                TraitId = "MO_Adventurer",
                StringToken = "ADVENTURER",
                RoomId = "MO_BeachParadise",
                EffectId = RoomEffectIds.TraitAdventurer
            },
            new RoomTraitSpec
            {
                TraitId = "MO_ArtistTrait",
                StringToken = "ARTIST",
                RoomId = "MO_Gallery",
                EffectId = RoomEffectIds.TraitArtist
            },
            new RoomTraitSpec
            {
                TraitId = "MO_RocketExpert",
                StringToken = "ROCKET",
                RoomId = "MO_WindTunnel",
                EffectId = RoomEffectIds.TraitRocket
            }
        };

        public static RoomTraitSpec[] All
        {
            get { return AllValue; }
        }

        public static RoomTraitSpec Get(string traitId)
        {
            if (string.IsNullOrEmpty(traitId))
            {
                return null;
            }

            for (int i = 0; i < AllValue.Length; i++)
            {
                if (string.Equals(AllValue[i].TraitId, traitId, StringComparison.Ordinal))
                {
                    return AllValue[i];
                }
            }

            return null;
        }

        public static RoomTraitSpec FirstForRoom(string roomId)
        {
            if (string.IsNullOrEmpty(roomId))
            {
                return null;
            }

            for (int i = 0; i < AllValue.Length; i++)
            {
                if (string.Equals(AllValue[i].RoomId, roomId, StringComparison.Ordinal))
                {
                    return AllValue[i];
                }
            }

            return null;
        }

        public static int Count
        {
            get { return AllValue.Length; }
        }
    }
}
