using System.Collections.Generic;

namespace MaterialOverhaul.Core
{
    /// <summary>
    /// 测试替身：替代 Core\ModLog.cs（后者依赖 UnityEngine.Debug）。
    /// 记录日志文本，使"不允许静默失效"这条约定也能被断言。
    /// </summary>
    public static class ModLog
    {
        private static readonly HashSet<string> reportedOnce = new HashSet<string>();

        public static readonly List<string> Messages = new List<string>();

        public static void Info(string message)
        {
            Messages.Add("INFO " + message);
        }

        public static void Warn(string message)
        {
            Messages.Add("WARN " + message);
        }

        public static void Error(string message)
        {
            Messages.Add("ERROR " + message);
        }

        public static bool WarnOnce(string key, string message)
        {
            if (!reportedOnce.Add(key))
            {
                return false;
            }

            Warn(message + "（本条只报告一次）");
            return true;
        }

        public static void ResetOnce()
        {
            reportedOnce.Clear();
        }

        public static void Clear()
        {
            Messages.Clear();
            reportedOnce.Clear();
        }

        public static int CountOf(string level)
        {
            int count = 0;
            foreach (string message in Messages)
            {
                if (message.StartsWith(level + " "))
                {
                    count++;
                }
            }

            return count;
        }
    }
}
