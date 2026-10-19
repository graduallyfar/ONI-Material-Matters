using System.Collections.Generic;
using MaterialOverhaul.Rooms;

namespace MaterialOverhaul.Game
{
    /// <summary>
    /// 一次房间判定的快照缓存。
    ///
    /// <c>RoomTypes.GetRoomType</c> 会在一次判定里对多个候选房间类型反复调用
    /// <c>isSatisfactory</c>；若每条环境条件都重新扫描格子，成本会随候选数放大。
    /// 这里按 <see cref="Room"/> 缓存一份快照，并在每次新的房间判定开始时清空。
    /// 缓存不跨模拟步保存，读档或拓扑变化后自然重建。
    /// </summary>
    public static class RoomSnapshotCache
    {
        private static readonly Dictionary<Room, RoomSnapshot> Snapshots = new Dictionary<Room, RoomSnapshot>();

        /// <summary>开始一轮房间判定：丢弃上一轮的快照，避免读到过期环境值。</summary>
        public static void BeginEvaluation()
        {
            Snapshots.Clear();
        }

        public static RoomSnapshot Get(Room room)
        {
            if (room == null)
            {
                return new RoomSnapshot();
            }

            RoomSnapshot snapshot;
            if (!Snapshots.TryGetValue(room, out snapshot))
            {
                snapshot = RoomSnapshotFactory.Create(room);
                Snapshots[room] = snapshot;
            }

            return snapshot;
        }
    }
}
