using UnityEngine;

namespace MaterialOverhaul.Game
{
    /// <summary>直接读取选中电网已维护的电线名单长度，不扫描全网或另设全局计数。</summary>
    internal static class PowerWireCount
    {
        internal static bool TryGet(GameObject target, out int count)
        {
            count = 0;
            var game = global::Game.Instance;
            var manager = game?.electricalConduitSystem;
            if (target == null || manager == null || manager.IsDirty || game.circuitManager == null) return false;
            ElectricalUtilityNetwork network;
            ICircuitConnected endpoint = target.GetComponent<ICircuitConnected>();
            if (endpoint != null)
            {
                // 沿用原版信息卡的选网优先级，包括变压器和虚拟接电对象。
                ushort id = game.circuitManager.GetCircuitID(endpoint);
                if (id == ushort.MaxValue) return true; // 未连接电网，数量为0。
                network = manager.GetNetworkByID(id) as ElectricalUtilityNetwork;
                if (network == null) return false;
            }
            else
            {
                Wire wire = target.GetComponent<Wire>();
                if (wire == null) return false;
                if (wire.IsDisconnected()) return true;
                network = manager.GetNetworkForCell(wire.GetNetworkCell()) as ElectricalUtilityNetwork;
                if (network == null) return true;
            }
            // 原版AddItem仅将建成Wire加入allWires；电桥、建筑和规划线不会加入。
            count = network.allWires.Count;
            return true;
        }
    }
}
