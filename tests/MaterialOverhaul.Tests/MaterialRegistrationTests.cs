using System;
using System.Reflection;
using Klei.AI;
using MaterialOverhaul.Game;
using MaterialOverhaul.Heating;

namespace MaterialOverhaul.Tests
{
    public static class MaterialRegistrationTests
    {
        private const float Tol = .001f;
        private const string Heat = "OverheatTemperature";
        public static void Run()
        {
            ElementLoader.elements.Clear();
            MaterialRegistration.AddAbyssaliteBuildingTag(); // 缺少元素不能崩溃。
            Element abyss = new Element { id = SimHashes.Katairite, oreTags = new[] { new Tag("Other") } };
            ElementLoader.elements.Add(abyss);
            MaterialRegistration.AddAbyssaliteBuildingTag();
            MaterialRegistration.AddAbyssaliteBuildingTag();
            TestRunner.Check(abyss.HasTag(GameTags.BuildableRaw), "深渊晶石可用于接受矿物原料的建筑");
            TestRunner.Check(!abyss.HasTag(GameTags.Metal), "不替代金属矿石");
            TestRunner.CheckEqual(2, abyss.oreTags.Length, "重复加载不重复增加标签，原标签保留");
            TestRunner.Check(abyss.HasTag(new Tag("Other")), "原有其它分类保留");
            float[] kelvin = { 200f, 50f, 15f, -20f, 0f, 25f, 500f, 900f };
            float[] multipliers = { 1f, 1.6f, 1.6f, .98f, 1f, 1.6f, 1.6f, 1.6f };
            for (int i = 0; i < kelvin.Length; i++)
            {
                Element element = i == 0 ? abyss : new Element { id = SimHashes.Ceramic };
                if (i > 0) ElementLoader.elements.Add(element);
                AttributeModifier modifier = new AttributeModifier(Heat, kelvin[i], "原材料说明");
                modifier.NameCB = () => "材料名";
                modifier.DescriptionCB = () => "说明";
                modifier.OverrideTimeSlice = "保留";
                element.attributeModifiers.Add(modifier);
                element.attributeModifiers.Add(new AttributeModifier("Decor", .2f, "装饰", true));
                MaterialRegistration.ConvertOverheatBonuses();
                MaterialRegistration.ConvertOverheatBonuses();
                TestRunner.CheckClose(multipliers[i], MaterialRegistration.OverheatMultiplier(element), Tol, "材料倍率 " + kelvin[i]);
                if (i == 0)
                {
                    TestRunner.CheckEqual(1, element.attributeModifiers.Count, "深渊晶石移除过热条目，仅保留其它属性");
                    TestRunner.CheckEqual("Decor", element.attributeModifiers[0].AttributeId, "深渊晶石装饰属性保留");
                    continue;
                }
                TestRunner.Check(modifier.IsMultiplier && ReferenceEquals(modifier, element.attributeModifiers[0]), "原地转倍率，引用不失效");
                TestRunner.CheckEqual("材料名", modifier.NameCB(), "名称回调保留");
                TestRunner.CheckEqual("说明", modifier.DescriptionCB(), "说明回调保留");
                TestRunner.CheckEqual("保留", (string)modifier.OverrideTimeSlice, "显示时间口径保留");
                TestRunner.CheckClose(.2f, element.attributeModifiers[1].Value, Tol, "装饰属性不变");
                float result;
                TestRunner.Check(OverheatRule.TryCompute(3695f, .75f, multipliers[i], out result), "带材料倍率计算合法");
                TestRunner.CheckClose(3695f * .75f * multipliers[i], result, Tol, "在 K 基准乘算");
            }
            float upper;
            TestRunner.Check(OverheatRule.TryCompute(3695f, .75f, MaterialRegistration.OverheatMultiplier(abyss), out upper), "深渊晶石上限可用");
            TestRunner.CheckClose(2771.25f, upper, Tol, "深渊晶石上限 2498.10 ℃，无加成");
            foreach (float invalid in new[] { 0f, -1f, float.NaN, float.PositiveInfinity })
                TestRunner.Check(!OverheatRule.TryCompute(3695f, .75f, invalid, out upper), "异常倍率不能生成上限");

            AttributeInstance ordinary = new AttributeInstance();
            ordinary.Modifiers.Add(new AttributeModifier(Heat, 348.15f));
            ordinary.Modifiers.Add(ElementLoader.elements[1].attributeModifiers[0]);
            TestRunner.CheckClose(557.04f, ordinary.GetTotalValue(), Tol, "三栋外75℃基础阈值按K增加60%，得到283.89℃");
            ordinary.Modifiers.Remove(ElementLoader.elements[1].attributeModifiers[0]);
            ordinary.Modifiers.Add(ElementLoader.elements[3].attributeModifiers[0]);
            TestRunner.CheckClose(341.187f, ordinary.GetTotalValue(), Tol, "负加成在原版属性系统同样按K减少2%");

            AttributeInstance instance = new AttributeInstance();
            instance.Modifiers.Add(new AttributeModifier(Heat, 348.15f));
            AttributeModifier materialBonus = ElementLoader.elements[1].attributeModifiers[0];
            instance.Modifiers.Add(materialBonus);
            var correction = new AttributeModifier(Heat, 0f);
            instance.Modifiers.Add(correction);
            for (int repeat = 0; repeat < 5; repeat++)
            {
                TestRunner.Check(MaterialRegistration.SetOverheatTarget(instance, correction, 3325.5f), "旧档／重复应用可用");
                TestRunner.CheckClose(3325.5f, instance.GetTotalValue(), Tol, "过热属性与工作上限一致且不叠加");
            }
            instance.Modifiers.Add(new AttributeModifier(Heat, .1f, "辅料", true));
            MaterialRegistration.SetOverheatTarget(instance, correction, 3325.5f);
            TestRunner.CheckClose(3325.5f, instance.GetTotalValue(), Tol, "三栋只使用主材料的倍率");
            instance.Modifiers.Remove(materialBonus);
            MaterialRegistration.SetOverheatTarget(instance, correction, 900f);
            TestRunner.CheckClose(900f, instance.GetTotalValue(), Tol, "换材料后阈值重算");
            instance.Modifiers.Add(new AttributeModifier(Heat, float.NaN, "异常", true));
            TestRunner.Check(!MaterialRegistration.SetOverheatTarget(instance, correction, 900f), "异常总倍率有明确失败结果");
            TestRunner.Check(typeof(MaterialOverhaul.Patches.Patch_ElementLoader_Load).GetMethod("Postfix", BindingFlags.NonPublic | BindingFlags.Static) != null,
                "元素加载挂钩存在");
            ConstructionCategories();
        }

        private static void ConstructionCategories()
        {
            MaterialRegistration.AllowRefinedMetalConstruction(null);
            MaterialRegistration.AllowRefinedMetalConstruction(new BuildingDef());
            string[] shared = { "Metal", "BuildableRaw&Metal", "Plastic", "RefinedMetal", "Metal&RefinedMetal", "LiquidMetal", null };
            var def = new BuildingDef { MaterialCategory = shared };
            MaterialRegistration.AllowRefinedMetalConstruction(def);
            MaterialRegistration.AllowRefinedMetalConstruction(def);
            TestRunner.CheckEqual("Metal&RefinedMetal", def.MaterialCategory[0], "矿石槽位允许精炼金属");
            TestRunner.CheckEqual("BuildableRaw&Metal&RefinedMetal", def.MaterialCategory[1], "混合类别保留旧候选并增加精炼金属");
            TestRunner.CheckEqual("Plastic", def.MaterialCategory[2], "非金属槽位不变");
            TestRunner.CheckEqual("RefinedMetal", def.MaterialCategory[3], "纯精炼金属不反向开放矿石");
            TestRunner.CheckEqual("Metal&RefinedMetal", def.MaterialCategory[4], "已有组合不重复添加");
            TestRunner.CheckEqual("LiquidMetal", def.MaterialCategory[5], "只匹配完整类别名");
            TestRunner.Check(def.MaterialCategory[6] == null, "空槽位不崩溃");
            TestRunner.CheckEqual("Metal", shared[0], "不修改原版共享类别数组");
            TestRunner.CheckEqual(7, def.MaterialCategory.Length, "建材槽位数量保持");
        }
    }
}
