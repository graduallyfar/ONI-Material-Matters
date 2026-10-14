using System;
using System.IO;
using MaterialOverhaul.Zones;

namespace MaterialOverhaul.Core
{
    /// <summary>
    /// 调试样例值开关。**这些样例值不是正式平衡数值**，只用于在游戏里观察机制。
    ///
    /// v0.0.3 起，本版的常规数值（铝配方温区、金属精炼器要求硬度、加热功率、磨损与伤害转换）
    /// 已经默认生效，**不需要这个文件**。标记文件只把样例值填补给「还没有数值」的对象：
    /// 其余白名单配方的温区、其余白名单建筑的要求硬度。它不会覆盖任何已确认数值。
    ///
    /// 开启方式：在 Mod 目录下放一个空文件 <c>debug_values.txt</c>，重启游戏即可生效；
    /// 删掉该文件就回到「只有常规数值」的状态。**不需要重新编译。**
    /// </summary>
    public static class DebugValues
    {
        public const string MarkerFileName = "debug_values.txt";

        private static bool enabled;

        /// <summary>调试样例值是否启用（由 Mod 目录下是否存在标记文件决定）。</summary>
        public static bool Enabled
        {
            get { return enabled; }
        }

        /// <summary>
        /// 样例温区（V0.1.2：只填**配方最佳温区**）＝ 35 ℃ ~ 55 ℃，仅用于观察六态与效率。
        /// 建筑下限仍需另有数值；没有建筑下限的建筑不会因为这条样例而启用温控。
        /// </summary>
        public static TempZone SampleZone
        {
            get { return TempZone.Celsius(35f, 55f); }
        }

        /// <summary>
        /// 样例建筑最低工作温度＝5 ℃。只填补给「还没有建筑下限数值」的温区白名单建筑，
        /// 不覆盖金属精炼器已确认的 80 ℃。
        /// </summary>
        public static float SampleWorkMinKelvin
        {
            get { return Temperatures.FromCelsius(5f); }
        }

        /// <summary>样例要求硬度：对应原版分级 FIRM = 25。</summary>
        public static int SampleRequiredHardness
        {
            get { return 25; }
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
                ModLog.Warn("调试开关已启用：样例温区与样例要求硬度会填补给尚未配置的对象，"
                    + "**不是正式平衡数值**，也不会覆盖已确认的常规数值。删掉 " + MarkerFileName + " 即关闭。");
            }
        }
    }
}
