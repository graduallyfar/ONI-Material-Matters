using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace MaterialOverhaul.Power
{
    public readonly struct LineNode
    {
        public int Cell { get; }
        public int NetworkId { get; }
        public LineMaterial Material { get; }
        public LineNode(int cell, int networkId, LineMaterial material)
        {
            Cell = cell; NetworkId = networkId;
            Material = material ?? throw new ArgumentNullException(nameof(material));
        }
    }

    /// <summary>普通邻格的中间长度为0；电桥只计两端之间未被端点占据的长度。</summary>
    public readonly struct LineLink
    {
        public int First { get; }
        public int Second { get; }
        public double InteriorLength { get; }
        public LineMaterial Material { get; }
        public LineLink(int first, int second, double interiorLength = 0, LineMaterial material = null)
        {
            if (first == second) throw new ArgumentException("A link must join different cells.");
            PowerNumbers.Nonnegative(interiorLength, nameof(interiorLength));
            if (interiorLength > 0 && material == null) throw new ArgumentNullException(nameof(material));
            First = first; Second = second; InteriorLength = interiorLength; Material = material;
        }
        internal int Other(int cell) => cell == First ? Second : First;
    }

    public sealed class LinePath
    {
        public IReadOnlyList<int> Cells { get; }
        public IReadOnlyList<LineSegment> Segments { get; }
        public double Length { get; }
        internal LinePath(List<int> cells, List<LineSegment> segments, double length)
        {
            Cells = cells.AsReadOnly(); Segments = segments.AsReadOnly(); Length = length;
        }
    }

    /// <summary>只读拓扑快照。每个线格计1；不同原版电网之间禁止新增边。</summary>
    public sealed class PowerTopology
    {
        internal readonly Dictionary<int, LineNode> Nodes = new Dictionary<int, LineNode>();
        internal readonly Dictionary<int, List<LineLink>> Adjacency = new Dictionary<int, List<LineLink>>();
        public int NodeCount => Nodes.Count;
        public int LinkCount { get; }

        public PowerTopology(IEnumerable<LineNode> nodes, IEnumerable<LineLink> links)
        {
            if (nodes == null || links == null) throw new ArgumentNullException();
            foreach (LineNode node in nodes)
            {
                if (node.Material == null) throw new ArgumentException("Node material is required.");
                Nodes.Add(node.Cell, node);
                Adjacency.Add(node.Cell, new List<LineLink>());
            }
            int count = 0;
            foreach (LineLink link in links)
            {
                LineNode a, b;
                if (link.First == link.Second || !PowerNumbers.Finite(link.InteriorLength) || link.InteriorLength < 0
                    || (link.InteriorLength > 0 && link.Material == null)
                    || !Nodes.TryGetValue(link.First, out a) || !Nodes.TryGetValue(link.Second, out b)
                    || a.NetworkId != b.NetworkId) throw new ArgumentException("Link endpoints must belong to the same physical network.");
                Adjacency[link.First].Add(link); Adjacency[link.Second].Add(link); count++;
            }
            // 邻接顺序和优先队列均固定，等长路径不受 HashSet/字典插入顺序影响。
            foreach (KeyValuePair<int, List<LineLink>> pair in Adjacency)
                pair.Value.Sort((a, b) => CompareLinks(pair.Key, a, b));
            LinkCount = count;
        }

        private static int CompareLinks(int cell, LineLink a, LineLink b)
        {
            int compare = a.Other(cell).CompareTo(b.Other(cell));
            if (compare != 0) return compare;
            compare = a.InteriorLength.CompareTo(b.InteriorLength);
            if (compare != 0) return compare;
            compare = string.CompareOrdinal(a.Material?.MaterialId, b.Material?.MaterialId);
            return compare != 0 ? compare : (a.Material?.CapacityWatts ?? 0).CompareTo(b.Material?.CapacityWatts ?? 0);
        }
    }

    /// <summary>按源格缓存距离树，按需保存路径。网络／线材重建时整体替换，不持久化电路ID。</summary>
    public sealed class PowerPathCache
    {
        private sealed class SearchTree
        {
            internal readonly Dictionary<int, double> Distance = new Dictionary<int, double>();
            internal readonly Dictionary<int, LineLink> Previous = new Dictionary<int, LineLink>();
            internal readonly Dictionary<int, LinePath> Paths = new Dictionary<int, LinePath>();
        }
        private readonly struct QueueEntry : IComparable<QueueEntry>
        {
            internal readonly double Distance;
            internal readonly int Cell;
            internal QueueEntry(double distance, int cell) { Distance = distance; Cell = cell; }
            public int CompareTo(QueueEntry other)
            {
                int compare = Distance.CompareTo(other.Distance);
                return compare != 0 ? compare : Cell.CompareTo(other.Cell);
            }
        }

        private PowerTopology topology;
        private readonly Dictionary<int, SearchTree> trees = new Dictionary<int, SearchTree>();
        public int SearchCount { get; private set; }
        public int Generation { get; private set; }

        public PowerPathCache(PowerTopology topology) { Replace(topology); }
        public void Replace(PowerTopology replacement)
        {
            topology = replacement ?? throw new ArgumentNullException(nameof(replacement));
            trees.Clear(); SearchCount = 0; Generation++;
        }

        public bool TryGetPath(int source, int receiver, out LinePath path)
        {
            path = null;
            LineNode a, b;
            if (!topology.Nodes.TryGetValue(source, out a) || !topology.Nodes.TryGetValue(receiver, out b)
                || a.NetworkId != b.NetworkId) return false;
            SearchTree tree;
            if (!trees.TryGetValue(source, out tree))
            {
                tree = Search(source); trees.Add(source, tree);
            }
            double length;
            if (!tree.Distance.TryGetValue(receiver, out length)) return false;
            if (tree.Paths.TryGetValue(receiver, out path)) return true;
            var cells = new List<int> { receiver };
            var segments = new List<LineSegment> { new LineSegment(1, b.Material) };
            int current = receiver;
            while (current != source)
            {
                LineLink edge = tree.Previous[current];
                if (edge.InteriorLength > 0) segments.Add(new LineSegment(edge.InteriorLength, edge.Material));
                current = edge.Other(current);
                cells.Add(current); segments.Add(new LineSegment(1, topology.Nodes[current].Material));
            }
            cells.Reverse(); segments.Reverse();
            path = new LinePath(cells, segments, length);
            tree.Paths.Add(receiver, path);
            return true;
        }

        private SearchTree Search(int source)
        {
            SearchCount++;
            var tree = new SearchTree();
            var queue = new SortedSet<QueueEntry>();
            tree.Distance.Add(source, 1); queue.Add(new QueueEntry(1, source));
            while (queue.Count > 0)
            {
                QueueEntry entry = queue.Min; queue.Remove(entry);
                if (entry.Distance != tree.Distance[entry.Cell]) continue;
                foreach (LineLink edge in topology.Adjacency[entry.Cell])
                {
                    int next = edge.Other(entry.Cell);
                    double distance = entry.Distance + 1 + edge.InteriorLength;
                    double previous;
                    if (!tree.Distance.TryGetValue(next, out previous) || distance < previous)
                    {
                        if (!PowerNumbers.Finite(distance)) throw new OverflowException("Path length overflow.");
                        tree.Distance[next] = distance; tree.Previous[next] = edge;
                        queue.Add(new QueueEntry(distance, next));
                    }
                }
            }
            return tree;
        }
    }

    /// <summary>固定参数与电导率快照下缓存系数；更换配置需新建实例。</summary>
    public sealed class PowerLossPathCache
    {
        private readonly PowerPathCache paths;
        private readonly LineLossParameters parameters;
        private readonly IReadOnlyDictionary<string, double> conductivities;
        private readonly Dictionary<LinePath, LineLossCoefficients> coefficients = new Dictionary<LinePath, LineLossCoefficients>();
        private int generation;
        public PowerLossPathCache(PowerPathCache paths, LineLossParameters parameters, IDictionary<string, double> conductivities)
        {
            this.paths = paths ?? throw new ArgumentNullException(nameof(paths));
            this.parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
            if (conductivities == null) throw new ArgumentNullException(nameof(conductivities));
            this.conductivities = new ReadOnlyDictionary<string, double>(new Dictionary<string, double>(conductivities));
            generation = paths.Generation;
        }

        public LineLossStatus TryGet(int source, int receiver, out LineLossCoefficients result, out string missingMaterial)
        {
            result = null; missingMaterial = null;
            if (generation != paths.Generation) { coefficients.Clear(); generation = paths.Generation; }
            LinePath path;
            if (!paths.TryGetPath(source, receiver, out path)) return LineLossStatus.InvalidInput;
            if (coefficients.TryGetValue(path, out result)) return LineLossStatus.Success;
            LineLossStatus status = LineLossCalculator.Compile(path.Segments, parameters, conductivities, out result, out missingMaterial);
            if (status == LineLossStatus.Success) coefficients.Add(path, result);
            return status;
        }
    }
}
