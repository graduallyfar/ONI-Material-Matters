using System;
using HarmonyLib;
using KMod;
using MaterialOverhaul.Core;
using MaterialOverhaul.Hardness;
using MaterialOverhaul.Zones;

namespace MaterialOverhaul
{
    /// <summary>
    /// Mod 入口。原版 DLLLoader 会实例化程序集中的 <see cref="UserMod2"/> 派生类并调用 OnLoad，
    /// 基类默认执行 <c>harmony.PatchAll(assembly)</c>。
    /// </summary>
    public class MaterialOverhaulMod : UserMod2
    {
        public override void OnLoad(Harmony harmony)
        {
            try
            {
                base.OnLoad(harmony);
            }
            catch (Exception e)
            {
                // 补丁目标未匹配时必须留下可定位记录，不能静默声称生效（§9、AGENTS 实现约定）。
                ModLog.Error("Harmony 补丁装配失败，Mod 未能完整生效：" + e);
                throw;
            }

            // 注意：UserMod2 继承链上存在同名成员 ModInfo，此处必须用限定名指到本 Mod 的常量类。
            ModLog.Info("v" + Core.ModInfo.Version + " 已加载。白名单建筑 " + BuildingWhitelist.Count
                + " 项，配方温区条目 " + RecipeZoneTable.Count
                + "，要求硬度条目 " + HardnessRequirementTable.Count + "。");
        }
    }
}
