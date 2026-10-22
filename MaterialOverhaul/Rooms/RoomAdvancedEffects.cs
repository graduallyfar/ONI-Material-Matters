using System;

namespace MaterialOverhaul.Rooms
{
    /// <summary>
    /// 科研、航天、货运、医疗中心与宴会厅的房间效果参数与纯计算。
    ///
    /// 数值是 2026-10-21 指定的工作初值（平衡待验收），有效定义见 [[4. 房间系统设计]]，
    /// 接入点与口径见 <c>docs/开发计划/V3.0.0开发计划.md</c> §4.7（D08、D11、D12、D14、D15）与 §4.9。
    /// 这里不引用游戏类型，游戏接入层只读取结果。所有加成都是原版效果之外的「新增量」。
    /// </summary>
    public static class RoomAdvancedEffects
    {
        /// <summary>操作者科学（Learning 总值）严格大于该值时，房间新增量翻倍。</summary>
        public const float ScienceThreshold = 20f;

        public const float ScienceBonusFactor = 2f;

        /// <summary>任务控制室：指挥站助推期间新增的速度（基础速度的比例）。</summary>
        public const float MissionControlSpeedBonus = 0.2f;

        /// <summary>任务控制室：群星指挥站新增的可助推范围（格）。</summary>
        public const int MissionControlRangeBonus = 2;

        /// <summary>原版群星指挥站范围（目标程序集字面量，核实值）。</summary>
        public const int VanillaMissionControlRange = 2;

        /// <summary>地质研究室：广播期间目标喷泉的喷发量加成（0.1 = 原版调谐之后 ×1.1）。</summary>
        public const float GeoTunerLabMassBonus = 0.1f;

        /// <summary>地质研究室：每座喷泉新增可指向的调谐仪台数。</summary>
        public const int GeoTunerLabExtraTuners = 1;

        /// <summary>原版每座喷泉的调谐仪上限（目标程序集字面量，核实值）。</summary>
        public const int VanillaGeotunerLimit = 5;

        /// <summary>航天发射场：本次飞行新增的速度（基础速度的比例）。</summary>
        public const float SpaceportSpeedBonus = 0.1f;

        /// <summary>航天发射场：本次飞行每格航程的燃料与氧化剂系数。</summary>
        public const float SpaceportFuelFactor = 0.9f;

        /// <summary>星际货运站：载荷星图飞行速度系数。</summary>
        public const float CargoPayloadSpeedFactor = 1.2f;

        /// <summary>星际货运站：每轮冷却前新增的连续发射次数。</summary>
        public const int CargoExtraLaunches = 2;

        /// <summary>原版星际发射器每轮冷却前的发射次数（核实值）。</summary>
        public const int VanillaLaunchesPerCooldown = 6;

        /// <summary>星际货运站：冷却计时递减速度系数。</summary>
        public const float CargoCooldownSpeedFactor = 1.5f;

        /// <summary>宴会厅士气（原版 <c>RoomBanquetHall</c> 效果的 QualityOfLife 修正统一为此值）。</summary>
        public const float BanquetMorale = 6f;

        public const string BanquetEffectId = "RoomBanquetHall";

        /// <summary>致命辐射剂量（乘以难度系数，目标程序集 <c>RadiationMonitor.COMPARE_GTE_DEADLY</c> 核实值）。</summary>
        public const float RadiationDeadlyDose = 900f;

        /// <summary>科学是否达到翻倍条件（严格大于阈值；NaN 视为不满足）。</summary>
        public static bool ScienceDoubles(float science)
        {
            return !float.IsNaN(science) && science > ScienceThreshold;
        }

        /// <summary>按操作者科学缩放房间新增量。</summary>
        public static float ScaleBonus(float bonus, float science)
        {
            return ScienceDoubles(science) ? bonus * ScienceBonusFactor : bonus;
        }

        public static int ScaleBonus(int bonus, float science)
        {
            return ScienceDoubles(science) ? (int)Math.Round(bonus * ScienceBonusFactor) : bonus;
        }

        /// <summary>任务控制室指挥站完成助推时给火箭登记的新增速度比例；不在房间内返回 0。</summary>
        public static float MissionControlSpeed(string roomTypeId, float science)
        {
            return IsRoom(roomTypeId, RoomCatalogue.MissionControlId)
                ? ScaleBonus(MissionControlSpeedBonus, science)
                : 0f;
        }

        /// <summary>群星指挥站的有效范围；不在任务控制室时为原版范围。</summary>
        public static int MissionControlRange(string roomTypeId, float science)
        {
            return IsRoom(roomTypeId, RoomCatalogue.MissionControlId)
                ? VanillaMissionControlRange + ScaleBonus(MissionControlRangeBonus, science)
                : VanillaMissionControlRange;
        }

        /// <summary>地质调谐仪广播期间的新增喷发量修正；不在地质研究室返回 0。</summary>
        public static float GeoTunerMassBonus(string roomTypeId, float science)
        {
            return IsRoom(roomTypeId, RoomCatalogue.GeoTunerLabId)
                ? ScaleBonus(GeoTunerLabMassBonus, science)
                : 0f;
        }

        /// <summary>当前调谐仪指向一座喷泉时允许的调谐仪总数上限。</summary>
        public static int GeotunerLimit(string roomTypeId, float science)
        {
            return IsRoom(roomTypeId, RoomCatalogue.GeoTunerLabId)
                ? VanillaGeotunerLimit + ScaleBonus(GeoTunerLabExtraTuners, science)
                : VanillaGeotunerLimit;
        }

        /// <summary>是否允许再增加一台调谐仪指向某喷泉（含已指向或将指向的台数）。</summary>
        public static bool CanAddGeotuner(int currentCount, int limit)
        {
            return currentCount + 1 <= limit;
        }

        /// <summary>
        /// 地质研究室的喷发量乘数（V1.0.2）：原版调谐全部算完后乘以（1 + 加成），每座喷泉只乘一次。
        /// </summary>
        public static float GeoTunerMassFactor(float bonus)
        {
            return bonus > 0f ? 1f + bonus : 1f;
        }

        /// <summary>
        /// 调谐仪超过上限时需要取消目标的项（返回 <paramref name="assignOrders"/> 的下标）。
        /// 序号越大越晚指定，先取消最晚的；序号未知（0，读档前指定）的排在已知序号之前，
        /// 彼此之间按下标倒序。未超过上限时返回空数组。
        /// </summary>
        public static int[] GeotunersToRelease(int[] assignOrders, int limit)
        {
            int count = assignOrders == null ? 0 : assignOrders.Length;
            int excess = count - Math.Max(limit, 0);
            if (excess <= 0)
            {
                return new int[0];
            }

            int[] indices = new int[count];
            for (int i = 0; i < count; i++)
            {
                indices[i] = i;
            }

            Array.Sort(indices, (a, b) =>
            {
                int byOrder = assignOrders[b].CompareTo(assignOrders[a]);
                return byOrder != 0 ? byOrder : b.CompareTo(a);
            });

            int[] release = new int[excess];
            Array.Copy(indices, release, excess);
            return release;
        }

        /// <summary>
        /// 同一对象有多个房间加成时按连乘结算（V1.0.2 #22，先触发的在前）。返回合计的新增比例，
        /// 例如 +20% 与 +10% 得 0.32。
        /// </summary>
        public static float ChainBonus(float first, float second)
        {
            return (1f + Math.Max(first, 0f)) * (1f + Math.Max(second, 0f)) - 1f;
        }

        /// <summary>辐射后遗症的运动修正（施治者医疗低于该值时不减轻）。</summary>
        public const float RadiationScarMedicineStart = 12f;

        /// <summary>施治者医疗达到该值（含）时不留运动修正。</summary>
        public const float RadiationScarMedicineFull = 20f;

        /// <summary>未经减轻的辐射后遗症运动修正。</summary>
        public const int RadiationScarAthletics = -6;

        /// <summary>
        /// 辐射后遗症运动修正（V1.0.2 #17）：施治者医疗 12 及以下为 −6，20 及以上为 0，
        /// 其间线性减轻并四舍五入到整数（远离零取整）。没有施治者（NaN）按 −6。
        /// </summary>
        public static int RadiationScarPenalty(float medicine)
        {
            if (float.IsNaN(medicine) || medicine <= RadiationScarMedicineStart)
            {
                return RadiationScarAthletics;
            }

            if (medicine >= RadiationScarMedicineFull)
            {
                return 0;
            }

            float share = (RadiationScarMedicineFull - medicine) / (RadiationScarMedicineFull - RadiationScarMedicineStart);
            return (int)Math.Round(RadiationScarAthletics * share, MidpointRounding.AwayFromZero);
        }

        /// <summary>从该房间内的平台发射时，本次飞行是否获得航天发射场加成。</summary>
        public static bool IsSpaceport(string roomTypeId)
        {
            return IsRoom(roomTypeId, RoomCatalogue.SpaceportId);
        }

        /// <summary>星际物流发射器是否位于星际货运站。</summary>
        public static bool IsCargoStation(string roomTypeId)
        {
            return IsRoom(roomTypeId, RoomCatalogue.CargoStationId);
        }

        /// <summary>
        /// 燃料按系数结算时，原版「尝试航程」应缩放成的值：原版按 <c>距离 × 每格燃料</c> 扣燃料，
        /// 把尝试航程乘以系数即等价于每格燃料乘以系数，原版的燃料／氧化剂截断照常生效。
        /// </summary>
        public static float FuelAttempt(float attemptDistance, float fuelFactor)
        {
            return fuelFactor > 0f ? attemptDistance * fuelFactor : attemptDistance;
        }

        /// <summary>与 <see cref="FuelAttempt"/> 配套：把原版返回的「实际航程」换回真实航程。</summary>
        public static float FuelTravelled(float travelledDistance, float fuelFactor)
        {
            return fuelFactor > 0f ? travelledDistance / fuelFactor : travelledDistance;
        }

        /// <summary>
        /// 星际货运站连发规则：原版在本次发射后把计数 +1，达到 6 即进入冷却。
        /// 若本次发射前计数为 <c>原版次数 − 1</c> 且本轮回拨次数未用完，把计数回拨 1，
        /// 使本轮总发射次数增加 <see cref="CargoExtraLaunches"/>。返回回拨后的计数（不回拨则原值）。
        /// </summary>
        public static int CargoAdjustedCount(int countBeforeShot, int extraUsed, out bool consumed)
        {
            consumed = countBeforeShot == VanillaLaunchesPerCooldown - 1 && extraUsed < CargoExtraLaunches;
            return consumed ? countBeforeShot - 1 : countBeforeShot;
        }

        /// <summary>冷却期间每次模拟额外递减的计时（使冷却按 <see cref="CargoCooldownSpeedFactor"/> 倍速进行）。</summary>
        public static float CargoExtraCooldown(float dt)
        {
            return dt > 0f ? dt * (CargoCooldownSpeedFactor - 1f) : 0f;
        }

        /// <summary>辐射剂量是否达到致命阈值（含）。</summary>
        public static bool IsDeadlyRadiation(float dose, float difficultyMultiplier)
        {
            float multiplier = difficultyMultiplier > 0f ? difficultyMultiplier : 1f;
            return !float.IsNaN(dose) && dose >= RadiationDeadlyDose * multiplier;
        }

        /// <summary>医疗中心是否提供辐射拯救者与优先入院。</summary>
        public static bool IsMedicalCenter(string roomTypeId)
        {
            return IsRoom(roomTypeId, RoomCatalogue.MedicalCenterId);
        }

        private static bool IsRoom(string roomTypeId, string expected)
        {
            return string.Equals(roomTypeId, expected, StringComparison.Ordinal);
        }
    }
}
