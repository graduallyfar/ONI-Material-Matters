using System;
using System.Collections.Generic;

namespace MaterialOverhaul.Power
{
    /// <summary>原版物理格的只读输入；方向值对应已核实的 UtilityConnections。</summary>
    public readonly struct PhysicalWireCell
    {
        public LineNode Node { get; }
        public int X { get; }
        public int Y { get; }
        public int Connections { get; }
        public PhysicalWireCell(LineNode node, int x, int y, int connections)
        {
            Node = node; X = x; Y = y; Connections = connections;
        }
    }

    public readonly struct PhysicalWireBridge
    {
        public int First { get; }
        public int Second { get; }
        public LineMaterial Material { get; }
        public PhysicalWireBridge(int first, int second, LineMaterial material)
        { First = first; Second = second; Material = material ?? throw new ArgumentNullException(nameof(material)); }
    }

    public static class PhysicalPowerTopology
    {
        public static PowerTopology Build(IEnumerable<PhysicalWireCell> cells, IEnumerable<PhysicalWireBridge> bridges)
        {
            var byCell = new Dictionary<int, PhysicalWireCell>();
            var byPosition = new Dictionary<(int, int), PhysicalWireCell>();
            var nodes = new List<LineNode>();
            var links = new List<LineLink>();
            foreach (PhysicalWireCell cell in cells)
            {
                byCell.Add(cell.Node.Cell, cell); byPosition.Add((cell.X, cell.Y), cell); nodes.Add(cell.Node);
            }
            foreach (PhysicalWireCell cell in byCell.Values)
            {
                // 只遍历右／上，且要求两侧真实连接互相匹配，交叉或规划线不能构造边。
                Connect(cell, 1, 0, 2, 1, byPosition, links);
                Connect(cell, 0, 1, 4, 8, byPosition, links);
            }
            var seen = new HashSet<(int, int)>();
            foreach (PhysicalWireBridge bridge in bridges)
            {
                PhysicalWireCell first, second;
                if (!byCell.TryGetValue(bridge.First, out first) || !byCell.TryGetValue(bridge.Second, out second))
                    throw new ArgumentException("A physical bridge is missing an endpoint wire.");
                var pair = (Math.Min(bridge.First, bridge.Second), Math.Max(bridge.First, bridge.Second));
                if (!seen.Add(pair)) throw new ArgumentException("Duplicate physical bridge.");
                double span = Math.Abs((double)first.X - second.X) + Math.Abs((double)first.Y - second.Y);
                if (span < 1) throw new ArgumentException("Invalid bridge span.");
                links.Add(new LineLink(bridge.First, bridge.Second, span - 1, bridge.Material));
            }
            return new PowerTopology(nodes, links);
        }

        private static void Connect(PhysicalWireCell cell, int dx, int dy, int outgoing, int incoming,
            Dictionary<(int, int), PhysicalWireCell> positions, List<LineLink> links)
        {
            PhysicalWireCell other;
            if ((cell.Connections & outgoing) != 0 && positions.TryGetValue((cell.X + dx, cell.Y + dy), out other)
                && (other.Connections & incoming) != 0 && cell.Node.NetworkId == other.Node.NetworkId)
                links.Add(new LineLink(cell.Node.Cell, other.Node.Cell));
        }
    }

    public enum PowerEndpointKind { Consumer, Generator, Battery, TransformerInput, TransformerOutput }

    public readonly struct PowerEndpoint
    {
        public int Id { get; }
        public int Cell { get; }
        public PowerEndpointKind Kind { get; }
        public PowerEndpoint(int id, int cell, PowerEndpointKind kind) { Id = id; Cell = cell; Kind = kind; }
    }

    public readonly struct PowerPathCandidate
    {
        public PowerEndpoint Source { get; }
        public LinePath Path { get; }
        internal PowerPathCandidate(PowerEndpoint source, LinePath path) { Source = source; Path = path; }
    }

    public static class PowerPathCandidates
    {
        /// <summary>
        /// 仅对调用方明确提供的源建立距离候选表，不决定类别优先级或源端取电资格。
        /// 电池自身不会成为自己的源；源端实时可用电量由第二版另行检查。
        /// </summary>
        public static IReadOnlyList<PowerPathCandidate> Find(PowerPathCache cache, PowerEndpoint receiver,
            IEnumerable<PowerEndpoint> allowedSources)
        {
            var result = new List<PowerPathCandidate>();
            var ids = new HashSet<int>();
            foreach (PowerEndpoint source in allowedSources)
            {
                if (!ids.Add(source.Id)) throw new ArgumentException("Duplicate source id.");
                if (source.Id == receiver.Id) continue;
                if (source.Kind != PowerEndpointKind.Generator && source.Kind != PowerEndpointKind.Battery
                    && source.Kind != PowerEndpointKind.TransformerOutput) throw new ArgumentException("Invalid source kind.");
                LinePath path;
                if (cache.TryGetPath(source.Cell, receiver.Cell, out path)) result.Add(new PowerPathCandidate(source, path));
            }
            result.Sort((a, b) =>
            {
                int compare = a.Path.Length.CompareTo(b.Path.Length);
                return compare != 0 ? compare : a.Source.Id.CompareTo(b.Source.Id);
            });
            return result.AsReadOnly();
        }
    }
}
