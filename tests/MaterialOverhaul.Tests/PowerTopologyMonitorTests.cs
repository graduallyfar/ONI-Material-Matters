using System;
using System.Collections.Generic;
using MaterialOverhaul.Game;
using MaterialOverhaul.Power;

namespace MaterialOverhaul.Tests
{
    public static class PowerTopologyMonitorTests
    {
        private static readonly HashSet<Generator> Sources = new HashSet<Generator>();
        private static readonly HashSet<IEnergyConsumer> Consumers = new HashSet<IEnergyConsumer>();
        private static readonly HashSet<WireUtilityNetworkLink> Bridges = new HashSet<WireUtilityNetworkLink>();
        public static void Run()
        {
            try { LifecycleAndMaterials(); RolesAndBridges(); UnsupportedAndFailure(); }
            finally { PowerTopologyMonitor.Reset(); global::Game.Instance = null; }
        }

        private static UtilityNetworkManager<ElectricalUtilityNetwork, Wire> Start()
        {
            PowerTopologyMonitor.Reset(); Sources.Clear(); Consumers.Clear(); Bridges.Clear();
            var manager = new UtilityNetworkManager<ElectricalUtilityNetwork, Wire>();
            global::Game.Instance = new global::Game { electricalConduitSystem = manager };
            PowerTopologyMonitor.Attach(global::Game.Instance);
            return manager;
        }
        private static T WithMaterial<T>(T component, SimHashes material = SimHashes.Lead) where T : KMonoBehaviour
        {
            component.SetComponent(new PrimaryElement { Element = new Element { id = material } });
            return component;
        }
        private static void Rebuild(UtilityNetworkManager<ElectricalUtilityNetwork, Wire> manager)
        {
            manager.SimulateRebuild(); PowerTopologyMonitor.AfterCircuitRebuild(Sources, Consumers, Bridges);
        }
        private static void LifecycleAndMaterials()
        {
            TestRunner.Section("只读快照订阅、网络变更与换材");
            var manager = Start();
            TestRunner.CheckEqual(1, manager.ListenerCount, "one subscription");
            PowerTopologyMonitor.Attach(global::Game.Instance);
            TestRunner.CheckEqual(1, manager.ListenerCount, "same owner does not duplicate subscription");
            manager.Put(0, WithMaterial(new Wire()), UtilityConnections.Right);
            manager.Put(1, WithMaterial(new Wire { MaxWattageRating = Wire.WattageRating.Max4000 }), UtilityConnections.Left);
            Rebuild(manager);
            TestRunner.CheckEqual(2, PowerTopologyMonitor.NodeCount, "two live wire cells");
            TestRunner.CheckEqual(1, PowerTopologyMonitor.LinkCount, "reciprocal physical edge");
            LinePath path;
            TestRunner.Check(PowerTopologyMonitor.TryGetPath(0, 1, out path), "monitor supplies confirmed physical path");
            PowerFoundationTests.Close(2, path.Length, "full start and end cells");
            PowerFoundationTests.Close(4000, path.Segments[1].Material.CapacityWatts, "DLC4000 rating read from original API");
            TestRunner.CheckEqual(0, manager.VisualConnectionReads, "never use planning connections");
            int readCount = manager.PhysicalConnectionReads;
            PowerTopologyMonitor.TryGetPath(0, 1, out path);
            PowerTopologyMonitor.AfterCircuitRebuild(Sources, Consumers, Bridges);
            TestRunner.CheckEqual(readCount, manager.PhysicalConnectionReads, "endpoint-only rebuild reuses topology");
            manager.Put(1, WithMaterial(new Wire(), SimHashes.GoldAmalgam), UtilityConnections.Left);
            TestRunner.Check(!PowerTopologyMonitor.TryGetPath(0, 1, out path), "dirty original manager prevents stale lookup");
            manager.SimulateRebuild();
            TestRunner.Check(!PowerTopologyMonitor.TryGetPath(0, 1, out path), "network event invalidates before circuit list refresh");
            TestRunner.CheckEqual(0, PowerTopologyMonitor.Endpoints.Count, "network callback drops stale endpoint list");
            PowerTopologyMonitor.AfterCircuitRebuild(Sources, Consumers, Bridges);
            TestRunner.Check(PowerTopologyMonitor.TryGetPath(0, 1, out path), "path after circuit refresh");
            TestRunner.CheckEqual("GoldAmalgam", path.Segments[1].Material.MaterialId, "replacement material used");
            manager.Put(1, WithMaterial(new Wire { Disconnected = true }), UtilityConnections.Left);
            Rebuild(manager);
            TestRunner.Check(!PowerTopologyMonitor.TryGetPath(0, 1, out path), "disconnected wire omitted");
            manager.Put(1, WithMaterial(new Wire()), UtilityConnections.Left); Rebuild(manager);
            TestRunner.Check(PowerTopologyMonitor.TryGetPath(0, 1, out path), "reconnected wire available");
            var replacement = new UtilityNetworkManager<ElectricalUtilityNetwork, Wire>();
            global::Game.Instance = new global::Game { electricalConduitSystem = replacement };
            PowerTopologyMonitor.Attach(global::Game.Instance);
            TestRunner.CheckEqual(0, manager.ListenerCount, "new world detaches old listener");
            TestRunner.CheckEqual(1, replacement.ListenerCount, "new world listener");
            TestRunner.Check(!PowerTopologyMonitor.TryGetPath(0, 1, out path), "new world never reuses old circuit IDs");
            PowerTopologyMonitor.Reset();
            TestRunner.CheckEqual(0, replacement.ListenerCount, "cleanup unsubscribes");
        }

        private static void RolesAndBridges()
        {
            TestRunner.Section("桥材料、变压器接电口、供电不变");
            var manager = Start();
            manager.Put(0, WithMaterial(new Wire()), 0);
            manager.Put(2, WithMaterial(new Wire()), 0);
            manager.Put(20, WithMaterial(new Wire()), 0, 2);
            var bridge = WithMaterial(new WireUtilityNetworkLink { First = 0, Second = 2 }, SimHashes.GoldAmalgam);
            manager.Bridge(0, 2); Bridges.Add(bridge);
            var generator = new Generator { PowerCell = 0 };
            var transformer = new PowerTransformer { PowerCell = 20 };
            var battery = new Battery { PowerCell = 2 };
            var input = new Battery { PowerCell = 2, powerTransformer = transformer };
            var consumer = new EnergyConsumer { PowerCell = 20, Watts = 800 };
            Sources.Add(generator); Sources.Add(transformer);
            Consumers.Add(battery); Consumers.Add(input); Consumers.Add(consumer);
            Rebuild(manager);
            LinePath path;
            TestRunner.Check(PowerTopologyMonitor.TryGetPath(0, 2, out path), "active bridge traversed");
            PowerFoundationTests.Close(3, path.Length, "span2 bridge counts total3");
            TestRunner.CheckEqual("GoldAmalgam", path.Segments[1].Material.MaterialId, "bridge uses bridge primary material");
            TestRunner.Check(!PowerTopologyMonitor.TryGetPath(2, 20, out path), "no transformer internal edge");
            var kinds = new Dictionary<int, PowerEndpointKind>();
            foreach (PowerEndpoint endpoint in PowerTopologyMonitor.Endpoints) kinds.Add(endpoint.Id, endpoint.Kind);
            TestRunner.CheckEqual(PowerEndpointKind.Generator, kinds[generator.GetInstanceID()], "generator role");
            TestRunner.CheckEqual(PowerEndpointKind.TransformerOutput, kinds[transformer.GetInstanceID()], "transformer output role");
            TestRunner.CheckEqual(PowerEndpointKind.TransformerInput, kinds[input.GetInstanceID()], "transformer battery input role");
            TestRunner.CheckEqual(PowerEndpointKind.Battery, kinds[battery.GetInstanceID()], "ordinary battery role");
            TestRunner.CheckEqual(PowerEndpointKind.Consumer, kinds[consumer.GetInstanceID()], "consumer role");
            PowerFoundationTests.Close(1000, generator.Joules, "monitor does not debit generator");
            PowerFoundationTests.Close(1000, battery.Joules, "monitor does not charge or debit battery");
            PowerFoundationTests.Close(800, consumer.Watts, "monitor does not rewrite heating or production demand");
        }

        private static void UnsupportedAndFailure()
        {
            TestRunner.Section("未知连接、虚拟路径与失败隔离");
            var manager = Start();
            manager.Put(0, WithMaterial(new Wire()), UtilityConnections.Right);
            manager.Put(1, WithMaterial(new Wire()), UtilityConnections.Left);
            manager.SemiVirtual(1, new object()); Rebuild(manager);
            LinePath path;
            TestRunner.Check(!PowerTopologyMonitor.TryGetPath(0, 1, out path), "network with unmeasured virtual shortcut is not estimated");
            manager = Start();
            manager.Put(0, WithMaterial(new Wire()), 0); manager.Put(2, WithMaterial(new Wire()), 0);
            manager.Bridge(0, 2); Rebuild(manager);
            TestRunner.Check(!PowerTopologyMonitor.TryGetPath(0, 2, out path), "unknown linked material is not free bridge");
            manager = Start();
            manager.Put(0, new Wire(), UtilityConnections.Right);
            manager.Put(1, WithMaterial(new Wire()), UtilityConnections.Left);
            Rebuild(manager);
            TestRunner.Check(!PowerTopologyMonitor.TryGetPath(0, 1, out path), "missing primary element disables snapshot");
            TestRunner.CheckEqual(0, PowerTopologyMonitor.NodeCount, "failed snapshot not advertised as populated");
            manager.Put(0, WithMaterial(new Wire()), UtilityConnections.Right); Rebuild(manager);
            TestRunner.Check(PowerTopologyMonitor.TryGetPath(0, 1, out path), "next rebuild recovers from failed snapshot");
            Consumers.Add(new EnergyConsumer { PowerCell = 0, IsVirtual = true });
            PowerTopologyMonitor.AfterCircuitRebuild(Sources, Consumers, Bridges);
            TestRunner.CheckEqual(0, PowerTopologyMonitor.Endpoints.Count, "virtual endpoint not assigned zero length");
            manager = Start();
            manager.Put(0, WithMaterial(new Wire()), 0); manager.Put(2, WithMaterial(new Wire()), 0);
            manager.Put(20, WithMaterial(new Wire()), UtilityConnections.Right, 2);
            manager.Put(21, WithMaterial(new Wire()), UtilityConnections.Left, 2);
            manager.Bridge(0, 2);
            Bridges.Add(WithMaterial(new WireUtilityNetworkLink { First = 0, Second = 2, visualizeOnly = true }));
            Rebuild(manager);
            TestRunner.Check(!PowerTopologyMonitor.TryGetPath(0, 2, out path), "visualization-only bridge not accepted as measured link");
            TestRunner.Check(PowerTopologyMonitor.TryGetPath(20, 21, out path), "unsupported network does not disable unrelated physical network");
        }
    }
}
