using System;
using System.Text;
using MaterialOverhaul.Core;
using MaterialOverhaul.Rooms;

namespace MaterialOverhaul.Game
{
    /// <summary>
    /// 房间悬停卡「效果」段前后的补充文本（V1.0.2 #16、#19）。
    ///
    /// 原版悬停卡依次绘制需求（<c>RoomConstraints.RoomCriteriaString</c>）与效果（<c>RoomDetails.EFFECTS</c>）。
    /// 不能给 <c>RoomConstraints</c> 打补丁（会提前触发其静态初始化，见 #2），因此把升级提示接在效果段之前：
    /// 替换 <c>RoomDetails.EFFECTS</c> 的求值委托，原结果保留在中间。房间详情列表也会读取该行，
    /// 读取期间（<see cref="InDetails"/>）只返回原版结果。
    /// </summary>
    public static class RoomHoverText
    {
        private const string Prefix = "STRINGS.ROOMS.MATERIALOVERHAUL.";

        /// <summary>正在生成房间详情列表；此时不追加悬停卡专用文本。</summary>
        public static bool InDetails;

        private static Func<Room, string> original;

        public static void Install()
        {
            if (original != null || RoomDetails.EFFECTS == null)
            {
                return;
            }

            original = RoomDetails.EFFECTS.resolve_string_function;
            RoomDetails.EFFECTS.resolve_string_function = Resolve;
        }

        private static string Resolve(Room room)
        {
            string effects = original == null ? null : original(room);
            if (InDetails || room == null || room.roomType == null)
            {
                return effects;
            }

            try
            {
                if (RoomAdvancedEffects.IsMedicalCenter(room.roomType.Id))
                {
                    string rescue = "\n    • " + Text(Prefix + "DETAILS.RADIATION_RESCUE");
                    effects = string.IsNullOrEmpty(effects) ? Text("STRINGS.ROOMS.EFFECTS.HEADER") + rescue : effects + rescue;
                }

                string upgrades = UpgradeHints(room);
                if (upgrades.Length == 0)
                {
                    return effects;
                }

                return string.IsNullOrEmpty(effects) ? upgrades : upgrades + "\n" + effects;
            }
            catch (Exception e)
            {
                ModLog.WarnOnce("room|hover|text", "房间悬停卡补充文本生成失败：" + e);
                return effects;
            }
        }

        /// <summary>
        /// 已成立房间可升级的房间类型及缺少的条件：原版升级路径或本 Mod 明列顶替当前房间的类型，
        /// 且房间里已有其主建筑（主条件满足）。中立房间由原版「可能的房间类型」负责，不重复。
        /// </summary>
        public static string UpgradeHints(Room room)
        {
            RoomType current = room.roomType;
            Database.RoomTypes types = Db.Get().RoomTypes;
            if (current == types.Neutral)
            {
                return string.Empty;
            }

            StringBuilder text = new StringBuilder();
            for (int i = 0; i < types.resources.Count; i++)
            {
                RoomType candidate = types.resources[i];
                if (candidate == null || candidate == current || candidate == types.Neutral || !IsUpgrade(current, candidate))
                {
                    continue;
                }

                if (candidate.isSatisfactory(room) == RoomType.RoomIdentificationResult.primary_unsatisfied)
                {
                    continue;
                }

                if (text.Length == 0)
                {
                    text.Append("<b>").Append(Text(Prefix + "CRITERIA.UPGRADE_HEADER")).Append("</b>");
                }

                text.Append("\n<b><color=#BCBCBC>    • ").Append(candidate.Name).Append("</b> (")
                    .Append(candidate.primary_constraint.conflictDescription).Append(")</color>");
                RoomConstraints.Constraint[] additional = candidate.additional_constraints;
                for (int j = 0; additional != null && j < additional.Length; j++)
                {
                    RoomConstraints.Constraint constraint = additional[j];
                    if (constraint == null || constraint.isSatisfied(room))
                    {
                        continue;
                    }

                    string format = constraint.building_criteria == null
                        ? Text("STRINGS.ROOMS.CRITERIA.CRITERIA_FAILED.FAILED")
                        : Text("STRINGS.ROOMS.CRITERIA.CRITERIA_FAILED.MISSING_BUILDING");
                    text.Append("\n<color=#F44A47FF>        • ").Append(string.Format(format, constraint.name)).Append("</color>");
                }
            }

            return text.ToString();
        }

        private static string Text(string key)
        {
            return Strings.Get(key);
        }

        private static bool IsUpgrade(RoomType current, RoomType candidate)
        {
            if (current.upgrade_paths != null && Array.IndexOf(current.upgrade_paths, candidate) >= 0)
            {
                return true;
            }

            return RoomEvaluator.Beats(RoomRegistration.DefinitionOf(candidate.Id), current.Id);
        }
    }
}
