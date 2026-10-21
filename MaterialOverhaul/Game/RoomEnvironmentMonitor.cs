using System.Collections.Generic;
using MaterialOverhaul.Rooms;

namespace MaterialOverhaul.Game
{
    /// <summary>
    /// 平均温度／装饰度的独立更新渠道。
    ///
    /// 原版只在拓扑变化（拆墙、建门、建筑增减）时重建房间；气体温度与装饰度变化不会触发。
    /// 本类按固定间隔抽查可能需要环境条件的房间；环境平均值没有变化时直接跳过，
    /// 只有环境变化或房间类型变化时才重新判定，类型变化后再调用
    /// <c>RoomProber.UpdateRoom</c>，避免每次采样都重建房间和触发建筑事件。
    ///
    /// 抽查范围：中立房间、本 Mod 房间、被本 Mod 覆盖的原版房间、以及带附加环境条件的原版房间。
    /// 其余原版房间不受环境影响，不参与抽查。
    /// </summary>
    public static class RoomEnvironmentMonitor
    {
        private const float Interval = 5f;

        private const float TemperatureTolerance = 0.01f;

        private const float DecorTolerance = 0.01f;

        private struct EnvironmentStamp
        {
            public float TemperatureK;
            public float Decor;
            public int CellCount;
        }

        private static readonly List<Room> Buffer = new List<Room>();
        private static readonly Dictionary<Room, EnvironmentStamp> Stamps =
            new Dictionary<Room, EnvironmentStamp>();
        private static readonly HashSet<string> WatchedIds = BuildWatchedIds();
        private static float elapsed;

        /// <summary>离开存档时丢弃旧档的 Room 引用与计时。</summary>
        public static void Clear()
        {
            Stamps.Clear();
            Buffer.Clear();
            elapsed = 0f;
        }

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

                RoomSnapshot snapshot = RoomSnapshotFactory.Create(room);
                EnvironmentStamp stamp = new EnvironmentStamp
                {
                    TemperatureK = snapshot.AverageTemperatureK,
                    Decor = snapshot.AverageDecor,
                    CellCount = snapshot.CellCount
                };

                EnvironmentStamp previous;
                if (Stamps.TryGetValue(room, out previous) && Same(previous, stamp))
                {
                    continue;
                }

                Stamps[room] = stamp;
                RoomType current = room.roomType;
                RoomType next = Db.Get().RoomTypes.GetRoomType(room);
                if (next != current)
                {
                    prober.UpdateRoom(room.cavity);
                }
            }

            CleanupStamps();
            Buffer.Clear();
        }

        private static bool Same(EnvironmentStamp left, EnvironmentStamp right)
        {
            return left.CellCount == right.CellCount
                && RoomEnvironment.ApproximatelyEqual(left.TemperatureK, right.TemperatureK, TemperatureTolerance)
                && RoomEnvironment.ApproximatelyEqual(left.Decor, right.Decor, DecorTolerance);
        }

        private static void CleanupStamps()
        {
            if (Stamps.Count == 0)
            {
                return;
            }

            List<Room> stale = null;
            HashSet<Room> live = null;
            foreach (KeyValuePair<Room, EnvironmentStamp> entry in Stamps)
            {
                if (entry.Key != null && entry.Key.cavity != null && entry.Key.cavity.room == entry.Key)
                {
                    continue;
                }

                if (stale == null)
                {
                    stale = new List<Room>();
                }

                stale.Add(entry.Key);
            }

            if (stale == null && Stamps.Count > Buffer.Count)
            {
                live = new HashSet<Room>(Buffer);
                stale = new List<Room>();
                foreach (Room room in Stamps.Keys)
                {
                    if (room == null || !live.Contains(room))
                    {
                        stale.Add(room);
                    }
                }
            }

            if (stale == null)
            {
                return;
            }

            for (int i = 0; i < stale.Count; i++)
            {
                if (stale[i] != null)
                {
                    Stamps.Remove(stale[i]);
                }
            }
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
                if (modifications[i].FallbackRoomIds != null)
                {
                    for (int j = 0; j < modifications[i].FallbackRoomIds.Length; j++)
                    {
                        ids.Add(modifications[i].FallbackRoomIds[j]);
                    }
                }
            }

            return ids;
        }
    }
}
