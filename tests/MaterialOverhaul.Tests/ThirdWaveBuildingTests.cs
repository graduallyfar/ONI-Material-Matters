using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using MaterialOverhaul.Core;
using MaterialOverhaul.Game;
using MaterialOverhaul.Hardness;
using MaterialOverhaul.Heating;
using MaterialOverhaul.Zones;

namespace MaterialOverhaul.Tests
{
    /// <summary>
    /// 第三批 23 栋工业建筑的专项回归（第三批工业23建筑开发计划 §8）。
    ///
    /// 覆盖范围：数值与范围、温控端点与启动目标、加热范围与过热豁免、60 个配方候选的完整材料匹配、
    /// 自动化端口占格与冲突、当前文本键与中文 PO 一致、连续转换的真实生产秒数与区间并集。
    ///
    /// **不覆盖**：Harmony 补丁在游戏内的实际装配、SIM 质量与电热结算、复制人寻路与动画、读档恢复。
    /// 这些仍需按 §9 的 I01–I20 在游戏内验收，替身测试不能替代。
    /// </summary>
    public static class ThirdWaveBuildingTests
    {
        private const float Tol = 0.01f;
        private static float C(float c) { return Temperatures.FromCelsius(c); }

        /// <summary>第三批 23 栋：prefab id、L(℃)、B₁、B₂、εL、εH、生产W、内置加热、过热豁免、HP/生产秒、类型。</summary>
        private sealed class Row
        {
            public string Id; public float L, B1, B2, Low, High, Watts, Wear;
            public bool Heating, Immune; public ProductionKind Kind;
            public Row(string id, float l, float b1, float b2, float low, float high, float watts,
                bool heating, bool immune, float wear, ProductionKind kind)
            { Id = id; L = l; B1 = b1; B2 = b2; Low = low; High = high; Watts = watts;
              Heating = heating; Immune = immune; Wear = wear; Kind = kind; }
        }

        private static readonly Row[] Third =
        {
            new Row("Chlorinator",          0f,  20f,  60f, .75f, .90f,  480f, false, false, .01f, ProductionKind.Batch),
            new Row("DiamondPress",        20f, 400f, 700f, .50f, .80f,  240f, true,  false, .03f, ProductionKind.Batch),
            new Row("UraniumCentrifuge",    0f,  40f, 100f, .70f, .90f,  480f, false, true,  .02f, ProductionKind.Batch),
            new Row("RubberMaker",          0f, 120f, 180f, .60f, .85f,  480f, true,  false, .02f, ProductionKind.Batch),
            new Row("FabricatedWoodMaker",-20f,  40f,  90f, .80f, .90f,  480f, false, false, .02f, ProductionKind.Batch),
            new Row("SludgePress",        -20f,  10f,  50f, .85f, .95f,  120f, false, false, .02f, ProductionKind.Batch),
            new Row("MissileFabricator",  -20f,  15f,  50f, .85f, .95f,  960f, false, false, .02f, ProductionKind.Batch),
            new Row("OilRefinery",          0f,  80f, 140f, .60f, .85f,  480f, true,  false, .02f, ProductionKind.Continuous),
            new Row("Polymerizer",          0f,  70f, 130f, .60f, .85f,  240f, true,  false, .02f, ProductionKind.Continuous),
            new Row("EthanolDistillery",    0f,  60f, 100f, .65f, .85f,  240f, true,  true,  .01f, ProductionKind.Continuous),
            new Row("FertilizerMaker",    -10f,  20f,  50f, .80f, .90f,  120f, false, false, .01f, ProductionKind.Continuous),
            new Row("AlgaeDistillery",    -10f,  20f,  40f, .85f, .90f,  120f, false, true,  .01f, ProductionKind.Continuous),
            new Row("OxyliteRefinery",    -20f,  10f,  40f, .85f, .90f, 1200f, false, true,  .01f, ProductionKind.Continuous),
            new Row("WaterPurifier",      -20f,  10f,  50f, .90f, .95f,  120f, false, false, .01f, ProductionKind.Continuous),
            new Row("Desalinator",        -20f,  10f,  50f, .85f, .95f,  480f, false, false, .02f, ProductionKind.Continuous),
            new Row("Generator",          -20f,  60f, 180f, .80f, .90f,    0f, false, false, .02f, ProductionKind.Generator),
            new Row("PeatGenerator",      -20f,  40f, 140f, .85f, .90f,    0f, false, false, .02f, ProductionKind.Generator),
            new Row("HydrogenGenerator",  -20f,  40f, 120f, .85f, .90f,    0f, false, false, .01f, ProductionKind.Generator),
            new Row("MethaneGenerator",   -20f,  60f, 160f, .80f, .90f,    0f, false, false, .02f, ProductionKind.Generator),
            new Row("PetroleumGenerator", -20f,  80f, 180f, .80f, .90f,    0f, false, false, .02f, ProductionKind.Generator),
            new Row("WoodGasGenerator",   -20f,  40f, 120f, .85f, .90f,    0f, false, false, .02f, ProductionKind.Generator),
            new Row("OilWellCap",           0f,  60f, 120f, .80f, .90f,  240f, false, false, .02f, ProductionKind.OilWell),
            new Row("OreScrubber",        -20f,  10f,  40f, .90f, .95f,    0f, false, false, .005f, ProductionKind.OreScrubber)
        };

        /// <summary>原六栋的冻结参数，第三批不得改动（回归）。</summary>
        private static readonly Row[] Legacy =
        {
            new Row("MetalRefinery",        80f, 450f,  650f, .30f, .80f, 1200f, true,  false, .05f, ProductionKind.Batch),
            new Row("RockCrusher",         -20f,  15f,   60f, .80f, .90f,  240f, false, false, .02f, ProductionKind.Batch),
            new Row("GlassForge",           80f, 900f, 1200f, .25f, .75f, 1200f, true,  false, .025f, ProductionKind.Batch),
            new Row("SupermaterialRefinery",20f, 450f,  650f, .50f, .80f, 1600f, true,  false, .03f, ProductionKind.Batch),
            new Row("ChemicalRefinery",      0f,  15f,   40f, .70f, .90f,  480f, true,  false, .01f, ProductionKind.Batch),
            new Row("Kiln",                -20f, 250f,  450f, .50f, .80f,    0f, false, true,  .02f, ProductionKind.Batch)
        };

        public static void Run()
        {
            ScopeAndNumbers();
            ProfileTable();
            HeatingAndImmunity();
            EfficiencyEndpoints();
            StartupTargets();
            ColdStartAt20C();
            CatalogueNumbers();
            ThirdWaveRecipeMatching();
            Ports();
            LocalizationKeys();
            NonBatchDescriptors();
            ConversionAccounting();
            PatchSourceContracts();
        }

        /// <summary>
        /// 工业补丁的**源码结构**检查。这些接线依赖原版组件与 Harmony 注入，替身无法验证其行为，
        /// 也不能替代游戏内验收；这里只防止「η 被乘两次」「异常后结算标记残留」「油井被整机温度标记卡死」
        /// 这类结构性回退被无声引入。所有断言的依据都是已核实的原版实现（见开发计划 §3）。
        /// </summary>
        private static void PatchSourceContracts()
        {
            TestRunner.Section("工业补丁源码结构（不代替游戏内验收）");
            string root = FindRepoRoot();
            TestRunner.Check(root != null, "能找到仓库根目录以读取补丁源码");
            if (root == null) return;
            string patches = File.ReadAllText(Path.Combine(root, "MaterialOverhaul", "Game", "Patches", "IndustrialPatches.cs"));
            string partial = File.ReadAllText(Path.Combine(root, "MaterialOverhaul", "Game", "MaterialOverhaulFab.Industrial.cs"));
            string fabricators = File.ReadAllText(Path.Combine(root, "MaterialOverhaul", "Game", "Patches", "FabricatorPatches.cs"));

            // 发电：同一个 dt 参与燃料、排放与发电量；只在 dt 上乘一次 η。
            string generation = Region(patches, "class Patch_IndustrialGeneration", "class Patch_IndustrialWattage");
            TestRunner.Check(generation.Contains("dt *= fab.IndustrialEfficiency;"), "发电只在结算 dt 上乘一次温度效率");
            TestRunner.Check(generation.Contains("fab.GeneratorAdvancing=true;"), "结算期间置位发电标记");
            TestRunner.Check(generation.Contains("Finalizer") && generation.Contains("fab.GeneratorAdvancing=false;"),
                "异常收尾也清除发电标记，不影响后续功率读取");
            string wattage = Region(patches, "class Patch_IndustrialWattage", "class Patch_IndustrialOperatingHeat");
            TestRunner.Check(wattage.Contains("!fab.GeneratorAdvancing"), "界面功率读取在结算期间不重复乘 η（避免 η²）");
            // 生产热与排气热共用同一个温度系数。
            TestRunner.Check(patches.Contains("\"get_OperatingKilowatts\"") && patches.Contains("\"get_ExhaustKilowatts\""),
                "生产热与排气热都挂上温度系数补丁");
            string heatBody = Region(patches, "class Patch_IndustrialOperatingHeat", "class Patch_OilWellStart");
            TestRunner.Check(heatBody.Contains("Patch_IndustrialOperatingHeat.Postfix(__instance,ref __result);"),
                "排气热复用生产热的同一份系数，不使用第二套口径");

            // 连续转换：GetSpeedMultiplier 后置乘 η（输入输出共用同一因子），ConvertMass 前置只做门控。
            string converterSpeed = Region(patches, "class Patch_IndustrialConverterSpeed", "class Patch_IndustrialConversionGate");
            TestRunner.Check(patches.Contains("\"GetSpeedMultiplier\"") && converterSpeed.Contains("*= fab.IndustrialEfficiency"),
                "连续转换的效率乘在原版速度因子上，输入与全部产物同比");
            string gate = Region(patches, "class Patch_IndustrialConversionGate", "class Patch_IndustrialGeneration");
            TestRunner.Check(gate.Contains("return fab.IndustrialTemperatureAllowed") && gate.Contains("AutomationAllowsProduction"),
                "实际转换入口再次检查温度、自动化、供电与损坏");

            // 肥料天然气：关闭原异步 SIM 源，改由转换回调按实际耗料结算，避免两套来源重复产气。
            string emitter = Region(patches, "class Patch_IndustrialEmitter", "class Patch_IndustrialEmitterDescriptor");
            TestRunner.Check(emitter.Contains("fab.PrefabId != \"FertilizerMaker\""), "只接管肥料合成器的独立排放器");
            TestRunner.Check(emitter.Contains("Active(__instance)=false;") && emitter.Contains("Dirty(__instance)=true;"),
                "读档或运行状态变化后仍会关闭旧 SIM 排放源");
            string note = Region(partial, "private void NoteConversion", "public void NoteGeneratorProduction");
            TestRunner.Check(note.Contains("consumedMass/nominal*originalEmitRate"), "副产物按实际耗料比例结算，缺料与堵塞时不额外生成");
            TestRunner.Check(note.Contains("ProductionAccounting.ConversionSeconds"), "磨损按实际耗料反解真实生产秒数");

            // 油井：温度只阻止抽油，不能用整机 Operational 标记卡死泄压。
            string oilWellPrepare = Region(partial, "if (IsOilWell)", "else if (operational != null)");
            TestRunner.Check(!oilWellPrepare.Contains("SetFlag(TemperatureFlag"), "油井抽油门控不写入整机温度标记，泄压路径保持可用");
            TestRunner.Check(oilWellPrepare.Contains("Trigger((int)GameHashes.OnStorageChange)"),
                "温度许可变化时触发原状态机重检抽油条件");
            string oilWellPatches = Region(patches, "class Patch_OilWellStart", "class Patch_IndustrialEmitter");
            TestRunner.Check(!oilWellPatches.Contains("SetOperationalActive"), "油井补丁不改写整机可运行状态");
            TestRunner.Check(oilWellPatches.Contains("__result &= fab.IndustrialTemperatureAllowed")
                && oilWellPatches.Contains("__result |= !fab.IndustrialTemperatureAllowed"),
                "抽油入口与抽油退出都只按温度许可收紧，不放宽原版条件");
            string pressure = Region(patches, "class Patch_OilWellPressure", "class Patch_IndustrialEmitter");
            TestRunner.Check(pressure.Contains("dt *= fab.IndustrialEfficiency"),
                "背压增长跟随抽油效率，泄压速率不在此处改动");

            // 洗涤器：不适合清洗时不接管搬运者；已开始的清洗安全停止并释放。
            string bypass = Region(patches, "class Patch_ScrubberBypass", "class Patch_ScrubberWorkGate");
            TestRunner.Check(bypass.Contains("__result=false;") && bypass.Contains("return false;"),
                "温度不合适时不接管搬运复制人，物流继续");
            string workGate = Region(patches, "class Patch_ScrubberWorkGate", "class Patch_ScrubberWear");
            TestRunner.Check(workGate.Contains("worker.StopWork();"), "清洗中越温或损坏时安全停止工作");
            string scrubberWear = Region(patches, "class Patch_ScrubberWear", null);
            TestRunner.Check(scrubberWear.Contains("OnDuplicantWorkTick"), "清洗磨损按真实工作秒数累计");
            string scrubberDup = Region(fabricators, "if (__instance is OreScrubber.Work)", "ComplexFabricatorWorkable workable");
            TestRunner.Check(scrubberDup.Contains("scrubber.IndustrialEfficiency : 0f"),
                "清洗进度、去菌与氯气共用同一工作效率");

            // 整合版：房间工作速度加成须先于工业补丁乘入，工业记录的倍率才包含它，磨损仍按真实秒数。
            string rooms = File.ReadAllText(Path.Combine(root, "MaterialOverhaul", "Game", "Patches", "RoomPatches.cs"));
            const string efficiencyTarget = "[HarmonyPatch(typeof(Workable), \"GetEfficiencyMultiplier\")]";
            string roomAttrs = Region(rooms, efficiencyTarget, "class Patch_Room_Workable_GetEfficiencyMultiplier");
            TestRunner.Check(roomAttrs.Contains("[HarmonyPriority(Priority.HigherThanNormal)]"),
                "房间工作速度加成的后置优先级高于工业倍率记录");
            string industrialAttrs = Region(fabricators, efficiencyTarget, "class Patch_Workable_GetEfficiencyMultiplier");
            TestRunner.Check(industrialAttrs.Length > 0 && !industrialAttrs.Contains("HarmonyPriority"),
                "工业倍率记录保持普通优先级，在房间加成之后执行");
            string industrialNote = Region(fabricators, "__result *= gate.AllowsProduction ? gate.Efficiency : 0f;", "catch");
            TestRunner.Check(industrialNote.Contains("fab.NoteEfficiencyMultiplier(__result);"),
                "工业在乘入温度效率后记录包含其他加成的最终倍率");
        }

        /// <summary>取两个标记之间的源码片段；end 为 null 表示取到文件末尾。</summary>
        private static string Region(string text, string start, string end)
        {
            int from = text.IndexOf(start, StringComparison.Ordinal);
            if (from < 0) return string.Empty;
            if (end == null) return text.Substring(from);
            int to = text.IndexOf(end, from + start.Length, StringComparison.Ordinal);
            return to < 0 ? text.Substring(from) : text.Substring(from, to - from);
        }

        private static void ScopeAndNumbers()
        {
            TestRunner.Section("第三批范围：29栋唯一、名单与参数同步、排除项不扩散");
            TestRunner.CheckEqual(29, BuildingProfiles.All.Count, "参数表 29 栋");
            TestRunner.CheckEqual(29, BuildingWhitelist.Count, "白名单 29 栋");
            TestRunner.CheckEqual(29, BuildingProfiles.All.Keys.Distinct().Count(), "参数表键唯一");
            TestRunner.CheckEqual(0, BuildingProfiles.All.Keys.Except(BuildingWhitelist.All).Count(), "参数表不超出白名单");
            TestRunner.CheckEqual(0, BuildingWhitelist.All.Except(BuildingProfiles.All.Keys).Count(), "白名单都有参数");
            foreach (Row row in Third)
                TestRunner.Check(BuildingProfiles.All.ContainsKey(row.Id), row.Id + " 有独立参数");
            foreach (Row row in Legacy)
                TestRunner.Check(BuildingWhitelist.Contains(row.Id), row.Id + " 原六栋仍在名单内");
            // 用户明确排除：工业制造三栋与生活类建筑。
            string[] excluded = { "CraftingTable", "SuitFabricator", "AdvancedCraftingTable", "ClothingFabricator",
                "CookingStation", "GourmetCookingStation", "Apothecary", "Compost", "FertilizerMakerFactory",
                "SpiceGrinder", "WaterCooler", "PlantFiberFabricator", "SteamTurbine2", "NuclearReactor" };
            foreach (string id in excluded)
                TestRunner.Check(!BuildingWhitelist.Contains(id), id + " 不在接入名单");
        }

        private static void ProfileTable()
        {
            TestRunner.Section("第三批数值契约：逐栋 L／最佳区／效率底值／功率／加热／豁免／磨损");
            foreach (Row row in Third.Concat(Legacy))
            {
                BuildingProfile p = BuildingProfiles.All[row.Id];
                TestRunner.CheckClose(row.L, p.WorkMinCelsius, Tol, row.Id + " 建筑下限");
                TestRunner.CheckClose(row.Low, p.LowEfficiency, 0.0001f, row.Id + " 低温底值");
                TestRunner.CheckClose(row.High, p.HighEfficiency, 0.0001f, row.Id + " 高温底值");
                TestRunner.CheckClose(row.Watts, p.ProductionWatts, Tol, row.Id + " 生产功率（发电记0，不记为耗电）");
                TestRunner.CheckEqual(row.Kind, p.Kind, row.Id + " 生产类型");
                TestRunner.CheckEqual(row.Heating, p.BuiltInHeating, row.Id + " 内置加热");
                TestRunner.CheckEqual(row.Immune, p.PreserveOverheatImmunity, row.Id + " 原版过热豁免");
                TestRunner.CheckClose(row.Wear, p.DamagePerWearUnit, 0.0001f, row.Id + " 磨损 HP／生产秒");
                TestRunner.CheckEqual(25, p.RequiredHardness, row.Id + " 要求硬度 25");
                if (row.Kind == ProductionKind.Batch)
                {
                    // 批次建筑的最佳区来自配方候选，不写进建筑参数；非批次才用建筑级工艺温区。
                    TestRunner.CheckEqual(0f, p.BestMinCelsius, row.Id + " 批次建筑不写建筑级最佳下限");
                    TestRunner.CheckEqual(0f, p.BestMaxCelsius, row.Id + " 批次建筑不写建筑级最佳上限");
                    TestRunner.Check(RecipeZoneCatalogue.All.Any(s => s.FabricatorId == row.Id), row.Id + " 有配方候选");
                }
                else
                {
                    TestRunner.CheckClose(row.B1, p.BestMinCelsius, Tol, row.Id + " 工艺最佳下限");
                    TestRunner.CheckClose(row.B2, p.BestMaxCelsius, Tol, row.Id + " 工艺最佳上限");
                    TestRunner.Check(!RecipeZoneCatalogue.All.Any(s => s.FabricatorId == row.Id), row.Id + " 非批次不占配方候选");
                }
            }
        }

        private static void HeatingAndImmunity()
        {
            TestRunner.Section("加热范围与过热豁免名单");
            string[] heatable = { "MetalRefinery", "GlassForge", "SupermaterialRefinery", "ChemicalRefinery",
                "DiamondPress", "RubberMaker", "OilRefinery", "Polymerizer", "EthanolDistillery" };
            TestRunner.CheckEqual(9, HeatingScope.All.Count(), "内置加热共 9 栋（原四栋＋新增五栋）");
            foreach (Row row in Third.Concat(Legacy))
                TestRunner.CheckEqual(row.Heating, HeatingScope.IsEnabled(row.Id), row.Id + " 加热范围与参数一致");
            foreach (string id in heatable)
                TestRunner.Check(HeatingScope.IsEnabled(id), id + " 在加热名单");
            // 用户确认的前两批豁免＋第三批四栋建议豁免。
            string[] immune = { "Kiln", "UraniumCentrifuge", "EthanolDistillery", "AlgaeDistillery", "OxyliteRefinery" };
            TestRunner.CheckEqual(immune.Length, BuildingProfiles.All.Values.Count(p => p.PreserveOverheatImmunity), "过热豁免共 5 栋");
            foreach (KeyValuePair<string, BuildingProfile> entry in BuildingProfiles.All)
                TestRunner.CheckEqual(immune.Contains(entry.Key), entry.Value.PreserveOverheatImmunity, entry.Key + " 豁免名单");
            foreach (string id in new[] { "Generator", "PeatGenerator", "HydrogenGenerator", "MethaneGenerator",
                "PetroleumGenerator", "WoodGasGenerator", "OilWellCap", "OreScrubber", "DiamondPress" })
                TestRunner.Check(!HeatingScope.IsEnabled(id) || id == "DiamondPress", id + " 发电机／油井／洗涤器不新增内置加热");
        }

        private static ResolvedZone ZoneOf(string id, float ceilingCelsius)
        {
            BuildingProfile p = BuildingProfiles.All[id];
            TempZone best;
            if (p.Kind == ProductionKind.Batch)
            {
                best = RecipeZoneCatalogue.All.First(s => s.FabricatorId == id).Zone;
                TestRunner.Check(Temperatures.ToCelsius(best.BestMin) >= p.WorkMinCelsius,
                    id + " 配方最佳下限不低于建筑下限");
            }
            else
            {
                best = TempZone.Celsius(p.BestMinCelsius, p.BestMaxCelsius);
            }

            return TempZoneResolver.Resolve(id, C(p.WorkMinCelsius), best,
                (out float upper) => { upper = C(ceilingCelsius); return true; },
                p.LowEfficiency, p.HighEfficiency, !p.BuiltInHeating);
        }

        private static float EfficiencyAt(float celsius, ResolvedZone zone)
        {
            return TempZoneEvaluator.Efficiency(TempZoneEvaluator.Evaluate(C(celsius), zone), C(celsius), zone);
        }

        private static void EfficiencyEndpoints()
        {
            TestRunner.Section("温控端点：最佳区100%、两侧底值、强制越界与关闭强制");
            foreach (Row row in Third)
            {
                // 上限取 2000 ℃ 的虚构高熔点建材，使所有配方最佳区都不被材料截断。
                ResolvedZone z = ZoneOf(row.Id, 2000f);
                TestRunner.Check(z.Configured, row.Id + " 温区可合成");
                TestRunner.CheckClose(C(row.L), z.Min, Tol, row.Id + " 下限");
                float mid = (row.B1 + row.B2) / 2f;
                RunDecision atBest = RunController.Update(C(mid), z, RunPolicy.ControlModePolicy, RunState.Fresh);
                TestRunner.Check(atBest.AllowsProduction && Math.Abs(atBest.Efficiency - 1f) < 0.0001f,
                    row.Id + " 最佳区控制模式 100% 生产");
                TestRunner.CheckClose(row.Low, EfficiencyAt(row.L, z), 0.0001f, row.Id + " 下限处低侧底值");
                TestRunner.CheckClose(row.Low, EfficiencyAt(row.L - 30f, z), 0.0001f, row.Id + " 强制低于下限仍取低侧底值");
                TestRunner.CheckClose(row.High, EfficiencyAt(2000f, z), 0.0001f, row.Id + " 上限处高侧底值");
                TestRunner.CheckClose(row.High, EfficiencyAt(2100f, z), 0.0001f, row.Id + " 强制超过上限取高侧底值");
                TestRunner.CheckClose((float)Math.Sqrt(row.Low), EfficiencyAt((row.L + row.B1) / 2f, z), 0.0001f,
                    row.Id + " 低侧指数中点");
                TestRunner.CheckClose((float)Math.Sqrt(row.High), EfficiencyAt((2000f + row.B2) / 2f, z), 0.0001f,
                    row.Id + " 高侧指数中点");

                RunDecision hot = RunController.Update(C(row.B1 + 0.5f * (row.B2 - row.B1)), z, RunPolicy.ControlModePolicy, RunState.Fresh);
                TestRunner.Check(hot.AllowsProduction && Math.Abs(hot.Efficiency - 1f) < 0.0001f, row.Id + " 最佳区内恒 100% 生产");

                // 建材上限截断：U 落在最佳区内部。强制运行越过 U 但仍在最佳区时效率不得被建材压低；
                // 普通模式仍然受 U 限制（这是「材料不提供防熔化，但也不额外惩罚效率」的口径）。
                float limitedCeiling = row.B1 + 0.8f * (row.B2 - row.B1);
                float aboveU = row.B1 + 0.9f * (row.B2 - row.B1);
                ResolvedZone limited = ZoneOf(row.Id, limitedCeiling);
                TestRunner.Check(aboveU > Temperatures.ToCelsius(limited.Max) && aboveU < row.B2,
                    row.Id + " 取样点位于材料U与最佳上限之间");
                RunDecision forcedInBest = RunController.Update(C(aboveU), limited, new RunPolicy { Forced = true }, RunState.Fresh);
                TestRunner.Check(forcedInBest.AllowsProduction && Math.Abs(forcedInBest.Efficiency - 1f) < 0.0001f,
                    row.Id + " 强制越过材料U但仍在最佳区：效率不因建材降低");
                TestRunner.Check(!RunController.Update(C(aboveU), limited, RunPolicy.ControlModePolicy, RunState.Fresh).AllowsProduction,
                    row.Id + " 材料U仍限制普通控制生产");
                TestRunner.Check(!RunController.Update(C(aboveU), limited, RunPolicy.DefaultMode, RunState.Fresh).AllowsProduction,
                    row.Id + " 材料U仍限制普通默认生产");

                RunDecision over = RunController.Update(C(2100f), z, new RunPolicy { Forced = true }, RunState.Fresh);
                TestRunner.Check(over.AllowsProduction, row.Id + " 强制越界仍允许生产");
                TestRunner.CheckEqual(row.Heating ? HeatingIntent.Sustained : HeatingIntent.None, over.Heating,
                    row.Id + " 强制加热意图跟随内置加热能力");
                RunDecision normalOver = RunController.Update(C(2100f), z, RunPolicy.DefaultMode, RunState.Fresh);
                TestRunner.Check(!normalOver.AllowsProduction && normalOver.Heating == HeatingIntent.None,
                    row.Id + " 普通模式超过上限停产停热");
            }

            // 材料上限截断：部分交集、单点交集与不可达。
            BuildingProfile diamond = BuildingProfiles.All["DiamondPress"];
            TempZone diamondBest = RecipeZoneCatalogue.All.First(s => s.FabricatorId == "DiamondPress").Zone;
            ResolvedZone narrow = TempZoneResolver.Resolve("diamond-aluminum", C(diamond.WorkMinCelsius), diamondBest,
                (out float u) => { u = C(426.94f); return true; }, diamond.LowEfficiency, diamond.HighEfficiency, false);
            TestRunner.Check(narrow.Configured && narrow.ControlRangeLimited, "铝建材：钻石压机最佳区被截断");
            TestRunner.CheckClose(C(426.94f), narrow.ControlMax, 0.01f, "交集上限为材料U");
            TestRunner.Check(!RunController.Update(C(427f), narrow, RunPolicy.ControlModePolicy, RunState.Fresh).AllowsProduction,
                "超出材料U的控制模式停产");
            TestRunner.Check(RunController.Update(C(427f), narrow, new RunPolicy { Forced = true }, RunState.Fresh).AllowsProduction,
                "强制运行越过材料U仍可生产（材料相变仍按原版）");

            ResolvedZone single = TempZoneResolver.Resolve("diamond-single", C(diamond.WorkMinCelsius), diamondBest,
                (out float u) => { u = C(400f); return true; }, diamond.LowEfficiency, diamond.HighEfficiency, false);
            TestRunner.Check(single.Configured && single.HasControlRange, "B₁=U 的单点交集仍然可用（不误报无交集）");
            TestRunner.Check(!single.ControlRangeLimited, "单点交集不算「最佳温区受限」");
            TestRunner.Check(RunController.Update(C(400f), single, RunPolicy.ControlModePolicy, RunState.Fresh).AllowsProduction,
                "单点交集温度上允许控制生产");
            TestRunner.Check(!RunController.Update(C(401f), single, RunPolicy.ControlModePolicy, RunState.Fresh).AllowsProduction,
                "单点交集以外停产");
            TestRunner.CheckClose(0f, StartThreshold.ControlHysteresis(single), 0.0001f, "单点交集控制回差为 0");

            ResolvedZone unreachable = TempZoneResolver.Resolve("diamond-lead", C(diamond.WorkMinCelsius), diamondBest,
                (out float u) => { u = C(168.33f); return true; }, diamond.LowEfficiency, diamond.HighEfficiency, false);
            TestRunner.Check(unreachable.Configured && unreachable.ControlRangeUnreachable, "铅建材：钻石最佳区不可达");
            RunDecision stuck = RunController.Update(C(168.33f), unreachable, RunPolicy.ControlModePolicy, RunState.Fresh);
            TestRunner.Check(!stuck.AllowsProduction, "不可达时控制模式暂停生产");
            TestRunner.Check(stuck.Heating == HeatingIntent.None, "不可达但已到材料上限：不再预热");
            TestRunner.Check(stuck.Next.ZoneUnreachableLatched, "不可达状态被锁存以便状态栏区分");
            TestRunner.Check(RunController.Update(C(100f), unreachable, RunPolicy.ControlModePolicy, RunState.Fresh).Heating
                == HeatingIntent.Preheat, "不可达且未到上限：继续预热到材料上限");
            // 最佳区不可达只影响控制模式：默认模式仍可在 [L, U] 内低效生产，这是设计口径而不是缺陷。
            RunDecision defaultLow = RunController.Update(C(168.33f), unreachable, RunPolicy.DefaultMode, RunState.Fresh);
            TestRunner.Check(defaultLow.AllowsProduction, "不可达时默认模式仍可在建筑范围内生产");
            TestRunner.Check(defaultLow.Efficiency > diamond.LowEfficiency && defaultLow.Efficiency < 1f,
                "不可达时默认模式沿低侧曲线低效生产");
        }

        private static void StartupTargets()
        {
            TestRunner.Section("启动目标：默认 L+0.1(B₁−L)、控制 B₁+0.1(B₂−B₁)（方案 §8.1）");
            float[] defaultTargets = { 2f, 58f, 4f, 12f, -14f, -17f, -16.5f, 8f, 7f, 6f, -7f, -7f, -17f, -17f, -17f,
                -12f, -14f, -14f, -12f, -10f, -14f, 6f, -17f };
            float[] controlTargets = { 24f, 430f, 46f, 126f, 45f, 14f, 18.5f, 86f, 76f, 64f, 23f, 22f, 13f, 14f, 14f,
                72f, 50f, 48f, 70f, 90f, 48f, 66f, 13f };
            for (int i = 0; i < Third.Length; i++)
            {
                ResolvedZone z = ZoneOf(Third[i].Id, 2000f);
                TestRunner.CheckClose(C(defaultTargets[i]), StartThreshold.OriginalDefault(z), 0.05f,
                    Third[i].Id + " 默认原目标");
                TestRunner.CheckClose(C(controlTargets[i]), StartThreshold.OriginalControl(z), 0.05f,
                    Third[i].Id + " 控制原目标");
                TestRunner.CheckClose(Math.Min(C(defaultTargets[i]), z.Max), StartThreshold.Start(z, RunPolicy.DefaultMode), 0.05f,
                    Third[i].Id + " 默认实际目标按上限封顶");
            }
        }

        private static void ColdStartAt20C()
        {
            TestRunner.Section("20 ℃ 常温行为：22栋可默认启动，钻石压机需先预热");
            foreach (Row row in Third)
            {
                ResolvedZone z = ZoneOf(row.Id, 2000f);
                RunDecision at20 = RunController.Update(C(20f), z, RunPolicy.DefaultMode, RunState.Fresh);
                if (row.Id == "DiamondPress")
                {
                    TestRunner.Check(!at20.AllowsProduction && at20.Heating == HeatingIntent.Preheat,
                        "钻石压机 20 ℃ 默认模式未达启动线，需要预热");
                }
                else
                {
                    TestRunner.Check(at20.AllowsProduction, row.Id + " 20 ℃ 默认模式可开工");
                }
            }

            // 控制模式仍然要求最佳区：常温水处理可满效，高温工艺需要升温。
            ResolvedZone purifier = ZoneOf("WaterPurifier", 2000f);
            TestRunner.Check(RunController.Update(C(20f), purifier, RunPolicy.ControlModePolicy, RunState.Fresh).AllowsProduction,
                "净水器 20 ℃ 控制模式即在最佳区（不增加常温门槛）");
            ResolvedZone rubber = ZoneOf("RubberMaker", 2000f);
            RunDecision rubberCold = RunController.Update(C(20f), rubber, RunPolicy.ControlModePolicy, RunState.Fresh);
            TestRunner.Check(!rubberCold.AllowsProduction && rubberCold.Heating == HeatingIntent.Preheat,
                "硫化器 20 ℃ 未达 120 ℃ 控制目标：有内置加热，先预热再开工");
            TestRunner.Check(RunController.Update(C(50f), rubber, RunPolicy.ControlModePolicy, RunState.Fresh).Heating
                == HeatingIntent.Preheat, "硫化器低于控制启动目标时给出预热意图");
            ResolvedZone fertilizer = ZoneOf("FertilizerMaker", 2000f);
            RunDecision fertilizerCold = RunController.Update(C(5f), fertilizer, RunPolicy.ControlModePolicy, RunState.Fresh);
            TestRunner.Check(!fertilizerCold.AllowsProduction && fertilizerCold.Heating == HeatingIntent.None,
                "无内置加热的肥料合成器温度不足时等待外部升温，不显示正在预热");
            TestRunner.Check(fertilizerCold.State != TempState.Disabled, "等待升温时仍有真实温度状态");

            // 无内置加热建筑不会产生注热意图（依靠生产自热或外部热源）。
            foreach (string id in new[] { "Chlorinator", "FabricatedWoodMaker", "SludgePress", "MissileFabricator",
                "FertilizerMaker", "AlgaeDistillery", "OxyliteRefinery", "WaterPurifier", "Desalinator",
                "Generator", "WoodGasGenerator", "OilWellCap", "OreScrubber" })
            {
                ResolvedZone z = ZoneOf(id, 2000f);
                TestRunner.Check(RunController.Update(C(-40f), z, RunPolicy.ControlModePolicy, RunState.Fresh).Heating
                    == HeatingIntent.None, id + " 无内置加热不产生注热意图");
                TestRunner.Check(RunController.Update(C(-40f), z, new RunPolicy { Forced = true }, RunState.Fresh).Heating
                    == HeatingIntent.None, id + " 强制运行也不免费注热");
            }
        }

        private static void CatalogueNumbers()
        {
            TestRunner.Section("配方候选：共60条，第三批9条完整材料与质量");
            TestRunner.CheckEqual(60, RecipeZoneCatalogue.Count, "候选总数");
            var expected = new Dictionary<string, int>
            {
                { "MetalRefinery", 13 }, { "RockCrusher", 26 }, { "GlassForge", 1 },
                { "SupermaterialRefinery", 5 }, { "ChemicalRefinery", 4 }, { "Kiln", 2 },
                { "Chlorinator", 1 }, { "DiamondPress", 1 }, { "UraniumCentrifuge", 1 }, { "RubberMaker", 1 },
                { "FabricatedWoodMaker", 1 }, { "SludgePress", 2 }, { "MissileFabricator", 2 }
            };
            foreach (KeyValuePair<string, int> entry in expected)
                TestRunner.CheckEqual(entry.Value, RecipeZoneCatalogue.All.Count(s => s.FabricatorId == entry.Key),
                    entry.Key + " 候选数");
            TestRunner.CheckEqual(RecipeZoneCatalogue.Count, expected.Values.Sum(), "候选只落在已设计建筑上");

            foreach (RecipeZoneSpec spec in RecipeZoneCatalogue.All.Where(s => s.FabricatorId == "Chlorinator"))
            {
                TestRunner.CheckEqual("Salt+Gold", string.Join("+", spec.Inputs), "漂白石料斗投入");
                TestRunner.CheckClose(30f, spec.InputAmounts[0], 0.0001f, "盐 30 kg");
                TestRunner.CheckClose(0.5f, spec.InputAmounts[1], 0.0001f, "金催化剂 0.5 kg");
                TestRunner.CheckEqual("BleachStone+Sand", string.Join("+", spec.Outputs), "漂白石料斗产出");
                TestRunner.CheckClose(19.999998f, spec.OutputAmounts[1], 0.0001f, "砂按原版 19.999998 kg 不擅自修正");
                TestRunner.CheckClose(20f, Temperatures.ToCelsius(spec.Zone.BestMin), 0.01f, "漂白石最佳下限");
                TestRunner.CheckClose(60f, Temperatures.ToCelsius(spec.Zone.BestMax), 0.01f, "漂白石最佳上限");
            }
            RecipeZoneSpec diamond = RecipeZoneCatalogue.All.First(s => s.FabricatorId == "DiamondPress");
            TestRunner.CheckEqual(1000, diamond.ConsumedParticles.Value, "钻石压机每批 1000 辐射粒子");
            TestRunner.CheckClose(100f, diamond.InputAmounts[0], 0.0001f, "精炼碳 100 kg");
            TestRunner.CheckClose(80f, 80f, 0.0001f, "钻石原版基准时间 80 s（配方表不另存时间）");
            RecipeZoneSpec basic = RecipeZoneCatalogue.All.First(s => s.Outputs[0] == "MissileBasic");
            TestRunner.CheckEqual(5f, basic.OutputAmounts[0], "基础弹按 5 件而不是 5 kg");
            TestRunner.CheckEqual("BasicRefinedMetals", basic.InputAlternatives[0].RuntimeSet, "金属输入按运行时材料数组展开");
            TestRunner.CheckClose(25f, basic.InputAlternatives[0].Amount, 0.0001f, "基础弹金属 25 kg");
            RecipeZoneSpec longer = RecipeZoneCatalogue.All.First(s => s.Outputs[0] == "MissileLongRange");
            TestRunner.CheckEqual(3, longer.InputAlternatives.Length, "远程弹三种输入");
            TestRunner.CheckClose(200f, longer.InputAlternatives[2].Amount, 0.0001f, "远程弹液体 200 kg");
            TestRunner.CheckEqual(2, RecipeZoneCatalogue.All.Count(s => s.FabricatorId == "SludgePress"), "泥浆分离两条配方");
            foreach (RecipeZoneSpec spec in RecipeZoneCatalogue.All.Where(s => s.FabricatorId == "SludgePress"))
                TestRunner.CheckClose(150f, spec.InputAmounts[0], 0.0001f, "泥浆投入 150 kg");

            // 非批次建筑的工艺温区必须与建筑参数一致（同一份数值不得两处维护）。
            foreach (RecipeZoneSpec spec in RecipeZoneCatalogue.All)
            {
                BuildingProfile p;
                if (!BuildingProfiles.TryGet(spec.FabricatorId, out p) || p.Kind == ProductionKind.Batch) continue;
                TestRunner.Check(false, spec.FabricatorId + " 非批次建筑不应出现在配方候选里");
            }
        }

        private static ComplexRecipe.RecipeElement Element(string id, float amount)
        {
            return new ComplexRecipe.RecipeElement { material = new Tag(id), amount = amount };
        }

        private static void ThirdWaveRecipeMatching()
        {
            TestRunner.Section("第三批登记：按完整材料与质量匹配，粒子与件数不被静默放宽");
            ComplexRecipeManager manager = ComplexRecipeManager.Instance;
            manager.recipes.Clear();

            var specs = new List<RecipeZoneSpec>(RecipeZoneCatalogue.All.Where(
                s => s.FabricatorId == "Chlorinator" || s.FabricatorId == "DiamondPress"
                  || s.FabricatorId == "UraniumCentrifuge" || s.FabricatorId == "RubberMaker"
                  || s.FabricatorId == "FabricatedWoodMaker" || s.FabricatorId == "SludgePress"
                  || s.FabricatorId == "MissileFabricator"));
            TestRunner.CheckEqual(9, specs.Count, "第三批候选 9 条");

            int index = 0;
            var matchedIds = new List<string>();
            foreach (RecipeZoneSpec spec in specs)
            {
                string id = "third-wave-" + index++;
                ComplexRecipe recipe = new ComplexRecipe
                {
                    id = id,
                    consumedHEP = spec.ConsumedParticles.HasValue ? spec.ConsumedParticles.Value : 0,
                    fabricators = new List<Tag> { new Tag(spec.FabricatorId) },
                    ingredients = spec.Inputs.Select((s, i) => Element(s, spec.InputAmounts == null ? 0f : spec.InputAmounts[i])).ToArray(),
                    results = spec.Outputs.Select((s, i) => Element(s, spec.OutputAmounts[i])).ToArray()
                };
                if (spec.InputAlternatives != null)
                {
                    recipe.ingredients = spec.InputAlternatives.Select(m => new ComplexRecipe.RecipeElement
                    {
                        material = m.Materials.Length == 1 && m.RuntimeSet == null ? new Tag(m.Materials[0]) : null,
                        possibleMaterials = m.RuntimeSet == "BasicRefinedMetals"
                            ? GameTags.BasicRefinedMetals
                            : m.Materials.Select(x => new Tag(x)).ToArray(),
                        possibleMaterialAmounts = m.Amounts,
                        amount = m.Amount
                    }).ToArray();
                }
                matchedIds.Add(id);
                manager.recipes.Add(recipe);
            }

            // 反面样例：粒子需求不符、质量不符、建筑不符的第三方配方。
            ComplexRecipe wrongParticles = new ComplexRecipe
            {
                id = "third-wrong-particles", consumedHEP = 999,
                fabricators = new List<Tag> { new Tag("DiamondPress") },
                ingredients = new[] { Element("RefinedCarbon", 100f) }, results = new[] { Element("Diamond", 100f) }
            };
            ComplexRecipe wrongAmount = new ComplexRecipe
            {
                id = "third-wrong-amount", consumedHEP = 1000,
                fabricators = new List<Tag> { new Tag("DiamondPress") },
                ingredients = new[] { Element("RefinedCarbon", 120f) }, results = new[] { Element("Diamond", 100f) }
            };
            ComplexRecipe wrongOutput = new ComplexRecipe
            {
                id = "third-wrong-output",
                fabricators = new List<Tag> { new Tag("Chlorinator") },
                ingredients = new[] { Element("Salt", 30f), Element("Gold", 0.5f) },
                results = new[] { Element("BleachStone", 10f), Element("Sand", 25f) }
            };
            manager.recipes.Add(wrongParticles);
            manager.recipes.Add(wrongAmount);
            manager.recipes.Add(wrongOutput);

            ContentRegistration.RegisterRecipeZones();
            TestRunner.Check(ContentRegistration.ZonesResolved, "登记完成");
            foreach (string id in matchedIds)
                TestRunner.Check(RecipeZoneTable.GetOrUnconfigured(id).Configured, id + " 按完整材料与质量登记");
            TestRunner.Check(!RecipeZoneTable.GetOrUnconfigured(wrongParticles.id).Configured, "粒子需求不符不接管");
            TestRunner.Check(!RecipeZoneTable.GetOrUnconfigured(wrongAmount.id).Configured, "质量不符不接管");
            TestRunner.Check(!RecipeZoneTable.GetOrUnconfigured(wrongOutput.id).Configured, "砂质量与 19.999998 kg 不符不接管");

            // 金属数组按运行时实际元素展开后再匹配；未知金属不静默接管。
            RecipeZoneSpec basic = RecipeZoneCatalogue.All.First(s => s.Outputs[0] == "MissileBasic");
            var runtime = new List<RecipeMaterial>
            {
                new RecipeMaterial(GameTags.BasicRefinedMetals.Select(t => t.ToString()).ToArray(), 25f),
                new RecipeMaterial(new[] { "RefinedLipid" }, 50f)
            };
            TestRunner.Check(basic.MatchesRecipe(runtime, new[] { new RecipeMaterial("MissileBasic", 5f) }),
                "爆破弹按运行时金属数组匹配");
            TestRunner.Check(basic.MatchesRecipe(new[]
            {
                new RecipeMaterial(new[] { "Iron", "Copper", "Aluminum", "Cobalt", "Nickel" }, 25f),
                new RecipeMaterial("Petroleum", 50f)
            }, new[] { new RecipeMaterial("MissileBasic", 5f) }), "替代液体两种都接受");
            // 原版把该输入声明为运行时材料数组；登记后按数组里的可用元素匹配。
            // 只取数组子集、且用量相同的第三方配方仍属于同一工艺，应被接管；
            // 出现数组以外的未知金属、或用料不同，则不得静默接管。
            TestRunner.Check(basic.MatchesRecipe(new[]
            {
                new RecipeMaterial(new[] { "Iron", "Copper" }, 25f),
                new RecipeMaterial("Petroleum", 50f)
            }, new[] { new RecipeMaterial("MissileBasic", 5f) }), "运行时金属子集（用量相同）仍属同一工艺");
            TestRunner.Check(!basic.MatchesRecipe(new[]
            {
                new RecipeMaterial(new[] { "Iron", "UnknownMetal" }, 25f),
                new RecipeMaterial("Petroleum", 50f)
            }, new[] { new RecipeMaterial("MissileBasic", 5f) }), "数组以外的未知金属不接管");
            TestRunner.Check(!basic.MatchesRecipe(new[]
            {
                new RecipeMaterial(new[] { "Iron", "Copper" }, 40f),
                new RecipeMaterial("Petroleum", 50f)
            }, new[] { new RecipeMaterial("MissileBasic", 5f) }), "金属用量不符不接管");
            TestRunner.Check(!basic.MatchesRecipe(new[]
            {
                new RecipeMaterial(new[] { "Iron", "Iron" }, 25f),
                new RecipeMaterial("Petroleum", 50f)
            }, new[] { new RecipeMaterial("MissileBasic", 5f) }), "重复候选材料不接管");
            TestRunner.Check(!basic.MatchesRecipe(new[]
            {
                new RecipeMaterial(new[] { "Iron", "Copper" }, 25f)
            }, new[] { new RecipeMaterial("MissileBasic", 5f) }), "缺少液体输入不接管");
            TestRunner.Check(!basic.MatchesRecipe(new[]
            {
                new RecipeMaterial(new[] { "Iron", "Copper" }, 25f),
                new RecipeMaterial("Petroleum", 50f)
            }, new[] { new RecipeMaterial("MissileBasic", 4f) }), "件数不符（4 件 vs 5 件）不接管");
        }

        private sealed class PortRow
        {
            public string Id; public int W, H; public string Vanilla; public string ReusedLogic; public string Note;
            public PortRow(string id, int w, int h, string vanilla, string reused, string note = null)
            { Id = id; W = w; H = h; Vanilla = vanilla; ReusedLogic = reused; Note = note; }
        }

        /// <summary>
        /// 占格与冲突表。尺寸取目标版本 `BuildingTemplates.CreateBuildingDef(...)` 的 width/height；
        /// 原版端口偏移取各 `*Config` 的 LogicInputPorts／LogicOutputPorts／PowerInputOffset／
        /// UtilityInputOffset／UtilityOutputOffset（本机反编译代码核对，2026-10-17）。
        /// `ReusedLogic` 是该建筑原版已有的逻辑控制输入位置：本 Mod 复用该端口，不新增第二个。
        /// </summary>
        private static readonly PortRow[] PortTable =
        {
            new PortRow("MetalRefinery", 3, 4, "(-1,1)(1,0)", null),
            new PortRow("GlassForge", 5, 4, "", null),
            new PortRow("RockCrusher", 4, 4, "", null),
            new PortRow("SupermaterialRefinery", 4, 5, "", null),
            new PortRow("ChemicalRefinery", 4, 3, "(1,0)(2,1)", "(0,0)"),
            new PortRow("Kiln", 2, 2, "", "(0,1)"),
            new PortRow("Chlorinator", 3, 3, "(0,0)", "(0,1)"),
            new PortRow("DiamondPress", 3, 5, "(0,2)", null, "原版辐射粒子存量输出"),
            new PortRow("UraniumCentrifuge", 3, 4, "(0,0)", "(0,0)"),
            new PortRow("RubberMaker", 3, 3, "(0,0)(1,0)", "(0,1)"),
            new PortRow("FabricatedWoodMaker", 4, 3, "(0,0)", "(1,0)"),
            new PortRow("SludgePress", 4, 3, "(1,0)", null),
            new PortRow("MissileFabricator", 5, 4, "(1,0)(-1,1)", null),
            new PortRow("OilRefinery", 4, 4, "(1,0)(0,0)(1,1)", null),
            new PortRow("Polymerizer", 3, 3, "(0,0)", "(0,1)"),
            new PortRow("EthanolDistillery", 4, 3, "(2,0)(-1,0)", null),
            new PortRow("FertilizerMaker", 4, 3, "(1,0)(0,0)", "(-1,0)"),
            new PortRow("AlgaeDistillery", 3, 4, "(1,0)(0,0)", "(0,1)"),
            new PortRow("OxyliteRefinery", 3, 4, "(0,0)(1,0)", "(0,1)"),
            new PortRow("WaterPurifier", 4, 3, "(2,0)(-1,2)(2,2)", "(-1,0)"),
            new PortRow("Desalinator", 4, 3, "(-1,0)(0,0)", null),
            new PortRow("Generator", 3, 3, "", "(0,0)"),
            new PortRow("PeatGenerator", 3, 2, "", "(0,0)"),
            new PortRow("HydrogenGenerator", 4, 3, "(-1,0)", "(-1,0)"),
            new PortRow("MethaneGenerator", 4, 3, "(2,2)", "(0,0)"),
            new PortRow("PetroleumGenerator", 3, 4, "(-1,0)", "(0,0)"),
            new PortRow("WoodGasGenerator", 2, 2, "", "(1,1)"),
            new PortRow("OilWellCap", 4, 4, "(0,1)(1,1)", "(0,0)"),
            new PortRow("OreScrubber", 3, 3, "(1,1)", null)
        };

        private static List<CellOffset> Parse(string text)
        {
            var list = new List<CellOffset>();
            if (string.IsNullOrEmpty(text)) return list;
            foreach (Match m in Regex.Matches(text, @"\((-?\d+),(-?\d+)\)"))
                list.Add(new CellOffset(int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value)));
            return list;
        }

        private static void Ports()
        {
            TestRunner.Section("自动化端口：29栋齐全、在占格内、不覆盖原版接口");
            TestRunner.CheckEqual(29, PortTable.Length, "端口表覆盖 29 栋");
            TestRunner.CheckEqual(29, AutomationControlScope.All.Count(), "输入端口表 29 栋");
            TestRunner.CheckEqual(0, BuildingWhitelist.All.Except(AutomationControlScope.All).Count(), "白名单都有输入端口位置");
            TestRunner.CheckEqual(0, BuildingWhitelist.All.Except(PortTable.Select(p => p.Id)).Count(), "每栋都有占格与冲突基准");

            int reused = 0;
            foreach (PortRow row in PortTable)
            {
                CellOffset input, output;
                TestRunner.Check(AutomationControlScope.TryGetOffset(row.Id, out input), row.Id + " 配置了输入端口");
                TestRunner.Check(AutomationControlScope.TryGetOutputOffset(row.Id, out output), row.Id + " 配置了温区输出");
                TestRunner.CheckEqual(true, AutomationControlScope.IsEnabled(row.Id), row.Id + " 启用生产控制输入");

                int left = row.W / 2 - row.W + 1, right = row.W / 2;
                TestRunner.Check(input.x >= left && input.x <= right && input.y >= 0 && input.y < row.H,
                    row.Id + " 输入在占格内(" + row.W + "x" + row.H + ")");
                TestRunner.Check(output.x >= left && output.x <= right && output.y >= 0 && output.y < row.H,
                    row.Id + " 温区输出在占格内(" + row.W + "x" + row.H + ")");
                TestRunner.Check(input.x != output.x || input.y != output.y, row.Id + " 输入与输出不重叠");

                List<CellOffset> vanilla = Parse(row.Vanilla);
                bool clashes = vanilla.Any(v => v.x == output.x && v.y == output.y);
                TestRunner.Check(!clashes, row.Id + " 温区输出不覆盖原版接口" + (row.Note == null ? "" : "（" + row.Note + "）"));

                if (row.ReusedLogic != null)
                {
                    reused++;
                    List<CellOffset> logic = Parse(row.ReusedLogic);
                    TestRunner.Check(logic.Any(v => v.x == input.x && v.y == input.y),
                        row.Id + " 复用原版逻辑控制输入位置，不新增第二个端口");
                }
                else
                {
                    TestRunner.Check(!vanilla.Any(v => v.x == input.x && v.y == input.y),
                        row.Id + " 新增输入不与原版接口重叠");
                }
            }
            TestRunner.CheckEqual(18, reused, "18 栋已有原版逻辑控制输入，直接复用位置");

            CellOffset diamond;
            AutomationControlScope.TryGetOutputOffset("DiamondPress", out diamond);
            TestRunner.Check(diamond.x != 0 || diamond.y != 2, "钻石压机温区输出不覆盖原版粒子存量输出(0,2)");
            CellOffset chemicalIn, chemicalOut;
            AutomationControlScope.TryGetOffset("ChemicalRefinery", out chemicalIn);
            AutomationControlScope.TryGetOutputOffset("ChemicalRefinery", out chemicalOut);
            TestRunner.Check(chemicalIn.x == 0 && chemicalIn.y == 0, "化学精炼厂复用原版输入(0,0)");
            TestRunner.Check(chemicalOut.x == 2 && chemicalOut.y == 2, "化学精炼厂输出避开液体输出(2,1)");
        }

        private static string FindRepoRoot()
        {
            DirectoryInfo dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "MaterialOverhaul", "mod", "strings", "zh.po")))
                    return dir.FullName;
                dir = dir.Parent;
            }
            return null;
        }

        private static void LocalizationKeys()
        {
            TestRunner.Section("文本：102条字段树键与中文PO一一对应");
            LocString.CreateLocStringKeys(typeof(ModStrings.UI), "STRINGS.");
            LocString.CreateLocStringKeys(typeof(ModStrings.BUILDINGS), "STRINGS.");
            LocString.CreateLocStringKeys(typeof(ModStrings.BUILDING), "STRINGS.");
            TestRunner.CheckEqual(102, Strings.Values.Count, "字段树登记102条（电力14条）");
            foreach (KeyValuePair<string, string> entry in Strings.Values)
            {
                TestRunner.Check(!string.IsNullOrEmpty(entry.Value), entry.Key + " 有默认文本");
                TestRunner.Check(!entry.Value.Contains("MISSING"), entry.Key + " 默认文本不是 MISSING");
            }

            string root = FindRepoRoot();
            TestRunner.Check(root != null, "能找到仓库根目录以读取 zh.po");
            if (root == null) return;
            string[] lines = File.ReadAllLines(Path.Combine(root, "MaterialOverhaul", "mod", "strings", "zh.po"));
            var po = new Dictionary<string, string>();
            var poIds = new Dictionary<string, string>();
            string key = null;
            foreach (string line in lines)
            {
                Match context = Regex.Match(line, "^msgctxt \"(.*)\"$");
                if (context.Success) { key = context.Groups[1].Value; continue; }
                Match id = Regex.Match(line, "^msgid \"(.*)\"$");
                if (id.Success && key != null) { poIds[key] = id.Groups[1].Value; continue; }
                Match str = Regex.Match(line, "^msgstr \"(.*)\"$");
                if (str.Success && key != null) { po[key] = Unescape(str.Groups[1].Value); key = null; }
            }
            // 房间系统的 69 条键与工业/电力共用同一个 zh.po，由 ProfileChecks 按构建模式
            // 分别校验；这里只核对工业与电力共用的 102 条，避免把房间包差异算成工业回归。
            foreach (string roomKey in po.Keys.Where(k => k.StartsWith("STRINGS.ROOMS.MATERIALOVERHAUL.", StringComparison.Ordinal)).ToList())
                po.Remove(roomKey);
            TestRunner.CheckEqual(102, po.Count, "zh.po有102条工业/电力msgctxt");
            foreach (KeyValuePair<string, string> entry in Strings.Values)
            {
                string translated;
                TestRunner.Check(po.TryGetValue(entry.Key, out translated), entry.Key + " 在 zh.po 中");
                if (!po.TryGetValue(entry.Key, out translated)) continue;
                TestRunner.Check(!string.IsNullOrEmpty(translated), entry.Key + " 有中文文本");
                TestRunner.CheckEqual(Placeholders(entry.Value), Placeholders(translated), entry.Key + " 占位符与字段默认值一致");
                TestRunner.CheckEqual(entry.Value, translated, entry.Key + " 中文PO与字段默认文本一致");
            }
            TestRunner.CheckEqual(0, po.Keys.Except(Strings.Values.Keys).Count(), "zh.po 没有多余键");
            // 第三批新增文本确实登记了（防止把新键漏在字段树外）。
            foreach (string id in new[] { "PROCESS_BEST", "PROCESS_BEST_TOOLTIP", "INDUSTRIAL_STATUS", "GENERATION_STATUS", "IMMUNE_HEATING_TOOLTIP" })
                TestRunner.Check(Strings.Values.ContainsKey("STRINGS.UI.BUILDINGEFFECTS.MATERIALOVERHAUL." + id), id + " 已登记");
            foreach (string id in new[] { "INDUSTRIAL_ACTIVE", "INDUSTRIAL_INACTIVE" })
                TestRunner.Check(Strings.Values.ContainsKey("STRINGS.BUILDING.LOGICPORTS.MATERIALOVERHAUL.TEMPERATURE_ZONE." + id), id + " 已登记");
        }

        /// <summary>PO 文件里换行与引号是转义写法，比较前必须还原成字段默认文本的实际字符。</summary>
        private static string Unescape(string text)
        {
            return (text ?? string.Empty).Replace("\\n", "\n").Replace("\\\"", "\"").Replace("\\\\", "\\");
        }

        private static string Placeholders(string text)        {
            var set = new SortedSet<string>();
            foreach (Match m in Regex.Matches(text ?? string.Empty, @"\{(\d+)\}")) set.Add(m.Groups[1].Value);
            return string.Join(",", set);
        }

        private static void NonBatchDescriptors()
        {
            TestRunner.Section("非批次建筑：效果区显示工艺最佳温区，批次建筑不重复");
            foreach (Row row in Third.Where(r => r.Kind != ProductionKind.Batch))
            {
                var fab = new MaterialOverhaulFab(new ComplexFabricator(), r => ProductionDecision.Passthrough)
                    { PrefabId = row.Id, DisplayWorkingCeiling = C(2000f) };
                var descriptors = fab.GetDescriptors(null);
                // 保留原版过热豁免的非批次建筑（乙醇、藻类、氧石）除工艺温区外还有一条工作温度行。
                TestRunner.CheckEqual(row.Immune ? 2 : 1, descriptors.Count, row.Id + " 效果区条目数");
                Descriptor process = descriptors[descriptors.Count - 1];
                TestRunner.Check(process.Text.Contains("最佳"), row.Id + " 最后一条是工艺最佳温区");
                TestRunner.Check(process.Tooltip.Contains("温度效率"), row.Id + " 说明温度效率口径");
                if (row.Immune)
                    TestRunner.Check(descriptors[0].Tooltip.Contains("过热豁免"), row.Id + " 工作温度行说明原版过热豁免");
            }
            foreach (Row row in Third.Where(r => r.Kind == ProductionKind.Batch))
            {
                var fab = new MaterialOverhaulFab(new ComplexFabricator(), r => ProductionDecision.Passthrough)
                    { PrefabId = row.Id, DisplayWorkingCeiling = C(2000f) };
                // 批次建筑温区只放配方详情；豁免建筑仍需要一条工作温度行。
                TestRunner.CheckEqual(row.Immune ? 1 : 0, fab.GetDescriptors(null).Count, row.Id + " 批次建筑不重复显示工艺温区");
            }
            // 豁免建筑必须有工作温度行（这类建筑没有原版 Overheatable 描述器）。
            foreach (string id in new[] { "UraniumCentrifuge", "EthanolDistillery", "AlgaeDistillery", "OxyliteRefinery" })
            {
                var fab = new MaterialOverhaulFab(new ComplexFabricator(), r => ProductionDecision.Passthrough)
                    { PrefabId = id, DisplayWorkingCeiling = C(857.25f) };
                var descriptors = fab.GetDescriptors(null);
                TestRunner.Check(descriptors.Any(d => d.Tooltip.Contains("过热豁免")), id + " 说明保留原版过热豁免");
            }
        }

        private static void ConversionAccounting()
        {
            TestRunner.Section("连续转换：真实生产秒数与同栋区间并集");
            // 满速：1 s 的转换就是 1 s 真实生产时间。
            TestRunner.CheckClose(1f, ProductionAccounting.ConversionSeconds(5f, 5f, 1f), 0.0001f, "满速 1 s 结算");
            // η=50% 时每秒只消耗一半原料，但每分钟仍是 1 s 真实生产时间：η 不降低每秒机械损失。
            TestRunner.CheckClose(1f, ProductionAccounting.ConversionSeconds(2.5f, 5f, 0.5f), 0.0001f, "低效不改变真实生产秒数");
            // 部分转换（供料不足一帧）按实际耗料比例还原时间。
            TestRunner.CheckClose(0.4f, ProductionAccounting.ConversionSeconds(2f, 5f, 1f), 0.0001f, "部分耗料按比例");
            TestRunner.CheckClose(0.5f, ProductionAccounting.ConversionSeconds(1.25f, 5f, 0.5f), 0.0001f, "部分耗料叠加低效");
            // 非法输入不产生磨损。
            TestRunner.CheckClose(0f, ProductionAccounting.ConversionSeconds(0f, 5f, 1f), 0.0001f, "零耗料不计磨损");
            TestRunner.CheckClose(0f, ProductionAccounting.ConversionSeconds(5f, 0f, 1f), 0.0001f, "额定速率为0不计磨损");
            TestRunner.CheckClose(0f, ProductionAccounting.ConversionSeconds(5f, 5f, 0f), 0.0001f, "速度倍率为0不计磨损");
            TestRunner.CheckClose(0f, ProductionAccounting.ConversionSeconds(float.NaN, 5f, 1f), 0.0001f, "NaN 耗料不计磨损");
            TestRunner.CheckClose(0f, ProductionAccounting.ConversionSeconds(5f, 5f, float.PositiveInfinity), 0.0001f,
                "无限速度倍率不计磨损");
            TestRunner.CheckClose(1f, ProductionAccounting.ConversionSeconds(100f, 5f, 1f), 0.0001f, "单次结算不超过 1 s");

            // 脱盐器同栋多转换器：同一时刻的转换区间只计一次磨损（区间并集）。
            var credit = new ProductionIntervalCredit();
            TestRunner.CheckClose(1f, credit.Add(100.0, 1f), 0.0001f, "首次区间全额计入");
            TestRunner.CheckClose(0f, credit.Add(100.0, 1f), 0.0001f, "完全重叠的第二个转换器不重复计");
            TestRunner.CheckClose(0f, credit.Add(100.0, 0.5f), 0.0001f, "被已计区间包含的短区间不重复计");
            TestRunner.CheckClose(1f, credit.Add(101.0, 2f), 0.0001f, "跨帧区间只补未计入的部分");
            TestRunner.CheckClose(0f, credit.Add(101.0, 1f), 0.0001f, "同一时刻的重复回调不再累计");
            TestRunner.CheckClose(1f, credit.Add(102.0, 1f), 0.0001f, "紧接其后的新区间全额计入");
            TestRunner.CheckClose(0f, credit.Add(101.5, 1f), 0.0001f, "时间回退不重复计");
            TestRunner.CheckClose(0f, credit.Add(103.0, 0f), 0.0001f, "零时长不计");
            TestRunner.CheckClose(0f, credit.Add(103.0, float.NaN), 0.0001f, "NaN 时长不计");
            TestRunner.CheckClose(0f, credit.Add(double.NaN, 1f), 0.0001f, "NaN 时间不计");
            TestRunner.CheckClose(1f, credit.Add(104.0, 1f), 0.0001f, "非法输入后仍正常工作");

            // 磨损只按真实生产秒数累计：低效同一批耗时更长，损失更多。
            float normalWear, slowWear;
            var normal = new WearAccumulator();
            DamageConversion.AccumulateProduction(normal, 40f, BuildingProfiles.All["OilRefinery"].DamagePerWearUnit);
            normalWear = normal.DamageBalance;
            var slow = new WearAccumulator();
            DamageConversion.AccumulateProduction(slow, 40f, BuildingProfiles.All["OilRefinery"].DamagePerWearUnit);
            slowWear = slow.DamageBalance;
            TestRunner.CheckClose(normalWear, slowWear, 0.0001f, "同一真实生产秒数下，每秒磨损不随效率改变");
            TestRunner.CheckClose(0.8f, normalWear, 0.0001f, "原油精炼器 40 s 生产损失 0.8 HP");
            var desalinator = new WearAccumulator();
            DamageConversion.AccumulateProduction(desalinator, 40f, BuildingProfiles.All["Desalinator"].DamagePerWearUnit);
            TestRunner.CheckClose(0.8f, desalinator.DamageBalance, 0.0001f, "脱盐器磨损同样只按真实秒数");
            var scrubber = new WearAccumulator();
            DamageConversion.AccumulateProduction(scrubber, 200f, BuildingProfiles.All["OreScrubber"].DamagePerWearUnit);
            TestRunner.CheckClose(1f, scrubber.DamageBalance, 0.0001f, "洗涤器 0.005 HP/s：200 s 扣 1 HP");
        }
    }
}
