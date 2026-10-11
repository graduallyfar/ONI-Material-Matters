using System.Collections.Generic;
using KSerialization;
using MaterialOverhaul.Core;
using MaterialOverhaul.Hardness;
using MaterialOverhaul.Orders;
using MaterialOverhaul.Zones;

namespace MaterialOverhaul.Game
{
    /// <summary>当前配方的生产许可与温度效率评估结果。</summary>
    public struct ProductionGate
    {
        /// <summary>是否允许推进生产。低于最低可工作温度或高于最高可工作温度时为 false。</summary>
        public bool AllowsProduction;

        /// <summary>温度效率 η_T。未配置温区时恒为 1。</summary>
        public float Efficiency;

        public TempState State;

        public TempZone Zone;

        /// <summary>该配方是否在温区侧表中匹配到条目；false 表示已退化为原版行为。</summary>
        public bool ZoneConfigured;
    }

    /// <summary>
    /// 建筑级组件：温区判断、加热判断与硬度磨损累计的游戏侧载体
    /// （V0.0.1 开发计划 §3.5：判断机制属于建筑级组件，所有启用建筑共用同一份实现）。
    ///
    /// 组件只挂在白名单建筑上（见 <see cref="BuildingWhitelist"/>），其余建筑行为等同原版。
    /// 判断函数本体在 Zones/Heating/Hardness 的判断层里，本类只负责：
    ///   - 读取建筑本体温度与主建造材料硬度；
    ///   - 按原版配方标识查侧表；
    ///   - 保存本 Mod 自有数据（§8.1）。
    ///
    /// 本版不接管原版产热、过热损伤、批次结算、取料与运行状态判断（§1.3 原则 1）。
    /// </summary>
    [SerializationConfig(MemberSerialization.OptIn)]
    public class MaterialOverhaulFab : KMonoBehaviour
    {
        /// <summary>
        /// 磨损速率**占位值**。磨损到生命值伤害的转换系数属待定项（§6.4、§10 第 10 项），
        /// 本版只累计自有字段、不扣血，因此该占位值不影响玩法平衡，数值阶段确定后替换。
        /// </summary>
        private const float WearPerSecondPlaceholder = 1f;

        private ComplexFabricator fabricator;
        private PrimaryElement primaryElement;

        private readonly OrderQueue orders = new OrderQueue();
        private readonly WearAccumulator wear = new WearAccumulator();

        private string cachedRecipeId;
        private TempZone cachedZone = TempZone.Disabled;
        private bool cachedZoneConfigured;

        private TempState lastLoggedState = TempState.Disabled;
        private bool hasLoggedState;

        /// <summary>本建筑的 prefab id，作为侧表索引。</summary>
        public string PrefabId { get; private set; }

        // ---- 本 Mod 自有数据，随存档落盘（§8.1）。字段在每次变化时同步，无需保存钩子。----
        [Serialize] private string[] orderRecipeIds = new string[0];
        [Serialize] private int[] orderRemaining = new int[0];
        [Serialize] private int[] orderQuotas = new int[0];
        [Serialize] private int[] orderRoundDone = new int[0];
        [Serialize] private int orderCursor;
        [Serialize] private float wearAccumulated;
        [Serialize] private string lockedRecipeId = string.Empty;

        public OrderQueue Orders
        {
            get { return orders; }
        }

        public float Wear
        {
            get { return wear.Wear; }
        }

        public TempState CurrentState { get; private set; }

        public float CurrentEfficiency { get; private set; }

        /// <summary>当前正在生产的配方；没有生产中的批次时为 null。</summary>
        public ComplexRecipe CurrentWorkingRecipe
        {
            get { return fabricator != null ? fabricator.CurrentWorkingOrder : null; }
        }

        protected override void OnSpawn()
        {
            base.OnSpawn();
            fabricator = GetComponent<ComplexFabricator>();
            primaryElement = GetComponent<PrimaryElement>();

            KPrefabID prefabIdComponent = GetComponent<KPrefabID>();
            PrefabId = prefabIdComponent != null ? prefabIdComponent.PrefabTag.ToString() : gameObject.name;

            CurrentEfficiency = 1f;
            CurrentState = TempState.Disabled;

            wear.Restore(wearAccumulated);
            RestoreOrders();
        }

        /// <summary>建筑本体温度。不使用周围气体温度代替（§3.1）。</summary>
        public float BodyTemperature
        {
            get { return primaryElement != null ? primaryElement.Temperature : 0f; }
        }

        /// <summary>
        /// 评估该配方的生产许可与温度效率。配方标识变化时才重新查侧表，避免每次调用查表与刷日志。
        /// </summary>
        public ProductionGate EvaluateFor(ComplexRecipe recipe)
        {
            if (recipe == null)
            {
                ProductionGate passthrough = default(ProductionGate);
                passthrough.AllowsProduction = true;
                passthrough.Efficiency = 1f;
                passthrough.State = TempState.Disabled;
                passthrough.Zone = TempZone.Disabled;
                passthrough.ZoneConfigured = false;
                return passthrough;
            }

            if (recipe.id != cachedRecipeId)
            {
                cachedRecipeId = recipe.id;
                cachedZoneConfigured = RecipeZoneTable.TryGet(cachedRecipeId, out cachedZone);
                if (!cachedZoneConfigured)
                {
                    cachedZone = TempZone.Disabled;
                    ModLog.WarnOnce("zone|missing|" + cachedRecipeId,
                        "配方温区：未匹配到配方 '" + cachedRecipeId + "'（建筑 " + PrefabId + "），该配方退化为原版行为。");
                }
            }

            float temperature = BodyTemperature;
            TempState state = TempZoneEvaluator.Evaluate(temperature, cachedZone);
            float efficiency = TempZoneEvaluator.Efficiency(state, temperature, cachedZone);

            CurrentState = state;
            CurrentEfficiency = efficiency;

            ProductionGate gate = default(ProductionGate);
            gate.AllowsProduction = state.AllowsProduction();
            gate.Efficiency = efficiency;
            gate.State = state;
            gate.Zone = cachedZone;
            gate.ZoneConfigured = cachedZoneConfigured;
            return gate;
        }

        /// <summary>
        /// 按实际生产时间累计机械磨损。只有实际生产推进时调用
        /// （预热、待机、暂停、维修、损坏停机都不调用，§6.3）。
        /// </summary>
        public void AccumulateProduction(float seconds)
        {
            if (seconds <= 0f || !HasHardnessDeficit())
            {
                return;
            }

            wear.AccumulateProduction(seconds, WearPerSecondPlaceholder);
            wearAccumulated = wear.Wear;
        }

        /// <summary>
        /// 主建造材料硬度是否低于要求硬度。
        /// 实际硬度取主建造材料（原版索引 0 的材料写入 PrimaryElement.ElementID），辅料不参与（§6.1）。
        /// 未列入要求硬度表的建筑不产生机械磨损（§6.2）。
        /// </summary>
        public bool HasHardnessDeficit()
        {
            if (primaryElement == null || primaryElement.Element == null)
            {
                return false;
            }

            if (!HardnessRequirementTable.TryGet(PrefabId, out byte required))
            {
                return false;
            }

            return primaryElement.Element.hardness < required;
        }

        /// <summary>由模拟更新驱动刷新（补丁挂在 ComplexFabricator.Sim200ms 上，不自行注册调度）。</summary>
        public void RefreshStatus()
        {
            ComplexRecipe working = CurrentWorkingRecipe;
            if (working == null)
            {
                cachedRecipeId = null;
                cachedZone = TempZone.Disabled;
                cachedZoneConfigured = false;
                CurrentState = TempState.Disabled;
                CurrentEfficiency = 1f;
                hasLoggedState = false;
                return;
            }

            ProductionGate gate = EvaluateFor(working);
            LogStateIfChanged(working, gate);
        }

        private void LogStateIfChanged(ComplexRecipe recipe, ProductionGate gate)
        {
            if (hasLoggedState && gate.State == lastLoggedState)
            {
                return;
            }

            hasLoggedState = true;
            lastLoggedState = gate.State;

            if (!gate.ZoneConfigured)
            {
                ModLog.Info(PrefabId + "：" + recipe.id + " 未配置温区，温度 " + BodyTemperature.ToString("F1")
                    + " K，按原版行为运行。");
                return;
            }

            ModLog.Info(PrefabId + "：" + recipe.id + " 状态 " + gate.State
                + "，本体温度 " + BodyTemperature.ToString("F1") + " K，效率 " + gate.Efficiency.ToString("F3")
                + "，允许生产 " + gate.AllowsProduction + "。");
        }

        // ---- 订单顺序（本版最小部分：有序列表、轮换指针、锁定批次，§7.3）----

        /// <summary>记录一笔新添加的订单。同一配方重复添加视为新订单，不合并。</summary>
        public void NoteRecipeQueued(string recipeId)
        {
            if (string.IsNullOrEmpty(recipeId))
            {
                return;
            }

            foreach (OrderEntry entry in orders.Orders)
            {
                if (entry.RecipeId == recipeId)
                {
                    return;
                }
            }

            orders.Add(OrderEntry.Limited(recipeId, 1));
            SyncOrderFields();
        }

        /// <summary>原版队列中该配方数量归零时，从本 Mod 订单顺序中移除。</summary>
        public void NoteRecipeCleared(string recipeId)
        {
            if (string.IsNullOrEmpty(recipeId))
            {
                return;
            }

            for (int i = 0; i < orders.Count; i++)
            {
                if (orders.Orders[i].RecipeId == recipeId)
                {
                    orders.RemoveAt(i);
                    SyncOrderFields();
                    return;
                }
            }
        }

        private void SyncOrderFields()
        {
            int count = orders.Count;
            orderRecipeIds = new string[count];
            orderRemaining = new int[count];
            orderQuotas = new int[count];
            orderRoundDone = new int[count];

            for (int i = 0; i < count; i++)
            {
                OrderEntry entry = orders.Orders[i];
                orderRecipeIds[i] = entry.RecipeId;
                orderRemaining[i] = entry.Remaining;
                orderQuotas[i] = entry.IsInfinite ? entry.BatchQuota : 0;
                orderRoundDone[i] = entry.BatchesDoneThisRound;
            }

            orderCursor = orders.Cursor;
            OrderEntry locked = orders.LockedBatch;
            lockedRecipeId = locked != null ? locked.RecipeId : string.Empty;
        }

        private void RestoreOrders()
        {
            List<OrderEntry> entries = new List<OrderEntry>();
            int count = orderRecipeIds != null ? orderRecipeIds.Length : 0;

            for (int i = 0; i < count; i++)
            {
                string recipeId = orderRecipeIds[i];
                if (string.IsNullOrEmpty(recipeId))
                {
                    continue;
                }

                int quota = ArrayValue(orderQuotas, i, 0);
                OrderEntry entry = quota > 0
                    ? OrderEntry.Infinite(recipeId, quota)
                    : OrderEntry.Limited(recipeId, ArrayValue(orderRemaining, i, 1));
                entry.BatchesDoneThisRound = ArrayValue(orderRoundDone, i, 0);
                entries.Add(entry);
            }

            orders.Restore(entries, orderCursor);

            if (entries.Count > 0)
            {
                // 读档只恢复顺序与轮换位置；锁定批次需要按实际温度与供电重新校验（§8.3）。
                ModLog.Info("读档恢复订单：" + PrefabId + " 共 " + entries.Count + " 项，轮换位置 " + orders.Cursor + "。");
            }

            lockedRecipeId = string.Empty;
        }

        private static int ArrayValue(int[] array, int index, int fallback)
        {
            if (array == null || index < 0 || index >= array.Length)
            {
                return fallback;
            }

            return array[index];
        }
    }
}
