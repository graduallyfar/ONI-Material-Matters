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
    /// 剩余队列数、原料判断、操作任务（chore）与批次进度。访问点集中在这里，
    /// 字段名或方法名一旦变化，**静态初始化即失败**（可定位失败，不是静默失效；
    /// 见 V0.0.1 开发计划 §11、V0.0.3 开发计划 §3.2）。
    ///
    /// 只读判断尽量调用原版方法本身，避免复制一份原版逻辑后产生偏差：
    ///   - <c>GetRemainingQueueCount</c> 是私有非虚方法，用开放委托直接调用；
    ///   - <c>HasIngredients</c> 是受保护**虚**方法（LiquidCooledRefinery 覆写以要求冷却液），
    ///     因此用反射调用以保证虚分派，不能绑成基类委托。
    ///
    /// 温度暂停使用的私有动作（已核实实现）：
    ///   - <c>CancelChore()</c>：取消操作任务并释放复制人，**不动批次与进度**；
    ///   - <c>UpdateChore()</c>：在「总体可运行且有当前批次」时重新建立操作任务；
    ///   - <c>CancelWorkingOrder()</c>：把加工仓原料退回输入仓并清零本批进度（取消批次用）；
    ///   - <c>StartWorkingOrder(int)</c>：唯一的「开始一批」入口，转移原料并建立预取状态。
    /// </summary>
    internal static class FabricatorAccess
    {
        internal static readonly AccessTools.FieldRef<ComplexFabricator, int> NextOrderIdx =
            AccessTools.FieldRefAccess<ComplexFabricator, int>("nextOrderIdx");

        internal static readonly AccessTools.FieldRef<ComplexFabricator, bool> NextOrderIsWorkable =
            AccessTools.FieldRefAccess<ComplexFabricator, bool>("nextOrderIsWorkable");

        internal static readonly AccessTools.FieldRef<ComplexFabricator, Dictionary<string, int>> QueueCounts =
            AccessTools.FieldRefAccess<ComplexFabricator, Dictionary<string, int>>("recipeQueueCounts");

        /// <summary>当前正在生产的配方标识（原版序列化字段，用于读档批次识别）。</summary>
        internal static readonly AccessTools.FieldRef<ComplexFabricator, string> LastWorkingRecipe =
            AccessTools.FieldRefAccess<ComplexFabricator, string>("lastWorkingRecipe");

        /// <summary>当前操作任务；为 null 表示没有任务（被温度暂停取消时也是 null）。</summary>
        internal static readonly AccessTools.FieldRef<ComplexFabricator, Chore> ChoreField =
            AccessTools.FieldRefAccess<ComplexFabricator, Chore>("chore");

        /// <summary>当前工作订单下标；-1 表示没有已锁定批次。</summary>
        internal static readonly AccessTools.FieldRef<ComplexFabricator, int> WorkingOrderIdx =
            AccessTools.FieldRefAccess<ComplexFabricator, int>("workingOrderIdx");

        /// <summary>预取额度数组：每种配方当前允许预取几批。</summary>
        internal static readonly AccessTools.FieldRef<ComplexFabricator, List<int>> OpenOrderCounts =
            AccessTools.FieldRefAccess<ComplexFabricator, List<int>>("openOrderCounts");

        private static readonly MethodInfo GetRemainingQueueCountMethod =
            AccessTools.Method(typeof(ComplexFabricator), "GetRemainingQueueCount");

        private static readonly MethodInfo HasIngredientsMethod =
            AccessTools.Method(typeof(ComplexFabricator), "HasIngredients");

        private static readonly Func<ComplexFabricator, ComplexRecipe, int> GetRemainingQueueCountDelegate =
            AccessTools.MethodDelegate<Func<ComplexFabricator, ComplexRecipe, int>>(GetRemainingQueueCountMethod);

        private static readonly Action<ComplexFabricator> CancelChoreDelegate =
            AccessTools.MethodDelegate<Action<ComplexFabricator>>(AccessTools.Method(typeof(ComplexFabricator), "CancelChore"));

        private static readonly Action<ComplexFabricator> UpdateChoreDelegate =
            AccessTools.MethodDelegate<Action<ComplexFabricator>>(AccessTools.Method(typeof(ComplexFabricator), "UpdateChore"));

        private static readonly Action<ComplexFabricator> RefreshQueueDelegate =
            AccessTools.MethodDelegate<Action<ComplexFabricator>>(AccessTools.Method(typeof(ComplexFabricator), "RefreshQueue"));

        private static readonly Action<ComplexFabricator> CancelWorkingOrderDelegate =
            AccessTools.MethodDelegate<Action<ComplexFabricator>>(AccessTools.Method(typeof(ComplexFabricator), "CancelWorkingOrder"));

        private static readonly Action<ComplexFabricator, int> StartWorkingOrderDelegate =
            AccessTools.MethodDelegate<Action<ComplexFabricator, int>>(AccessTools.Method(typeof(ComplexFabricator), "StartWorkingOrder"));

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

        /// <summary>是否有已锁定批次。</summary>
        public static bool HasWorkingOrder(ComplexFabricator fabricator)
        {
            return fabricator != null && WorkingOrderIdx(fabricator) > -1;
        }

        /// <summary>取消操作任务并释放复制人；批次、原料与进度都保留。</summary>
        public static void CancelChore(ComplexFabricator fabricator)
        {
            CancelChoreDelegate(fabricator);
        }

        /// <summary>在总体可运行且有当前批次时重新建立操作任务（温度恢复时用）。</summary>
        public static void UpdateChore(ComplexFabricator fabricator)
        {
            UpdateChoreDelegate(fabricator);
        }

        /// <summary>取消当前批次：加工仓原料退回输入仓，本批进度清零。</summary>
        public static void CancelWorkingOrder(ComplexFabricator fabricator)
        {
            CancelWorkingOrderDelegate(fabricator);
        }

        /// <summary>开始一批（唯一的开始入口）：转移原料、扣预取额度、建立操作任务。</summary>
        public static void StartWorkingOrder(ComplexFabricator fabricator, int recipeIndex)
        {
            StartWorkingOrderDelegate(fabricator, recipeIndex);
        }

        /// <summary>重算队列：校验当前批次、刷新预取额度、重算材料需求。</summary>
        public static void RefreshQueue(ComplexFabricator fabricator)
        {
            RefreshQueueDelegate(fabricator);
        }

        /// <summary>该配方当前是否已有可预取额度（开始一批的前提）。</summary>
        public static bool HasPrefetchQuota(ComplexFabricator fabricator, int recipeIndex)
        {
            List<int> counts = OpenOrderCounts(fabricator);
            return counts != null && recipeIndex >= 0 && recipeIndex < counts.Count && counts[recipeIndex] > 0;
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
