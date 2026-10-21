using System;
using System.Collections.Generic;

namespace MaterialOverhaul.Game
{
    /// <summary>
    /// 房间重建前后的类型延续，供保持回差判定使用。
    ///
    /// 原版拓扑变化（建造、拆除、改墙）时先 <c>ClearRoom</c> 旧房间、再 <c>new Room</c>，
    /// 新对象判定时没有类型，保持回差会失效。这里在清除前按格子记下旧类型，
    /// 新房间判定时取其格子中占多数的旧类型作为「上一类型」；本轮重建结束即丢弃，
    /// 不跨模拟步保存，也不写入存档。
    /// </summary>
    public static class RoomRetention
    {
        private static readonly Dictionary<int, string> PreviousByCell = new Dictionary<int, string>();
        private static readonly Dictionary<string, int> Votes = new Dictionary<string, int>(StringComparer.Ordinal);

        /// <summary>原版清除房间前记录其格子与类型。</summary>
        public static void Remember(Room room)
        {
            if (room == null || room.roomType == null || room.cavity == null || room.cavity.cells == null)
            {
                return;
            }

            string id = room.roomType.Id;
            List<int> cells = room.cavity.cells;
            for (int i = 0; i < cells.Count; i++)
            {
                PreviousByCell[cells[i]] = id;
            }
        }

        /// <summary>新建房间（尚无类型）的上一类型：其格子中占多数的旧类型；没有记录时为 null。</summary>
        public static string PreviousTypeOf(Room room)
        {
            if (PreviousByCell.Count == 0 || room == null || room.cavity == null || room.cavity.cells == null)
            {
                return null;
            }

            Votes.Clear();
            string best = null;
            int bestCount = 0;
            List<int> cells = room.cavity.cells;
            for (int i = 0; i < cells.Count; i++)
            {
                string id;
                if (!PreviousByCell.TryGetValue(cells[i], out id))
                {
                    continue;
                }

                int count;
                Votes.TryGetValue(id, out count);
                count++;
                Votes[id] = count;
                if (count > bestCount)
                {
                    bestCount = count;
                    best = id;
                }
            }

            Votes.Clear();
            return best;
        }

        /// <summary>一轮重建结束：丢弃记录，避免影响之后无关的新房间。</summary>
        public static void EndRebuild()
        {
            PreviousByCell.Clear();
        }
    }
}
