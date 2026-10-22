using System.Collections.Generic;

namespace MaterialOverhaul.Radiation
{
    /// <summary>
    /// 活化建筑自身放射的位置索引：格子 → 该格上活化建筑的放射强度之和。
    ///
    /// 用于估算某格受到的辐射里有多少来自活化物本身（不计吸收和距离衰减，是上界），
    /// 计算活化与污染时扣掉，防止活化物互相照射而无法衰减。
    /// </summary>
    public sealed class InducedSourceIndex
    {
        private readonly Dictionary<int, float> byCell = new Dictionary<int, float>();

        public int Count
        {
            get { return byCell.Count; }
        }

        public void Clear()
        {
            byCell.Clear();
        }

        /// <summary>在格子上增加（或减少）放射强度；结果不大于 0 时移除该格。</summary>
        public void Add(int cell, float rads)
        {
            if (cell < 0 || float.IsNaN(rads) || rads == 0f)
            {
                return;
            }

            float current;
            byCell.TryGetValue(cell, out current);
            float next = current + rads;
            if (next > 0.001f)
            {
                byCell[cell] = next;
            }
            else
            {
                byCell.Remove(cell);
            }
        }

        public float At(int cell)
        {
            float value;
            return byCell.TryGetValue(cell, out value) ? value : 0f;
        }

        /// <summary>
        /// 以 <paramref name="cell"/> 为中心、切比雪夫半径 <paramref name="radius"/> 内全部活化放射之和。
        /// 格子按原版行优先编号：cell = y × width + x；不跨越世界左右边界。
        /// </summary>
        public float UpperBound(int cell, int widthInCells, int radius)
        {
            if (byCell.Count == 0 || widthInCells <= 0 || cell < 0)
            {
                return 0f;
            }

            int cx = cell % widthInCells;
            int cy = cell / widthInCells;
            float sum = 0f;
            int span = (2 * radius + 1) * (2 * radius + 1);
            if (byCell.Count < span)
            {
                foreach (KeyValuePair<int, float> entry in byCell)
                {
                    int x = entry.Key % widthInCells;
                    int y = entry.Key / widthInCells;
                    if (System.Math.Abs(x - cx) <= radius && System.Math.Abs(y - cy) <= radius)
                    {
                        sum += entry.Value;
                    }
                }

                return sum;
            }

            for (int dy = -radius; dy <= radius; dy++)
            {
                int y = cy + dy;
                if (y < 0)
                {
                    continue;
                }

                for (int dx = -radius; dx <= radius; dx++)
                {
                    int x = cx + dx;
                    if (x < 0 || x >= widthInCells)
                    {
                        continue;
                    }

                    float value;
                    if (byCell.TryGetValue(y * widthInCells + x, out value))
                    {
                        sum += value;
                    }
                }
            }

            return sum;
        }
    }
}
