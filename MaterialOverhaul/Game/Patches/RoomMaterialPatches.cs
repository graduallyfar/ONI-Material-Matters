using System;
using HarmonyLib;
using MaterialOverhaul.Core;
using MaterialOverhaul.Game;

namespace MaterialOverhaul.Patches
{
    /// <summary>
    /// 在原版建立元素装饰度修饰之后写入房间系统的材料装饰度配置。
    /// 与工业系统的过热修正补丁作用于同一方法：两者处理不同属性，互不覆盖。
    /// </summary>
    [Core.SystemPart(Core.ModSystem.Room)]
    [HarmonyPatch(typeof(LegacyModMain), "ConfigElements")]
    internal static class Patch_RoomMaterial_ConfigElements
    {
        private static void Postfix()
        {
            try
            {
                MaterialDecorRegistration.Apply();
            }
            catch (Exception e)
            {
                ModLog.Error("材料装饰度登记失败：" + e);
            }
        }
    }
}
