using Klei.AI;
using MaterialOverhaul.Core;
using MaterialOverhaul.Rooms;

namespace MaterialOverhaul.Game
{
    /// <summary>
    /// 房间效果的登记与创建。
    ///
    /// 已接入：**士气**（<c>QualityOfLife</c> 属性）与**压力上升速度百分比**
    /// （<c>Db.Get().Amounts.Stress.deltaAttribute</c> 的乘数修正）。效果对象在
    /// <c>Db.Initialize</c> 后置处理里创建并加入 <c>Db.Get().effects</c>，房间类型只保存 id；
    /// 实际发放由原版 <c>RoomType.TriggerRoomEffects</c> 在设施被使用／进入时完成。
    ///
    /// 工作初值（D08 未确认）：持续时间 1 周期（600 秒）；压力百分比按「压力上升速度乘数」
    /// 解释，而不是一次性扣除压力。确认后必须回写分设计并调整这里。
    /// </summary>
    public static class RoomEffects
    {
        private const float Duration = 600f;

        private const string Prefix = "STRINGS.ROOMS.MATERIALOVERHAUL.EFFECTS.";

        public static void Register()
        {
            AddEffect(
                RoomEffectIds.ArtificialParkMorale,
                "ARTIFICIAL_PARK_MORALE",
                2f,
                0f);
            AddEffect(
                RoomEffectIds.ShowerRelief,
                "SHOWER_RELIEF",
                0f,
                10f);
            AddEffect(
                RoomEffectIds.DanceHallMorale,
                "DANCE_HALL_MORALE",
                2f,
                0f);
            AddEffect(
                RoomEffectIds.ArcadeHallMorale,
                "ARCADE_HALL_MORALE",
                2f,
                0f);
            AddEffect(
                RoomEffectIds.BeachParadiseMorale,
                "BEACH_PARADISE_MORALE",
                2f,
                0f);
            AddEffect(
                RoomEffectIds.GalleryMorale,
                "GALLERY_MORALE",
                2f,
                0f);
            AddEffect(
                RoomEffectIds.WindTunnelMorale,
                "WIND_TUNNEL_MORALE",
                2f,
                0f);
            AddEffect(
                RoomEffectIds.SpaMorale,
                "SPA_MORALE",
                2f,
                50f);
            AddEffect(
                RoomEffectIds.TeaRoomMorale,
                "TEA_ROOM_MORALE",
                2f,
                30f);
        }

        private static void AddEffect(string id, string stringToken, float morale, float stressReductionPercent)
        {
            if (Db.Get().effects.TryGet(id) != null)
            {
                return;
            }

            string name = Strings.Get(Prefix + stringToken + ".NAME");
            string tooltip = Strings.Get(Prefix + stringToken + ".TOOLTIP");
            Effect effect = new Effect(
                id,
                name,
                tooltip,
                Duration,
                true,
                false,
                false);

            if (morale != 0f)
            {
                effect.Add(new AttributeModifier(Db.Get().Attributes.QualityOfLife.Id, morale, name));
            }

            if (stressReductionPercent != 0f)
            {
                effect.Add(new AttributeModifier(
                    Db.Get().Amounts.Stress.deltaAttribute.Id,
                    -stressReductionPercent / 100f,
                    name,
                    is_multiplier: true));
            }

            Db.Get().effects.Add(effect);
            ModLog.Info("房间效果：" + id + "（" + name + "）士气 " + Signed(morale)
                + "、压力上升速度 " + (stressReductionPercent == 0f ? "不变" : "−" + stressReductionPercent.ToString("0") + "%")
                + "，持续 " + Duration.ToString("0") + " 秒（工作初值，D08 未确认）。");
        }

        private static string Signed(float value)
        {
            return (value >= 0f ? "+" : string.Empty) + value.ToString("0");
        }
    }
}
