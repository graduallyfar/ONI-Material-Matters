using System;
using MaterialOverhaul.Core;
using MaterialOverhaul.Rooms;

namespace MaterialOverhaul.Game
{
    /// <summary>
    /// 建筑与火箭状态栏里的「房间加成」条目。
    ///
    /// 建筑：房间建立（<c>RoomProber.CreateRoom</c>）后对房间内获得加成的建筑挂上，房间清除前摘下；
    /// 建造、拆除与环境重判都会重建房间，因此不需要在模拟更新里扫描建筑。
    /// 火箭：发射登记、助推登记与加成到期时按当前加成刷新；只写公开的基础加成，不写科学翻倍等隐藏机制。
    /// 喷泉：地质研究室乘数变化时刷新，显示实际乘数。
    /// </summary>
    public static class RoomBonusStatus
    {
        private const string Prefix = "STRINGS.ROOMS.MATERIALOVERHAUL.STATUS.";

        public static StatusItem BuildingItem { get; private set; }

        public static StatusItem RocketItem { get; private set; }

        public static StatusItem GeyserItem { get; private set; }

        /// <summary>在 Db 初始化之后登记（Db.Initialize 会重建状态项集合）。</summary>
        public static void Register()
        {
            Db db = Db.Get();
            if (db == null || BuildingItem != null && db.BuildingStatusItems.TryGet(BuildingItem.Id) != null)
            {
                return;
            }

            BuildingItem = new StatusItem("MaterialOverhaulRoomBonus", Strings.Get(Prefix + "BUILDING_BONUS.NAME"),
                Strings.Get(Prefix + "BUILDING_BONUS.TOOLTIP"), string.Empty, StatusItem.IconType.Info,
                NotificationType.Good, allow_multiples: false, OverlayModes.None.ID, showWorldIcon: false);
            BuildingItem.resolveStringCallback = (text, data) => ResolveBuilding(text, data, false);
            BuildingItem.resolveTooltipCallback = (text, data) => ResolveBuilding(text, data, true);
            db.BuildingStatusItems.Add(BuildingItem);

            RocketItem = new StatusItem("MaterialOverhaulRocketRoomBonus", Strings.Get(Prefix + "ROCKET_BONUS.NAME"),
                Strings.Get(Prefix + "ROCKET_BONUS.TOOLTIP"), string.Empty, StatusItem.IconType.Info,
                NotificationType.Good, allow_multiples: false, OverlayModes.None.ID, showWorldIcon: false);
            RocketItem.resolveStringCallback = (text, data) => ResolveRocket(text, data, false);
            RocketItem.resolveTooltipCallback = (text, data) => ResolveRocket(text, data, true);
            db.BuildingStatusItems.Add(RocketItem);

            GeyserItem = new StatusItem("MaterialOverhaulGeyserRoomBonus", Strings.Get(Prefix + "GEYSER_BONUS.NAME"),
                Strings.Get(Prefix + "GEYSER_BONUS.TOOLTIP"), string.Empty, StatusItem.IconType.Info,
                NotificationType.Good, allow_multiples: false, OverlayModes.None.ID, showWorldIcon: false);
            GeyserItem.resolveStringCallback = (text, data) => ResolveGeyser(text, data);
            GeyserItem.resolveTooltipCallback = (text, data) => ResolveGeyser(text, data);
            db.BuildingStatusItems.Add(GeyserItem);
        }

        /// <summary>房间建立后：给房间内获得加成的建筑挂上状态。</summary>
        public static void Apply(Room room)
        {
            if (BuildingItem == null || room == null || room.roomType == null || room.buildings == null)
            {
                return;
            }

            string roomTypeId = room.roomType.Id;
            foreach (KPrefabID building in room.buildings)
            {
                if (building == null)
                {
                    continue;
                }

                string[] lines = RoomText.BuildingBonusLines(roomTypeId, building.PrefabID().ToString());
                if (lines.Length == 0)
                {
                    continue;
                }

                KSelectable selectable = building.GetComponent<KSelectable>();
                if (selectable != null)
                {
                    selectable.RemoveStatusItem(BuildingItem);
                    selectable.AddStatusItem(BuildingItem, new BuildingBonus(room.roomType.Name, lines));
                }
            }
        }

        /// <summary>房间清除前：摘下房间内建筑的加成状态。</summary>
        public static void Remove(Room room)
        {
            if (BuildingItem == null || room == null || room.buildings == null)
            {
                return;
            }

            foreach (KPrefabID building in room.buildings)
            {
                KSelectable selectable = building == null ? null : building.GetComponent<KSelectable>();
                if (selectable != null)
                {
                    selectable.RemoveStatusItem(BuildingItem);
                }
            }
        }

        /// <summary>按火箭当前的房间加成挂上或摘下状态；状态文字每次显示时按实时数值生成。</summary>
        public static void RefreshRocket(Clustercraft craft)
        {
            KSelectable selectable = craft == null || RocketItem == null ? null : craft.GetComponent<KSelectable>();
            if (selectable == null)
            {
                return;
            }

            bool show = RoomSpaceEffects.ClustercraftSpeedBonus(craft) > 0f;
            if (show && !selectable.HasStatusItem(RocketItem))
            {
                selectable.AddStatusItem(RocketItem, craft);
            }
            else if (!show && selectable.HasStatusItem(RocketItem))
            {
                selectable.RemoveStatusItem(RocketItem);
            }
        }

        /// <summary>喷泉受地质研究室加成时挂上「地质研究室：喷发量 ×N」，加成消失时摘下。</summary>
        public static void RefreshGeyser(Geyser geyser)
        {
            KSelectable selectable = geyser == null || GeyserItem == null ? null : geyser.GetComponent<KSelectable>();
            if (selectable == null)
            {
                return;
            }

            bool show = RoomGeoTunerEffects.GeyserBonus(geyser) > 0f;
            if (show && !selectable.HasStatusItem(GeyserItem))
            {
                selectable.AddStatusItem(GeyserItem, geyser);
            }
            else if (!show && selectable.HasStatusItem(GeyserItem))
            {
                selectable.RemoveStatusItem(GeyserItem);
            }
        }

        private static string ResolveGeyser(string text, object data)
        {
            Geyser geyser = data as Geyser;
            return geyser == null
                ? text
                : string.Format(text, (1f + RoomGeoTunerEffects.GeyserBonus(geyser)).ToString("0.##"));
        }

        private static string ResolveBuilding(string text, object data, bool tooltip)
        {
            BuildingBonus bonus = data as BuildingBonus;
            if (bonus == null)
            {
                return text;
            }

            string separator = tooltip ? "\n" : Strings.Get(Prefix + "SEPARATOR");
            return string.Format(text, bonus.RoomName, string.Join(separator, bonus.Lines));
        }

        private static string ResolveRocket(string text, object data, bool tooltip)
        {
            Clustercraft craft = data as Clustercraft;
            if (craft == null)
            {
                return text;
            }

            try
            {
                if (!tooltip)
                {
                    return string.Format(text, RoomText.Percent(RoomSpaceEffects.ClustercraftSpeedBonus(craft)));
                }

                return string.Format(text, RoomText.RocketBonusTooltipLines(
                    RoomSpaceEffects.MissionControlBoostOf(craft), RoomSpaceEffects.IsFromSpaceport(craft)));
            }
            catch (Exception e)
            {
                ModLog.Error("火箭房间加成状态文本生成失败：" + e);
                return text;
            }
        }

        private sealed class BuildingBonus
        {
            public readonly string RoomName;
            public readonly string[] Lines;

            public BuildingBonus(string roomName, string[] lines)
            {
                RoomName = roomName;
                Lines = lines;
            }
        }
    }
}
