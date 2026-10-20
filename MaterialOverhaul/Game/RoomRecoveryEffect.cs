using Klei.AI;
using MaterialOverhaul.Rooms;

namespace MaterialOverhaul.Game
{
    /// <summary>
    /// 医院／医疗中心的治疗恢复效果选择。
    ///
    /// 工作口径（D10，2026-10-20 指定的工作初值）：患者进入诊疗床治疗状态时追加生命回复乘率，
    /// 医院 +25%，医疗中心 +50%，护理员再 +25%；医院与医疗中心互斥，护理员与房间效果叠加；
    /// 离开治疗状态时移除。
    /// </summary>
    public static class RoomRecoveryEffect
    {
        public static string ForRoomType(string roomTypeId)
        {
            if (string.Equals(roomTypeId, "Hospital", System.StringComparison.Ordinal))
            {
                return RoomEffectIds.HospitalRecovery;
            }

            if (string.Equals(roomTypeId, RoomCatalogue.MedicalCenterId, System.StringComparison.Ordinal))
            {
                return RoomEffectIds.MedicalCenterRecovery;
            }

            return null;
        }

        public static bool IsRecoveryRoom(string roomTypeId)
        {
            return ForRoomType(roomTypeId) != null;
        }

        public static void RemoveAll(Effects effects)
        {
            if (effects == null)
            {
                return;
            }

            Remove(effects, RoomEffectIds.HospitalRecovery);
            Remove(effects, RoomEffectIds.MedicalCenterRecovery);
            Remove(effects, RoomEffectIds.CaregiverBoost);
        }

        private static void Remove(Effects effects, string id)
        {
            if (effects.HasEffect(id))
            {
                effects.Remove(id);
            }
        }
    }
}
