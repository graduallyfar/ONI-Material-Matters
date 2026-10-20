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
    /// 辐射拯救者在患者开始治疗时一次性结算，永久后遗症只有一份。
    /// </summary>
    public static class RoomMedicalEffects
    {
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
            if (effects != null && !effects.HasEffect(RoomEffectIds.RadiationScar))
            {
                effects.Add(RoomEffectIds.RadiationScar, true);
            }

            ModLog.Info("医疗中心：辐射拯救者为 " + patient.name + " 清除辐射剂量 "
                + before.ToString("0") + " rad，并附加永久后遗症。");
        }
    }
}
