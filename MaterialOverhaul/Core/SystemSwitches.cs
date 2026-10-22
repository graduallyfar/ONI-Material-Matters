using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace MaterialOverhaul.Core
{
    /// <summary>
    /// 整合版的系统开关：工业、电力、房间、辐射各自能否加载。
    ///
    /// 保存在 <c>mods/config/MaterialOverhaul/systems.json</c>，不放在 Mod 本体目录里，
    /// 这样重新部署或更新 Mod 不会把玩家的选择冲掉。只在游戏启动时读取一次：
    /// 补丁装配发生在启动时，所以改动必须重启游戏才生效。
    /// 文件缺失、损坏或缺少某一项时，该项按“开启”处理，保证默认行为与改动前的整合版一致。
    /// </summary>
    public sealed class SystemSwitches
    {
        public const string FolderName = "MaterialOverhaul";
        public const string FileName = "systems.json";

        private static readonly ModSystem[] Toggleable =
        {
            ModSystem.Industrial, ModSystem.Power, ModSystem.Room, ModSystem.Radiation
        };

        public SystemSwitches(bool industrial, bool power, bool room, bool radiation)
        {
            Industrial = industrial;
            Power = power;
            Room = room;
            Radiation = radiation;
        }

        public static SystemSwitches AllOn => new SystemSwitches(true, true, true, true);

        /// <summary>本次启动实际生效的开关；启动时由 Mod 入口写入，之后不再改变。</summary>
        public static SystemSwitches Active { get; private set; } = AllOn;

        /// <summary>界面上依次显示的四个系统。</summary>
        public static ModSystem[] Systems => (ModSystem[])Toggleable.Clone();

        public bool Industrial { get; }
        public bool Power { get; }
        public bool Room { get; }
        public bool Radiation { get; }

        public static void SetActive(SystemSwitches switches)
        {
            Active = switches ?? AllOn;
        }

        public bool IsEnabled(ModSystem system)
        {
            switch (system)
            {
                case ModSystem.Shell: return true;
                case ModSystem.Industrial: return Industrial;
                case ModSystem.Power: return Power;
                case ModSystem.Room: return Room;
                case ModSystem.Radiation: return Radiation;
                default: return false;
            }
        }

        public SystemSwitches With(ModSystem system, bool enabled)
        {
            return new SystemSwitches(
                system == ModSystem.Industrial ? enabled : Industrial,
                system == ModSystem.Power ? enabled : Power,
                system == ModSystem.Room ? enabled : Room,
                system == ModSystem.Radiation ? enabled : Radiation);
        }

        public bool SameAs(SystemSwitches other)
        {
            return other != null && Industrial == other.Industrial && Power == other.Power
                && Room == other.Room && Radiation == other.Radiation;
        }

        public static string Key(ModSystem system)
        {
            switch (system)
            {
                case ModSystem.Industrial: return "industrial";
                case ModSystem.Power: return "power";
                case ModSystem.Room: return "room";
                case ModSystem.Radiation: return "radiation";
                default: return "shell";
            }
        }

        /// <summary>日志与界面用的中文名。</summary>
        public static string ChineseName(ModSystem system)
        {
            switch (system)
            {
                case ModSystem.Industrial: return "工业";
                case ModSystem.Power: return "电力";
                case ModSystem.Room: return "房间";
                case ModSystem.Radiation: return "辐射";
                default: return "外壳";
            }
        }

        /// <summary>例：“工业=开；电力=关；房间=开；辐射=开”。</summary>
        public string Describe()
        {
            StringBuilder text = new StringBuilder();
            foreach (ModSystem system in Toggleable)
            {
                if (text.Length > 0)
                {
                    text.Append("；");
                }

                text.Append(ChineseName(system)).Append('=').Append(IsEnabled(system) ? "开" : "关");
            }

            return text.ToString();
        }

        public string Serialize()
        {
            StringBuilder json = new StringBuilder("{\n");
            for (int i = 0; i < Toggleable.Length; i++)
            {
                json.Append("  \"").Append(Key(Toggleable[i])).Append("\": ")
                    .Append(IsEnabled(Toggleable[i]) ? "true" : "false")
                    .Append(i + 1 < Toggleable.Length ? ",\n" : "\n");
            }

            return json.Append("}\n").ToString();
        }

        /// <summary>宽松读取：只认 <c>"键": true/false</c>，缺项或写错的项按开启处理。</summary>
        public static SystemSwitches Parse(string text)
        {
            SystemSwitches result = AllOn;
            if (string.IsNullOrEmpty(text))
            {
                return result;
            }

            foreach (ModSystem system in Toggleable)
            {
                Match match = Regex.Match(text, "\"" + Key(system) + "\"\\s*:\\s*(true|false)\\b",
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                if (match.Success)
                {
                    result = result.With(system, string.Equals(match.Groups[1].Value, "true", StringComparison.OrdinalIgnoreCase));
                }
            }

            return result;
        }

        public static string ConfigPath(string modsRoot)
        {
            return Path.Combine(Path.Combine(Path.Combine(modsRoot ?? string.Empty, "config"), FolderName), FileName);
        }

        /// <summary>读取配置；文件不存在时返回全开且 <paramref name="problem"/> 为空，读取失败时返回全开并说明原因。</summary>
        public static SystemSwitches Load(string path, out string problem)
        {
            problem = null;
            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                {
                    return AllOn;
                }

                return Parse(File.ReadAllText(path, Encoding.UTF8));
            }
            catch (Exception e)
            {
                problem = e.Message;
                return AllOn;
            }
        }

        public static bool Save(string path, SystemSwitches switches, out string problem)
        {
            problem = null;
            try
            {
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(path, (switches ?? AllOn).Serialize(), new UTF8Encoding(false));
                return true;
            }
            catch (Exception e)
            {
                problem = e.Message;
                return false;
            }
        }
    }
}
