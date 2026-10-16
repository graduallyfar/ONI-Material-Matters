using System.Collections.Generic;
using UnityEngine;

namespace MaterialOverhaul.Core
{
    /// <summary>
    /// 可定位日志。V0.0.1 开发计划 §2.3：未匹配建筑、未匹配配方、温区校验失败都要留下可定位记录，
    /// 不允许静默失效；同时不能每次模拟更新都刷日志，所以提供去重入口。
    /// </summary>
    public static class ModLog
    {
        private const string Prefix = "[MaterialOverhaul]";

        private static readonly HashSet<string> Reported = new HashSet<string>();

        public static void Info(string message)
        {
            Debug.Log(Prefix + " " + message);
        }

        public static void Warn(string message)
        {
            Debug.LogWarning(Prefix + " " + message);
        }

        public static void Error(string message)
        {
            Debug.LogError(Prefix + " " + message);
        }

        /// <summary>同一条标识只报告一次；返回 true 表示本次是首次报告。</summary>
        public static bool WarnOnce(string key, string message)
        {
            if (!Reported.Add(key))
            {
                return false;
            }

            Warn(message + "（本条只报告一次）");
            return true;
        }

        /// <summary>
        /// 同一条标识只报告一次（Info 级）。
        /// 用于「接线是否真的生效」这类只需要确认一次的事实，避免每个模拟步刷屏。
        /// </summary>
        public static bool InfoOnce(string key, string message)
        {
            if (!Reported.Add(key))
            {
                return false;
            }

            Info(message + "（本条只报告一次）");
            return true;
        }

        /// <summary>动态对象清理或读档时重置去重状态，避免长期累积。</summary>
        public static void ResetOnce()
        {
            Reported.Clear();
        }
    }
}
