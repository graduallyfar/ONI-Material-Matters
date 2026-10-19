using System.Collections.Generic;
using MaterialOverhaul.Game;
using MaterialOverhaul.Patches;

namespace UnityEngine
{
    public sealed partial class GameObject
    {
        public readonly List<object> Components = new List<object>();
        public T GetComponent<T>() where T : class
        {
            foreach (object component in Components) if (component is T value) return value;
            return null;
        }
    }
}

public sealed class AdditionalDetailsPanel { }
public sealed class CollapsibleDetailContentPanel
{
    public sealed class Row { public string Text, Tooltip; public bool Used, Visible; }
    public readonly Dictionary<string, Row> Rows = new Dictionary<string, Row>();
    public int Commits;
    public void SetLabel(string id, string text, string tooltip)
    {
        if (!Rows.TryGetValue(id, out Row row)) { row = new Row(); Rows.Add(id, row); }
        row.Text = text; row.Tooltip = tooltip; row.Used = true;
    }
    // 对应原版Commit的used语义，防止后置再Commit把原版行隐藏。
    public void Commit()
    {
        Commits++;
        foreach (Row row in Rows.Values) { row.Visible = row.Used; row.Used = false; }
    }
}

namespace MaterialOverhaul.Tests
{
    public static class PowerWireCountTests
    {
        private static UnityEngine.GameObject Target(params object[] components)
        {
            var result = new UnityEngine.GameObject(); result.Components.AddRange(components); return result;
        }
        private static void CheckCount(UnityEngine.GameObject target, int expected, string name)
        {
            TestRunner.Check(PowerWireCount.TryGet(target, out int count) && count == expected, name);
        }
        public static void Run()
        {
            PowerTopologyMonitor.Reset();
            var manager = new UtilityNetworkManager<ElectricalUtilityNetwork, Wire>();
            global::Game.Instance = new global::Game { electricalConduitSystem = manager };
            var circuits = global::Game.Instance.circuitManager;
            try
            {
                TestRunner.Section("选中独立电网的电线数量");
                var wire0 = new Wire(); var wire20 = new Wire(); var offWire = new Wire { Disconnected = true };
                var target = Target(wire0); var otherTarget = Target(wire20);
                manager.Put(0, wire0, UtilityConnections.Right, 1);
                manager.Put(1, new Wire(), UtilityConnections.Left | UtilityConnections.Right, 1);
                manager.Put(2, new Wire(), UtilityConnections.Left, 1);
                manager.Put(20, wire20, UtilityConnections.Right, 2);
                manager.Put(21, new Wire(), UtilityConnections.Left, 2);
                manager.Put(30, offWire, 0, 3);
                var display = new ElectricalUtilityNetwork { id = 500 }; display.allWires.Add(wire0);
                manager.ExtraNetworks.Add(display); manager.ExtraNetworks.Add(new ElectricalUtilityNetwork { id = 501 });
                TestRunner.Check(!PowerWireCount.TryGet(target, out _), "dirty graph not reported as zero");
                manager.SimulateRebuild();
                CheckCount(target, 3, "selected network has three wires, not total networks or global wires");
                CheckCount(otherTarget, 2, "another network has its own two wires");
                CheckCount(Target(new EnergyConsumer { PowerCell = 1 }), 3, "consumer uses its own circuit");
                CheckCount(Target(offWire), 0, "disconnected wire has no circuit count");
                CheckCount(Target(new EnergyConsumer { PowerCell = 999 }), 0, "unconnected consumer shows zero");
                var virtualBattery = new Battery { IsVirtual = true };
                circuits.VirtualIds[virtualBattery] = 77;
                manager.ExtraNetworks.Add(new ElectricalUtilityNetwork { id = 77 });
                CheckCount(Target(virtualBattery), 0, "virtual circuit without physical wires has zero wire cells");
                circuits.VirtualIds[virtualBattery] = 1;
                CheckCount(Target(virtualBattery), 3, "virtual endpoint reads original circuit physical wire list");
                var transformerOutput = new PowerTransformer { PowerCell = 20 };
                var transformerInput = new Battery { PowerCell = 0, powerTransformer = transformerOutput };
                CheckCount(Target(transformerInput, transformerOutput), 3, "transformer follows original first ICircuitConnected selection");
                CheckCount(Target(transformerOutput), 2, "transformer output reads downstream circuit");
                TestRunner.Check(!PowerWireCount.TryGet(Target(), out _), "unrelated object not a power target");

                TestRunner.Section("实际信息卡前缀、切换目标和原版提交");
                var panel = new CollapsibleDetailContentPanel(); string labelId = Patch_PowerInfoCard_WireCount.LabelId;
                int reads = manager.NetworkReads;
                for (int pass = 0; pass < 2; pass++)
                {
                    Patch_PowerInfoCard_WireCount.Prefix(panel, target);
                    panel.SetLabel("native", "原版发电／耗电", "原版说明"); panel.Commit();
                    TestRunner.CheckEqual("电线数量：3", panel.Rows[labelId].Text, "selected circuit count shown");
                    TestRunner.Check(panel.Rows[labelId].Visible && panel.Rows["native"].Visible, "native and custom rows remain visible");
                }
                TestRunner.CheckEqual(2, panel.Commits, "no extra commit");
                TestRunner.CheckEqual(reads, manager.NetworkReads, "UI never enumerates all networks");
                Patch_PowerInfoCard_WireCount.Prefix(panel, otherTarget);
                TestRunner.CheckEqual("电线数量：2", panel.Rows[labelId].Text, "switching networks updates count immediately");
                TestRunner.Check(panel.Rows[labelId].Tooltip.Contains("选中对象") && panel.Rows[labelId].Tooltip.Contains("电桥本体"), "tooltip defines wire cells and excludes bridge bodies");
                panel.Commit();
                Patch_PowerInfoCard_WireCount.Prefix(panel, Target()); panel.Commit();
                TestRunner.Check(!panel.Rows[labelId].Visible, "nonpower target hides row");
                Patch_PowerInfoCard_WireCount.Prefix(null, target); Patch_PowerInfoCard_WireCount.Prefix(panel, null);

                TestRunner.Section("合并／拆分、重建和备用文本");
                manager.Put(20, wire20, 0, 1); manager.Put(21, new Wire(), 0, 1);
                Patch_PowerInfoCard_WireCount.Prefix(panel, target);
                TestRunner.CheckEqual("电线数量：统计中", panel.Rows[labelId].Text, "dirty topology does not show stale list");
                manager.SimulateRebuild(); CheckCount(target, 5, "merged network has five wires");
                var splitWire = new Wire(); manager.Put(1, splitWire, 0, 4); manager.SimulateRebuild();
                CheckCount(target, 4, "old network loses split wire"); CheckCount(Target(splitWire), 1, "split network has one wire");
                string key = "STRINGS.UI.UISIDESCREENS.MATERIALOVERHAUL.POWER_WIRE_COUNT";
                var saved = new Dictionary<string, string>();
                foreach (string suffix in new[] { "", "_PENDING", "_TOOLTIP" })
                {
                    if (Strings.Values.TryGetValue(key + suffix, out string value)) saved[key + suffix] = value;
                    Strings.Values.Remove(key + suffix);
                }
                Patch_PowerInfoCard_WireCount.Prefix(panel, target);
                TestRunner.CheckEqual("电线数量：4", panel.Rows[labelId].Text, "Chinese fallback uses new wire semantics");
                TestRunner.Check(!panel.Rows[labelId].Tooltip.Contains("MISSING"), "fallback tooltip valid");
                foreach (var entry in saved) Strings.Values[entry.Key] = entry.Value;
                global::Game.Instance = new global::Game { electricalConduitSystem = new UtilityNetworkManager<ElectricalUtilityNetwork, Wire>() };
                CheckCount(target, 0, "new save cannot reuse old count");
                global::Game.Instance = null;
                TestRunner.Check(!PowerWireCount.TryGet(target, out _), "exiting world has no cached count");
            }
            finally { PowerTopologyMonitor.Reset(); global::Game.Instance = null; }
        }
    }
}
