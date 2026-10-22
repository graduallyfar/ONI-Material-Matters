using System;
using System.Collections.Generic;
using MaterialOverhaul.Core;
using MaterialOverhaul.Rooms;

namespace MaterialOverhaul.Game
{
    /// <summary>
    /// 任务控制室、航天发射场与星际货运站效果的运行期状态。
    ///
    /// 记录哪艘火箭带指挥站新增速度、哪次飞行受航天发射场加成、哪个载荷来自星际货运站，以及
    /// 发射器本轮已用的额外连发次数。航天发射场的飞行标记保存在火箭的 <see cref="RoomSpaceportFlight"/>
    /// 组件里，随存档保留到落地；其余条目是派生状态，只在本次游戏会话内保存（D14／D15 工作口径）：
    /// 读档后在途的加成丢失，直到下一次操作、发射完成。条目在加成结束、火箭落地或对象失效时清除，
    /// 不在模拟更新中遍历全部火箭。
    /// </summary>
    public static class RoomSpaceEffects
    {
        // 本体版：火箭（Spacecraft 普通对象）→ 指挥站新增推进比例。
        private static readonly Dictionary<Spacecraft, float> SpacecraftBoost = new Dictionary<Spacecraft, float>();

        // 群星版：按 Unity 实例 id 记录，避免持有已销毁对象。
        private static readonly Dictionary<int, float> ClustercraftBoost = new Dictionary<int, float>();

        // 群星指挥站：实例 id → 最近一次完成操作者的科学值（决定扩展范围）。
        private static readonly Dictionary<int, float> StationScience = new Dictionary<int, float>();

        // 星际货运站：载荷实例 id；发射器实例 id → 本轮已用的额外连发次数。
        private static readonly HashSet<int> CargoPayloads = new HashSet<int>();
        private static readonly Dictionary<int, int> CargoExtraUsed = new Dictionary<int, int>();

        [ThreadStatic]
        private static int launchingRailGunRoomFlag;

        [ThreadStatic]
        private static int extendedRangeContext;

        /// <summary>读档或回到主菜单时清空派生状态。</summary>
        public static void Clear()
        {
            SpacecraftBoost.Clear();
            ClustercraftBoost.Clear();
            StationScience.Clear();
            CargoPayloads.Clear();
            CargoExtraUsed.Clear();
        }

        // ---------- 任务控制室 ----------

        public static void RegisterSpacecraftBoost(Spacecraft spacecraft, float bonus)
        {
            if (spacecraft == null)
            {
                return;
            }

            if (bonus > 0f)
            {
                SpacecraftBoost[spacecraft] = bonus;
            }
            else
            {
                SpacecraftBoost.Remove(spacecraft);
            }
        }

        /// <summary>本体版任务推进的额外增量；助推结束时同时清除登记。</summary>
        public static float ExtraMissionProgress(Spacecraft spacecraft, float dt)
        {
            if (spacecraft == null || !SpacecraftBoost.TryGetValue(spacecraft, out float bonus))
            {
                return 0f;
            }

            if (spacecraft.state != Spacecraft.MissionState.Underway || spacecraft.controlStationBuffTimeRemaining <= 0f)
            {
                SpacecraftBoost.Remove(spacecraft);
                return 0f;
            }

            return dt * bonus;
        }

        public static void RegisterClustercraftBoost(Clustercraft craft, float bonus)
        {
            if (craft == null)
            {
                return;
            }

            int id = craft.GetInstanceID();
            if (bonus > 0f)
            {
                ClustercraftBoost[id] = bonus;
            }
            else
            {
                ClustercraftBoost.Remove(id);
            }

            RoomBonusStatus.RefreshRocket(craft);
        }

        /// <summary>火箭当前仍有效的任务控制室新增速度；没有或已到期为 0。</summary>
        public static float MissionControlBoostOf(Clustercraft craft)
        {
            return craft != null && craft.controlStationBuffTimeRemaining > 0f
                && ClustercraftBoost.TryGetValue(craft.GetInstanceID(), out float boost) ? boost : 0f;
        }

        public static bool IsFromSpaceport(Clustercraft craft)
        {
            return IsSpaceportFlight(craft);
        }

        /// <summary>
        /// 群星火箭当前的房间新增速度比例（基础速度的比例）。任务控制室助推与航天发射场同时生效时连乘
        /// （V1.0.2 #22）：+20% 与 +10% 得 +32%。只作用于基础速度，不受驾驶员与驾驶方式倍率放大。
        /// </summary>
        public static float ClustercraftSpeedBonus(Clustercraft craft)
        {
            if (craft == null)
            {
                return 0f;
            }

            int id = craft.GetInstanceID();
            float missionControl = 0f;
            if (ClustercraftBoost.TryGetValue(id, out float boost))
            {
                if (craft.controlStationBuffTimeRemaining > 0f)
                {
                    missionControl = boost;
                }
                else
                {
                    ClustercraftBoost.Remove(id);
                    // 本方法也被状态文字回调调用，不能在显示途中增删状态项，推迟到下一帧刷新。
                    if (GameScheduler.Instance != null)
                    {
                        GameScheduler.Instance.Schedule("MaterialOverhaulRocketRoomBonus", 0f,
                            data => RoomBonusStatus.RefreshRocket(data as Clustercraft), craft);
                    }
                }
            }

            float spaceport = IsSpaceportFlight(craft) ? RoomAdvancedEffects.SpaceportSpeedBonus : 0f;
            return RoomAdvancedEffects.ChainBonus(missionControl, spaceport);
        }

        public static void RememberStationScience(UnityEngine.Component station, float science)
        {
            if (station != null)
            {
                StationScience[station.GetInstanceID()] = science;
            }
        }

        /// <summary>群星指挥站当前有效范围（按房间与最近一次完成操作者的科学值）。</summary>
        public static int StationRange(UnityEngine.Component station)
        {
            if (station == null)
            {
                return RoomAdvancedEffects.VanillaMissionControlRange;
            }

            float science = StationScience.TryGetValue(station.GetInstanceID(), out float value) ? value : float.NaN;
            return RoomAdvancedEffects.MissionControlRange(RoomQuery.RoomTypeIdOf(station), science);
        }

        /// <summary>指挥站工作期间的范围判定上下文；0 表示未设置（按原版）。</summary>
        public static int ExtendedRangeContext
        {
            get { return extendedRangeContext; }
            set { extendedRangeContext = value; }
        }

        // ---------- 航天发射场 ----------

        /// <summary>
        /// 起飞时按平台所在房间写入飞行标记；平台不在航天发射场时清除旧标记。
        /// 由 <c>CraftModuleInterface.DoLaunch</c> 前置补丁调用，早于原版扣除发射那一格燃料。
        /// </summary>
        public static void BeginFlight(Clustercraft craft, LaunchPad pad)
        {
            RoomSpaceportFlight flight = FlightOf(craft);
            if (flight == null)
            {
                if (craft != null)
                {
                    ModLog.InfoOnce("room|spaceport|missing", "航天发射场：火箭 " + craft.Name
                        + " 缺少飞行标记组件，本次飞行不获得发射场加成。");
                }

                return;
            }

            bool fromSpaceport = pad != null && RoomAdvancedEffects.IsSpaceport(RoomQuery.RoomTypeIdOf(pad));
            flight.FromSpaceport = fromSpaceport;
            RoomBonusStatus.RefreshRocket(craft);
            if (fromSpaceport)
            {
                ModLog.InfoOnce("room|spaceport|first", "航天发射场：火箭 " + craft.Name + " 从发射场平台发射，本次飞行速度 +"
                    + (RoomAdvancedEffects.SpaceportSpeedBonus * 100f).ToString("0") + "%、燃料 ×"
                    + RoomAdvancedEffects.SpaceportFuelFactor.ToString("0.##") + "，随存档保留到落地。");
            }
        }

        public static void EndFlight(Clustercraft craft)
        {
            RoomSpaceportFlight flight = FlightOf(craft);
            if (flight != null)
            {
                flight.FromSpaceport = false;
            }

            RoomBonusStatus.RefreshRocket(craft);
        }

        /// <summary>
        /// 单位距离燃料系数；无加成时为 1。飞行中按存档的飞行标记；停在平台上（<c>Grounded</c>）时按
        /// 平台当前所在房间预判，使起飞前的航程显示与目的地可达判定和起飞后的实际扣除一致。
        /// 速度加成不做此预判，仍只在飞行中生效。
        /// </summary>
        public static float FuelFactor(Clustercraft craft)
        {
            return IsSpaceportFlight(craft) || IsGroundedOnSpaceport(craft) ? RoomAdvancedEffects.SpaceportFuelFactor : 1f;
        }

        /// <summary>按火箭模块接口查询燃料系数（航程与每格耗量显示使用）。</summary>
        public static float FuelFactor(CraftModuleInterface craftInterface)
        {
            return craftInterface == null ? 1f : FuelFactor(craftInterface.GetComponent<Clustercraft>());
        }

        private static bool IsGroundedOnSpaceport(Clustercraft craft)
        {
            if (craft == null || craft.Status != Clustercraft.CraftStatus.Grounded || craft.ModuleInterface == null)
            {
                return false;
            }

            LaunchPad pad = craft.ModuleInterface.CurrentPad;
            return pad != null && RoomAdvancedEffects.IsSpaceport(RoomQuery.RoomTypeIdOf(pad));
        }

        private static bool IsSpaceportFlight(Clustercraft craft)
        {
            RoomSpaceportFlight flight = FlightOf(craft);
            return flight != null && flight.FromSpaceport;
        }

        private static RoomSpaceportFlight FlightOf(Clustercraft craft)
        {
            return craft == null ? null : craft.GetComponent<RoomSpaceportFlight>();
        }

        // ---------- 星际货运站 ----------

        /// <summary>发射器 <c>LaunchProjectile</c> 调用期间是否位于星际货运站。</summary>
        public static bool LaunchingFromCargoStation
        {
            get { return launchingRailGunRoomFlag > 0; }
            set { launchingRailGunRoomFlag = value ? 1 : 0; }
        }

        public static void MarkCargoPayload(UnityEngine.Component payload)
        {
            if (payload != null)
            {
                CargoPayloads.Add(payload.gameObject.GetInstanceID());
            }
        }

        public static void ForgetCargoPayload(UnityEngine.Component payload)
        {
            if (payload != null)
            {
                CargoPayloads.Remove(payload.gameObject.GetInstanceID());
            }
        }

        public static bool IsCargoPayload(UnityEngine.Component payload)
        {
            return payload != null && CargoPayloads.Contains(payload.gameObject.GetInstanceID());
        }

        /// <summary>
        /// 发射一次后按连发规则回拨计数；返回应写回的计数，不需要修改时返回 -1。
        /// 计数为 0（新一轮）时重置本轮已用次数。
        /// </summary>
        public static int CargoCountAfterLaunch(UnityEngine.Component railGun, int countBeforeIncrement)
        {
            if (railGun == null)
            {
                return -1;
            }

            int id = railGun.GetInstanceID();
            if (countBeforeIncrement <= 0)
            {
                CargoExtraUsed.Remove(id);
            }

            int used = CargoExtraUsed.TryGetValue(id, out int value) ? value : 0;
            int adjusted = RoomAdvancedEffects.CargoAdjustedCount(countBeforeIncrement, used, out bool consumed);
            if (!consumed)
            {
                return -1;
            }

            CargoExtraUsed[id] = used + 1;
            return adjusted;
        }

        public static void ForgetRailGun(UnityEngine.Component railGun)
        {
            if (railGun != null)
            {
                CargoExtraUsed.Remove(railGun.GetInstanceID());
            }
        }
    }
}
