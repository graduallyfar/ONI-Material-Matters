using Klei.AI;
using MaterialOverhaul.Core;
using MaterialOverhaul.Rooms;

namespace MaterialOverhaul.Game
{
    /// <summary>
    /// 房间效果的登记与创建。
    ///
    /// 本轮只接入数值与语义明确的**士气**效果（<c>QualityOfLife</c> 属性）。效果对象在
    /// <c>Db.Initialize</c> 后置处理里创建并加入 <c>Db.Get().effects</c>，房间类型只保存 id；
    /// 实际发放由原版 <c>RoomType.TriggerRoomEffects</c> 在设施被使用／进入时完成。
    ///
    /// 持续时间取 1 周期（600 秒）为工作初值：分设计 D08 尚未确认进入／使用触发与持续时间，
    /// 确认后必须回写并调整这里，不能把工作初值当成已确认规则。
    /// </summary>
    public static class RoomEffects
    {
        private const float Duration = 600f;

        private const string Prefix = "STRINGS.ROOMS.MATERIALOVERHAUL.EFFECTS.";

        public static void Register()
        {
            AddMorale(
                RoomEffectIds.ArtificialParkMorale,
                "ARTIFICIAL_PARK_MORALE",
                2f);
            AddMorale(
                RoomEffectIds.DanceHallMorale,
                "DANCE_HALL_MORALE",
                2f);
            AddMorale(
                RoomEffectIds.ArcadeHallMorale,
                "ARCADE_HALL_MORALE",
                2f);
            AddMorale(
                RoomEffectIds.BeachParadiseMorale,
                "BEACH_PARADISE_MORALE",
                2f);
            AddMorale(
                RoomEffectIds.GalleryMorale,
                "GALLERY_MORALE",
                2f);
            AddMorale(
                RoomEffectIds.WindTunnelMorale,
                "WIND_TUNNEL_MORALE",
                2f);
            AddMorale(
                RoomEffectIds.SpaMorale,
                "SPA_MORALE",
                2f);
            AddMorale(
                RoomEffectIds.TeaRoomMorale,
                "TEA_ROOM_MORALE",
                2f);
        }

        private static void AddMorale(string id, string stringToken, float morale)
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
            effect.Add(new AttributeModifier(Db.Get().Attributes.QualityOfLife.Id, morale, name));
            Db.Get().effects.Add(effect);
            ModLog.Info("房间效果：" + id + "（" + name + "）士气 +" + morale.ToString("0")
                + "，持续 " + Duration.ToString("0") + " 秒（工作初值，D08 未确认）。");
        }
    }
}
