using System;
using System.Collections.Generic;
using MaterialOverhaul.Power;

namespace MaterialOverhaul.Tests
{
    public static class PowerFoundationTests
    {
        // 全部为测试参数，不是正式平衡值或游戏材料电导率。
        internal static LineLossParameters Parameters(double lambdaMin = .01, double a = .5)
            => new LineLossParameters(100, 10, 1000, 1, .01, .02, 1, 1, .5, 1, lambdaMin, .1, 1, a);
        private static readonly LineMaterial Copper = new LineMaterial("test-copper", 1000);
        private static readonly LineMaterial Gold = new LineMaterial("test-gold", 2000);
        internal static Dictionary<string, double> Conductivities()
            => new Dictionary<string, double> { { "test-copper", 1 }, { "test-gold", .7 } };

        public static void Run()
        {
            Formula(); NumericalLimits(); Paths(); PhysicalConnections(); CacheAndCandidates(); Ledger(); RandomShortestPaths();
        }

        internal static void Close(double expected, double actual, string name, double relativeTolerance = 1e-11)
        {
            TestRunner.Check(!double.IsNaN(actual) && !double.IsInfinity(actual)
                && Math.Abs(expected - actual) <= relativeTolerance * Math.Max(Math.Abs(expected), 1e-100),
                name + ": expected=" + expected + ", actual=" + actual);
        }

        private static void Throws(Action action, string name)
        {
            bool thrown = false;
            try { action(); } catch (ArgumentException) { thrown = true; }
            TestRunner.Check(thrown, name);
        }

        internal static LineLossCoefficients Compile(LineSegment[] segments, LineLossParameters p = null)
        {
            string missing;
            LineLossCoefficients c;
            TestRunner.CheckEqual(LineLossStatus.Success,
                LineLossCalculator.Compile(segments, p ?? Parameters(), Conductivities(), out c, out missing), "compile valid path");
            return c;
        }

        private static double Loss(LineLossCoefficients c, double power)
        {
            double b, l;
            TestRunner.CheckEqual(LineLossStatus.Success, c.Evaluate(power, out b, out l), "evaluate valid power");
            return b + l;
        }

        // 直接代入逐段公式作独立算术对照，不读取被测缓存的 C0/C1/Z。
        private static double Direct(LineSegment[] segments, double power, LineLossParameters p)
        {
            double w = power / (power + p.Pc), b = 0, z = 0;
            foreach (LineSegment s in segments)
            {
                double sigma = Conductivities()[s.Material.MaterialId];
                b += power * s.Length / p.L0 * Math.Pow(p.ConductivityReference / sigma, p.Gamma)
                    * (p.A0 + p.A1 * Math.Pow(s.Material.CapacityWatts / p.PowerReference, p.Nu) * (1 - w));
                double g = Math.Pow(s.Material.CapacityWatts / p.PowerReference, p.Alpha)
                    * Math.Pow(sigma / p.ConductivityReference, p.Beta);
                z += s.Length / p.L0 * (p.LambdaMin + p.DeltaLambda / (1 + p.K * g));
            }
            return b + power * p.A * w * (Math.Exp(z) - 1 - z);
        }

        private static void Formula()
        {
            TestRunner.Section("统一／混合线材公式与代数性质");
            var p = Parameters();
            var whole = new[] { new LineSegment(15, Copper) };
            var split = new[] { new LineSegment(1, Copper), new LineSegment(4, Copper), new LineSegment(10, Copper) };
            var mixed = new[] { new LineSegment(10, Copper), new LineSegment(5, Gold) };
            var reverse = new[] { mixed[1], mixed[0] };
            LineLossCoefficients cWhole = Compile(whole), cSplit = Compile(split), cMixed = Compile(mixed), cReverse = Compile(reverse);
            foreach (double watts in new[] { 0d, .01, 10, 100, 1000, 20000, 50000 })
            {
                Close(Direct(whole, watts, p), Loss(cWhole, watts), "homogeneous direct formula");
                Close(Loss(cWhole, watts), Loss(cSplit, watts), "homogeneous splitting invariant");
                Close(Direct(mixed, watts, p), Loss(cMixed, watts), "mixed one exponential");
                Close(Loss(cMixed, watts), Loss(cReverse, watts), "segment permutation invariant");
            }
            var cGold = Compile(new[] { new LineSegment(15, Gold) });
            Close((cWhole.C0 * 10 + cGold.C0 * 5) / 15, cMixed.C0, "partial replacement C0");
            Close((cWhole.C1 * 10 + cGold.C1 * 5) / 15, cMixed.C1, "partial replacement C1");
            Close((cWhole.Z * 10 + cGold.Z * 5) / 15, cMixed.Z, "partial replacement Z");
            double previous = 0;
            for (int n = 0; n <= 100; n++)
            {
                double power = n * n;
                double loss = Loss(cMixed, power);
                TestRunner.Check(loss >= previous, "nonnegative parameters give monotone loss " + n); previous = loss;
            }
            var lowSigma = Conductivities(); lowSigma["test-copper"] = .5;
            LineLossCoefficients low; string missing;
            LineLossCalculator.Compile(whole, p, lowSigma, out low, out missing);
            TestRunner.Check(Loss(low, 1000) > Loss(cWhole, 1000), "lower conductivity increases loss");
            TestRunner.Check(Loss(cMixed, 0) == 0, "zero actual transfer has zero base and length loss");
        }

        private static void NumericalLimits()
        {
            TestRunner.Section("非法参数、缺少材料、消去误差与溢出");
            foreach (double invalid in new[] { -1d, double.NaN, double.PositiveInfinity })
            {
                Throws(() => new LineMaterial("x", invalid), "invalid capacity");
                Throws(() => new LineSegment(invalid, Copper), "invalid length");
                Throws(() => Parameters(invalid), "invalid formula coefficient");
            }
            Throws(() => new LineMaterial("", 1), "empty material id");
            Throws(() => new LineMaterial("x", 0), "zero capacity");
            Throws(() => new LineLossParameters(0, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0), "zero Pc");
            var segments = new[] { new LineSegment(10, Copper) };
            LineLossCoefficients coefficients; string missing;
            TestRunner.CheckEqual(LineLossStatus.MissingConductivity,
                LineLossCalculator.Compile(segments, Parameters(), new Dictionary<string, double>(), out coefficients, out missing), "missing conductivity explicit");
            TestRunner.CheckEqual("test-copper", missing, "missing material id locatable");
            TestRunner.Check(coefficients == null, "missing material never produces free path");
            foreach (double badSigma in new[] { 0d, -1, double.NaN, double.PositiveInfinity })
                TestRunner.CheckEqual(LineLossStatus.InvalidInput,
                    LineLossCalculator.Compile(segments, Parameters(), new Dictionary<string, double> { { "test-copper", badSigma } }, out coefficients, out missing), "bad conductivity rejected");
            TestRunner.CheckEqual(LineLossStatus.InvalidInput,
                LineLossCalculator.Compile(new[] { default(LineSegment) }, Parameters(), Conductivities(), out coefficients, out missing), "default segment rejected");
            TestRunner.CheckEqual(LineLossStatus.InvalidInput,
                LineLossCalculator.Compile(Array.Empty<LineSegment>(), Parameters(), Conductivities(), out coefficients, out missing), "empty path not zero-length supply");
            coefficients = Compile(segments);
            foreach (double invalid in new[] { -1d, double.NaN, double.PositiveInfinity })
            {
                double b, l;
                TestRunner.CheckEqual(LineLossStatus.InvalidInput, coefficients.Evaluate(invalid, out b, out l), "invalid actual power");
            }
            // 独立解析式：a0=a1=0、lambda=1e-12，Z=1e-12，w=1/2。
            var tinyParameters = new LineLossParameters(1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 1e-12, 0, 0, 1);
            var tiny = Compile(new[] { new LineSegment(1, Copper) }, tinyParameters);
            double baseLoss, lengthLoss;
            TestRunner.CheckEqual(LineLossStatus.Success, tiny.Evaluate(1, out baseLoss, out lengthLoss), "small Z success");
            Close(2.5e-25, lengthLoss, "tiny positive exponential retained", 1e-11);
            var huge = Compile(segments, Parameters(1000));
            TestRunner.CheckEqual(LineLossStatus.NumericOverflow, huge.Evaluate(1, out baseLoss, out lengthLoss), "exponential overflow is explicit");
            var disabled = Compile(segments, Parameters(1000, 0));
            TestRunner.CheckEqual(LineLossStatus.Success, disabled.Evaluate(1, out baseLoss, out lengthLoss), "disabled length term need not exponentiate");
            Close(0, lengthLoss, "disabled length zero");
            TestRunner.CheckEqual(LineLossStatus.Success, huge.Evaluate(0, out baseLoss, out lengthLoss), "zero power avoids overflow");
            var enormousSegment = new[] { new LineSegment(double.MaxValue, Copper), new LineSegment(double.MaxValue, Copper) };
            var unitLength = new LineLossParameters(1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0);
            TestRunner.CheckEqual(LineLossStatus.NumericOverflow,
                LineLossCalculator.Compile(enormousSegment, unitLength, Conductivities(), out coefficients, out missing), "coefficient sum overflow");
            var noLossParameters = new LineLossParameters(1e308, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
            var noLoss = Compile(new[] { new LineSegment(1, Copper) }, noLossParameters);
            TestRunner.CheckEqual(LineLossStatus.Success, noLoss.Evaluate(1e308, out baseLoss, out lengthLoss), "finite P plus Pc does not overflow weight");
            Close(0, baseLoss + lengthLoss, "zero coefficient remains zero at huge finite power");
        }

        private static PowerPathCache Cache(LineNode[] nodes, params LineLink[] links) => new PowerPathCache(new PowerTopology(nodes, links));
        private static LineNode Node(int cell, int network = 1, LineMaterial material = null) => new LineNode(cell, network, material ?? Copper);
        private static LinePath Path(PowerPathCache cache, int a, int b, double length)
        {
            LinePath path;
            TestRunner.Check(cache.TryGetPath(a, b, out path), "reachable " + a + "->" + b);
            if (path != null)
            {
                Close(length, path.Length, "path length");
                double sum = 0; foreach (LineSegment segment in path.Segments) sum += segment.Length;
                Close(length, sum, "material segment lengths cover entire path");
            }
            return path;
        }

        private static void Paths()
        {
            TestRunner.Section("最短长度、起止格、环路、桥与稳定路径");
            var nodes = new[] { Node(0), Node(1), Node(2), Node(3), Node(4), Node(9, 2) };
            var cache = Cache(nodes, new LineLink(0, 1), new LineLink(1, 2), new LineLink(2, 3),
                new LineLink(0, 4), new LineLink(4, 3), new LineLink(1, 4));
            Path(cache, 0, 0, 1); Path(cache, 0, 1, 2); var shortest = Path(cache, 0, 3, 3);
            TestRunner.CheckEqual(4, shortest.Cells[1], "shortest route rather than all network wires");
            LinePath absent;
            TestRunner.Check(!cache.TryGetPath(0, 9, out absent), "different network not connected");
            TestRunner.Check(!cache.TryGetPath(0, 999, out absent), "missing endpoint not zero length");
            Throws(() => Cache(nodes, new LineLink(0, 9)), "no cross-transformer graph edge");
            Throws(() => Cache(nodes, default(LineLink)), "default self-link rejected");
            var bridgeCache = Cache(new[] { Node(0), Node(2) }, new LineLink(0, 2, 1, Gold));
            var bridged = Path(bridgeCache, 0, 2, 3);
            TestRunner.CheckEqual("test-gold", bridged.Segments[1].Material.MaterialId, "bridge interior uses its own material");
            var weighted = Cache(new[] { Node(0), Node(1), Node(2) }, new LineLink(0, 2, 10, Gold), new LineLink(0, 1), new LineLink(1, 2));
            TestRunner.CheckEqual(1, Path(weighted, 0, 2, 3).Cells[1], "weighted search does not prefer fewer hops");
            var diamondNodes = new[] { Node(0), Node(1, 1, Gold), Node(2), Node(3) };
            var first = Cache(diamondNodes, new LineLink(0, 2), new LineLink(2, 3), new LineLink(0, 1), new LineLink(1, 3));
            var second = Cache(diamondNodes, new LineLink(1, 3), new LineLink(0, 1), new LineLink(2, 3), new LineLink(0, 2));
            TestRunner.CheckEqual(1, Path(first, 0, 3, 3).Cells[1], "equal-length stable cell order, not best material");
            TestRunner.CheckEqual(1, Path(second, 0, 3, 3).Cells[1], "edge insertion order independent");
        }

        private static void PhysicalConnections()
        {
            TestRunner.Section("真实方向与电桥转换");
            var cells = new[] { new PhysicalWireCell(Node(0), 0, 0, 2), new PhysicalWireCell(Node(1), 1, 0, 1),
                new PhysicalWireCell(Node(2), 2, 0, 0), new PhysicalWireCell(Node(3), 1, 1, 8) };
            var graph = PhysicalPowerTopology.Build(cells, Array.Empty<PhysicalWireBridge>());
            var cache = new PowerPathCache(graph);
            Path(cache, 0, 1, 2); LinePath absent;
            TestRunner.Check(!cache.TryGetPath(1, 2, out absent), "visual adjacency alone is not connection");
            TestRunner.Check(!cache.TryGetPath(1, 3, out absent), "one-sided direction is not connection");
            var bridge = PhysicalPowerTopology.Build(cells, new[] { new PhysicalWireBridge(0, 2, Gold) });
            var bridgedPath = Path(new PowerPathCache(bridge), 0, 2, 3);
            TestRunner.CheckEqual(2, bridgedPath.Cells.Count, "bridge does not connect visual crossing middle wire");
            Throws(() => PhysicalPowerTopology.Build(cells, new[] { new PhysicalWireBridge(0, 99, Gold) }), "missing bridge endpoint explicit");
            // 左右边界具有相邻cell id，但不同坐标，不得跨行连接。
            var wrap = new[] { new PhysicalWireCell(Node(4), 4, 0, 2), new PhysicalWireCell(Node(5), 0, 1, 1) };
            var wrapCache = new PowerPathCache(PhysicalPowerTopology.Build(wrap, Array.Empty<PhysicalWireBridge>()));
            TestRunner.Check(!wrapCache.TryGetPath(4, 5, out absent), "no row wrap");
        }

        private static void CacheAndCandidates()
        {
            TestRunner.Section("缓存与候选源，换材／断连／重连");
            var nodes = new[] { Node(0), Node(1), Node(2) };
            var links = new[] { new LineLink(0, 1), new LineLink(1, 2) };
            var cache = Cache(nodes, links);
            Path(cache, 0, 1, 2); LinePath first = Path(cache, 0, 2, 3);
            TestRunner.CheckEqual(1, cache.SearchCount, "one source search reused across receivers");
            TestRunner.Check(ReferenceEquals(first, Path(cache, 0, 2, 3)), "same cached path reused");
            var table = Conductivities();
            var losses = new PowerLossPathCache(cache, Parameters(), table);
            LineLossCoefficients c1, c2; string missing;
            losses.TryGet(0, 2, out c1, out missing); table["test-copper"] = 100;
            losses.TryGet(0, 2, out c2, out missing);
            TestRunner.Check(ReferenceEquals(c1, c2), "coefficient cache reused and external table mutation isolated");
            double original = Loss(c1, 1000);
            cache.Replace(new PowerTopology(new[] { Node(0), Node(1, 1, Gold), Node(2) }, links));
            losses.TryGet(0, 2, out c2, out missing);
            TestRunner.Check(!ReferenceEquals(c1, c2), "material replacement invalidates coefficient cache");
            TestRunner.Check(original != Loss(c2, 1000), "new segment material used");
            cache.Replace(new PowerTopology(nodes, new[] { links[0] }));
            LinePath absent;
            TestRunner.Check(!cache.TryGetPath(0, 2, out absent), "disconnect invalidates old path");
            cache.Replace(new PowerTopology(nodes, links)); Path(cache, 0, 2, 3);
            var sources = new[] { new PowerEndpoint(9, 2, PowerEndpointKind.Generator), new PowerEndpoint(5, 0, PowerEndpointKind.Battery),
                new PowerEndpoint(6, 0, PowerEndpointKind.TransformerOutput), new PowerEndpoint(99, 99, PowerEndpointKind.Generator) };
            var candidates = PowerPathCandidates.Find(cache, new PowerEndpoint(7, 1, PowerEndpointKind.Consumer), sources);
            TestRunner.CheckEqual(3, candidates.Count, "all reachable allowed sources, not nearest only");
            TestRunner.CheckEqual(5, candidates[0].Source.Id, "equal distance stable id order");
            TestRunner.CheckEqual(6, candidates[1].Source.Id, "stable tie independent of source category");
            var batteryCandidates = PowerPathCandidates.Find(cache, new PowerEndpoint(5, 0, PowerEndpointKind.Battery), sources);
            TestRunner.CheckEqual(2, batteryCandidates.Count, "battery excludes self without imposing charging eligibility");
            var incomplete = new PowerLossPathCache(cache, Parameters(), new Dictionary<string, double>());
            TestRunner.CheckEqual(LineLossStatus.MissingConductivity, incomplete.TryGet(0, 2, out c2, out missing), "missing material never cached as success");
            TestRunner.CheckEqual(LineLossStatus.MissingConductivity, incomplete.TryGet(0, 2, out c2, out missing), "repeated missing lookup stays explicit");
        }

        private static void Ledger()
        {
            TestRunner.Section("实际焦耳、分账、聚合与收支守恒");
            var c = Compile(new[] { new LineSegment(20, Copper) });
            var one = new PowerTransferLedger(.2);
            var split = new PowerTransferLedger(.2);
            TestRunner.CheckEqual(LineLossStatus.Success, one.Record(1, 2, PowerReceiverKind.Consumer, 100, c), "one transfer");
            split.Record(1, 2, PowerReceiverKind.Consumer, 40, c); split.Record(1, 2, PowerReceiverKind.Consumer, 60, c);
            Close(one.LossJoules, split.LossJoules, "splitting same transfer does not reset nonlinear weight");
            Close(Loss(c, 500) * .2, one.LossJoules, "actual J to W conversion uses step");
            Close(one.DeliveredJoules + one.LossJoules, one.OutputJoules(1), "source delivered plus loss");
            Close(one.LossJoules / .2, one.LossWatts, "loss W from J");
            one.Record(1, 3, PowerReceiverKind.Battery, 80, c);
            one.Record(3, 4, PowerReceiverKind.TransformerInput, 60, c);
            Close(80, one.InputJoules(3), "battery charge retained rather than net energy");
            TestRunner.Check(one.OutputJoules(3) > 60, "same battery discharge separately records source expense");
            Close(one.SourceJoules, one.OutputJoules(1) + one.OutputJoules(3), "network source sums close");
            Close(Loss(c, 500) * .2 + Loss(c, 400) * .2 + Loss(c, 300) * .2, one.LossJoules, "per receiver sum, no second trunk charge");
            double oldLoss = one.LossJoules;
            TestRunner.CheckEqual(LineLossStatus.InvalidInput, one.Record(3, 3, PowerReceiverKind.Battery, 10, c), "no self charging");
            TestRunner.CheckEqual(LineLossStatus.InvalidInput, one.Record(9, 8, PowerReceiverKind.Consumer, 1, null), "no missing-path free transfer");
            TestRunner.CheckEqual(LineLossStatus.InvalidInput, one.Record(1, 2, PowerReceiverKind.Battery, 1, c), "same pair cannot silently change category");
            TestRunner.CheckEqual(LineLossStatus.InvalidInput, one.Record(1, 2, PowerReceiverKind.Consumer, -1, c), "negative delivery rejected");
            TestRunner.CheckEqual(LineLossStatus.InvalidInput, one.Record(1, 8, (PowerReceiverKind)99, 1, c), "invalid receiver category rejected");
            Close(oldLoss, one.LossJoules, "failed record is atomic");
            one.BeginStep(.2);
            Close(0, one.LossJoules, "next round clears old loss"); Close(0, one.InputJoules(3), "no saved old input");
            Close(0, one.OutputJoules(3), "self-discharge not manufactured as a transfer");
            var huge = Compile(new[] { new LineSegment(10, Copper) }, Parameters(1000));
            TestRunner.CheckEqual(LineLossStatus.NumericOverflow, one.Record(1, 2, PowerReceiverKind.Consumer, 10, huge), "overflow refused atomically");
            Close(0, one.SourceJoules, "overflow does not become zero-loss paid delivery");
            Throws(() => one.BeginStep(0), "invalid timestep rejected");
            var multi = new PowerTransferLedger(.2);
            multi.Record(1, 8, PowerReceiverKind.Consumer, 20, c);
            multi.Record(2, 8, PowerReceiverKind.Consumer, 30, c);
            Close(Loss(c, 100) * .2 + Loss(c, 150) * .2, multi.LossJoules, "per-source pair aggregation remains separate");
        }

        private static void RandomShortestPaths()
        {
            TestRunner.Section("随机小图对独立全对最短路校核");
            var random = new Random(744825);
            const int size = 8;
            for (int sample = 0; sample < 16; sample++)
            {
                var nodes = new LineNode[size]; var links = new List<LineLink>();
                var distances = new double[size, size];
                for (int i = 0; i < size; i++)
                {
                    nodes[i] = Node(i);
                    for (int j = 0; j < size; j++) distances[i, j] = i == j ? 0 : double.PositiveInfinity;
                }
                for (int i = 0; i < size; i++)
                    for (int j = i + 1; j < size; j++)
                        if (random.Next(3) == 0)
                        {
                            int extra = random.Next(4);
                            links.Add(new LineLink(i, j, extra, Gold)); distances[i, j] = distances[j, i] = 1 + extra;
                        }
                for (int k = 0; k < size; k++)
                    for (int i = 0; i < size; i++)
                        for (int j = 0; j < size; j++) distances[i, j] = Math.Min(distances[i, j], distances[i, k] + distances[k, j]);
                var cache = new PowerPathCache(new PowerTopology(nodes, links));
                for (int i = 0; i < size; i++)
                    for (int j = 0; j < size; j++)
                    {
                        LinePath path; bool found = cache.TryGetPath(i, j, out path);
                        bool reachable = !double.IsInfinity(distances[i, j]);
                        TestRunner.CheckEqual(reachable, found, "random graph connectivity");
                        if (reachable) Close(1 + distances[i, j], path.Length, "random graph weighted shortest length");
                    }
            }
        }
    }
}
