using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using MaterialOverhaul.Game;

namespace MaterialOverhaul.Tests
{
    /// <summary>
    /// 房间本地化：字段树键、中文 PO 与默认文本三方一致。
    /// 与 ThirdWaveBuildingTests 的工业文本检查分开，避免把房间包差异算成工业回归。
    /// </summary>
    public static class RoomLocalizationTests
    {
        private const string Prefix = "STRINGS.ROOMS.MATERIALOVERHAUL.";

        public static void Run()
        {
            TestRunner.Section("房间文本：字段树与中文 PO 一一对应");

            LocString.CreateLocStringKeys(typeof(RoomStrings.ROOMS), "STRINGS.");
            Dictionary<string, string> roomKeys = Strings.Values
                .Where(pair => pair.Key.StartsWith(Prefix, StringComparison.Ordinal))
                .ToDictionary(pair => pair.Key, pair => pair.Value);

            TestRunner.CheckEqual(89, roomKeys.Count, "房间字段树登记 89 条");
            foreach (KeyValuePair<string, string> entry in roomKeys)
            {
                TestRunner.Check(!string.IsNullOrEmpty(entry.Value), entry.Key + " 有默认文本");
                TestRunner.Check(!entry.Value.Contains("MISSING"), entry.Key + " 默认文本不是 MISSING");
            }

            string root = FindRepoRoot();
            TestRunner.Check(root != null, "能找到仓库根目录以读取 zh.po");
            if (root == null)
            {
                return;
            }

            Dictionary<string, string> po = ReadPo(Path.Combine(root, "MaterialOverhaul", "mod", "strings", "zh.po"));
            Dictionary<string, string> roomPo = po
                .Where(pair => pair.Key.StartsWith(Prefix, StringComparison.Ordinal))
                .ToDictionary(pair => pair.Key, pair => pair.Value);
            TestRunner.CheckEqual(89, roomPo.Count, "zh.po 有 89 条房间键");
            TestRunner.Check(roomKeys.Keys.OrderBy(k => k).SequenceEqual(roomPo.Keys.OrderBy(k => k)),
                "房间字段树键与 PO 键集合一致");

            foreach (KeyValuePair<string, string> entry in roomKeys)
            {
                string translated;
                if (!roomPo.TryGetValue(entry.Key, out translated))
                {
                    continue;
                }

                TestRunner.CheckEqual(entry.Value, translated, entry.Key + " 中文 PO 与字段默认文本一致");
                TestRunner.CheckEqual(Placeholders(entry.Value), Placeholders(translated), entry.Key + " 占位符一致");
            }
        }

        private static Dictionary<string, string> ReadPo(string path)
        {
            Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.Ordinal);
            string key = null;
            foreach (string line in File.ReadAllLines(path))
            {
                Match context = Regex.Match(line, "^msgctxt \"(.*)\"$");
                if (context.Success)
                {
                    key = context.Groups[1].Value;
                    continue;
                }

                Match str = Regex.Match(line, "^msgstr \"(.*)\"$");
                if (str.Success && key != null)
                {
                    values[key] = Unescape(str.Groups[1].Value);
                    key = null;
                }
            }

            return values;
        }

        private static string Placeholders(string text)
        {
            return string.Join(",", Regex.Matches(text, @"\{\d+\}").Cast<Match>().Select(m => m.Value).OrderBy(v => v));
        }

        private static string Unescape(string value)
        {
            return value.Replace("\\n", "\n").Replace("\\\"", "\"");
        }

        private static string FindRepoRoot()
        {
            DirectoryInfo directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                if (Directory.Exists(Path.Combine(directory.FullName, "docs"))
                    && Directory.Exists(Path.Combine(directory.FullName, "MaterialOverhaul")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            return null;
        }
    }
}
