using System.Collections.Generic;
using MaterialOverhaul.Rooms;

namespace MaterialOverhaul.Game
{
    /// <summary>
    /// 平均温度／装饰度的独立更新渠道。
    ///
    /// 原版只在拓扑变化（拆墙、建门、建筑增减）时重建房间；气体温度与装饰度变化不会触发。
    /// 本类按固定间隔抽查可能需要环境条件的房间，重新判定房间类型；只有类型变化时才调用
    /// <c>RoomProber.UpdateRoom</c>，避免每次采样都重建房间和触发建筑事件。
    ///
    /// 抽查范围：中立房间、本 Mod 房间、被本 Mod 覆盖的原版房间、以及带附加环境条件的原版房间。
    /// 其余原版房间不受环境影响，不参与抽查。
    /// </summary>
    public static class RoomEnvironmentMonitor
    {
        private const float Interval = 5f;

        private static readonly List<Room> Buffer = new List<Room>();
        private static readonly HashSet<string> WatchedIds = BuildWatchedIds();
        private static float elapsed;

        public static void Tick(float dt)
        {
            elapsed += dt;
            if (elapsed < Interval)
            {
                return;
            }

            elapsed = 0f;
            if (!RoomRegistration.Registered)
            {
                return;
            }

            global::Game game = global::Game.Instance;
            RoomProber prober = game == null ? null : game.roomProber;
            if (prober == null)
            {
                return;
            }

            Buffer.Clear();
            Buffer.AddRange(prober.rooms);

            for (int i = 0; i < Buffer.Count; i++)
            {
                Room room = Buffer[i];
                if (room == null || room.cavity == null || room.cavity.room != room || !Watched(room.roomType))
                {
                    continue;
                }

                RoomType current = room.roomType;
                RoomType next = Db.Get().RoomTypes.GetRoomType(room);
                if (next != current)
                {
                    prober.UpdateRoom(room.cavity);
                }
            }

            Buffer.Clear();
        }

        private static bool Watched(RoomType type)
        {
            if (type == null)
            {
                return true;
            }

            return type.Id == "Neutral" || WatchedIds.Contains(type.Id);
        }

        private static HashSet<string> BuildWatchedIds()
        {
            HashSet<string> ids = new HashSet<string>(System.StringComparer.Ordinal);
            RoomDefinition[] definitions = RoomCatalogue.NewRooms;
            for (int i = 0; i < definitions.Length; i++)
            {
                ids.Add(definitions[i].Id);
                if (definitions[i].Beats != null)
                {
                    for (int j = 0; j < definitions[i].Beats.Length; j++)
                    {
                        ids.Add(definitions[i].Beats[j]);
                    }
                }
            }

            RoomDefinition[] modifications = RoomCatalogue.Modifications;
            for (int i = 0; i < modifications.Length; i++)
            {
                ids.Add(modifications[i].ExistingRoomId);
            }

            return ids;
        }
    }
}
