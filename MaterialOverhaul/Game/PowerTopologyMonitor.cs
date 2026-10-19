using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using MaterialOverhaul.Core;
using MaterialOverhaul.Power;

namespace MaterialOverhaul.Game
{
    /// <summary>
    /// 只读拓扑服务：在原版重建后拍快照，按需寻路并缓存损耗系数；能量由结算服务处理。
    /// 使用经过目标程序集核对的闭合泛型私有字段，仅取原版真实物理连接。
    /// </summary>
    internal static class PowerTopologyMonitor
    {
        private static UtilityNetworkManager<ElectricalUtilityNetwork, Wire> manager;
        private static bool topologyDirty = true;
        private static readonly Type ManagerType = typeof(UtilityNetworkManager<ElectricalUtilityNetwork, Wire>);
        private static readonly FieldInfo NodesField = AccessTools.Field(ManagerType, "physicalNodes");
        private static readonly FieldInfo ItemsField = AccessTools.Field(ManagerType, "items");
        private static readonly FieldInfo LinksField = AccessTools.Field(ManagerType, "links");
        private static readonly FieldInfo SemiVirtualField = AccessTools.Field(ManagerType, "semiVirtualLinks");
        private static readonly HashSet<int> unsupportedNetworks = new HashSet<int>();
        private static readonly Dictionary<int, int> cellNetworks = new Dictionary<int, int>();
        private static PowerPathCache paths;
        private static PowerLossPathCache losses;
        private static IReadOnlyList<PowerEndpoint> endpoints = new List<PowerEndpoint>().AsReadOnly();
        internal static IReadOnlyList<PowerEndpoint> Endpoints => endpoints;
        internal static int NodeCount { get; private set; }
        internal static int LinkCount { get; private set; }

        internal static void Attach(global::Game game)
        {
            var next = game?.electricalConduitSystem;
            if (ReferenceEquals(next, manager)) return;
            Reset(); manager = next;
            if (manager != null) manager.AddNetworksRebuiltListener(OnNetworksRebuilt);
        }

        internal static void Reset()
        {
            PowerSettlement.Reset();
            if (manager != null) manager.RemoveNetworksRebuiltListener(OnNetworksRebuilt);
            manager = null; topologyDirty = true; paths = null; losses = null;
            cellNetworks.Clear(); unsupportedNetworks.Clear();
            endpoints = new List<PowerEndpoint>().AsReadOnly(); NodeCount = LinkCount = 0;
        }

        private static void OnNetworksRebuilt(IList<UtilityNetwork> networks, ICollection<int> cells)
        {
            // 这里 CircuitManager 的设备名单尚未刷新，只使缓存失效。
            topologyDirty = true; paths = null; losses = null;
            PowerSettlement.Reset();
            endpoints = new List<PowerEndpoint>().AsReadOnly(); NodeCount = LinkCount = 0;
        }

        internal static bool TryGetPath(int sourceCell, int receiverCell, out LinePath path)
        {
            path = null;
            int network;
            return manager != null && !manager.IsDirty && !topologyDirty && paths != null
                && cellNetworks.TryGetValue(sourceCell, out network) && !unsupportedNetworks.Contains(network)
                && paths.TryGetPath(sourceCell, receiverCell, out path);
        }

        internal static bool TryGetLoss(int sourceCell, int receiverCell, out double length,
            out LineLossCoefficients coefficients, out string reason)
        {
            coefficients = null; length = 0; reason = PowerText.Get("POWER_REASON_UNSUPPORTED", "路径或连接类型尚不支持");
            if (!TryGetPath(sourceCell, receiverCell, out LinePath path)) return false;
            length = path.Length;
            if (losses == null) losses = new PowerLossPathCache(paths, PowerBalance.Parameters, PowerBalance.Conductivities);
            LineLossStatus status = losses.TryGet(sourceCell, receiverCell, out coefficients, out string material);
            reason = status == LineLossStatus.MissingConductivity
                ? string.Format(PowerText.Get("POWER_REASON_MATERIAL", "未配置电导率：{0}"), material)
                : PowerText.Get("POWER_REASON_RANGE", "路径损耗超出数值范围");
            return status == LineLossStatus.Success;
        }

        internal static void AfterCircuitRebuild(IEnumerable<Generator> generators,
            IEnumerable<IEnergyConsumer> consumers, IEnumerable<WireUtilityNetworkLink> bridges)
        {
            Attach(global::Game.Instance);
            if (manager == null) return;
            try
            {
                if (topologyDirty) CaptureTopology(bridges);
                var result = new List<PowerEndpoint>();
                foreach (Generator source in generators)
                {
                    if (source == null) continue;
                    AddEndpoint(result, source, source.GetInstanceID(), source is PowerTransformer
                        ? PowerEndpointKind.TransformerOutput : PowerEndpointKind.Generator);
                }
                foreach (IEnergyConsumer consumer in consumers)
                {
                    var component = consumer as KMonoBehaviour;
                    if (component == null)
                    {
                        ModLog.WarnOnce("power-endpoint-type", "电力基础版未采集非KMonoBehaviour用电端；原版供电保留。");
                        continue;
                    }
                    Battery battery = consumer as Battery;
                    PowerEndpointKind kind = battery == null ? PowerEndpointKind.Consumer
                        : battery.powerTransformer == null ? PowerEndpointKind.Battery : PowerEndpointKind.TransformerInput;
                    AddEndpoint(result, consumer, component.GetInstanceID(), kind);
                }
                result.Sort((a, b) => a.Id.CompareTo(b.Id)); endpoints = result.AsReadOnly();
                ModLog.InfoOnce("power-foundation-snapshot", "电力系统已收到原版重建：物理线格 " + NodeCount
                    + "、连接 " + LinkCount + "；供电结算按物理路径读取缓存。");
            }
            catch (Exception e)
            {
                paths = null; topologyDirty = true; endpoints = new List<PowerEndpoint>().AsReadOnly();
                NodeCount = LinkCount = 0;
                ModLog.WarnOnce("power-topology-failed", "电力基础版拓扑采集失败，原版供电保留：" + e);
            }
        }

        private static void AddEndpoint(List<PowerEndpoint> result, ICircuitConnected endpoint, int id, PowerEndpointKind kind)
        {
            if (endpoint.IsVirtual)
            {
                ModLog.WarnOnce("power-virtual-endpoint", "电力基础版不为虚拟接电端假定零长度；该类路径尚不支持，原版供电保留。");
                return;
            }
            result.Add(new PowerEndpoint(id, endpoint.PowerCell, kind));
        }

        private static T Read<T>(FieldInfo field)
        {
            if (field == null) throw new MissingFieldException(ManagerType.FullName, typeof(T).Name);
            return (T)field.GetValue(manager);
        }

        private static LineMaterial Material(KMonoBehaviour component, Wire.WattageRating rating)
        {
            PrimaryElement primary = component.GetComponent<PrimaryElement>();
            if (primary == null || primary.Element == null) throw new InvalidOperationException("Missing wire primary element.");
            return new LineMaterial(primary.Element.id.ToString(), Wire.GetMaxWattageAsFloat(rating));
        }

        private static void CaptureTopology(IEnumerable<WireUtilityNetworkLink> bridges)
        {
            paths = null; losses = null; cellNetworks.Clear(); unsupportedNetworks.Clear();
            var physicalNodes = Read<HashSet<int>>(NodesField);
            var items = Read<Dictionary<int, object>>(ItemsField);
            var actualLinks = Read<Dictionary<int, int>>(LinksField);
            var semiVirtualLinks = Read<Dictionary<int, object>>(SemiVirtualField);
            var cells = new List<PhysicalWireCell>();
            var liveCells = new HashSet<int>();
            foreach (int cell in physicalNodes)
            {
                if (!Grid.IsValidCell(cell)) continue;
                UtilityNetwork network = manager.GetNetworkForCell(cell);
                if (network == null) continue;
                cellNetworks[cell] = network.id;
                object item;
                Wire wire = items.TryGetValue(cell, out item) ? item as Wire : null;
                if (wire == null || wire.IsDisconnected()) continue;
                Vector2I xy = Grid.CellToXY(cell);
                cells.Add(new PhysicalWireCell(new LineNode(cell, network.id, Material(wire, wire.MaxWattageRating)),
                    xy.x, xy.y, (int)manager.GetConnections(cell, true)));
                liveCells.Add(cell);
            }
            foreach (int cell in semiVirtualLinks.Keys) Unsupported(cell, "power-semi-virtual");
            var physicalBridges = new List<PhysicalWireBridge>();
            var covered = new HashSet<int>();
            foreach (WireUtilityNetworkLink bridge in bridges)
            {
                if (bridge == null || bridge.visualizeOnly) continue;
                int a, b, linked;
                bridge.GetCells(out a, out b);
                if (!actualLinks.TryGetValue(a, out linked) || linked != b) continue;
                if (!liveCells.Contains(a) || !liveCells.Contains(b)) { Unsupported(a, "power-bridge-endpoint"); continue; }
                physicalBridges.Add(new PhysicalWireBridge(a, b, Material(bridge, bridge.maxWattageRating)));
                covered.Add(a); covered.Add(b);
            }
            foreach (int cell in actualLinks.Keys)
                if (!covered.Contains(cell)) Unsupported(cell, "power-unknown-link");
            PowerTopology snapshot = PhysicalPowerTopology.Build(cells, physicalBridges);
            paths = new PowerPathCache(snapshot); NodeCount = snapshot.NodeCount; LinkCount = snapshot.LinkCount;
            topologyDirty = false;
        }

        private static void Unsupported(int cell, string reason)
        {
            int network;
            if (cellNetworks.TryGetValue(cell, out network)) unsupportedNetworks.Add(network);
            ModLog.WarnOnce(reason, "电力基础版发现尚无完整长度或线材定义的连接（" + reason
                + "，cell=" + cell + "）；相关电网不返回估算路径，原版供电保留。");
        }
    }
}
