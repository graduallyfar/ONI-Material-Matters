using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MaterialOverhaul.Core;
using MaterialOverhaul.Game;
using MaterialOverhaul.Power;
using MaterialOverhaul.Rooms;

namespace MaterialOverhaul.Tests
{
    /// <summary>
    /// V1.0.1 修复与调整：装饰度门槛、线损系数、深渊晶石导热、房间文本，以及只能做源码结构检查的
    /// 游戏接入（静态初始化时机、房间加成状态、预热状态）。替身不证明游戏内行为，游戏验收另行执行。
    /// </summary>
    public static class V101FixTests
    {
        public static void Run()
        {
            DecorThresholds();
            PowerCoefficients();
            AbyssaliteConductivity();
            RoomTexts();
            SourceStructure();
        }

        private static void DecorThresholds()
        {
            TestRunner.Section("房间平均装饰度门槛（V1.0.1）");
            var expected = new Dictionary<string, float>(StringComparer.Ordinal)
            {
                { RoomCatalogue.IndustrialAreaId, 20f }, { RoomCatalogue.MissionControlId, 20f }, { RoomCatalogue.GeoTunerLabId, 20f },
                { RoomCatalogue.MedicalCenterId, 50f }, { "MO_BeachParadise", 50f }, { "MO_WindTunnel", 50f }, { "MO_Spa", 50f },
                { "MO_DanceHall", 65f }, { "MO_ArcadeHall", 65f }, { "MO_TeaRoom", 65f },
                { "MO_Gallery", 80f },
                { "Private Bedroom", 50f }, { "BanquetHall", 80f }
            };

            int found = 0;
            foreach (RoomDefinition definition in RoomCatalogue.NewRooms.Concat(RoomCatalogue.Modifications))
            {
                string id = definition.IsModification ? definition.ExistingRoomId : definition.Id;
                RoomRequirement decor = definition.Additional.FirstOrDefault(r => r.Kind == RoomRequirementKind.MinimumDecor);
                if (decor == null)
                {
                    TestRunner.Check(!expected.ContainsKey(id), id + " 应有装饰度要求");
                    continue;
                }

                found++;
                TestRunner.Check(expected.ContainsKey(id), id + " 不应有装饰度要求");
                if (expected.TryGetValue(id, out float value))
                    TestRunner.CheckClose(value, decor.FloatValue, 0.001f, id + " 装饰度门槛");
            }

            TestRunner.CheckEqual(expected.Count, found, "带装饰度要求的房间数");
        }

        private static void PowerCoefficients()
        {
            TestRunner.Section("线路损耗基础系数放大 5 倍（V1.0.1）");
            TestRunner.CheckClose(0.04f, (float)PowerBalance.Parameters.A0, 0.00001f, "a0 = 0.04");
            TestRunner.CheckClose(0.06f, (float)PowerBalance.Parameters.A1, 0.00001f, "a1 = 0.06");
        }

        private static void AbyssaliteConductivity()
        {
            TestRunner.Section("深渊晶石导热系数（V1.0.1）");
            ElementLoader.elements.Clear();
            MaterialRegistration.SetAbyssaliteConductivity(); // 缺少元素不能崩溃。
            Element abyss = new Element { id = SimHashes.Katairite, thermalConductivity = 0.00001f };
            Element ceramic = new Element { id = SimHashes.Ceramic, thermalConductivity = 0.62f };
            ElementLoader.elements.Add(abyss);
            ElementLoader.elements.Add(ceramic);
            MaterialRegistration.SetAbyssaliteConductivity();
            MaterialRegistration.SetAbyssaliteConductivity();
            TestRunner.CheckClose(0.1f, abyss.thermalConductivity, 0.000001f, "深渊晶石导热系数为 0.1");
            TestRunner.CheckClose(Balance.AbyssaliteThermalConductivity, abyss.thermalConductivity, 0.000001f, "取自集中配置");
            TestRunner.CheckClose(0.62f, ceramic.thermalConductivity, 0.000001f, "其他材料不受影响");
            ElementLoader.elements.Clear();
        }

        private static void RoomTexts()
        {
            TestRunner.Section("房间文本：图例简述与建筑名（V1.0.1）");
            LocString.CreateLocStringKeys(typeof(RoomStrings.ROOMS), "STRINGS.");
            foreach (RoomDefinition definition in RoomCatalogue.NewRooms)
            {
                string key = definition.NameKey.Substring(0, definition.NameKey.Length - ".NAME".Length) + ".EFFECT";
                TestRunner.Check(Strings.Values.TryGetValue(key, out string effect) && effect.StartsWith("- ", StringComparison.Ordinal),
                    definition.Id + " 有原版格式的图例简述");
                TestRunner.Check(Strings.Values.TryGetValue(definition.TooltipKey, out string tooltip) && !tooltip.Contains("格。"),
                    definition.Id + " 提示不再重复构成条件");
            }

            string all = string.Join("\n", Strings.Values.Values);
            TestRunner.Check(!all.Contains("群星指挥站"), "不再使用「群星指挥站」");
            TestRunner.Check(!all.Contains("首轮测试初值"), "界面文本不含开发备注");
            TestRunner.CheckEqual("至少 1 个{0}", Strings.Values["STRINGS.ROOMS.MATERIALOVERHAUL.CRITERIA.PRIMARY"], "主条件写建筑名");
        }

        private static void SourceStructure()
        {
            TestRunner.Section("游戏接入源码结构（V1.0.1，不代替游戏内验收）");
            string root = FindRepoRoot();
            TestRunner.Check(root != null, "能找到仓库根目录以读取源码");
            if (root == null) return;

            string game = Path.Combine(root, "MaterialOverhaul", "Game");
            string allPatches = string.Join("\n", Directory.GetFiles(game, "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText));
            string rooms = File.ReadAllText(Path.Combine(game, "Patches", "RoomPatches.cs"));
            string advanced = File.ReadAllText(Path.Combine(game, "Patches", "RoomAdvancedPatches.cs"));
            string registration = File.ReadAllText(Path.Combine(game, "RoomRegistration.cs"));
            string text = File.ReadAllText(Path.Combine(game, "RoomText.cs"));
            string heating = File.ReadAllText(Path.Combine(game, "MaterialOverhaulFab.Heating.cs"));
            string status = File.ReadAllText(Path.Combine(game, "RunStatusText.cs"));

            // #2：给 RoomConstraints 打补丁会在语言载入前触发其静态初始化，原版条件名变成英文。
            TestRunner.Check(!allPatches.Contains("[HarmonyPatch(typeof(RoomConstraints)"), "不给 RoomConstraints 打补丁");

            // #13：补回加载阶段被提前初始化为空的调谐仪音效路径。
            string geo = Region(advanced, "[HarmonyPatch(typeof(GeoTuner), \"InitializeStates\")]", "[HarmonyPatch");
            foreach (string field in new[] { "liquidGeyserTuningSoundPath", "gasGeyserTuningSoundPath", "metalGeyserTuningSoundPath" })
                TestRunner.Check(geo.Contains("string.IsNullOrEmpty(GeoTuner." + field + ")") && geo.Contains("GeoTuner." + field + " = GlobalAssets.GetSound("),
                    "状态机初始化时补回 " + field);

            // #1、#3、#4：所有房间显示平均装饰度；新增房间带图例简述；主条件显示建筑名。
            TestRunner.Check(registration.Contains("AppendAverageDecor(types);") && registration.Contains("foreach (RoomType type in types.resources)"),
                "全部房间类型追加平均装饰度");
            TestRunner.Check(registration.Contains("RoomText.RoomEffect(definition),"), "新增房间传入图例简述");
            TestRunner.Check(Region(text, "public static string PrimaryName", "public static string RequirementName").Contains("PrefabName(ids[i])"),
                "主条件按专属设施建筑名显示");

            // #9、#10：建筑房间加成状态随房间建立挂上、清除前摘下。
            TestRunner.Check(Region(rooms, "[HarmonyPatch(typeof(RoomProber), \"CreateRoom\")]", "[HarmonyPatch").Contains("RoomBonusStatus.Apply"),
                "房间建立后挂上建筑加成状态");
            TestRunner.Check(Region(rooms, "[HarmonyPatch(typeof(RoomProber), \"ClearRoom\")]", "[HarmonyPatch").Contains("RoomBonusStatus.Remove"),
                "房间清除前摘下建筑加成状态");

            // #6：温度暂停期间不显示原版「操作任务」，预热状态写出目标温度。
            string pending = Region(heating, "private void UpdatePendingWorkStatus", "/// <summary>");
            TestRunner.Check(pending.Contains("productionPausedByTemperature") && pending.Contains("RemoveStatusItem(pending)"),
                "温度暂停时摘下原版操作任务状态");
            TestRunner.Check(pending.Contains("smi.GoTo(smi.sm.idle.waitingForMaterial)"), "恢复后交还原版状态机重新挂回");
            TestRunner.Check(status.Contains("Localized(\"TARGET_SUFFIX\""), "预热状态附带目标温度");
        }

        private static string Region(string text, string start, string end)
        {
            text = text.Replace("\r\n", "\n");
            int from = text.IndexOf(start, StringComparison.Ordinal);
            if (from < 0) return string.Empty;
            int to = text.IndexOf(end, from + start.Length, StringComparison.Ordinal);
            return to < 0 ? text.Substring(from) : text.Substring(from, to - from);
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
    }
}
