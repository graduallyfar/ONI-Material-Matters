using System;
using System.Collections.Generic;
using MaterialOverhaul.Core;
using MaterialOverhaul.Power;

namespace MaterialOverhaul.Game
{
    /// <summary>
    /// 接管有完整物理路径的电网取电，保留原版模拟阶段、设备状态及过载检查。
    /// 缓存只在重建时失效；账本每0.2秒重置，无需写入存档。
    /// </summary>
    internal static class PowerSettlement
    {
        private sealed class Source
        {
            internal KMonoBehaviour Component;
            internal ICircuitConnected Endpoint;
            internal int Id => Component.GetInstanceID();
            internal bool IsBattery => Component is Battery;
            internal float Available => Component is Generator g ? g.JoulesAvailable : ((Battery)Component).JoulesAvailable;
            internal void Debit(float joules, PowerReceiverKind kind)
            {
                // 变压器的重载会同步其内部电池，不能再手动扣一次输入电池。
                if (Component is Generator g)
                {
                    if (kind == PowerReceiverKind.TransformerInput) g.ConsumeEnergy(joules);
                    else g.ApplyDeltaJoules(-joules);
                }
                else ((Battery)Component).ConsumeEnergy(joules);
            }
        }
        private sealed class Route
        {
            internal Source Source;
            internal double Length;
            internal LineLossCoefficients Coefficients;
        }
        private sealed class Circuit
        {
            internal readonly List<Source> Sources = new List<Source>();
            internal readonly List<IEnergyConsumer> Receivers = new List<IEnergyConsumer>();
            internal readonly Dictionary<int, List<Route>> Routes = new Dictionary<int, List<Route>>();
            internal readonly Dictionary<int, float> ConsumerRemainders = new Dictionary<int, float>();
            internal readonly HashSet<int> Charged = new HashSet<int>();
            internal readonly PowerTransferLedger Ledger = new PowerTransferLedger(StepSeconds);
            internal bool Prepared, Supported;
            internal string Reason;
            internal double ActualSource, ActualDelivered;
        }
        internal const float StepSeconds = 0.2f;
        private static readonly Dictionary<ushort, Circuit> circuits = new Dictionary<ushort, Circuit>();
        private static bool inStep;
        internal static bool PatchReady { get; set; }

        internal static void Reset()
        {
            circuits.Clear(); inStep = false;
        }

        internal static void Rebuild(IEnumerable<Generator> generators, IEnumerable<IEnergyConsumer> consumers)
        {
            Reset();
            CircuitManager manager = global::Game.Instance?.circuitManager;
            if (manager == null) return;
            foreach (Generator generator in generators)
            {
                if (generator == null) continue;
                ushort id = manager.GetCircuitID(generator);
                if (id != ushort.MaxValue) GetOrCreate(id).Sources.Add(new Source { Component = generator, Endpoint = generator });
            }
            foreach (IEnergyConsumer consumer in consumers)
            {
                ushort id = manager.GetCircuitID(consumer);
                if (id == ushort.MaxValue) continue;
                Circuit circuit = GetOrCreate(id); circuit.Receivers.Add(consumer);
                if (consumer is Battery battery && battery.powerTransformer == null)
                    circuit.Sources.Add(new Source { Component = battery, Endpoint = battery });
            }
        }

        private static Circuit GetOrCreate(ushort id)
        {
            if (!circuits.TryGetValue(id, out Circuit circuit)) { circuit = new Circuit(); circuits.Add(id, circuit); }
            return circuit;
        }

        internal static void Begin(float dt, float elapsed)
        {
            inStep = PatchReady && elapsed + dt >= StepSeconds;
            if (!inStep) return;
            foreach (Circuit circuit in circuits.Values)
            {
                circuit.Ledger.BeginStep(StepSeconds); circuit.ConsumerRemainders.Clear(); circuit.Charged.Clear();
                circuit.ActualDelivered = circuit.ActualSource = 0;
                if (!circuit.Prepared) { Prepare(circuit); WarnUnsupported(circuit); }
            }
        }

        internal static void End() { inStep = false; }

        private static bool SameBuilding(Source source, KMonoBehaviour receiver)
            => source.Component.gameObject == receiver.gameObject;

        private static void Prepare(Circuit circuit)
        {
            circuit.Prepared = true; circuit.Supported = false;
            circuit.Reason = PowerText.Get("POWER_LOSS_PENDING", "线路损耗：等待结算");
            var manager = global::Game.Instance?.electricalConduitSystem;
            if (manager == null || manager.IsDirty) { circuit.Prepared = false; return; }
            foreach (Source source in circuit.Sources)
                if (source.Endpoint.IsVirtual) { circuit.Reason = PowerText.Get("POWER_REASON_VIRTUAL", "接电端类型或虚拟连接尚不支持"); return; }
            foreach (IEnergyConsumer receiver in circuit.Receivers)
            {
                var component = receiver as KMonoBehaviour;
                if (component == null || receiver.IsVirtual) { circuit.Reason = PowerText.Get("POWER_REASON_VIRTUAL", "接电端类型或虚拟连接尚不支持"); return; }
                var routes = new List<Route>();
                foreach (Source source in circuit.Sources)
                {
                    if (receiver is Battery && SameBuilding(source, component)) continue;
                    if (source.Id == component.GetInstanceID())
                    { circuit.Reason = PowerText.Get("POWER_REASON_VIRTUAL", "接电端类型或虚拟连接尚不支持"); return; }
                    if (receiver is Battery ordinary && ordinary.powerTransformer == null && source.IsBattery) continue;
                    if (!PowerTopologyMonitor.TryGetLoss(source.Endpoint.PowerCell, receiver.PowerCell,
                        out double length, out LineLossCoefficients coefficients, out string reason))
                    { circuit.Reason = reason; return; }
                    // 提前确认指数可表示；不在已经扣过电之后才发现整网不兼容。
                    if (coefficients.Evaluate(1, out _, out _) != LineLossStatus.Success)
                    { circuit.Reason = PowerText.Get("POWER_REASON_RANGE", "路径损耗超出数值范围"); return; }
                    routes.Add(new Route { Source = source, Length = length, Coefficients = coefficients });
                }
                routes.Sort((a, b) =>
                {
                    int order = a.Length.CompareTo(b.Length);
                    if (order == 0) order = a.Source.Endpoint.PowerCell.CompareTo(b.Source.Endpoint.PowerCell);
                    return order != 0 ? order : a.Source.Id.CompareTo(b.Source.Id);
                });
                circuit.Routes[component.GetInstanceID()] = routes;
            }
            circuit.Supported = true; circuit.Reason = null;
        }

        private static bool TryCircuit(ICircuitConnected endpoint, out Circuit circuit)
        {
            circuit = null;
            var game = global::Game.Instance;
            if (!inStep || game?.circuitManager == null || game.electricalConduitSystem == null
                || game.electricalConduitSystem.IsDirty) return false;
            if (!circuits.TryGetValue(game.circuitManager.GetCircuitID(endpoint), out circuit)) return false;
            if (!circuit.Prepared)
            {
                Prepare(circuit);
                WarnUnsupported(circuit);
            }
            return circuit.Supported;
        }

        private static void WarnUnsupported(Circuit circuit)
        {
            if (!circuit.Supported && circuit.Prepared)
                ModLog.WarnOnce("power-settlement-unsupported-" + circuit.Reason,
                    "电网保留原版结算，线路损耗未启用：" + circuit.Reason);
        }

        internal static bool TryConsumer(IEnergyConsumer receiver, float requested, out float remaining)
        {
            remaining = requested;
            if (!TryCircuit(receiver, out Circuit circuit)) return false;
            int id = ((KMonoBehaviour)receiver).GetInstanceID();
            if (circuit.ConsumerRemainders.TryGetValue(id, out remaining)) return true;
            float delivered = Transfer(circuit, receiver, requested, PowerReceiverKind.Consumer, true);
            remaining = Math.Max(0, requested - delivered);
            circuit.ConsumerRemainders.Add(id, remaining);
            if (delivered > 0) ReportManager.Instance.ReportValue(ReportManager.ReportType.EnergyCreated, -delivered, receiver.Name);
            return true;
        }

        internal static bool TryCharge(Battery receiver, bool transformer, out float delivered)
        {
            delivered = 0;
            if (!TryCircuit(receiver, out Circuit circuit)) return false;
            if (!circuit.Charged.Add(receiver.GetInstanceID())) return true;
            float requested = Math.Max(0, receiver.Capacity - receiver.JoulesAvailable);
            if (transformer) requested = Math.Min(requested, receiver.ChargeCapacity);
            delivered = Transfer(circuit, receiver, requested,
                transformer ? PowerReceiverKind.TransformerInput : PowerReceiverKind.Battery, transformer);
            if (delivered > 0) receiver.AddEnergy(delivered);
            return true;
        }

        private static float Transfer(Circuit circuit, IEnergyConsumer receiver, float requested,
            PowerReceiverKind kind, bool allowBatteries)
        {
            if (float.IsNaN(requested) || float.IsInfinity(requested) || requested <= 0) return 0;
            int receiverId = ((KMonoBehaviour)receiver).GetInstanceID();
            double remaining = requested, total = 0;
            foreach (Route route in circuit.Routes[receiverId])
            {
                Source source = route.Source;
                if (!allowBatteries && source.IsBattery) continue; // 普通电池不能相互循环充电。
                float available = source.Available;
                if (float.IsNaN(available) || float.IsInfinity(available))
                {
                    ModLog.WarnOnce("power-source-energy-" + source.Id, "跳过异常电源可用能量：instance=" + source.Id + ", J=" + available);
                    continue;
                }
                if (available <= 0 || (!allowBatteries && available < 1)) continue; // 普通电池保留原版发电端至少1J的充电启动门槛。
                LineLossStatus status = circuit.Ledger.Quote(source.Id, receiverId, kind, remaining, available,
                    route.Coefficients, out PowerTransferQuote quote);
                if (status != LineLossStatus.Success) throw new InvalidOperationException("Power budget failed: " + status);
                // 交付向下取到float可表示值，再以该值重算损耗，避免交付的float舍入越过预算。
                float received = (float)quote.DeliveredJoules;
                if (received > quote.DeliveredJoules) received = BitConverter.Int32BitsToSingle(BitConverter.SingleToInt32Bits(received) - 1);
                if (received <= 0) continue;
                status = circuit.Ledger.Quote(source.Id, receiverId, kind, received, available, route.Coefficients, out quote);
                if (status != LineLossStatus.Success || quote.DeliveredJoules != received)
                    throw new InvalidOperationException("Power float budget failed: " + status);
                float debit = (float)quote.SourceJoules;
                if (debit < quote.SourceJoules) debit = BitConverter.Int32BitsToSingle(BitConverter.SingleToInt32Bits(debit) + 1);
                debit = Math.Min(debit, available);
                status = circuit.Ledger.Record(source.Id, receiverId, kind, received, route.Coefficients);
                if (status != LineLossStatus.Success) throw new InvalidOperationException("Power ledger failed: " + status);
                source.Debit(debit, kind);
                circuit.ActualSource += debit; circuit.ActualDelivered += received;
                ReportManager.Instance.ReportValue(ReportManager.ReportType.EnergyCreated, -(debit - received),
                    PowerText.Get("POWER_LOSS_REPORT", "线路损耗"));
                total += received; remaining -= received;
                if (remaining <= 0) break;
            }
            return (float)total;
        }

        internal static bool TryRead(ushort id, out double lossWatts, out double deliveredWatts, out string reason)
        {
            lossWatts = deliveredWatts = 0; reason = null;
            if (!PatchReady) { reason = PowerText.Get("POWER_REASON_PATCH", "结算补丁未就绪"); return false; }
            if (global::Game.Instance?.electricalConduitSystem == null || global::Game.Instance.electricalConduitSystem.IsDirty) return false;
            if (!circuits.TryGetValue(id, out Circuit circuit) || !circuit.Prepared) return false;
            if (!circuit.Supported) { reason = circuit.Reason; return false; }
            lossWatts = (circuit.ActualSource - circuit.ActualDelivered) / StepSeconds;
            deliveredWatts = circuit.ActualDelivered / StepSeconds;
            reason = null; return true;
        }
    }
}
