using System;
using System.Collections.Generic;
using MaterialOverhaul.Core;
using MaterialOverhaul.Rooms;

namespace MaterialOverhaul.Game
{
    /// <summary>
    /// 地质研究室：调谐产量加成与每喷泉调谐上限。
    ///
    /// 产量加成按房间加成「最后结算」（V1.0.2 #20、#22）：原版调谐等修正全部算完后，
    /// 每座喷泉只乘一次（1 + 加成），不按台数连乘。只要有一台位于地质研究室、且正在广播的调谐仪指向
    /// 该喷泉即生效；任一台的研究者科学值大于 20 时取翻倍值。乘数在
    /// <c>GeyserInstanceConfiguration.GetMassPerCycle</c> 后置中施加，不写入原版 <c>modifications</c>，
    /// 因此原版调谐计数与分项提示不受影响。
    ///
    /// 调谐上限（#14）：调谐仪所在房间变化后延迟复查其指向的喷泉，超出上限时按最近指定的顺序
    /// 取消多出的调谐仪目标，需要玩家重新选择。指定顺序只在本次会话内记录，读档后未知的按组件列表倒序处理。
    /// 研究者科学值同样只在本次会话内记录（D14 工作口径），读档后按未翻倍处理，直到下一次调谐研究完成。
    /// </summary>
    public static class RoomGeoTunerEffects
    {
        /// <summary>房间变化后延迟复查的秒数，避开拓扑重建时先清除、后新建房间的瞬间。</summary>
        private const float RecheckDelaySeconds = 2f;

        // 调谐仪 smi → 最近一次完成调谐研究的复制人科学值。
        private static readonly Dictionary<int, float> ResearcherScience = new Dictionary<int, float>();

        // 喷泉配置 → 地质研究室产量乘数（只存大于 1 的）。
        private static readonly Dictionary<GeyserConfigurator.GeyserInstanceConfiguration, float> GeyserFactors =
            new Dictionary<GeyserConfigurator.GeyserInstanceConfiguration, float>();

        // 调谐仪 → 指定目标的先后序号（越大越晚）。
        private static readonly Dictionary<int, int> AssignOrder = new Dictionary<int, int>();

        // 已排队复查的调谐仪，避免同一次房间重建重复排程。
        private static readonly HashSet<int> PendingChecks = new HashSet<int>();

        private static int nextOrder;

        public static void Clear()
        {
            ResearcherScience.Clear();
            GeyserFactors.Clear();
            AssignOrder.Clear();
            PendingChecks.Clear();
            nextOrder = 0;
        }

        public static void RememberResearcher(UnityEngine.Component tuner, float science)
        {
            if (tuner != null)
            {
                ResearcherScience[tuner.gameObject.GetInstanceID()] = science;
            }
        }

        private static float ScienceOf(GeoTuner.Instance smi)
        {
            if (smi == null || smi.gameObject == null)
            {
                return float.NaN;
            }

            return ResearcherScience.TryGetValue(smi.gameObject.GetInstanceID(), out float value) ? value : float.NaN;
        }

        /// <summary>原版施加或撤销调谐后，按当前广播中的调谐仪重算喷泉乘数。</summary>
        public static void Apply(GeoTuner.Instance smi)
        {
            if (smi != null)
            {
                RefreshGeyser(smi.GetAssignedGeyser());
            }
        }

        public static void Remove(GeoTuner.Instance smi)
        {
            Apply(smi);
        }

        /// <summary>喷发量乘数；没有地质研究室加成时为 1。</summary>
        public static float MassFactor(GeyserConfigurator.GeyserInstanceConfiguration configuration)
        {
            return configuration != null && GeyserFactors.TryGetValue(configuration, out float factor) ? factor : 1f;
        }

        /// <summary>喷泉当前的地质研究室产量加成比例（如 0.1）；没有时为 0。</summary>
        public static float GeyserBonus(Geyser geyser)
        {
            return geyser == null ? 0f : MassFactor(geyser.configuration) - 1f;
        }

        public static void RefreshGeyser(Geyser geyser)
        {
            if (geyser == null || geyser.configuration == null)
            {
                return;
            }

            float bonus = 0f;
            foreach (GeoTuner.Instance tuner in Components.GeoTuners.GetItems(geyser.GetMyWorldId()))
            {
                if (tuner == null || tuner.GetAssignedGeyser() != geyser || !IsBroadcasting(tuner, geyser))
                {
                    continue;
                }

                bonus = Math.Max(bonus, RoomAdvancedEffects.GeoTunerMassBonus(RoomQuery.RoomTypeIdOf(tuner.gameObject), ScienceOf(tuner)));
            }

            float before = MassFactor(geyser.configuration);
            float after = RoomAdvancedEffects.GeoTunerMassFactor(bonus);
            if (after > 1f)
            {
                GeyserFactors[geyser.configuration] = after;
            }
            else
            {
                GeyserFactors.Remove(geyser.configuration);
            }

            if (after != before)
            {
                // 原版只在修正变化时重写喷发参数；乘数变化同样需要立即写回喷口。
                geyser.ApplyConfigurationEmissionValues(geyser.configuration);
                ModLog.InfoOnce("room|geotuner|factor", "地质研究室：喷泉喷发量在原版调谐之后 ×"
                    + after.ToString("0.##") + "。");
            }

            RoomBonusStatus.RefreshGeyser(geyser);
        }

        private static bool IsBroadcasting(GeoTuner.Instance tuner, Geyser geyser)
        {
            if (geyser.modifications == null || string.IsNullOrEmpty(tuner.originID))
            {
                return false;
            }

            for (int i = 0; i < geyser.modifications.Count; i++)
            {
                if (string.Equals(geyser.modifications[i].originID, tuner.originID, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>调谐仪指定新目标时记录先后。</summary>
        public static void RememberAssignment(GeoTuner.Instance smi, Geyser geyser)
        {
            if (smi != null && smi.gameObject != null && geyser != null)
            {
                AssignOrder[smi.gameObject.GetInstanceID()] = ++nextOrder;
            }
        }

        /// <summary>调谐仪所在房间变化：延迟复查产量乘数与调谐上限。</summary>
        public static void OnRoomChanged(UnityEngine.GameObject go)
        {
            GeoTuner.Instance smi = go == null ? null : go.GetSMI<GeoTuner.Instance>();
            if (smi == null || GameScheduler.Instance == null || !PendingChecks.Add(go.GetInstanceID()))
            {
                return;
            }

            GameScheduler.Instance.Schedule("MaterialOverhaulGeoTunerRoomCheck", RecheckDelaySeconds, data =>
            {
                GeoTuner.Instance tuner = data as GeoTuner.Instance;
                try
                {
                    if (tuner != null && tuner.gameObject != null)
                    {
                        PendingChecks.Remove(tuner.gameObject.GetInstanceID());
                        RefreshGeyser(tuner.GetAssignedGeyser());
                        EnforceLimit(tuner.GetFutureGeyser(), tuner.GetMyWorldId());
                        if (tuner.GetAssignedGeyser() != tuner.GetFutureGeyser())
                        {
                            EnforceLimit(tuner.GetAssignedGeyser(), tuner.GetMyWorldId());
                        }
                    }
                }
                catch (Exception e)
                {
                    ModLog.Error("地质研究室调谐上限复查失败：" + e);
                }
            }, smi);
        }

        /// <summary>
        /// 指向该喷泉的调谐仪超过上限时，按指定先后取消最晚的几台。上限取各台调谐仪允许值中的最大者：
        /// 只要还有一台位于地质研究室，第 6 台就仍被允许。
        /// </summary>
        public static void EnforceLimit(Geyser geyser, int worldId)
        {
            if (geyser == null)
            {
                return;
            }

            List<GeoTuner.Instance> tuners = Components.GeoTuners.GetItems(worldId);
            List<int> pointing = new List<int>();
            List<int> orders = new List<int>();
            int limit = RoomAdvancedEffects.VanillaGeotunerLimit;
            for (int i = 0; i < tuners.Count; i++)
            {
                GeoTuner.Instance tuner = tuners[i];
                if (tuner == null || (tuner.GetFutureGeyser() != geyser && tuner.GetAssignedGeyser() != geyser))
                {
                    continue;
                }

                pointing.Add(i);
                orders.Add(AssignOrder.TryGetValue(tuner.gameObject.GetInstanceID(), out int order) ? order : 0);
                limit = Math.Max(limit, LimitFor(tuner));
            }

            int[] release = RoomAdvancedEffects.GeotunersToRelease(orders.ToArray(), limit);
            for (int i = 0; i < release.Length; i++)
            {
                GeoTuner.Instance tuner = tuners[pointing[release[i]]];
                tuner.AssignFutureGeyser(null);
                AssignOrder.Remove(tuner.gameObject.GetInstanceID());
                ModLog.Info("地质研究室：喷泉 " + geyser.name + " 的调谐仪超过上限 " + limit
                    + " 台，取消 " + tuner.gameObject.name + " 的目标，需要重新选择喷泉。");
            }
        }

        /// <summary>该调谐仪指向一座喷泉时的调谐仪总数上限。</summary>
        public static int LimitFor(GeoTuner.Instance smi)
        {
            if (smi == null)
            {
                return RoomAdvancedEffects.VanillaGeotunerLimit;
            }

            return RoomAdvancedEffects.GeotunerLimit(RoomQuery.RoomTypeIdOf(smi.gameObject), ScienceOf(smi));
        }
    }
}
