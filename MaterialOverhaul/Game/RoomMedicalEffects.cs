using System.Collections.Generic;
using Klei.AI;
using MaterialOverhaul.Core;
using MaterialOverhaul.Rooms;
using UnityEngine;

namespace MaterialOverhaul.Game
{
    /// <summary>
    /// 医疗中心：优先入院（D11）与辐射拯救者（D12）。
    ///
    /// 优先入院只改变「自动分配诊疗床」时的候选顺序：先选医疗中心内未分配、可自动分配且可到达的
    /// 最近诊疗床，没有时交回原版。已分配的诊疗床不改动，不限制复制人行动（不禁足）。
    /// 辐射拯救者在患者开始治疗时一次性结算，先留下最重的永久后遗症（运动 −6），只有一份；
    /// 同一次住院中医生完成诊疗时，按施治者医疗减轻为对应档（V1.0.2 #17），只减轻不加重。
    /// 「本次住院获救」只在本次会话内记录，读档后不再减轻。没有医生施治时保持 −6。
    /// </summary>
    public static class RoomMedicalEffects
    {
        // 本次住院中获救、尚待医生减轻后遗症的患者。
        private static readonly HashSet<int> RescuedThisStay = new HashSet<int>();

        public static void Clear()
        {
            RescuedThisStay.Clear();
        }
        /// <summary>
        /// 为诊疗床槽位优先分配医疗中心内的床位；成功时返回已分配的床位，否则返回 null 交回原版。
        /// </summary>
        public static Assignable TryAssignMedicalCenterCot(Assignables owner, AssignableSlot slot)
        {
            if (owner == null || slot == null || slot != Db.Get().AssignableSlots.Clinic)
            {
                return null;
            }

            if (owner.GetAssignable(slot) != null)
            {
                return null;
            }

            MinionAssignablesProxy proxy = owner.GetComponent<MinionAssignablesProxy>();
            GameObject target = proxy == null ? null : proxy.GetTargetGameObject();
            Navigator navigator = target == null ? null : target.GetComponent<Navigator>();
            global::Game game = global::Game.Instance;
            if (navigator == null || game == null || game.assignmentManager == null)
            {
                return null;
            }

            // 与原版受保护的 Assignables.GetAssignableIdentity 相同：复制人本体取其代理，代理本身即身份。
            MinionIdentity minion = owner.GetComponent<MinionIdentity>();
            IAssignableIdentity identity = minion != null && minion.assignableProxy != null
                ? (IAssignableIdentity)minion.assignableProxy.Get()
                : proxy;
            if (identity == null)
            {
                return null;
            }
            Assignable best = null;
            int bestCost = int.MaxValue;
            foreach (Assignable item in game.assignmentManager)
            {
                if (item == null || item.IsAssigned() || item.slot != slot || !item.CanAutoAssignTo(identity))
                {
                    continue;
                }

                if (!RoomAdvancedEffects.IsMedicalCenter(RoomQuery.RoomTypeIdOf(item)))
                {
                    continue;
                }

                int cost = item.GetNavigationCost(navigator);
                if (cost != -1 && cost < bestCost)
                {
                    bestCost = cost;
                    best = item;
                }
            }

            if (best != null)
            {
                best.Assign(identity);
                ModLog.InfoOnce("room|medical|admission", "医疗中心：优先为患者分配医疗中心内的诊疗床。");
            }

            return best;
        }

        /// <summary>患者在医疗中心诊疗床开始治疗时，致命辐射剂量清零并留下永久后遗症。</summary>
        public static void TryRescueRadiation(GameObject patient, string roomTypeId)
        {
            if (patient == null || !RoomAdvancedEffects.IsMedicalCenter(roomTypeId))
            {
                return;
            }

            RadiationMonitor.Instance monitor = patient.GetSMI<RadiationMonitor.Instance>();
            AmountInstance dose = Db.Get().Amounts.RadiationBalance.Lookup(patient);
            if (monitor == null || dose == null)
            {
                return;
            }

            if (!RoomAdvancedEffects.IsDeadlyRadiation(dose.value, monitor.difficultySettingMod))
            {
                return;
            }

            float before = dose.value;
            dose.SetValue(0f);

            Effects effects = patient.GetComponent<Effects>();
            if (effects != null && CurrentScar(effects) == null)
            {
                effects.Add(RoomEffectIds.RadiationScar, true);
            }

            RescuedThisStay.Add(patient.GetInstanceID());
            ModLog.Info("医疗中心：辐射拯救者为 " + patient.name + " 清除辐射剂量 "
                + before.ToString("0") + " rad，并附加永久后遗症。");
        }

        /// <summary>医生完成诊疗：本次住院获救的患者按施治者医疗减轻后遗症，只减轻不加重。</summary>
        public static void EaseRadiationScar(GameObject patient, WorkerBase doctor)
        {
            if (patient == null || doctor == null || !RescuedThisStay.Contains(patient.GetInstanceID()))
            {
                return;
            }

            Effects effects = patient.GetComponent<Effects>();
            string current = effects == null ? null : CurrentScar(effects);
            if (current == null)
            {
                return;
            }

            AttributeInstance caring = Db.Get().Attributes.Caring.Lookup(doctor);
            float medicine = caring == null ? float.NaN : caring.GetTotalValue();
            int penalty = RoomAdvancedEffects.RadiationScarPenalty(medicine);
            RoomEffectSpec spec = RoomEffectCatalogue.Get(current);
            if (spec == null || penalty <= spec.AttributeBonus)
            {
                return;
            }

            effects.Remove(current);
            string eased = RoomEffectIds.RadiationScarFor(penalty);
            if (eased != null)
            {
                effects.Add(eased, true);
            }

            RescuedThisStay.Remove(patient.GetInstanceID());
            ModLog.Info("医疗中心：" + doctor.name + "（医疗 " + medicine.ToString("0.#") + "）为 " + patient.name
                + " 减轻辐射后遗症，运动 " + spec.AttributeBonus.ToString("0") + " → " + penalty + "。");
        }

        /// <summary>患者离床：本次住院结束，不再按医生减轻后遗症。</summary>
        public static void EndStay(GameObject patient)
        {
            if (patient != null)
            {
                RescuedThisStay.Remove(patient.GetInstanceID());
            }
        }

        /// <summary>患者当前的辐射后遗症档位 id（取最重的一档）；没有返回 null。</summary>
        private static string CurrentScar(Effects effects)
        {
            string[] ids = RoomEffectIds.RadiationScars;
            for (int i = 0; i < ids.Length; i++)
            {
                if (effects.HasEffect(ids[i]))
                {
                    return ids[i];
                }
            }

            return null;
        }
    }
}
