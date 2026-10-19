using System;
using System.Collections.Generic;
using System.Reflection;

// 电力基础版实际 Monitor 源码的替身。字段名与目标744825程序集核对；不加载游戏或Unity。
namespace HarmonyLib
{
    public sealed class CodeInstruction
    {
        public System.Reflection.Emit.OpCode opcode;
        public object operand;
        public CodeInstruction(System.Reflection.Emit.OpCode opcode, object operand) { this.opcode = opcode; this.operand = operand; }
    }
    public static class AccessTools
    {
        public static FieldInfo Field(Type type, string name)
            => type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
    }
}

public class KMonoBehaviour
{
    public UnityEngine.GameObject gameObject = new UnityEngine.GameObject();
    private static int nextId;
    private readonly int instanceId = ++nextId;
    private readonly Dictionary<Type, object> components = new Dictionary<Type, object>();
    public int GetInstanceID() => instanceId;
    public T GetComponent<T>() where T : class => components.TryGetValue(typeof(T), out object value) ? value as T : null;
    public void SetComponent<T>(T component) where T : class { components[typeof(T)] = component; }
}
public sealed class PrimaryElement { public Element Element; }
public struct Vector2I { public int x, y; }
public static class Grid
{
    public static bool IsValidCell(int cell) => cell >= 0 && cell < 1000;
    public static Vector2I CellToXY(int cell) => new Vector2I { x = cell % 10, y = cell / 10 };
}
[Flags]
public enum UtilityConnections { Left = 1, Right = 2, Up = 4, Down = 8 }
public class UtilityNetwork { public int id; }
public sealed class ElectricalUtilityNetwork : UtilityNetwork { public readonly List<Wire> allWires = new List<Wire>(); }
public interface ICircuitConnected { bool IsVirtual { get; } int PowerCell { get; } }
public interface IEnergyConsumer : ICircuitConnected { string Name { get; } }
public class Generator : KMonoBehaviour, ICircuitConnected
{
    public bool IsVirtual { get; set; }
    public int PowerCell { get; set; }
    public float Joules = 1000;
    public float JoulesAvailable => Joules;
    public virtual void ApplyDeltaJoules(float delta) { Joules = Math.Max(0, Joules + delta); }
    public virtual void ConsumeEnergy(float joules) { Joules = Math.Max(0, Joules - joules); }
}
public sealed class PowerTransformer : Generator
{
    public Battery Input;
    public override void ApplyDeltaJoules(float delta) { base.ApplyDeltaJoules(delta); Input?.ConsumeEnergy(-delta); }
    public override void ConsumeEnergy(float joules) { base.ConsumeEnergy(joules); Input?.ConsumeEnergy(joules); }
}
public class EnergyConsumer : KMonoBehaviour, IEnergyConsumer
{
    public bool IsVirtual { get; set; }
    public int PowerCell { get; set; }
    public float Watts = 100;
    public string Name => "test consumer";
}
public sealed class Battery : KMonoBehaviour, IEnergyConsumer
{
    public bool IsVirtual { get; set; }
    public int PowerCell { get; set; }
    public PowerTransformer powerTransformer;
    public float Joules = 1000;
    public string Name => "test battery";
    public float JoulesAvailable => Joules;
    public float Capacity = 20000;
    public float ChargeCapacity = float.PositiveInfinity;
    public void ConsumeEnergy(float joules) { Joules = Math.Max(0, Joules - joules); }
    public void AddEnergy(float joules) { Joules = Math.Min(Capacity, Joules + joules); ChargeCapacity -= joules; }
}

public sealed class ReportManager
{
    public enum ReportType { EnergyCreated }
    public static readonly ReportManager Instance = new ReportManager();
    public readonly List<(float Joules, string Name)> Entries = new List<(float, string)>();
    public void ReportValue(ReportType type, float value, string name) { Entries.Add((value, name)); }
}
public class Wire : KMonoBehaviour
{
    public enum WattageRating { Max500, Max1000, Max2000, Max20000, Max50000, Max4000, NumRatings }
    public WattageRating MaxWattageRating = WattageRating.Max1000;
    public bool Disconnected;
    public int Cell;
    public int GetNetworkCell() => Cell;
    public bool IsDisconnected() => Disconnected;
    public static float GetMaxWattageAsFloat(WattageRating rating)
    {
        switch (rating)
        {
            case WattageRating.Max500: return 500;
            case WattageRating.Max1000: return 1000;
            case WattageRating.Max2000: return 2000;
            case WattageRating.Max20000: return 20000;
            case WattageRating.Max50000: return 50000;
            case WattageRating.Max4000: return 4000;
            default: return 0;
        }
    }
}
public sealed class WireUtilityNetworkLink : KMonoBehaviour
{
    public int First, Second;
    public bool visualizeOnly;
    public Wire.WattageRating maxWattageRating = Wire.WattageRating.Max1000;
    public void GetCells(out int a, out int b) { a = First; b = Second; }
}

public sealed class UtilityNetworkManager<NetworkType, ItemType>
{
    private readonly HashSet<int> physicalNodes = new HashSet<int>();
    private readonly Dictionary<int, object> items = new Dictionary<int, object>();
    private readonly Dictionary<int, int> links = new Dictionary<int, int>();
    private readonly Dictionary<int, object> semiVirtualLinks = new Dictionary<int, object>();
    private readonly Dictionary<int, UtilityConnections> connections = new Dictionary<int, UtilityConnections>();
    private readonly Dictionary<int, UtilityNetwork> networks = new Dictionary<int, UtilityNetwork>();
    public readonly List<UtilityNetwork> ExtraNetworks = new List<UtilityNetwork>();
    public int NetworkReads;
    private Action<IList<UtilityNetwork>, ICollection<int>> rebuilt;
    public bool IsDirty { get; private set; }
    public int ListenerCount => rebuilt?.GetInvocationList().Length ?? 0;
    public int PhysicalConnectionReads, VisualConnectionReads;
    public void AddNetworksRebuiltListener(Action<IList<UtilityNetwork>, ICollection<int>> listener) { rebuilt += listener; }
    public void RemoveNetworksRebuiltListener(Action<IList<UtilityNetwork>, ICollection<int>> listener) { rebuilt -= listener; }
    public UtilityNetwork GetNetworkForCell(int cell) => networks.TryGetValue(cell, out UtilityNetwork value) ? value : null;
    public UtilityNetwork GetNetworkByID(int id)
    {
        foreach (UtilityNetwork network in networks.Values) if (network.id == id) return network;
        foreach (UtilityNetwork network in ExtraNetworks) if (network.id == id) return network;
        return null;
    }
    public IList<UtilityNetwork> GetNetworks()
    {
        NetworkReads++;
        var result = new List<UtilityNetwork>(networks.Values); result.AddRange(ExtraNetworks); return result;
    }
    public UtilityConnections GetConnections(int cell, bool physical)
    {
        if (physical) PhysicalConnectionReads++; else VisualConnectionReads++;
        return connections.TryGetValue(cell, out UtilityConnections value) ? value : 0;
    }
    public void Put(int cell, Wire wire, UtilityConnections directions, int network = 1)
    {
        physicalNodes.Add(cell); items[cell] = wire; connections[cell] = directions;
        var electrical = new ElectricalUtilityNetwork { id = network }; wire.Cell = cell; electrical.allWires.Add(wire);
        networks[cell] = electrical; IsDirty = true;
    }
    public void Bridge(int first, int second) { links[first] = second; links[second] = first; IsDirty = true; }
    public void SemiVirtual(int cell, object key) { semiVirtualLinks[cell] = key; IsDirty = true; }
    public void SimulateRebuild()
    {
        // 与原版一致：一个物理网络维护自己的完整allWires名单。
        var groups = new Dictionary<int, ElectricalUtilityNetwork>();
        foreach (int cell in new List<int>(networks.Keys))
        {
            int id = networks[cell].id;
            if (!groups.TryGetValue(id, out ElectricalUtilityNetwork group))
            { group = new ElectricalUtilityNetwork { id = id }; groups.Add(id, group); }
            Wire wire = items[cell] as Wire;
            if (wire != null && !wire.IsDisconnected()) group.allWires.Add(wire);
            networks[cell] = group;
        }
        IsDirty = false; rebuilt?.Invoke(new List<UtilityNetwork>(networks.Values), physicalNodes);
    }
}
public sealed class CircuitManager
{
    public int NativeGeneratorCalls, NativeBatteryCalls, NativeChargeCalls, NativeTransformerCalls;
    private float PowerFromGenerator(float needed, Generator source, IEnergyConsumer consumer) { NativeGeneratorCalls++; return needed; }
    private float PowerFromBatteries(float needed, List<Battery> sources, IEnergyConsumer consumer) { NativeBatteryCalls++; return needed; }
    private void ChargeBatteries(List<Battery> sinks, List<Generator> sources, ref float used) { NativeChargeCalls++; used += 7; }
    private void ChargeTransformers<T>(List<Battery> sinks, List<T> sources, ref float used) { NativeTransformerCalls++; used += 11; }
    public readonly Dictionary<ICircuitConnected, ushort> VirtualIds = new Dictionary<ICircuitConnected, ushort>();
    public ushort GetCircuitID(ICircuitConnected endpoint)
    {
        if (endpoint.IsVirtual) return VirtualIds.TryGetValue(endpoint, out ushort id) ? id : ushort.MaxValue;
        return (ushort)(Game.Instance.electricalConduitSystem.GetNetworkForCell(endpoint.PowerCell)?.id ?? ushort.MaxValue);
    }
}
public sealed class Game
{
    public static Game Instance;
    public UtilityNetworkManager<ElectricalUtilityNetwork, Wire> electricalConduitSystem;
    public CircuitManager circuitManager = new CircuitManager();
}
