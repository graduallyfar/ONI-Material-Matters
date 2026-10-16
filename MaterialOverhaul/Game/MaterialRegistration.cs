using System;
using Klei.AI;
using MaterialOverhaul.Core;

namespace MaterialOverhaul.Game
{
    /// <summary>材料类别与过热修饰项。启动时处理，不在模拟更新中扫描元素。</summary>
    internal static class MaterialRegistration
    {
        public static void AddAbyssaliteBuildingTag()
        {
            Element element = ElementLoader.FindElementByHash(SimHashes.Katairite);
            if (element == null)
            {
                ModLog.WarnOnce("material|missing-katairite", "找不到 Katairite，无法加入矿物原料建材。");
                return;
            }

            if (element.HasTag(GameTags.BuildableRaw)) return;
            Tag[] tags = element.oreTags ?? Array.Empty<Tag>();
            Array.Resize(ref tags, tags.Length + 1);
            tags[tags.Length - 1] = GameTags.BuildableRaw;
            element.oreTags = tags;
            ModLog.Info("深渊晶石已加入 BuildableRaw，所有接受矿物原料的建筑均可选用。");
        }

        public static void ConvertOverheatBonuses()
        {
            string id = Db.Get().BuildingAttributes.OverheatTemperature.Id;
            int count = 0;
            foreach (Element element in ElementLoader.elements)
            {
                if (element.id == SimHashes.Katairite)
                {
                    // 深渊晶石不提供过热加成，材料面板也不留下无意义的 0% 条目。
                    count += element.attributeModifiers.RemoveAll(m => m.AttributeId == id);
                    continue;
                }
                foreach (AttributeModifier modifier in element.attributeModifiers)
                {
                    if (modifier.AttributeId != id || modifier.UIOnly) continue;
                    if (float.IsNaN(modifier.Value) || float.IsInfinity(modifier.Value))
                    {
                        ModLog.WarnOnce("material|bad-bonus|" + element.id, element.id + " 的过热修正不可用，保留原值。");
                        continue;
                    }
                    float value = modifier.Value > 0f ? Balance.MaterialOverheatBonus
                        : modifier.IsMultiplier ? modifier.Value : modifier.Value * Balance.MaterialOverheatPenaltyPerKelvin;
                    if (float.IsNaN(value) || float.IsInfinity(value) || value <= -1f)
                    {
                        ModLog.WarnOnce("material|bad-bonus|" + element.id,
                            element.id + " 的过热修正不可用，保留原值。");
                        continue;
                    }

                    if (modifier.IsMultiplier && modifier.Value == value) continue;

                    // 原地修改，保留已引用此修饰项的原版对象；已符合规则的倍率项重复调用时跳过。
                    var name = modifier.NameCB;
                    var description = modifier.DescriptionCB;
                    var timeSlice = modifier.OverrideTimeSlice;
                    modifier.Reconstruct(id, value, modifier.Description, true, modifier.UIOnly, modifier.IsReadonly);
                    modifier.NameCB = name;
                    modifier.DescriptionCB = description;
                    modifier.OverrideTimeSlice = timeSlice;
                    count++;
                }
            }
            ModLog.Info("材料过热修正已更新，共 " + count + " 项；正向统一+60%，深渊晶石无加成，负向修正不变。");
        }

        /// <summary>只扩展建筑的建材槽位，不把精炼金属改成矿石元素，避免影响工业配方。</summary>
        public static void AllowRefinedMetalConstruction(BuildingDef def)
        {
            if (def == null || def.MaterialCategory == null) return;
            string[] categories = null;
            for (int i = 0; i < def.MaterialCategory.Length; i++)
            {
                string category = def.MaterialCategory[i];
                if (string.IsNullOrEmpty(category)) continue;
                string[] options = category.Split('&');
                if (Array.IndexOf(options, "Metal") < 0 || Array.IndexOf(options, "RefinedMetal") >= 0) continue;
                if (categories == null) categories = (string[])def.MaterialCategory.Clone();
                categories[i] = category + "&RefinedMetal";
            }
            if (categories != null) def.MaterialCategory = categories;
        }

        public static float OverheatMultiplier(Element element)
        {
            float bonus = 0f;
            string id = Db.Get().BuildingAttributes.OverheatTemperature.Id;
            foreach (AttributeModifier modifier in element.attributeModifiers)
                if (modifier.AttributeId == id && modifier.IsMultiplier && !modifier.UIOnly)
                    bonus += modifier.Value;
            return 1f + bonus;
        }

        /// <summary>补偿的是乘算前的基础项，避免倍率令旧补偿反复叠加或抵消材料收益。</summary>
        public static bool SetOverheatTarget(AttributeInstance instance, AttributeModifier correction, float target)
        {
            float multiplier = 1f;
            for (int i = 0; i < instance.Modifiers.Count; i++)
            {
                AttributeModifier modifier = instance.Modifiers[i];
                if (modifier.IsMultiplier && !modifier.UIOnly) multiplier += modifier.Value;
            }
            if (float.IsNaN(multiplier) || float.IsInfinity(multiplier) || multiplier <= 0f) return false;
            float fixedBase = instance.GetTotalValue() / multiplier - correction.Value;
            correction.SetValue(target / multiplier - fixedBase);
            return true;
        }
    }
}
