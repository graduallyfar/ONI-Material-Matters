using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace MaterialOverhaul.Game
{
    /// <summary>
    /// 原版 <c>ComplexFabricator</c> 私有／受保护成员的访问出口。
    ///
    /// 这些成员在原版是私有状态：订单轮换指针、下一个订单是否可执行、配方计数字典、
    /// 剩余队列数、原料判断。访问点集中在这里，字段名或方法名一旦变化，
    /// **静态初始化即失败**（可定位失败，不是静默失效；见 V0.0.1 开发计划 §11）。
    ///
    /// 只读判断尽量调用原版方法本身，避免复制一份原版逻辑后产生偏差：
    ///   - <c>GetRemainingQueueCount</c> 是私有非虚方法，用开放委托直接调用；
    ///   - <c>HasIngredients</c> 是受保护**虚**方法（LiquidCooledRefinery 覆写以要求冷却液），
    ///     因此用反射调用以保证虚分派，不能绑成基类委托。
    /// </summary>
    internal static class FabricatorAccess
    {
        internal static readonly AccessTools.FieldRef<ComplexFabricator, int> NextOrderIdx =
            AccessTools.FieldRefAccess<ComplexFabricator, int>("nextOrderIdx");

        internal static readonly AccessTools.FieldRef<ComplexFabricator, bool> NextOrderIsWorkable =
            AccessTools.FieldRefAccess<ComplexFabricator, bool>("nextOrderIsWorkable");

        internal static readonly AccessTools.FieldRef<ComplexFabricator, Dictionary<string, int>> QueueCounts =
            AccessTools.FieldRefAccess<ComplexFabricator, Dictionary<string, int>>("recipeQueueCounts");

        private static readonly MethodInfo GetRemainingQueueCountMethod =
            AccessTools.Method(typeof(ComplexFabricator), "GetRemainingQueueCount");

        private static readonly MethodInfo HasIngredientsMethod =
            AccessTools.Method(typeof(ComplexFabricator), "HasIngredients");

        private static readonly Func<ComplexFabricator, ComplexRecipe, int> GetRemainingQueueCountDelegate =
            AccessTools.MethodDelegate<Func<ComplexFabricator, ComplexRecipe, int>>(GetRemainingQueueCountMethod);

        /// <summary>原版口径的剩余队列数：无限订单恒有剩余，正在生产的那一批已从计数里扣掉。</summary>
        public static int GetRemainingQueueCount(ComplexFabricator fabricator, ComplexRecipe recipe)
        {
            return GetRemainingQueueCountDelegate(fabricator, recipe);
        }

        /// <summary>原版原料判断（虚分派：金属精炼器会额外要求冷却液充足）。</summary>
        public static bool HasIngredients(ComplexFabricator fabricator, ComplexRecipe recipe, Storage storage)
        {
            return (bool)HasIngredientsMethod.Invoke(fabricator, new object[] { recipe, storage });
        }

        /// <summary>
        /// 旧存档的「无限」哨兵值。原版 <c>ComplexFabricator.OnSpawn</c> 会把数量 100 改写成
        /// <c>QUEUE_INFINITE</c>，这里按同一口径识别，避免导入时把无限订单读成 100 批。
        /// </summary>
        public const int LegacyInfiniteCount = 100;

        /// <summary>把原版配方计数字典里的值映射成本 Mod 订单口径。</summary>
        public static bool IsInfinite(int vanillaCount)
        {
            return vanillaCount == ComplexFabricator.QUEUE_INFINITE || vanillaCount == LegacyInfiniteCount;
        }
    }
}
