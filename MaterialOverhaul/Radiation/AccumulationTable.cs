using System.Collections.Generic;

namespace MaterialOverhaul.Radiation
{
    /// <summary>每格累积表的四份累积量。</summary>
    public enum AccumulationLayer
    {
        /// <summary>格子里的物质：气体、液体或天然固体（建造砖块记在砖块自己身上）。</summary>
        Cell = 0,

        /// <summary>液体管里的物质。</summary>
        LiquidPipe = 1,

        /// <summary>气体管里的物质。</summary>
        GasPipe = 2,

        /// <summary>地上的物品（容器里的不累积，容器当屏蔽）。</summary>
        Items = 3
    }

    /// <summary>
    /// 每格累积表：每个格子记四份累积量，每份同时记下「现在是什么物质」（身份），物质一换就清零。
    /// 流动的物质因此在被替换时丢掉累积；静止的物质慢慢累积。
    ///
    /// 内存里按层用定长数组保存（读写快），存档时只写出有累积量的格子。
    /// 存档键 = 格子编号 × 4 + 层；身份 0 表示该份没有物质。
    /// </summary>
    public sealed class AccumulationTable
    {
        public const int LayerCount = 4;

        private readonly float[][] values = new float[LayerCount][];
        private readonly int[][] identities = new int[LayerCount][];

        public AccumulationTable(int cellCount)
        {
            CellCount = cellCount > 0 ? cellCount : 0;
            for (int layer = 0; layer < LayerCount; layer++)
            {
                values[layer] = new float[CellCount];
                identities[layer] = new int[CellCount];
            }
        }

        public int CellCount { get; private set; }

        public bool IsValid(int cell)
        {
            return cell >= 0 && cell < CellCount;
        }

        public float Get(int cell, AccumulationLayer layer)
        {
            return IsValid(cell) ? values[(int)layer][cell] : 0f;
        }

        public int IdentityOf(int cell, AccumulationLayer layer)
        {
            return IsValid(cell) ? identities[(int)layer][cell] : 0;
        }

        /// <summary>该格任一份有累积量。</summary>
        public bool HasAny(int cell)
        {
            if (!IsValid(cell))
            {
                return false;
            }

            return values[0][cell] > 0f || values[1][cell] > 0f || values[2][cell] > 0f || values[3][cell] > 0f;
        }

        /// <summary>
        /// 按当前物质身份取累积量：身份变了（物质被替换、流走、变成真空）就清零。
        /// 身份 0 表示没有物质，同样清零。
        /// </summary>
        public float Reconcile(int cell, AccumulationLayer layer, int identity)
        {
            if (!IsValid(cell))
            {
                return 0f;
            }

            int l = (int)layer;
            if (identity == 0 || identities[l][cell] != identity)
            {
                values[l][cell] = 0f;
                identities[l][cell] = identity == 0 ? 0 : identity;
                return 0f;
            }

            return values[l][cell];
        }

        /// <summary>写入累积量；不大于 0 时清除该份。</summary>
        public void Set(int cell, AccumulationLayer layer, int identity, float value)
        {
            if (!IsValid(cell))
            {
                return;
            }

            int l = (int)layer;
            float clamped = AccumulationModel.Clamp(value);
            if (identity == 0 || !(clamped > 0f))
            {
                values[l][cell] = 0f;
                identities[l][cell] = 0;
                return;
            }

            values[l][cell] = clamped;
            identities[l][cell] = identity;
        }

        /// <summary>按物质身份追加累积量（辐射粒子撞击）；身份不同时先清零。返回新的累积量。</summary>
        public float Add(int cell, AccumulationLayer layer, int identity, float rads)
        {
            if (!IsValid(cell) || identity == 0)
            {
                return 0f;
            }

            float next = AccumulationModel.Deposit(Reconcile(cell, layer, identity), rads);
            Set(cell, layer, identity, next);
            return Get(cell, layer);
        }

        public void Clear(int cell, AccumulationLayer layer)
        {
            if (IsValid(cell))
            {
                values[(int)layer][cell] = 0f;
                identities[(int)layer][cell] = 0;
            }
        }

        /// <summary>有累积量的份数（测试与日志用）。</summary>
        public int CountNonZero()
        {
            int count = 0;
            for (int layer = 0; layer < LayerCount; layer++)
            {
                float[] row = values[layer];
                for (int cell = 0; cell < row.Length; cell++)
                {
                    if (row[cell] > 0f)
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        /// <summary>存档：只写出累积量不低于 <paramref name="minRads"/> 的份。</summary>
        public void Pack(float minRads, out int[] keys, out float[] packedValues, out int[] packedIdentities)
        {
            List<int> keyList = new List<int>();
            List<float> valueList = new List<float>();
            List<int> identityList = new List<int>();
            for (int cell = 0; cell < CellCount; cell++)
            {
                for (int layer = 0; layer < LayerCount; layer++)
                {
                    float value = values[layer][cell];
                    if (value > 0f && value >= minRads && identities[layer][cell] != 0)
                    {
                        keyList.Add(cell * LayerCount + layer);
                        valueList.Add(value);
                        identityList.Add(identities[layer][cell]);
                    }
                }
            }

            keys = keyList.ToArray();
            packedValues = valueList.ToArray();
            packedIdentities = identityList.ToArray();
        }

        /// <summary>读档：写回存档中的累积量；格子超出范围、长度不一致或数值无效的条目跳过。返回写回的份数。</summary>
        public int Unpack(int[] keys, float[] packedValues, int[] packedIdentities)
        {
            if (keys == null || packedValues == null || packedIdentities == null)
            {
                return 0;
            }

            int count = keys.Length;
            if (packedValues.Length < count)
            {
                count = packedValues.Length;
            }

            if (packedIdentities.Length < count)
            {
                count = packedIdentities.Length;
            }

            int loaded = 0;
            for (int i = 0; i < count; i++)
            {
                int key = keys[i];
                if (key < 0)
                {
                    continue;
                }

                int cell = key / LayerCount;
                int layer = key % LayerCount;
                if (!IsValid(cell) || packedIdentities[i] == 0 || !(packedValues[i] > 0f))
                {
                    continue;
                }

                Set(cell, (AccumulationLayer)layer, packedIdentities[i], packedValues[i]);
                loaded++;
            }

            return loaded;
        }

        /// <summary>
        /// 地上物品的身份：由这一格里物品种类的集合决定，与顺序无关；种类变了身份就变。
        /// <paramref name="kindHashes"/> 由调用方去重。没有物品时为 0。
        /// </summary>
        public static int KindIdentity(IList<int> kindHashes)
        {
            if (kindHashes == null || kindHashes.Count == 0)
            {
                return 0;
            }

            unchecked
            {
                uint sum = 0u;
                for (int i = 0; i < kindHashes.Count; i++)
                {
                    uint h = (uint)kindHashes[i];
                    h ^= h >> 16;
                    h *= 0x7feb352du;
                    h ^= h >> 15;
                    h *= 0x846ca68bu;
                    h ^= h >> 16;
                    sum += h;
                }

                int identity = (int)sum;
                return identity == 0 ? 1 : identity;
            }
        }

        /// <summary>元素的身份（原版元素哈希）；为 0 时改用 1，避免与「没有物质」混淆。</summary>
        public static int ElementIdentity(int elementHash)
        {
            return elementHash == 0 ? 1 : elementHash;
        }
    }
}
