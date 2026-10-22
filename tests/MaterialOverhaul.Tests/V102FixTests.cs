using System;
using System.IO;
using System.Linq;
using MaterialOverhaul.Rooms;

namespace MaterialOverhaul.Tests
{
    /// <summary>
    /// V1.0.2 修复与调整（#14–#22）：调谐上限回退、喷发量最后结算、辐射后遗症按医疗减轻、装饰度门槛、
    /// 压力加减法与房间加成连乘的纯计算，以及只能做源码结构检查的游戏接入。替身不证明游戏内行为。
    /// </summary>
    public static class V102FixTests
    {
        public static void Run()
        {
            GeoTuner();
            Chain();
            RadiationScar();
            StressAndHealing();
            TestRunner.CheckClose(120f, RoomCatalogue.BaseAdvancedDecor, 0.001f, "#18：基地高级房间装饰度门槛 +120");
            SourceStructure();
        }

        private static void GeoTuner()
        {
            TestRunner.Section("#14、#20：地质研究室调谐上限与喷发量");
            TestRunner.CheckClose(1.1f, RoomAdvancedEffects.GeoTunerMassFactor(RoomAdvancedEffects.GeoTunerLabMassBonus), 0.0001f,
                "研究室调谐仪：原版调谐之后 ×1.1");
            TestRunner.CheckClose(1.2f, RoomAdvancedEffects.GeoTunerMassFactor(
                RoomAdvancedEffects.GeoTunerMassBonus(RoomCatalogue.GeoTunerLabId, 21f)), 0.0001f, "研究者科学 > 20：×1.2");
            TestRunner.CheckClose(1f, RoomAdvancedEffects.GeoTunerMassFactor(
                RoomAdvancedEffects.GeoTunerMassBonus("Laboratory", 30f)), 0.0001f, "不在研究室：×1");

            TestRunner.CheckEqual(0, RoomAdvancedEffects.GeotunersToRelease(new[] { 1, 2, 3, 4, 5 }, 5).Length, "5 台未超出原版上限");
            TestRunner.CheckEqual(0, RoomAdvancedEffects.GeotunersToRelease(new[] { 1, 2, 3, 4, 5, 6 }, 6).Length, "研究室仍在时第 6 台保留");
            int[] release = RoomAdvancedEffects.GeotunersToRelease(new[] { 3, 1, 6, 2, 5, 4 }, 5);
            TestRunner.Check(release.Length == 1 && release[0] == 2, "房间失效：取消最晚指定（序号 6）的一台");
            release = RoomAdvancedEffects.GeotunersToRelease(new[] { 0, 0, 0, 0, 0, 0 }, 5);
            TestRunner.Check(release.Length == 1 && release[0] == 5, "读档后序号未知：按列表倒序取消最后一台");
            release = RoomAdvancedEffects.GeotunersToRelease(new[] { 0, 0, 0, 0, 0, 1 }, 5);
            TestRunner.Check(release.Length == 1 && release[0] == 5, "读档后新指定的一台比旧的晚");
            release = RoomAdvancedEffects.GeotunersToRelease(new[] { 1, 7, 2, 3, 4, 5, 6 }, 5);
            TestRunner.Check(release.Length == 2 && release[0] == 1 && release[1] == 6, "超出 2 台：按先后取消最晚的两台");
        }

        private static void Chain()
        {
            TestRunner.Section("#22：房间加成连乘");
            TestRunner.CheckClose(0.32f, RoomAdvancedEffects.ChainBonus(0.2f, 0.1f), 0.0001f, "+20% 与 +10%：基础速度 × 32%");
            TestRunner.CheckClose(0.54f, RoomAdvancedEffects.ChainBonus(0.4f, 0.1f), 0.0001f, "+40% 与 +10%：× 54%");
            TestRunner.CheckClose(0.1f, RoomAdvancedEffects.ChainBonus(0f, 0.1f), 0.0001f, "只有发射场：+10%");
            TestRunner.CheckClose(0f, RoomAdvancedEffects.ChainBonus(0f, 0f), 0.0001f, "没有加成：0");
        }

        private static void RadiationScar()
        {
            TestRunner.Section("#17：辐射后遗症按施治者医疗减轻");
            float[] medicine = { float.NaN, 0f, 12f, 13f, 14f, 16f, 18f, 19f, 20f, 25f };
            int[] expected = { -6, -6, -6, -5, -5, -3, -2, -1, 0, 0 };
            for (int i = 0; i < medicine.Length; i++)
                TestRunner.CheckEqual(expected[i], RoomAdvancedEffects.RadiationScarPenalty(medicine[i]), "医疗 " + medicine[i] + " → 运动 " + expected[i]);

            TestRunner.CheckEqual(RoomEffectIds.RadiationScar, RoomEffectIds.RadiationScarFor(-6), "−6 沿用旧效果 id，兼容旧存档");
            TestRunner.Check(RoomEffectIds.RadiationScarFor(0) == null, "医疗 20 及以上不留后遗症");
            TestRunner.CheckEqual(6, RoomEffectIds.RadiationScars.Length, "后遗症共 6 档");
            for (int penalty = -6; penalty <= -1; penalty++)
            {
                RoomEffectSpec spec = RoomEffectCatalogue.Get(RoomEffectIds.RadiationScarFor(penalty));
                TestRunner.Check(spec != null && spec.Permanent && spec.IsBad && !spec.AttachedToRoom
                    && spec.AttributeId == "Athletics" && spec.AttributeBonus == penalty,
                    "后遗症档 " + penalty + "：永久、运动 " + penalty);
                TestRunner.Check(RoomEffectIds.RadiationScars.Contains(RoomEffectIds.RadiationScarFor(penalty)), "档位 " + penalty + " 在清单中");
            }
        }

        private static void StressAndHealing()
        {
            TestRunner.Section("#21、#22：压力加减法与生命回复连乘");
            TestRunner.CheckClose(-0.05f, RoomEffectCatalogue.StressDeltaPerSecond(30f), 0.00001f, "茶歇室 −30%／周期 = −0.05／秒");
            TestRunner.CheckClose(-10f / 600f, RoomEffectCatalogue.StressDeltaPerSecond(10f), 0.00001f, "淋浴间 −10%／周期");

            RoomEffectSpec hospitalCare = RoomEffectCatalogue.Get(RoomEffectIds.CaregiverBoost);
            RoomEffectSpec medicalCare = RoomEffectCatalogue.Get(RoomEffectIds.CaregiverBoostMedicalCenter);
            TestRunner.Check(hospitalCare != null && medicalCare != null && !medicalCare.AttachedToRoom, "护理员加成按房间分两档");
            if (hospitalCare != null && medicalCare != null)
            {
                TestRunner.CheckClose(1.25f * 1.25f, 1f + (RoomEffectCatalogue.HospitalPercent + hospitalCare.EffectiveHealthModifierPercent) / 100f,
                    0.0001f, "医院 × 护理员 = 1.25 × 1.25");
                TestRunner.CheckClose(1.5f * 1.25f, 1f + (RoomEffectCatalogue.MedicalCenterPercent + medicalCare.EffectiveHealthModifierPercent) / 100f,
                    0.0001f, "医疗中心 × 护理员 = 1.5 × 1.25");
                TestRunner.CheckClose(25f, medicalCare.HealthRecoveryPercent, 0.0001f, "护理员公开数值仍为 +25%");
            }
        }

        private static void SourceStructure()
        {
            TestRunner.Section("V1.0.2 游戏接入源码结构（不代替游戏内验收）");
            string root = FindRepoRoot();
            TestRunner.Check(root != null, "能找到仓库根目录以读取源码");
            if (root == null) return;

            string game = Path.Combine(root, "MaterialOverhaul", "Game");
            string rooms = File.ReadAllText(Path.Combine(game, "Patches", "RoomPatches.cs")).Replace("\r\n", "\n");
            string advanced = File.ReadAllText(Path.Combine(game, "Patches", "RoomAdvancedPatches.cs")).Replace("\r\n", "\n");
            string registration = File.ReadAllText(Path.Combine(game, "RoomRegistration.cs"));
            string geo = File.ReadAllText(Path.Combine(game, "RoomGeoTunerEffects.cs"));
            string effects = File.ReadAllText(Path.Combine(game, "RoomEffects.cs"));
            string medical = File.ReadAllText(Path.Combine(game, "RoomMedicalEffects.cs"));
            string hover = File.ReadAllText(Path.Combine(game, "RoomHoverText.cs"));
            string space = File.ReadAllText(Path.Combine(game, "RoomSpaceEffects.cs"));

            // #15：条件文本在进入游戏后按当前语言重写。
            TestRunner.Check(registration.Contains("public static void EnsureTextsRefreshed()") && registration.Contains("constraint.conflictDescription = text;"),
                "条件文本进入游戏后重写（含冲突说明）");
            TestRunner.Check(rooms.Contains("RoomRegistration.EnsureTextsRefreshed();\n                RoomEnvironmentMonitor.Tick(dt);"),
                "房间探测器每秒更新时确保已重写");

            // #16、#19：悬停卡效果段补救治说明与升级提示，详情列表不追加。
            TestRunner.Check(registration.Contains("RoomHoverText.Install();"), "登记房间时接管效果行求值");
            TestRunner.Check(hover.Contains("if (InDetails || room == null"), "详情列表读取时只返回原版效果");
            TestRunner.Check(hover.Contains("== RoomType.RoomIdentificationResult.primary_unsatisfied") && hover.Contains("upgrade_paths"),
                "升级提示只列主建筑已在房间内的升级房间");
            TestRunner.Check(rooms.Contains("private static void Finalizer()\n        {\n            RoomHoverText.InDetails = false;"),
                "详情生成异常时也会清除标记");

            // #14、#20：喷发量在 GetMassPerCycle 最后结算，不再写入原版修正列表；房间变化后复查上限。
            TestRunner.Check(advanced.Contains("[HarmonyPatch(typeof(GeyserConfigurator.GeyserInstanceConfiguration), \"GetMassPerCycle\")]")
                && advanced.Contains("__result *= RoomGeoTunerEffects.MassFactor(__instance);"), "喷发量最后乘以研究室乘数");
            TestRunner.Check(!geo.Contains("AddModification("), "不再向原版修正列表追加百分比");
            TestRunner.Check(geo.Contains("geyser.ApplyConfigurationEmissionValues(geyser.configuration);"), "乘数变化时写回喷口");
            TestRunner.Check(advanced.Contains("[HarmonyPatch(typeof(GeoTuner.Instance), \"AssignFutureGeyser\")]"), "记录调谐目标指定先后");
            TestRunner.Check(rooms.Contains("RoomGeoTunerEffects.OnRoomChanged(__instance == null ? null : __instance.gameObject);"),
                "调谐仪房间变化时复查");
            TestRunner.Check(geo.Contains("tuner.AssignFutureGeyser(null);"), "超出上限的调谐仪取消目标，需要重新选择");

            // #17：施治完成时减轻后遗症；#21：压力按加法；#22：火箭连乘。
            TestRunner.Check(rooms.Contains("RoomMedicalEffects.EaseRadiationScar(clinic.worker.gameObject, doctor);"), "医生完成诊疗时减轻后遗症");
            TestRunner.Check(medical.Contains("Db.Get().Attributes.Caring.Lookup(doctor)") && medical.Contains("penalty <= spec.AttributeBonus"),
                "按施治者医疗减轻，只减轻不加重");
            string stress = effects.Substring(effects.IndexOf("if (spec.HasStressReduction)", StringComparison.Ordinal));
            stress = stress.Substring(0, stress.IndexOf("if (spec.HasHealthRecovery)", StringComparison.Ordinal));
            TestRunner.Check(stress.Contains("StressDeltaPerSecond") && !stress.Contains("is_multiplier"), "压力缓解为加法修正");
            TestRunner.Check(space.Contains("RoomAdvancedEffects.ChainBonus(missionControl, spaceport)"), "火箭房间加成连乘");
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
