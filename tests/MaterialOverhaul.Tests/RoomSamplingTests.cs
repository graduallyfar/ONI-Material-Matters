using System;
using System.IO;

namespace MaterialOverhaul.Tests
{
    /// <summary>
    /// 房间快照采样的游戏接入源码结构。替身环境无法构造原版读档顺序，
    /// 这里只核对源码；读档后的实际房间识别需游戏内验收。
    /// </summary>
    public static class RoomSamplingTests
    {
        public static void Run()
        {
            LoadOrderLightDecor();
        }

        /// <summary>
        /// 读档时 RoomProber 在 ClusterManager 建立前就判定一轮房间，读取光照会空引用。
        /// 采样须在 ClusterManager 未就绪时跳过光照加成，并交给环境抽查补判。
        /// </summary>
        private static void LoadOrderLightDecor()
        {
            TestRunner.Section("房间采样：读档首轮不读光照");

            string root = FindRepoRoot();
            TestRunner.Check(root != null, "能找到仓库根目录以读取房间采样源码");
            if (root == null)
            {
                return;
            }

            string factory = File.ReadAllText(Path.Combine(root, "MaterialOverhaul", "Game", "RoomSnapshotFactory.cs"));
            string monitor = File.ReadAllText(Path.Combine(root, "MaterialOverhaul", "Game", "RoomEnvironmentMonitor.cs"));

            TestRunner.Check(factory.Contains("GetDecorAtCell(cell, includeLightDecor)")
                && !factory.Contains("GetDecorAtCell(cell)"),
                "装饰度采样显式传入是否计光照，不再走默认读光照的重载");
            TestRunner.Check(factory.Contains("ClusterManager.Instance != null")
                && factory.Contains("global::Game.Instance != null"),
                "光照加成只在 Game 与 ClusterManager 都已就绪时计入");
            TestRunner.Check(monitor.Contains("RoomSnapshotFactory.Create(room)")
                && monitor.Contains("Stamps.TryGetValue(room, out previous)"),
                "环境抽查对没有记录的房间重新采样判定，读档首轮结果会被补判");
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
