using System;
using System.Collections.Generic;

namespace MaterialOverhaul.Tests
{
    /// <summary>极简断言与统计。不引第三方测试框架，便于离线运行。</summary>
    public static class TestRunner
    {
        private static string section = string.Empty;

        public static int Checks;
        public static int Failures;

        public static void Section(string name)
        {
            section = name;
            Console.WriteLine("  -- " + name);
        }

        public static void Check(bool condition, string name)
        {
            Checks++;
            if (!condition)
            {
                Failures++;
                Console.WriteLine("     [失败] " + section + " / " + name);
            }
        }

        public static void CheckEqual<T>(T expected, T actual, string name)
        {
            Checks++;
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
            {
                Failures++;
                Console.WriteLine("     [失败] " + section + " / " + name + "：期望 " + expected + "，实际 " + actual);
            }
        }

        public static void CheckClose(float expected, float actual, float tolerance, string name)
        {
            Checks++;
            if (Math.Abs(expected - actual) > tolerance)
            {
                Failures++;
                Console.WriteLine("     [失败] " + section + " / " + name + "：期望 " + expected + "±" + tolerance + "，实际 " + actual);
            }
        }
    }
}
