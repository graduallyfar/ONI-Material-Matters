using System;

namespace MaterialOverhaul.Rooms
{
    /// <summary>
    /// 已实装房间效果的数值目录。
    ///
    /// 数值来自 [[4. 房间系统设计]] §4.1：只登记真正会发放的效果；未接入的压力以外的属性、
    /// 特质专项、生产效率等不在此占位。持续时间 600 秒（1 周期）是 D08 未确认前的工作初值。
    /// 压力本身是百分数，压力缓解按加减法结算（V1.0.2 #21）：−30% 即每周期压力 −30 个百分点。
    /// 生命回复是房间加成，按最后结算的连乘口径（V1.0.2 #22）：护理员加成接在房间加成之后再乘。
    /// </summary>
    public static class RoomEffectCatalogue
    {
        /// <summary>D08 工作初值：效果持续 1 周期。</summary>
        public const float DefaultDurationSeconds = 600f;

        /// <summary>原版一个周期的秒数。</summary>
        public const float CycleSeconds = 600f;

        public const float HospitalPercent = 25f;

        public const float MedicalCenterPercent = 50f;

        public const float CaregiverPercent = 25f;

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
                HealthRecoveryPercent = HospitalPercent,
                DurationSeconds = 0f
            },
            new RoomEffectSpec
            {
                Id = RoomEffectIds.MedicalCenterRecovery,
                StringToken = "MEDICAL_CENTER_RECOVERY",
                HealthRecoveryPercent = MedicalCenterPercent,
                DurationSeconds = 0f
            },
            new RoomEffectSpec
            {
                // 医院内：护理员 ×1.25 接在医院 ×1.25 之后，原版乘数之和需再加 31.25%。
                Id = RoomEffectIds.CaregiverBoost,
                StringToken = "CAREGIVER_BOOST",
                HealthRecoveryPercent = CaregiverPercent,
                HealthModifierPercent = ChainedPercent(HospitalPercent, CaregiverPercent),
                DurationSeconds = 0f,
                AttachedToRoom = false
            },
            new RoomEffectSpec
            {
                // 医疗中心内：护理员 ×1.25 接在医疗中心 ×1.5 之后，原版乘数之和需再加 37.5%。
                Id = RoomEffectIds.CaregiverBoostMedicalCenter,
                StringToken = "CAREGIVER_BOOST",
                HealthRecoveryPercent = CaregiverPercent,
                HealthModifierPercent = ChainedPercent(MedicalCenterPercent, CaregiverPercent),
                DurationSeconds = 0f,
                AttachedToRoom = false
            },
            new RoomEffectSpec
            {
                Id = RoomEffectIds.TraitSuperstar,
                StringToken = "TRAIT_SUPERSTAR",
                Morale = 4f,
                StressReductionPercent = 20f,
                AttributeId = "Athletics",
                AttributeBonus = 3f,
                DurationSeconds = DefaultDurationSeconds,
                AttachedToRoom = false
            },
            new RoomEffectSpec
            {
                Id = RoomEffectIds.TraitEsports,
                StringToken = "TRAIT_ESPORTS",
                Morale = 4f,
                StressReductionPercent = 20f,
                AttributeId = "Machinery",
                AttributeBonus = 3f,
                DurationSeconds = DefaultDurationSeconds,
                AttachedToRoom = false
            },
            new RoomEffectSpec
            {
                Id = RoomEffectIds.TraitBeach,
                StringToken = "TRAIT_BEACH",
                Morale = 4f,
                StressReductionPercent = 20f,
                AttributeId = "Digging",
                AttributeBonus = 3f,
                DurationSeconds = DefaultDurationSeconds,
                AttachedToRoom = false
            },
            new RoomEffectSpec
            {
                Id = RoomEffectIds.TraitAdventurer,
                StringToken = "TRAIT_ADVENTURER",
                Morale = 4f,
                StressReductionPercent = 20f,
                AttributeId = "Construction",
                AttributeBonus = 3f,
                DurationSeconds = DefaultDurationSeconds,
                AttachedToRoom = false
            },
            new RoomEffectSpec
            {
                Id = RoomEffectIds.TraitArtist,
                StringToken = "TRAIT_ARTIST",
                Morale = 4f,
                StressReductionPercent = 20f,
                AttributeId = "Art",
                AttributeBonus = 3f,
                DurationSeconds = DefaultDurationSeconds,
                AttachedToRoom = false
            },
            new RoomEffectSpec
            {
                Id = RoomEffectIds.TraitRocket,
                StringToken = "TRAIT_ROCKET",
                Morale = 4f,
                StressReductionPercent = 20f,
                AttributeId = "SpaceNavigation",
                AttributeBonus = 3f,
                DurationSeconds = DefaultDurationSeconds,
                AttachedToRoom = false
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
                StressReductionPercent = 40f,
                DurationSeconds = DefaultDurationSeconds
            },
            new RoomEffectSpec
            {
                Id = RoomEffectIds.TeaRoomMorale,
                StringToken = "TEA_ROOM_MORALE",
                Morale = 2f,
                StressReductionPercent = 30f,
                DurationSeconds = DefaultDurationSeconds
            },
            // 医疗中心辐射拯救者的永久后遗症（D12 工作口径）：持续到被移除，随存档保存。
            // 施治者医疗越高越轻（V1.0.2 #17），−6 沿用原 id 以兼容旧存档，−5～−1 各一档。
            RadiationScar(-6),
            RadiationScar(-5),
            RadiationScar(-4),
            RadiationScar(-3),
            RadiationScar(-2),
            RadiationScar(-1)
        };

        private static RoomEffectSpec RadiationScar(int athletics)
        {
            return new RoomEffectSpec
            {
                Id = RoomEffectIds.RadiationScarFor(athletics),
                StringToken = "RADIATION_SCAR",
                AttributeId = "Athletics",
                AttributeBonus = athletics,
                DurationSeconds = 0f,
                Permanent = true,
                IsBad = true,
                AttachedToRoom = false
            };
        }

        /// <summary>护理员加成接在房间加成之后连乘时，需要追加到原版乘数之和里的百分比。</summary>
        public static float ChainedPercent(float roomPercent, float caregiverPercent)
        {
            return RoomAdvancedEffects.ChainBonus(roomPercent / 100f, caregiverPercent / 100f) * 100f - roomPercent;
        }

        /// <summary>每周期压力缓解百分点换算为压力变化率（每秒，负值为下降）。</summary>
        public static float StressDeltaPerSecond(float percentPerCycle)
        {
            return -percentPerCycle / CycleSeconds;
        }

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
