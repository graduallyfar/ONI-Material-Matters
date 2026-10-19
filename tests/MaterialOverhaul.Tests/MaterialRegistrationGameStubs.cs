using System;
using System.Collections.Generic;
using Klei.AI;

// 与目标 DLL 的修饰项乘算口径一致；不加载 Unity，不代替游戏建造／读档验收。
public sealed class TestBuildingAttributes
{
    public readonly TestAttribute OverheatTemperature = new TestAttribute();
}
public sealed class TestAttribute { public string Id = "OverheatTemperature"; }
public enum SimHashes { Katairite, GoldAmalgam, Ceramic, Granite, Lead, SandStone, Copper, TestUnknown }
public static class GameTags
{
    public static Tag[] BasicRefinedMetals = {new Tag("Iron"),new Tag("Copper"),new Tag("Aluminum"),new Tag("Cobalt"),new Tag("Nickel")};
    public static readonly Tag BuildableRaw = new Tag("BuildableRaw");
    public static readonly Tag Metal = new Tag("Metal");
}
public sealed class Element
{
    public SimHashes id;
    public Tag[] oreTags = Array.Empty<Tag>();
    public readonly List<AttributeModifier> attributeModifiers = new List<AttributeModifier>();
    public bool HasTag(Tag tag) { return Array.Exists(oreTags, t => t.ToString() == tag.ToString()); }
}
public static class ElementLoader
{
    public static readonly List<Element> elements = new List<Element>();
    public static Element FindElementByHash(SimHashes id) { return elements.Find(e => e.id == id); }
}
public sealed class LegacyModMain { }
public sealed class BuildingLoader { }
public sealed class BuildingDef { public string[] MaterialCategory; }

namespace Klei.AI
{
    public sealed class AttributeModifier
    {
        public string AttributeId, Description;
        public float Value;
        public bool IsMultiplier, UIOnly, IsReadonly;
        public Func<string> NameCB, DescriptionCB;
        public object OverrideTimeSlice;
        public AttributeModifier(string id, float value, string description = null, bool is_multiplier = false,
            bool uiOnly = false, bool is_readonly = true)
        { Reconstruct(id, value, description, is_multiplier, uiOnly, is_readonly); }
        public void Reconstruct(string id, float value, string description, bool multiplier, bool uiOnly, bool readOnly)
        {
            AttributeId = id; Value = value; Description = description; IsMultiplier = multiplier;
            UIOnly = uiOnly; IsReadonly = readOnly; NameCB = null; DescriptionCB = null; OverrideTimeSlice = null;
        }
        public void SetValue(float value) { Value = value; }
    }
    public sealed class AttributeInstance
    {
        public readonly List<AttributeModifier> Modifiers = new List<AttributeModifier>();
        public float GetTotalValue()
        {
            float value = 0f, bonus = 0f;
            foreach (AttributeModifier modifier in Modifiers)
                if (!modifier.UIOnly)
                {
                    if (modifier.IsMultiplier) bonus += modifier.Value;
                    else value += modifier.Value;
                }
            return value + Math.Abs(value) * bonus;
        }
    }
}
