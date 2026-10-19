using System;
using System.Collections.Generic;
using MaterialOverhaul.Core;

namespace MaterialOverhaul.Game
{
    /// <summary>原版电路重建后统计实际电网。界面只读缓存，不触发重建或扫描。</summary>
    internal static class PowerCircuitCount
    {
        private static UtilityNetworkManager<ElectricalUtilityNetwork, Wire> manager;
        private static bool ready;
        private static int count;

        internal static void Reset() { manager = null; ready = false; count = 0; }

        internal static bool TryGet(out int result)
        {
            result = 0;
            if (!ready || manager == null || manager.IsDirty
                || !ReferenceEquals(manager, global::Game.Instance?.electricalConduitSystem)) return false;
            result = count;
            return true;
        }

        internal static void Refresh(CircuitManager circuits, IEnumerable<Generator> generators, IEnumerable<IEnergyConsumer> consumers)
        {
            Reset();
            var current = global::Game.Instance?.electricalConduitSystem;
            if (current == null || circuits == null || current.IsDirty) return;
            try
            {
                var ids = new HashSet<int>();
                // GetNetworks还包含显示网络，不能直接使用Count。
                // 每条已建成线回查其真实物理网络，规划网络不会被单独计数。
                foreach (UtilityNetwork network in current.GetNetworks())
                {
                    var electrical = network as ElectricalUtilityNetwork;
                    if (electrical == null) continue;
                    foreach (Wire wire in electrical.allWires)
                    {
                        if (wire == null || wire.IsDisconnected()) continue;
                        UtilityNetwork physical = current.GetNetworkForCell(wire.GetNetworkCell());
                        if (physical != null) ids.Add(physical.id);
                    }
                }
                // 没有物理电线的虚拟电路也从原版设备归属计入；同一电网只计一次。
                foreach (Generator generator in generators)
                    if (generator != null) Add(ids, circuits.GetCircuitID(generator));
                foreach (IEnergyConsumer consumer in consumers)
                    if (consumer != null) Add(ids, circuits.GetCircuitID(consumer));
                manager = current; count = ids.Count; ready = true;
            }
            catch (Exception e)
            {
                Reset();
                ModLog.WarnOnce("power-circuit-count", "电网数量统计失败，信息卡等待下次电路重建：" + e);
            }
        }

        private static void Add(HashSet<int> ids, ushort id)
        {
            if (id != ushort.MaxValue) ids.Add(id);
        }
    }
}
