using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using MaterialOverhaul.Core;
using MaterialOverhaul.Game;

namespace MaterialOverhaul.Tests
{
    /// <summary>
    /// 整合版系统开关：配置读写、补丁的系统标注、整合版按开关装配与选项文本。
    /// 模组界面按钮与对话框的外观需游戏内验收。
    /// </summary>
    public static class SystemSwitchTests
    {
        private const string Prefix = "STRINGS.MODOPTIONS.MATERIALOVERHAUL.";

        public static void Run()
        {
            Config();
            FileRoundTrip();
            PatchTags();
            EntryAndBuild();
            Localization();
        }

        private static void Config()
        {
            TestRunner.Section("开关配置：缺省全开、宽松读取、逐项修改");
            SystemSwitches all = SystemSwitches.AllOn;
            foreach (ModSystem system in SystemSwitches.Systems)
                TestRunner.Check(all.IsEnabled(system), SystemSwitches.ChineseName(system) + " 缺省开启");
            TestRunner.Check(all.IsEnabled(ModSystem.Shell), "外壳始终开启");
            TestRunner.CheckEqual(4, SystemSwitches.Systems.Length, "界面列出四个系统");

            SystemSwitches partial = all.With(ModSystem.Power, false).With(ModSystem.Radiation, false);
            TestRunner.Check(partial.Industrial && !partial.Power && partial.Room && !partial.Radiation, "With 只改指定系统");
            TestRunner.Check(all.Power, "With 不改原对象");
            TestRunner.CheckEqual("工业=开；电力=关；房间=开；辐射=关", partial.Describe(), "日志描述");
            TestRunner.Check(partial.SameAs(SystemSwitches.Parse(partial.Serialize())), "序列化后可原样读回");
            TestRunner.Check(!partial.SameAs(all), "SameAs 能区分不同开关");

            TestRunner.Check(SystemSwitches.Parse(null).SameAs(all), "空内容按全开");
            TestRunner.Check(SystemSwitches.Parse("坏文件 {{{").SameAs(all), "损坏内容按全开");
            SystemSwitches loose = SystemSwitches.Parse("{ \"ROOM\" : False, \"power\": maybe }");
            TestRunner.Check(!loose.Room && loose.Power && loose.Industrial && loose.Radiation, "大小写不敏感；写错的值与缺项按开启");
            TestRunner.Check(SystemSwitches.ConfigPath("/mods/").Replace('\\', '/').EndsWith("config/MaterialOverhaul/systems.json"),
                "配置放在 mods/config/MaterialOverhaul，不随重新部署被覆盖");
        }

        private static void FileRoundTrip()
        {
            TestRunner.Section("开关配置：文件读写");
            string dir = Path.Combine(Path.GetTempPath(), "mo-switch-" + Guid.NewGuid().ToString("N"));
            try
            {
                string path = SystemSwitches.ConfigPath(dir);
                string problem;
                TestRunner.Check(SystemSwitches.Load(path, out problem).SameAs(SystemSwitches.AllOn) && problem == null, "文件不存在时全开且不报错");
                SystemSwitches saved = SystemSwitches.AllOn.With(ModSystem.Industrial, false);
                TestRunner.Check(SystemSwitches.Save(path, saved, out problem), "保存时自动建目录");
                TestRunner.Check(SystemSwitches.Load(path, out problem).SameAs(saved), "读回保存的开关");
                TestRunner.Check(File.ReadAllText(path).Contains("\"industrial\": false"), "文件是可手改的 JSON");
            }
            finally
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }

        private static void PatchTags()
        {
            TestRunner.Section("补丁系统标注：每个补丁类恰好一个，与独立版文件归属一致");
            string root = FindRepoRoot();
            TestRunner.Check(root != null, "能找到仓库根目录");
            if (root == null) return;
            var owner = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["PowerTopologyPatches"] = "Power", ["PowerInfoCardPatches"] = "Power", ["PowerSettlementPatches"] = "Power",
                ["RoomPatches"] = "Room", ["RoomMaterialPatches"] = "Room", ["RoomAdvancedPatches"] = "Room",
                ["RadiationPatches"] = "Radiation", ["ModOptionsPatches"] = "Shell"
            };
            int total = 0;
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (string file in Directory.GetFiles(Path.Combine(root, "MaterialOverhaul", "Game", "Patches"), "*.cs"))
            {
                string name = Path.GetFileNameWithoutExtension(file);
                string expected = owner.TryGetValue(name, out string mapped) ? mapped : "Industrial";
                string[] lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    if (!Regex.IsMatch(lines[i], @"^\s*\[HarmonyPatch\b")) continue;
                    total++;
                    Match tag = i > 0 ? Regex.Match(lines[i - 1], @"^\s*\[Core\.SystemPart\(Core\.ModSystem\.(\w+)\)\]\s*$") : Match.Empty;
                    TestRunner.Check(tag.Success, name + " 第 " + (i + 1) + " 行补丁前有系统标注");
                    if (!tag.Success) continue;
                    TestRunner.CheckEqual(expected, tag.Groups[1].Value, name + " 第 " + (i + 1) + " 行标注为 " + expected);
                    counts[expected] = counts.TryGetValue(expected, out int n) ? n + 1 : 1;
                }
            }

            TestRunner.CheckEqual(101, total, "共 101 个补丁类（四个系统 100 个 + 模组界面外壳 1 个）");
            TestRunner.CheckEqual(45, counts.GetValueOrDefault("Industrial"), "工业 45 个");
            TestRunner.CheckEqual(5, counts.GetValueOrDefault("Power"), "电力 5 个");
            TestRunner.CheckEqual(46, counts.GetValueOrDefault("Room"), "房间 46 个");
            TestRunner.CheckEqual(4, counts.GetValueOrDefault("Radiation"), "辐射 4 个");
            TestRunner.CheckEqual(1, counts.GetValueOrDefault("Shell"), "外壳 1 个");
        }

        private static void EntryAndBuild()
        {
            TestRunner.Section("整合版按开关装配；独立版不含系统开关");
            string root = FindRepoRoot();
            if (root == null) return;
            string entry = File.ReadAllText(Path.Combine(root, "MaterialOverhaul", "Game", "MaterialOverhaulMod.cs"));
            TestRunner.Check(Regex.IsMatch(entry, @"#if MATERIAL_OVERHAUL_COMBINED\s+ApplySwitchedPatches\(harmony\);\s+#else\s+base\.OnLoad\(harmony\);"),
                "整合版按开关装配，独立版仍整体 PatchAll");
            TestRunner.Check(entry.Contains("harmony.CreateClassProcessor(type).Patch();"), "逐个补丁类装配");
            TestRunner.Check(entry.Contains("!switches.IsEnabled(part.System)"), "关闭系统的补丁类被跳过");
            TestRunner.Check(entry.Contains("SystemSwitches.SetActive(switches);"), "记下本次启动的实际开关");
            TestRunner.Check(Regex.IsMatch(entry, @"if \(IsOn\(ModSystem\.Industrial\)\)\s*\{\s*Core\.DebugValues\.Initialize\(path\);"),
                "工业关闭时不读工业调试数值");

            string project = File.ReadAllText(Path.Combine(root, "MaterialOverhaul", "MaterialOverhaul.csproj"));
            TestRunner.Check(project.Contains("'$(SystemProfile)' == 'Combined'\">$(DefineConstants);MATERIAL_OVERHAUL_COMBINED"), "只有整合版定义 MATERIAL_OVERHAUL_COMBINED");
            TestRunner.CheckEqual(3, Regex.Matches(project, @"Include=""Core\\BuildProfile\.cs;Core\\ModSystem\.cs;").Count, "电力、房间、辐射独立版都编入系统标注");
            TestRunner.Check(Regex.IsMatch(project, @"Remove=""Core\\SystemSwitches\.cs;Game\\OptionsStrings\.cs;Game\\SystemOptionsDialog\.cs;Game\\Patches\\ModOptionsPatches\.cs;"),
                "工业独立版排除系统开关");

            string dialog = File.ReadAllText(Path.Combine(root, "MaterialOverhaul", "Game", "SystemOptionsDialog.cs"));
            TestRunner.Check(dialog.Contains("mod.staticID != ModInfo.StaticID"), "按钮只加在本 Mod 那一行");
            TestRunner.Check(dialog.Contains("SystemSwitches.Save(ConfigPath, pending"), "保存写入配置文件");
            TestRunner.Check(dialog.Contains("App.instance.Restart"), "开关变化时提示重启");
        }

        private static void Localization()
        {
            TestRunner.Section("选项文本：字段树与中文 PO 一致");
            LocString.CreateLocStringKeys(typeof(OptionsStrings.MODOPTIONS), "STRINGS.");
            Dictionary<string, string> keys = Strings.Values
                .Where(pair => pair.Key.StartsWith(Prefix, StringComparison.Ordinal))
                .ToDictionary(pair => pair.Key, pair => pair.Value);
            TestRunner.CheckEqual(22, keys.Count, "选项字段树登记 22 条");
            foreach (ModSystem system in SystemSwitches.Systems)
            {
                string token = SystemSwitches.Key(system).ToUpperInvariant();
                TestRunner.Check(keys.ContainsKey(Prefix + "SYSTEMS." + token) && keys.ContainsKey(Prefix + "SYSTEMS." + token + "_TOOLTIP"),
                    SystemSwitches.ChineseName(system) + " 有名称与说明");
            }

            string root = FindRepoRoot();
            if (root == null) return;
            var po = new Dictionary<string, string>(StringComparer.Ordinal);
            string key = null;
            foreach (string line in File.ReadAllLines(Path.Combine(root, "MaterialOverhaul", "mod", "strings", "zh.po")))
            {
                Match context = Regex.Match(line, "^msgctxt \"(.*)\"$");
                if (context.Success) { key = context.Groups[1].Value; continue; }
                Match str = Regex.Match(line, "^msgstr \"(.*)\"$");
                if (str.Success && key != null) { po[key] = str.Groups[1].Value.Replace("\\n", "\n").Replace("\\\"", "\""); key = null; }
            }

            var optionsPo = po.Where(pair => pair.Key.StartsWith(Prefix, StringComparison.Ordinal)).ToDictionary(p => p.Key, p => p.Value);
            TestRunner.Check(keys.Keys.OrderBy(k => k).SequenceEqual(optionsPo.Keys.OrderBy(k => k)), "字段树键与 PO 键集合一致");
            foreach (KeyValuePair<string, string> entry in keys)
                if (optionsPo.TryGetValue(entry.Key, out string translated))
                    TestRunner.CheckEqual(entry.Value, translated, entry.Key + " 中文 PO 与字段默认文本一致");
        }

        private static string FindRepoRoot()
        {
            DirectoryInfo dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "MaterialOverhaul", "MaterialOverhaul.csproj")))
                    return dir.FullName;
                dir = dir.Parent;
            }
            return null;
        }
    }
}
