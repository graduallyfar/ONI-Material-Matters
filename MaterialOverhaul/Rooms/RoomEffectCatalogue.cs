using System;

namespace MaterialOverhaul.Rooms
{
    /// <summary>
    /// 已实装房间效果的数值目录。
    ///
    /// 数值来自 [[4. 房间系统设计]] §4.1：只登记真正会发放的效果；未接入的压力以外的属性、
    /// 特质专项、生产效率等不在此占位。持续时间 600 秒（1 周期）与「压力缓解百分比」
    /// 是 D08 未确认前的工作初值，确认后先回写分设计再改这里。
    /// </summary>
    public static class RoomEffectCatalogue
    {
        /// <summary>D08 工作初值：效果持续 1 周期。</summary>
        public const float DefaultDurationSeconds = 600f;

        private static readonly RoomEffectSpec[] AllValue =
        {
            new RoomEffectSpec
            {
                Id = RoomEffectIds.ArtificialParkMorale,
                StringToken = "ARTIFICIAL_PARK_MORALE",
                Morale = 2f,
                DurationSeconds = DefaultDurationSeconds
            },
            new RoomEffectSpec
            {
                Id = RoomEffectIds.ShowerRelief,
                StringToken = "SHOWER_RELIEF",
                StressReductionPercent = 10f,
                DurationSeconds = DefaultDurationSeconds
            },
            new RoomEffectSpec
            {
                Id = RoomEffectIds.BanquetRelief,
                StringToken = "BANQUET_RELIEF",
                StressReductionPercent = 10f,
                DurationSeconds = DefaultDurationSeconds
            },
            new RoomEffectSpec
            {
                Id = RoomEffectIds.HospitalRecovery,
                StringToken = "HOSPITAL_RECOVERY",
                HealthRecoveryPercent = 20f,
                DurationSeconds = 0f
            },
            new RoomEffectSpec
            {
                Id = RoomEffectIds.MedicalCenterRecovery,
                StringToken = "MEDICAL_CENTER_RECOVERY",
                HealthRecoveryPercent = 50f,
                DurationSeconds = 0f
            },
            new RoomEffectSpec
            {
                Id = RoomEffectIds.DanceHallMorale,
                StringToken = "DANCE_HALL_MORALE",
                Morale = 2f,
                DurationSeconds = DefaultDurationSeconds
            },
            new RoomEffectSpec
            {
                Id = RoomEffectIds.ArcadeHallMorale,
                StringToken = "ARCADE_HALL_MORALE",
                Morale = 2f,
                DurationSeconds = DefaultDurationSeconds
            },
            new RoomEffectSpec
            {
                Id = RoomEffectIds.BeachParadiseMorale,
                StringToken = "BEACH_PARADISE_MORALE",
                Morale = 2f,
                DurationSeconds = DefaultDurationSeconds
            },
            new RoomEffectSpec
            {
                Id = RoomEffectIds.GalleryMorale,
                StringToken = "GALLERY_MORALE",
                Morale = 2f,
                DurationSeconds = DefaultDurationSeconds
            },
            new RoomEffectSpec
            {
                Id = RoomEffectIds.WindTunnelMorale,
                StringToken = "WIND_TUNNEL_MORALE",
                Morale = 2f,
                DurationSeconds = DefaultDurationSeconds
            },
            new RoomEffectSpec
            {
                Id = RoomEffectIds.SpaMorale,
                StringToken = "SPA_MORALE",
                Morale = 2f,
                StressReductionPercent = 50f,
                DurationSeconds = DefaultDurationSeconds
            },
            new RoomEffectSpec
            {
                Id = RoomEffectIds.TeaRoomMorale,
                StringToken = "TEA_ROOM_MORALE",
                Morale = 2f,
                StressReductionPercent = 30f,
                DurationSeconds = DefaultDurationSeconds
            }
        };

        public static RoomEffectSpec[] All
        {
            get { return AllValue; }
        }

        public static RoomEffectSpec Get(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            for (int i = 0; i < AllValue.Length; i++)
            {
                if (string.Equals(AllValue[i].Id, id, StringComparison.Ordinal))
                {
                    return AllValue[i];
                }
            }

            return null;
        }
    }
}
