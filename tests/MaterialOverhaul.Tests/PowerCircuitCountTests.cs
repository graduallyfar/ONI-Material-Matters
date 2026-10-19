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
    public static class PowerCircuitCountTests
    {
        public static void Run()
        {
            var sources = new HashSet<Generator>();
            var consumers = new HashSet<IEnergyConsumer>();
            PowerTopologyMonitor.Reset(); PowerCircuitCount.Reset();
            var manager = new UtilityNetworkManager<ElectricalUtilityNetwork, Wire>();
            global::Game.Instance = new global::Game { electricalConduitSystem = manager };
            var circuits = global::Game.Instance.circuitManager;
            PowerTopologyMonitor.Attach(global::Game.Instance);
            try
            {
                TestRunner.Section("实际独立电网计数，规划／重复／虚拟网络");
                int count;
                TestRunner.Check(!PowerCircuitCount.TryGet(out count), "unbuilt cache not reported as zero");
                manager.SimulateRebuild(); PowerCircuitCount.Refresh(circuits, sources, consumers);
                TestRunner.Check(PowerCircuitCount.TryGet(out count) && count == 0, "empty save has zero actual networks");
                var wire0 = new Wire();
                manager.Put(0, wire0, UtilityConnections.Right, 1);
                manager.Put(1, new Wire(), UtilityConnections.Left, 1);
                manager.Put(20, new Wire(), 0, 2);
                manager.Put(30, new Wire { Disconnected = true }, 0, 3);
                // 显示网络可能重复引用实际线，但不应增加电网数。
                var display = new ElectricalUtilityNetwork { id = 500 };
                display.allWires.Add(wire0); manager.ExtraNetworks.Add(display);
                manager.ExtraNetworks.Add(new ElectricalUtilityNetwork { id = 501 });
                var orphanDisplay = new ElectricalUtilityNetwork { id = 502 };
                orphanDisplay.allWires.Add(new Wire { Cell = 999 }); manager.ExtraNetworks.Add(orphanDisplay);
                manager.SimulateRebuild(); PowerCircuitCount.Refresh(circuits, sources, consumers);
                TestRunner.Check(PowerCircuitCount.TryGet(out count) && count == 2,
                    "deduplicate actual ids; count isolated unpowered wire; exclude plans and disconnected wire");
                var virtualGenerator = new Generator { IsVirtual = true };
                var virtualBattery = new Battery { IsVirtual = true };
                sources.Add(virtualGenerator); consumers.Add(virtualBattery);
                circuits.VirtualIds[virtualGenerator] = 77; circuits.VirtualIds[virtualBattery] = 77;
                consumers.Add(new EnergyConsumer { IsVirtual = true }); // 原版INVALID_ID不计。
                PowerCircuitCount.Refresh(circuits, sources, consumers);
                TestRunner.Check(PowerCircuitCount.TryGet(out count) && count == 3, "one virtual circuit deduplicated across components");

                TestRunner.Section("实际信息卡前缀与原版提交生命周期");
                var target = new UnityEngine.GameObject(); target.Components.Add(wire0);
                var panel = new CollapsibleDetailContentPanel();
                string labelId = Patch_PowerInfoCard_NetworkCount.LabelId;
                int reads = manager.NetworkReads;
                for (int pass = 0; pass < 2; pass++)
                {
                    Patch_PowerInfoCard_NetworkCount.Prefix(panel, target);
                    panel.SetLabel("native", "原版发电／耗电", "原版说明"); panel.Commit();
                    TestRunner.CheckEqual("电网数量：3", panel.Rows[labelId].Text, "global total shown on wire info card");
                    TestRunner.Check(panel.Rows[labelId].Visible && panel.Rows["native"].Visible, "custom and native lines both remain visible");
                }
                TestRunner.CheckEqual(2, panel.Commits, "prefix does not perform extra commit");
                TestRunner.CheckEqual(reads, manager.NetworkReads, "UI refresh reads cached count without rescanning");
                TestRunner.Check(panel.Rows[labelId].Tooltip.Contains("当前存档") && panel.Rows[labelId].Tooltip.Contains("不计规划"), "tooltip explains global scope");
                var unrelated = new UnityEngine.GameObject();
                Patch_PowerInfoCard_NetworkCount.Prefix(panel, unrelated); panel.Commit();
                TestRunner.Check(!panel.Rows[labelId].Visible, "switching to unrelated target removes row via native commit");
                Patch_PowerInfoCard_NetworkCount.Prefix(null, target);
                Patch_PowerInfoCard_NetworkCount.Prefix(panel, null);
                var disconnectedTarget = new UnityEngine.GameObject();
                disconnectedTarget.Components.Add(new EnergyConsumer { PowerCell = 999 });
                Patch_PowerInfoCard_NetworkCount.Prefix(panel, disconnectedTarget);
                TestRunner.CheckEqual("电网数量：3", panel.Rows[labelId].Text, "disconnected selected consumer still sees global total");

                TestRunner.Section("合并／拆分、重建间隙、清理与备用文本");
                manager.Put(20, new Wire(), 0, 1);
                Patch_PowerInfoCard_NetworkCount.Prefix(panel, target);
                TestRunner.CheckEqual("电网数量：统计中", panel.Rows[labelId].Text, "dirty network does not show stale count");
                manager.SimulateRebuild();
                TestRunner.Check(!PowerCircuitCount.TryGet(out count), "rebuild callback invalidates before circuit refresh");
                PowerCircuitCount.Refresh(circuits, sources, consumers);
                TestRunner.Check(PowerCircuitCount.TryGet(out count) && count == 2, "merging physical networks reduces count");
                manager.Put(1, new Wire(), 0, 4); manager.SimulateRebuild();
                PowerCircuitCount.Refresh(circuits, sources, consumers);
                TestRunner.Check(PowerCircuitCount.TryGet(out count) && count == 3, "splitting physical network increases count");
                string key = "STRINGS.UI.UISIDESCREENS.MATERIALOVERHAUL.POWER_NETWORK_COUNT";
                var saved = new Dictionary<string, string>();
                foreach (string suffix in new[] { "", "_PENDING", "_TOOLTIP" })
                {
                    if (Strings.Values.TryGetValue(key + suffix, out string value)) saved[key + suffix] = value;
                    Strings.Values.Remove(key + suffix);
                }
                Patch_PowerInfoCard_NetworkCount.Prefix(panel, target);
                TestRunner.CheckEqual("电网数量：3", panel.Rows[labelId].Text, "missing localization uses Chinese fallback");
                TestRunner.Check(!panel.Rows[labelId].Tooltip.Contains("MISSING"), "fallback tooltip valid");
                foreach (var entry in saved) Strings.Values[entry.Key] = entry.Value;
                global::Game.Instance = new global::Game { electricalConduitSystem = new UtilityNetworkManager<ElectricalUtilityNetwork, Wire>() };
                TestRunner.Check(!PowerCircuitCount.TryGet(out count), "new world cannot reuse old total");
                PowerCircuitCount.Reset(); TestRunner.Check(!PowerCircuitCount.TryGet(out count), "cleanup clears total");
            }
            finally { PowerCircuitCount.Reset(); PowerTopologyMonitor.Reset(); global::Game.Instance = null; }
        }
    }
}
