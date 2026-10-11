using MaterialOverhaul.Core;

namespace MaterialOverhaul.Hardness
{
    /// <summary>
    /// 要求硬度侧表：索引为建筑 prefab id，值为原版语义化硬度分级。
    ///
    /// 原版 <c>Element.hardness</c> 是 byte，分级常量在 <c>GameUtil.Hardness</c>（int）：
    /// VERY_SOFT=0、SOFT=10、FIRM=25、VERY_FIRM=50、NEARLY_IMPENETRABLE=150、
    /// SUPER_DUPER_HARD=200、RADIOACTIVE_MATERIALS=251、IMPENETRABLE=255。
    /// 要求硬度直接使用这些分级，**不引入新刻度**（V0.0.1 开发计划 §6.1）。
    ///
    /// 名单与数值均属待定项（§6.4、§10 第 9 项）：未匹配的建筑**不产生机械磨损**，
    /// 行为等同原版（§6.2），因此这里不记未匹配日志——名单外建筑是正常情况，不是异常。
    /// </summary>
    public static class HardnessRequirementTable
    {
        private static readonly SideTable<byte> Table = new SideTable<byte>("要求硬度");

        public static int Count
        {
            get { return Table.Count; }
        }

        /// <param name="requiredHardness">原版硬度分级值，取值范围 0..255。</param>
        public static void Register(string buildingId, int requiredHardness)
        {
            if (requiredHardness < 0 || requiredHardness > byte.MaxValue)
            {
                ModLog.Error("要求硬度：'" + buildingId + "' 的分级值 " + requiredHardness
                    + " 超出 byte 范围 0..255，已忽略该条目。");
                return;
            }

            Table.Set(buildingId, (byte)requiredHardness);
        }

        public static bool TryGet(string buildingId, out byte requiredHardness)
        {
            return Table.TryGet(buildingId, out requiredHardness);
        }
    }
}
