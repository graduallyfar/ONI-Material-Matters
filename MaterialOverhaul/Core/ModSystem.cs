using System;

namespace MaterialOverhaul.Core
{
    /// <summary>本 Mod 的四个可开关系统；<see cref="Shell"/> 是不属于任何系统、始终加载的外壳（模组界面选项）。</summary>
    public enum ModSystem
    {
        Shell = 0,
        Industrial = 1,
        Power = 2,
        Room = 3,
        Radiation = 4
    }

    /// <summary>
    /// 标明一个 Harmony 补丁类属于哪个系统。整合版启动时按系统开关决定是否应用该补丁；
    /// 独立验证版仍整体 PatchAll，标注只供构建检查核对（每个补丁类必须恰好标注一次）。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class SystemPartAttribute : Attribute
    {
        public SystemPartAttribute(ModSystem system)
        {
            System = system;
        }

        public ModSystem System { get; }
    }
}
