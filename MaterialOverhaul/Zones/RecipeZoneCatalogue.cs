using System;
using System.Collections.Generic;
using MaterialOverhaul.Core;

namespace MaterialOverhaul.Zones
{
    /// <summary>
    /// 一条配方温区条目。用「建筑 + 输入材料 + 输出材料」描述，而不是直接写原版配方标识：
    /// 配方标识由建筑名与材料名拼接而成（<c>ComplexRecipeManager.MakeRecipeID</c>），
    /// 直接写字符串容易因为标识格式或材料名变化而静默失配。接入层按本描述在原版配方表里解析出标识，
    /// 解析失败会记录可定位日志（V0.0.1 开发计划 §4.2、§9）。
    ///
    /// V0.1.2 起 `Zone` 只携带**配方最佳温区**两个边界；建筑下限与材料上限不在这里。
    /// </summary>
    public sealed class RecipeMaterial
    {
        public readonly string[] Materials;
        public readonly float Amount;
        public RecipeMaterial(string material, float amount) : this(new[] { material }, amount) { }
        public RecipeMaterial(string[] materials, float amount) { Materials = materials; Amount = amount; }
    }

    public sealed class RecipeZoneSpec
    {
        /// <summary>原版建筑 prefab id，例如 MetalRefinery。</summary>
        public string FabricatorId;

        /// <summary>输入材料标识（元素 tag）。多输入时按原版配方书写顺序给出，但匹配与顺序无关。</summary>
        public string[] Inputs;

        /// <summary>输出材料标识（元素 tag）。匹配与顺序无关。</summary>
        public string[] Outputs;

        /// <summary>配方最佳温区两个边界，单位开尔文。</summary>
        public TempZone Zone;

        /// <summary>数值来源说明，只用于日志与文档对照，不参与运算。</summary>
        public string Source;
        public float[] InputAmounts;
        public float[] OutputAmounts;
        // 只用于原版单输入的制砂候选集合，不能把 null material 当成实际元素。
        public string[] InputOptions;

        public bool MatchesRecipe(IList<RecipeMaterial> inputs, IList<RecipeMaterial> outputs)
        {
            if (!MatchSide(Outputs, OutputAmounts, outputs)) return false;
            if (InputOptions == null) return MatchSide(Inputs, InputAmounts, inputs);
            if (inputs == null || inputs.Count != 1 || inputs[0] == null || InputAmounts == null || InputAmounts.Length != 1
                || inputs[0].Materials == null || inputs[0].Materials.Length == 0
                || float.IsNaN(inputs[0].Amount) || float.IsInfinity(inputs[0].Amount)
                || Math.Abs(inputs[0].Amount - InputAmounts[0]) > 0.0001f) return false;
            foreach (string material in inputs[0].Materials)
                if (Array.IndexOf(InputOptions, material) < 0) return false;
            return true;
        }

        private static bool MatchSide(string[] ids, float[] amounts, IList<RecipeMaterial> actual)
        {
            if (ids == null || amounts == null || ids.Length != amounts.Length || actual == null || actual.Count != ids.Length)
                return false;
            bool[] used = new bool[actual.Count];
            for (int i = 0; i < ids.Length; i++)
            {
                bool found = false;
                for (int j = 0; j < actual.Count; j++)
                {
                    RecipeMaterial item = actual[j];
                    if (!used[j] && item != null && item.Materials != null && item.Materials.Length == 1
                        && item.Materials[0] == ids[i] && !float.IsNaN(item.Amount) && !float.IsInfinity(item.Amount)
                        && Math.Abs(item.Amount - amounts[i]) <= 0.0001f)
                    { used[j] = true; found = true; break; }
                }
                if (!found) return false;
            }
            return true;
        }

        /// <summary>
        /// 给定的输入输出材料标识是否与本条目一致（按多重集合比较，与顺序无关）。
        /// 纯字符串逻辑，可在判断层单独检查。
        /// </summary>
        public bool MatchesMaterials(IList<string> inputs, IList<string> outputs)
        {
            return SameElements(Inputs, inputs) && SameElements(Outputs, outputs);
        }

        private static bool SameElements(string[] expected, IList<string> actual)
        {
            if (expected == null || actual == null || expected.Length != actual.Count)
            {
                return false;
            }

            List<string> pool = new List<string>(actual);
            foreach (string item in expected)
            {
                int index = pool.IndexOf(item);
                if (index < 0)
                {
                    return false;
                }

                pool.RemoveAt(index);
            }

            return pool.Count == 0;
        }
    }

    /// <summary>
    /// 用户授权接入的首发配方最佳温区初值，平衡仍待游戏验收；未设计配方不占位：
    /// 未登记的配方保持原版行为，并由接入层记录一条待定日志（避免「名单内配方静默落空」）。
    ///
    /// 首发候选：金属13、粉碎26、玻璃1。按完整材料与质量在真实配方表中匹配，DLC缺失跳过。
    /// 建筑下限见 <see cref="BuildingZoneTable"/>，上限由主建造材料推出；铝450–650 ℃沿用已确认值。
    ///
    /// 旧版「300／450／650／800 ℃ 四边界」中的 300 ℃ 已改由建筑下限承担（本版 80 ℃），
    /// 800 ℃ 不再是配方属性：它由主建造材料熔点 × 0.75 决定，因此同一配方可以用不同建材跑出
    /// 不同的最高工作温度（建筑细节文档 §2.1.5）。
    /// </summary>
    public static class RecipeZoneCatalogue
    {
        private static readonly RecipeZoneSpec[] Entries = BuildEntries();

        public static IList<RecipeZoneSpec> All
        {
            get { return Entries; }
        }

        public static int Count
        {
            get { return Entries.Length; }
        }

        private static RecipeZoneSpec Entry(string building, string[] inputs, float[] inputAmounts,
            string[] outputs, float[] outputAmounts, float bestMin, float bestMax)
        {
            return new RecipeZoneSpec
            {
                FabricatorId = building, Inputs = inputs, InputAmounts = inputAmounts,
                Outputs = outputs, OutputAmounts = outputAmounts,
                Zone = TempZone.Celsius(bestMin, bestMax),
                Source = "首发三建筑数值设计（用户授权接入，游戏平衡待验收）"
            };
        }

        private static RecipeZoneSpec[] BuildEntries()
        {
            List<RecipeZoneSpec> entries = new List<RecipeZoneSpec>();
            entries.Add(Entry("MetalRefinery", new[] { "Cinnabar" }, new[] { 100f }, new[] { "Mercury" }, new[] { 100f }, 140f, 240f));
            entries.Add(Entry("MetalRefinery", new[] { "Galena" }, new[] { 100f }, new[] { "Lead", "Sulfur" }, new[] { 87f, 13f }, 180f, 300f));
            entries.Add(Entry("MetalRefinery", new[] { "ZincOre" }, new[] { 100f }, new[] { "Zinc" }, new[] { 100f }, 200f, 320f));
            entries.Add(Entry("MetalRefinery", new[] { "GoldAmalgam" }, new[] { 100f }, new[] { "Gold" }, new[] { 100f }, 400f, 600f));
            entries.Add(Entry("MetalRefinery", new[] { "AluminumOre" }, new[] { 100f }, new[] { "Aluminum" }, new[] { 100f }, 450f, 650f));
            entries.Add(Entry("MetalRefinery", new[] { "Cuprite" }, new[] { 100f }, new[] { "Copper" }, new[] { 100f }, 500f, 700f));
            entries.Add(Entry("MetalRefinery", new[] { "NickelOre" }, new[] { 100f }, new[] { "Nickel" }, new[] { 100f }, 650f, 900f));
            entries.Add(Entry("MetalRefinery", new[] { "Cobaltite" }, new[] { 100f }, new[] { "Cobalt" }, new[] { 100f }, 700f, 950f));
            entries.Add(Entry("MetalRefinery", new[] { "IronOre" }, new[] { 100f }, new[] { "Iron" }, new[] { 100f }, 800f, 1100f));
            entries.Add(Entry("MetalRefinery", new[] { "FoolsGold" }, new[] { 100f }, new[] { "Iron" }, new[] { 100f }, 800f, 1100f));
            entries.Add(Entry("MetalRefinery", new[] { "Iron", "RefinedCarbon", "Lime" }, new[] { 70f, 20f, 10f }, new[] { "Steel" }, new[] { 100f }, 1000f, 1400f));
            entries.Add(Entry("MetalRefinery", new[] { "TempConductorSolid" }, new[] { 100f }, new[] { "Niobium" }, new[] { 100f }, 1000f, 1400f));
            entries.Add(Entry("MetalRefinery", new[] { "Wolframite" }, new[] { 100f }, new[] { "Tungsten" }, new[] { 100f }, 1200f, 1600f));
            entries.Add(Entry("RockCrusher", new[] { "Cinnabar" }, new[] { 100f }, new[] { "Mercury", "Sand" }, new[] { 50f, 50f }, 15f, 60f));
            entries.Add(Entry("RockCrusher", new[] { "Galena" }, new[] { 100f }, new[] { "Lead", "Sulfur", "Sand" }, new[] { 43.5f, 6.5f, 50f }, 15f, 60f));
            entries.Add(Entry("RockCrusher", new[] { "ZincOre" }, new[] { 100f }, new[] { "Zinc", "Sand" }, new[] { 50f, 50f }, 15f, 60f));
            entries.Add(Entry("RockCrusher", new[] { "GoldAmalgam" }, new[] { 100f }, new[] { "Gold", "Sand" }, new[] { 50f, 50f }, 15f, 60f));
            entries.Add(Entry("RockCrusher", new[] { "AluminumOre" }, new[] { 100f }, new[] { "Aluminum", "Sand" }, new[] { 50f, 50f }, 15f, 60f));
            entries.Add(Entry("RockCrusher", new[] { "Cuprite" }, new[] { 100f }, new[] { "Copper", "Sand" }, new[] { 50f, 50f }, 15f, 60f));
            entries.Add(Entry("RockCrusher", new[] { "NickelOre" }, new[] { 100f }, new[] { "Nickel", "Sand" }, new[] { 50f, 50f }, 15f, 60f));
            entries.Add(Entry("RockCrusher", new[] { "Cobaltite" }, new[] { 100f }, new[] { "Cobalt", "Sand" }, new[] { 50f, 50f }, 15f, 60f));
            entries.Add(Entry("RockCrusher", new[] { "IronOre" }, new[] { 100f }, new[] { "Iron", "Sand" }, new[] { 50f, 50f }, 15f, 60f));
            entries.Add(Entry("RockCrusher", new[] { "FoolsGold" }, new[] { 100f }, new[] { "Iron", "Sand" }, new[] { 50f, 50f }, 15f, 60f));
            entries.Add(Entry("RockCrusher", new[] { "TempConductorSolid" }, new[] { 100f }, new[] { "Niobium", "Sand" }, new[] { 50f, 50f }, 15f, 60f));
            entries.Add(Entry("RockCrusher", new[] { "Wolframite" }, new[] { 100f }, new[] { "Tungsten", "Sand" }, new[] { 50f, 50f }, 15f, 60f));
            entries.Add(Entry("RockCrusher", new[] { "EggShell" }, new[] { 5f }, new[] { "Lime" }, new[] { 5f }, 15f, 60f));
            entries.Add(Entry("RockCrusher", new[] { "CrabShell" }, new[] { 60f }, new[] { "Lime" }, new[] { 60f }, 15f, 60f));
            entries.Add(Entry("RockCrusher", new[] { "CrabWoodShell" }, new[] { 500f }, new[] { "WoodLog" }, new[] { 500f }, 15f, 60f));
            entries.Add(Entry("RockCrusher", new[] { "SnailShell" }, new[] { 10f }, new[] { "Lime" }, new[] { 10f }, 15f, 60f));
            entries.Add(Entry("RockCrusher", new[] { "SnailIronShell" }, new[] { 10f }, new[] { "GoldAmalgam" }, new[] { 10f }, 15f, 60f));
            entries.Add(Entry("RockCrusher", new[] { "Fossil" }, new[] { 100f }, new[] { "Lime", "SedimentaryRock" }, new[] { 5f, 95f }, 15f, 60f));
            entries.Add(Entry("RockCrusher", new[] { "Corallium" }, new[] { 100f }, new[] { "Lime", "Sand" }, new[] { 10f, 90f }, 15f, 60f));
            entries.Add(Entry("RockCrusher", new[] { "GarbageElectrobank" }, new[] { 1f }, new[] { "Katairite" }, new[] { 100f }, 15f, 60f));
            entries.Add(Entry("RockCrusher", new[] { "Salt" }, new[] { 100f }, new[] { "TableSalt", "Sand" }, new[] { 0.005f, 99.995f }, 15f, 60f));
            entries.Add(Entry("RockCrusher", new[] { "Fullerene" }, new[] { 100f }, new[] { "Graphite", "Sand" }, new[] { 90f, 10f }, 15f, 60f));
            entries.Add(Entry("RockCrusher", new[] { "IceBellyPoop" }, new[] { 120f }, new[] { "Phosphorite", "Clay" }, new[] { 32.004f, 87.996f }, 15f, 60f));
            entries.Add(Entry("RockCrusher", new[] { "Urchin" }, new[] { 1f }, new[] { "Diamond" }, new[] { 100f }, 15f, 60f));
            entries.Add(Entry("RockCrusher", new[] { "GoldBellyCrown" }, new[] { 1f }, new[] { "GoldAmalgam" }, new[] { 250f }, 15f, 60f));
            RecipeZoneSpec sand = Entry("RockCrusher", new[] { "Crushable" }, new[] { 100f },
                new[] { "Sand" }, new[] { 100f }, 15f, 60f);
            sand.InputOptions = new[] { "Ceramic", "Granite", "IgneousRock", "Obsidian", "SandStone",
                "SedimentaryRock", "Shale", "SuperInsulator", "SiltStone", "Coquina", "Basalt" };
            entries.Add(sand);
            entries.Add(Entry("GlassForge", new[] { "Sand" }, new[] { 100f }, new[] { "MoltenGlass" }, new[] { 25f }, 900f, 1200f));
            return entries.ToArray();
        }
    }
}
