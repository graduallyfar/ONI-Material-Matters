using System;
using System.Collections.Generic;
using System.Reflection;
using MaterialOverhaul.Heating;
using MaterialOverhaul.Zones;

// 仅用于实际状态项注册和回调的源码级检查。构造重载按本机目标 DLL 保留语义；不运行 Unity。
public sealed class LocString
{
    public readonly string Text;
    private LocString(string text) { Text = text; }
    public static implicit operator LocString(string value) { return new LocString(value); }
    public static void CreateLocStringKeys(Type type, string prefix)
    {
        string current = prefix + type.Name.ToUpperInvariant() + ".";
        foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
            if (field.FieldType == typeof(LocString)) Strings.Values[current + field.Name.ToUpperInvariant()] = ((LocString)field.GetValue(null)).Text;
        foreach (Type nested in type.GetNestedTypes(BindingFlags.Public)) CreateLocStringKeys(nested, current);
    }
}

public enum NotificationType { Neutral, BadMinor }
public static class OverlayModes
{
    public static class None { public const string ID = "None"; }
}
public sealed class StatusItem
{
    public enum IconType { Custom }
    public readonly string Id, Name, Tooltip, Icon;
    public readonly bool ShowWorldIcon;
    public readonly NotificationType Notification;
    public Func<string, object, string> resolveStringCallback, resolveTooltipCallback;
    public StatusItem(string id, string prefix, string icon, IconType icon_type, NotificationType notification_type,
        bool allow_multiples, string render_overlay, bool showWorldIcon = true, int status_overlays = 129022,
        Func<string, object, string> resolve_string_callback = null)
        : this(id, Strings.Get("STRINGS." + prefix + ".STATUSITEMS." + id.ToUpperInvariant() + ".NAME"),
            Strings.Get("STRINGS." + prefix + ".STATUSITEMS." + id.ToUpperInvariant() + ".TOOLTIP"),
            icon, icon_type, notification_type, allow_multiples, render_overlay, status_overlays, showWorldIcon, resolve_string_callback) { }
    public StatusItem(string id, string name, string tooltip, string icon, IconType icon_type,
        NotificationType notification_type, bool allow_multiples, string render_overlay, int status_overlays = 129022,
        bool showWorldIcon = true, Func<string, object, string> resolve_string_callback = null)
    {
        Id = id; Name = name; Tooltip = tooltip; Icon = icon; Notification = notification_type;
        ShowWorldIcon = showWorldIcon; resolveStringCallback = resolve_string_callback;
    }
    public string ResolvedName(object data) { return resolveStringCallback == null ? Name : resolveStringCallback(Name, data); }
    public string ResolvedTooltip(object data) { return resolveTooltipCallback == null ? Tooltip : resolveTooltipCallback(Tooltip, data); }
}
public sealed class StatusItemCategory
{
    public readonly string Id;
    public StatusItemCategory(string id, object group, string name) { Id = id; }
}
public sealed class StatusCategories : List<StatusItemCategory>
{
    public readonly StatusItemCategory Main = new StatusItemCategory("Main", null, "Main");
}
public sealed class Db
{
    private static readonly Db Instance = new Db();
    public readonly StatusCategories StatusItemCategories = new StatusCategories();
    public readonly List<StatusItem> BuildingStatusItems = new List<StatusItem>();
    public static Db Get() { return Instance; }
}
public sealed class KSelectable
{
    public readonly Dictionary<StatusItemCategory, StatusItem> Items = new Dictionary<StatusItemCategory, StatusItem>();
    public void SetStatusItem(StatusItemCategory category, StatusItem item, object data)
    {
        if (item == null) Items.Remove(category); else Items[category] = item;
    }
}
public sealed class Descriptor
{
    public enum DescriptorType { Effect }
    public readonly string Text, Tooltip;
    public Descriptor(string text, string tooltip, DescriptorType type) { Text = text; Tooltip = tooltip; }
}
public static class GameUtil
{
    public static string GetFormattedTemperature(float temperature) { return (temperature - 273.15f).ToString("F1") + "℃"; }
    public static string GetFormattedPercent(float percent) { return percent.ToString("F0") + "%"; }
    public static string GetFormattedWattage(float watts) { return watts.ToString("F0") + " W"; }
}
namespace UnityEngine
{
    public static class Mathf
    {
        public static float Clamp01(float value) { return Math.Max(0f, Math.Min(1f, value)); }
    }
}
namespace MaterialOverhaul.Game
{
    public partial class MaterialOverhaulFab
    {
        private readonly KSelectable selectable = new KSelectable();
        public KSelectable Selection { get { return selectable; } }
        public bool ControlMode, ModeConnectionReady = true, IsCooling;
        public bool AutomationAllowsProduction = true, HardnessDeficit;
        public float BodyTemperature = 293.15f, CurrentEfficiency = 1f;
        public ResolvedZone CurrentZone;
        public RunDecision CurrentDecision;
        public TempState CurrentState = TempState.Normal;
        public RunPolicy Policy { get { return new RunPolicy { ControlMode = ControlMode, Forced = forcedRun }; } }
        public bool ZoneConfigured { get { return CurrentZone.Configured; } }
        public PowerDemand CurrentPower { get { return SafetyPower; } }
        public float CurrentRequestedWatts { get { return SafetyPower.RequestedTotalWatts; } }
        public float CurrentUsedWatts { get { return SafetyPower.UsedTotalWatts; } }
        public HeatingIntent AppliedHeating { get { return SafetyPower.AppliedHeating; } }
        public bool ProductionRunning { get { return SafetyPower.ProductionRunning; } }
        public float BatchProgress { get { return fabricator.OrderProgress; } }
        public string MaterialName { get { return "测试建材"; } }
        public bool HasMechanicalWear { get { return damageState.MechanicalLatched || damageState.ForcedOverheatLatched; } }
        private bool HasHardnessDeficit() { return HardnessDeficit; }
        public void RefreshRisks() { UpdateMaterialRiskStatusItems(); }
        public string HardnessText() { return "建造材料：测试建材\n生命值：" + HP.HitPoints; }
        internal static string LocalizedOr(string key, string fallback)
        {
            string text = Strings.Get(key);
            return string.IsNullOrEmpty(text) || text == key || text.StartsWith("MISSING.") ? fallback : text;
        }
    }
}
