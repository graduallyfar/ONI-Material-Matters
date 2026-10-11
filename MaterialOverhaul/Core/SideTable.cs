using System.Collections.Generic;

namespace MaterialOverhaul.Core
{
    /// <summary>
    /// 侧表基础设施：本 Mod 自有数据，索引为原版标识（配方标识或建筑 prefab id），
    /// 原版类一字不改，也不参与存档（V0.0.1 开发计划 §4.3、§8.1）。
    /// </summary>
    public sealed class SideTable<TValue>
    {
        private readonly Dictionary<string, TValue> entries = new Dictionary<string, TValue>();
        private readonly string kind;

        /// <param name="kind">侧表名称，用于日志定位（例如“配方温区”“要求硬度”）。</param>
        public SideTable(string kind)
        {
            this.kind = kind;
        }

        public int Count
        {
            get { return entries.Count; }
        }

        public string Kind
        {
            get { return kind; }
        }

        /// <summary>登记一条。重复登记同一索引视为配置错误：保留先登记的值并记录日志。</summary>
        public void Set(string id, TValue value)
        {
            if (string.IsNullOrEmpty(id))
            {
                ModLog.Error(kind + "：索引为空，已忽略该条目。");
                return;
            }

            if (entries.ContainsKey(id))
            {
                ModLog.Error(kind + "：索引重复登记 '" + id + "'，保留先登记的条目。");
                return;
            }

            entries.Add(id, value);
        }

        public bool Contains(string id)
        {
            return !string.IsNullOrEmpty(id) && entries.ContainsKey(id);
        }

        public bool TryGet(string id, out TValue value)
        {
            value = default(TValue);
            return !string.IsNullOrEmpty(id) && entries.TryGetValue(id, out value);
        }

        /// <summary>
        /// 取配置；未匹配时返回 fallback 并记录一次可定位日志。
        /// V0.0.1 开发计划 §9：未匹配不得静默失效，也不允许每次模拟更新重复刷日志。
        /// </summary>
        public TValue GetOrLogMissing(string id, TValue fallback)
        {
            if (TryGet(id, out TValue value))
            {
                return value;
            }

            ModLog.WarnOnce(kind + "|missing|" + id, kind + "：未匹配到 '" + id + "'，该对象退化为原版行为。");
            return fallback;
        }
    }
}
