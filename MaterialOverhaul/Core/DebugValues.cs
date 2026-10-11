using System;
using System.IO;

namespace MaterialOverhaul.Core
{
    /// <summary>
    /// 调试数值开关。**这些样例值不是正式平衡数值**，只用于在游戏里确认机制是否生效。
    ///
    /// 背景：温区四边界、要求硬度、磨损与伤害转换、加热功率目前全部是待定项，
    /// 判断层因此一律退化为原版行为——装上 Mod 后游戏里看不到任何变化，机制也就无法验证。
    ///
    /// 开启方式：在 Mod 目录下放一个空文件 <c>debug_values.txt</c>，重启游戏即可生效；
    /// 删掉该文件就回到「无行为变化」状态。**不需要重新编译。**
    /// </summary>
    public static class DebugValues
    {
        public const string MarkerFileName = "debug_values.txt";

        /// <summary>打磨掉时使用的占位磨损速率（正式数值未定，不产生玩法影响之外的变化）。</summary>
        public const float DefaultWearPerSecond = 1f;

        private static bool enabled;

        /// <summary>调试数值是否启用（由 Mod 目录下是否存在标记文件决定）。</summary>
        public static bool Enabled
        {
            get { return enabled; }
        }

        /// <summary>样例温区：5 ℃ / 35 ℃ / 55 ℃ / 75 ℃，仅用于观察五态与效率。</summary>
        public static Zones.TempZone SampleZone
        {
            get { return new Zones.TempZone(278.15f, 308.15f, 328.15f, 348.15f); }
        }

        /// <summary>样例要求硬度：对应原版分级 FIRM = 25。</summary>
        public static int SampleRequiredHardness
        {
            get { return 25; }
        }

        public static float WearPerSecond
        {
            get { return enabled ? 10f : DefaultWearPerSecond; }
        }

        public static float DamagePerWearUnit
        {
            get { return enabled ? 1f : 0f; }
        }

        public static float HeatingKilowatts
        {
            get { return enabled ? 5f : 0f; }
        }

        /// <summary>由 Mod 入口调用，检查标记文件。</summary>
        public static void Initialize(string modDirectory)
        {
            try
            {
                enabled = !string.IsNullOrEmpty(modDirectory)
                    && File.Exists(Path.Combine(modDirectory, MarkerFileName));
            }
            catch (Exception e)
            {
                enabled = false;
                ModLog.Warn("调试数值开关检查失败：" + e.Message);
            }

            if (enabled)
            {
                ModLog.Warn("调试数值已启用：温区／要求硬度／磨损与加热功率使用样例值，"
                    + "**不是正式平衡数值**。删掉 " + MarkerFileName + " 即恢复原版行为。");
            }
        }
    }
}
