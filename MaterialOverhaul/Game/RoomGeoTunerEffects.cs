using System;
using System.Collections.Generic;
using MaterialOverhaul.Core;
using MaterialOverhaul.Rooms;

namespace MaterialOverhaul.Game
{
    /// <summary>
    /// 地质研究室：调谐产量新增量与每喷泉调谐上限。
    ///
    /// 新增产量作为一项独立的 <c>Geyser.GeyserModification</c> 与原版调谐同时增删，
    /// <c>originID</c> 不含 "GeoTuner"，因此不计入原版的调谐仪数量统计；原版 <c>modifications</c>
    /// 不序列化，读档后由调谐仪状态机重新施加原版修正，这里跟随 <c>ApplyTuning</c> 一并重建。
    /// 研究者科学值只在本次会话内记录（D14 工作口径），读档后按未翻倍处理，直到下一次调谐研究完成。
    /// </summary>
    public static class RoomGeoTunerEffects
    {
        private const string OriginPrefix = "MO_RoomLab[";

        // 调谐仪 smi → 最近一次完成调谐研究的复制人科学值。
        private static readonly Dictionary<int, float> ResearcherScience = new Dictionary<int, float>();

        public static void Clear()
        {
            ResearcherScience.Clear();
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

        private static string OriginOf(GeoTuner.Instance smi)
        {
            return OriginPrefix + smi.gameObject.GetInstanceID() + "]";
        }

        /// <summary>原版施加调谐后，若调谐仪位于地质研究室，追加独立的产量修正。</summary>
        public static void Apply(GeoTuner.Instance smi)
        {
            if (smi == null)
            {
                return;
            }

            Geyser geyser = smi.GetAssignedGeyser();
            if (geyser == null)
            {
                return;
            }

            Remove(smi, geyser);
            float bonus = RoomAdvancedEffects.GeoTunerMassBonus(RoomQuery.RoomTypeIdOf(smi.gameObject), ScienceOf(smi));
            if (bonus <= 0f)
            {
                return;
            }

            float massModifier = Geyser.massModificationMethod == Geyser.ModificationMethod.Percentages
                ? bonus
                : bonus * geyser.configuration.scaledRate;
            Geyser.GeyserModification modification = default(Geyser.GeyserModification);
            modification.originID = OriginOf(smi);
            modification.massPerCycleModifier = massModifier;
            geyser.AddModification(modification);
            ModLog.InfoOnce("room|geotuner|first", "地质研究室：调谐仪为喷泉追加喷发量修正 +"
                + (bonus * 100f).ToString("0") + "%。");
        }

        /// <summary>原版撤销调谐后，移除本调谐仪追加的产量修正。</summary>
        public static void Remove(GeoTuner.Instance smi)
        {
            if (smi == null)
            {
                return;
            }

            Remove(smi, smi.GetAssignedGeyser());
        }

        private static void Remove(GeoTuner.Instance smi, Geyser geyser)
        {
            if (geyser == null || geyser.modifications == null)
            {
                return;
            }

            string origin = OriginOf(smi);
            for (int i = geyser.modifications.Count - 1; i >= 0; i--)
            {
                if (string.Equals(geyser.modifications[i].originID, origin, StringComparison.Ordinal))
                {
                    // RemoveModification 会重算喷泉修正；按值匹配，取列表里的同一项。
                    geyser.RemoveModification(geyser.modifications[i]);
                }
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
