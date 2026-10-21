using System;
using System.IO;

namespace MaterialOverhaul.Tests
{
    /// <summary>
    /// V1.0.0 代码查错后的房间补丁源码结构检查。房间接入依赖原版 Room／Clinic 等对象，
    /// 替身无法验证行为，也不替代游戏内验收；这里只防止已修复的结构问题被无声改回。
    /// </summary>
    public static class RoomPatchFixTests
    {
        public static void Run()
        {
            TestRunner.Section("房间补丁源码结构（不代替游戏内验收）");
            string root = FindRepoRoot();
            TestRunner.Check(root != null, "能找到仓库根目录以读取补丁源码");
            if (root == null) return;

            string game = Path.Combine(root, "MaterialOverhaul", "Game");
            string rooms = File.ReadAllText(Path.Combine(game, "Patches", "RoomPatches.cs"));
            string advanced = File.ReadAllText(Path.Combine(game, "Patches", "RoomAdvancedPatches.cs"));
            string registration = File.ReadAllText(Path.Combine(game, "RoomRegistration.cs"));

            // 1. 诊疗床恢复效果：原版内部状态切换也调用 StopEffect，移除只能挂在患者离床。
            TestRunner.Check(!rooms.Contains("\"StopEffect\")]"), "恢复效果不在 StopEffect 时移除");
            string stopWork = Region(rooms, "[HarmonyPatch(typeof(Clinic), \"OnStopWork\")]", "[HarmonyPatch");
            TestRunner.Check(stopWork.Contains("RoomRecoveryEffect.RemoveAll"), "患者离床（Clinic.OnStopWork）时移除恢复效果");

            // 2. 顶替房间保留原版实验室 +10% 与娱乐室闲聊格。
            string lab = Region(rooms, "[HarmonyPatch(typeof(Workable), \"OnUpdateRoom\")]", "[HarmonyPatch");
            TestRunner.Check(lab.Contains("CountsAsRoom(room, Db.Get().RoomTypes.Laboratory.Id)")
                && lab.Contains("InLaboratory(__instance) = true")
                && lab.Contains("LaboratoryWorkEfficiencyBonus"),
                "顶替实验室的房间补上原版实验室工作效率标记与状态项");
            string mingle = Region(rooms, "[HarmonyPatch(typeof(MingleCellTracker), \"Sim1000ms\")]", "[HarmonyPatch");
            TestRunner.Check(mingle.Contains("CountsAsRoom(room, recRoom.Id)")
                && mingle.Contains("room.roomType == recRoom")
                && mingle.Contains("mingleCells.Add"),
                "顶替娱乐室的房间按原版条件补闲聊格，原版娱乐室不重复添加");

            // 3. 保持回差跨房间重建：清除前记下旧类型，新房间无类型时取回，重建结束丢弃。
            TestRunner.Check(Region(rooms, "[HarmonyPatch(typeof(RoomProber), \"ClearRoom\")]", "[HarmonyPatch")
                .Contains("RoomRetention.Remember"), "清除旧房间前记录类型");
            TestRunner.Check(Region(rooms, "[HarmonyPatch(typeof(RoomProber), \"Refresh\")]", "[HarmonyPatch")
                .Contains("RoomRetention.EndRebuild"), "拓扑刷新结束丢弃记录");
            TestRunner.Check(Region(rooms, "[HarmonyPatch(typeof(RoomProber), \"UpdateRoom\")]", "[HarmonyPatch")
                .Contains("RoomRetention.EndRebuild"), "单房间重建结束丢弃记录");
            TestRunner.Check(Region(registration, "private static string PreviousRoomId", "}\n        }")
                .Contains("RoomRetention.PreviousTypeOf(room)"), "新建房间的上一类型取自重建前记录");

            // 4. 设施使用后的房间／特质效果只由主设施触发。
            string complete = Region(rooms, "class Patch_Workable_CompleteWork", "[HarmonyPatch");
            int primary = complete.IndexOf("primary.building_criteria(prefab)", StringComparison.Ordinal);
            int trait = complete.IndexOf("RoomTraitEffects.TryApply", StringComparison.Ordinal);
            TestRunner.Check(primary >= 0 && trait > primary, "特质效果在主设施判定之后才发放");

            // 5. 离开存档清理旧档房间缓存。
            string cleanup = Region(advanced, "class Patch_RoomAdvanced_Game_OnCleanUp", "[HarmonyPatch");
            TestRunner.Check(cleanup.Contains("RoomEnvironmentMonitor.Clear()")
                && cleanup.Contains("RoomSnapshotCache.BeginEvaluation()")
                && cleanup.Contains("RoomRetention.EndRebuild()"),
                "Game.OnCleanUp 清空环境监视、快照与重建记录");

            // 6. 货运站冷却只在原版递减计时的 loop 状态加速。
            TestRunner.Check(advanced.Contains("smi.IsInsideState(smi.sm.on.cooldown.loop)"), "货运站冷却加速限定 cooldown.loop");
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
