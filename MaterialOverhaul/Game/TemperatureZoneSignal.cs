using HarmonyLib;
using MaterialOverhaul.Core;

namespace MaterialOverhaul.Game
{
    /// <summary>
    /// 温区输出信号：端口标识与发送口径。
    ///
    /// 设计要求（建筑细节文档 §2.2.3）：输出端监督**当前已锁定配方**的最佳温区——
    /// 有已锁定配方且本体处于其最佳温区时输出绿色，其余情况输出红色；
    /// 没有配方时输出不活动的红信号，不设置第三种空信号。**绿色只代表温度条件满足，不代表正在生产。**
    /// </summary>
    public static class TemperatureZoneSignal
    {
        public static readonly HashedString PortId = new HashedString("MaterialOverhaulTemperatureZone");

        /// <summary>按「是否有已锁定配方且处于最佳温区」发送信号；值未变化时不重复发送。</summary>
        public static void Send(LogicPorts ports, ref int lastValue, bool hasLockedBatch, bool inBestZone)
        {
            if (ports == null)
            {
                return;
            }

            int value = hasLockedBatch && inBestZone ? 1 : 0;
            if (value == lastValue)
            {
                return;
            }

            ports.SendSignal(PortId, value);
            lastValue = value;
        }
    }
}
