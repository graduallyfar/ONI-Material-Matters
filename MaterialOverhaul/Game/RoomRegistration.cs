using System;
using System.Collections.Generic;
using System.Reflection;
using MaterialOverhaul.Core;
using MaterialOverhaul.Rooms;

namespace MaterialOverhaul.Game
{
    /// <summary>
    /// 房间类型的登记、原版房间改造与冲突仲裁。
    ///
    /// 登记时机：<c>Db.Initialize</c> 后置补丁。此时 <c>Db.Get().RoomTypes</c> 已由原版构造函数
    /// 建好，房间探测器尚未运行；在此 <see cref="RoomTypes.Add(RoomType)"/> 不会覆盖原版条目，
    /// 也不会在每次刷新时重复登记。
    ///
    /// 冲突处理：原版 <c>GetRoomType</c> 对两个都满足且无升级关系的房间会返回中立房间。
    /// 本 Mod 不修改原版共享判定，而是在其后置补丁按 <see cref="RoomDefinition.Beats"/> 明列的
    /// 覆盖关系选择；未列出的原版房间保持原版结果。
    /// </summary>
    public static class RoomRegistration
    {
        /// <summary>
        /// 原版房间探测器上限（Tuning.json 的 128 格）不足以容纳设计中的大房间。
        /// 运行期放宽到 2400 格，覆盖航天发射场上限；房间类型自己的尺寸条件仍然生效。
        /// 这会改变 129–2400 格开放区域的显示（从「太大」变为中立房间），属已知差异。
        /// </summary>
        public const int MaxRoomSize = 2400;

        private static readonly List<RoomDefinition> ActiveRoomsValue = new List<RoomDefinition>();

        /// <summary>已经追加上附加条件的原版房间类型；避免同一实例重复追加。</summary>
        private static readonly HashSet<RoomType> ExtendedTypes = new HashSet<RoomType>();

        private static bool registered;

        public static bool Registered
        {
            get { return registered; }
        }

        public static IReadOnlyList<RoomDefinition> ActiveRooms
        {
            get { return ActiveRoomsValue; }
        }

        /// <summary>登记新增房间；可重复调用（同 id 已存在时只补登记活动列表）。</summary>
        public static void Register()
        {
            Database.RoomTypes types = Db.Get().RoomTypes;
            if (types == null)
            {
                ModLog.Warn("房间类型登记：Db.Get().RoomTypes 为空，本次跳过。");
                return;
            }

            ActiveRoomsValue.Clear();
            int added = 0;
            int skippedDlc = 0;
            RoomDefinition[] definitions = RoomCatalogue.NewRooms;
            for (int i = 0; i < definitions.Length; i++)
            {
                RoomDefinition definition = definitions[i];
                if (!DlcActive(definition))
                {
                    skippedDlc++;
                    ModLog.Info("房间类型：" + definition.Id + " 需要 DLC " + Join(definition.RequiredDlcIds)
                        + "，当前未启用，跳过。");
                    continue;
                }

                RoomType existing = types.TryGet(definition.Id);
                if (existing == null)
                {
                    existing = CreateRoomType(definition);
                    types.Add(existing);
                    added++;
                    ModLog.Info("房间类型：" + definition.Id + "（" + RoomText.RoomName(definition) + "）已登记，"
                        + "等级 T" + (int)definition.Tier + "，构成条件 " + definition.Additional.Length
                        + " 项，优先于 " + Join(definition.Beats) + "。");
                }

                ActiveRoomsValue.Add(definition);
            }

            registered = true;
            AppendModifications(types);
            ModLog.Info("房间类型登记完成：新增 " + added + "，活动 " + ActiveRoomsValue.Count
                + "，因 DLC 跳过 " + skippedDlc + "；原版房间改造 " + RoomCatalogue.ModificationCount + " 项。");
        }

        /// <summary>
        /// 把私人卧室／宴会厅的附加条件追加到原版 <c>RoomType.additional_constraints</c>，
        /// 使原版判定、失败原因红字和类型提示都使用同一份条件，而不是只在补丁里私下判定。
        ///
        /// 字段是私有自动属性；若目标版本字段名变化，记录警告并保留 <c>isSatisfactory</c> 后置补丁
        /// 作为判定兜底（界面说明会缺少这些条件）。
        /// </summary>
        private static void AppendModifications(Database.RoomTypes types)
        {
            RoomDefinition[] modifications = RoomCatalogue.Modifications;
            for (int i = 0; i < modifications.Length; i++)
            {
                RoomDefinition definition = modifications[i];
                RoomType type = types.TryGet(definition.ExistingRoomId);
                if (type == null)
                {
                    ModLog.WarnOnce("room|mod|missing|" + definition.ExistingRoomId,
                        "房间改造：找不到原版房间 " + definition.ExistingRoomId + "，本次跳过。");
                    continue;
                }

                if (!ExtendedTypes.Add(type))
                {
                    continue;
                }

                AppendModificationEffects(type, definition);

                FieldInfo field = typeof(RoomType).GetField(
                    "<additional_constraints>k__BackingField",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (field == null)
                {
                    ModLog.WarnOnce("room|mod|field",
                        "房间改造：无法访问 RoomType.additional_constraints 的私有字段，"
                        + "原版房间界面不会显示新增条件；判定仍由 isSatisfactory 后置补丁执行。");
                    continue;
                }

                RoomConstraints.Constraint[] current =
                    (RoomConstraints.Constraint[])field.GetValue(type);
                RoomConstraints.Constraint[] extra = BuildAdditional(definition);
                RoomConstraints.Constraint[] merged =
                    new RoomConstraints.Constraint[(current == null ? 0 : current.Length) + extra.Length];
                if (current != null)
                {
                    Array.Copy(current, merged, current.Length);
                }

                Array.Copy(extra, 0, merged, merged.Length - extra.Length, extra.Length);
                field.SetValue(type, merged);
                ModLog.Info("房间改造：" + definition.ExistingRoomId + " 追加 "
                    + extra.Length + " 项条件（" + JoinRequirementNames(definition) + "）。");
            }
        }

        /// <summary>
        /// 把改造项新增的效果 id 追加到原版房间的 <c>effects</c> 数组。
        /// 只追加未登记的效果 id，保留原版已有的房间效果（例如宴会厅原本的士气效果）。
        /// </summary>
        private static void AppendModificationEffects(RoomType type, RoomDefinition definition)
        {
            string[] effectIds = ExistingEffectIds(definition.EffectIds);
            if (effectIds.Length == 0)
            {
                return;
            }

            FieldInfo field = typeof(RoomType).GetField(
                "<effects>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
            {
                ModLog.WarnOnce("room|mod|effects-field",
                    "房间改造：无法访问 RoomType.effects 的私有字段，"
                    + definition.ExistingRoomId + " 的新增效果不会发放。");
                return;
            }

            string[] current = (string[])field.GetValue(type);
            List<string> merged = new List<string>();
            if (current != null)
            {
                merged.AddRange(current);
            }

            int added = 0;
            for (int i = 0; i < effectIds.Length; i++)
            {
                if (!merged.Contains(effectIds[i]))
                {
                    merged.Add(effectIds[i]);
                    added++;
                }
            }

            if (added == 0)
            {
                return;
            }

            field.SetValue(type, merged.ToArray());
            AppendDisplayDetail(type, RoomDetails.EFFECTS);
            ModLog.Info("房间改造：" + definition.ExistingRoomId + " 追加 " + added + " 项效果。");
        }

        /// <summary>把一条房间详情追加到原版房间类型；已存在同一实例时跳过。</summary>
        private static void AppendDisplayDetail(RoomType type, RoomDetails.Detail detail)
        {
            FieldInfo field = typeof(RoomType).GetField(
                "<display_details>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
            {
                ModLog.WarnOnce("room|mod|details-field",
                    "房间改造：无法访问 RoomType.display_details 的私有字段，效果不会显示在房间详情。");
                return;
            }

            RoomDetails.Detail[] current = (RoomDetails.Detail[])field.GetValue(type);
            if (current != null)
            {
                for (int i = 0; i < current.Length; i++)
                {
                    if (ReferenceEquals(current[i], detail))
                    {
                        return;
                    }
                }
            }

            RoomDetails.Detail[] merged =
                new RoomDetails.Detail[(current == null ? 0 : current.Length) + 1];
            if (current != null)
            {
                Array.Copy(current, merged, current.Length);
            }

            merged[merged.Length - 1] = detail;
            field.SetValue(type, merged);
        }

        /// <summary>
        /// 运行期放宽房间探测器上限。只在低于目标值时提高，不覆盖更高配置。
        /// </summary>
        public static void EnsureRoomSize()
        {
            try
            {
                RoomProber.Tuning tuning = TuningData<RoomProber.Tuning>.Get();
                if (tuning != null && tuning.maxRoomSize < MaxRoomSize)
                {
                    tuning.maxRoomSize = MaxRoomSize;
                }
            }
            catch (Exception e)
            {
                ModLog.WarnOnce("room|tuning", "放宽房间探测器上限失败，大房间可能无法识别：" + e.Message);
            }
        }

        /// <summary>
        /// 后置仲裁：先还原「只考虑原版房间」时本应得到的结果，再按明列的覆盖关系决定
        /// 是否换成本 Mod 房间。这样新增房间不会因为原版冲突判定返回中立而意外抢占原版房间
        /// （例如同时有厕所与淋浴间时仍保持卫生间）。
        /// </summary>
        public static RoomType Choose(RoomType current, Room room)
        {
            if (!registered || ActiveRoomsValue.Count == 0 || room == null)
            {
                return null;
            }

            RoomSnapshot snapshot = RoomSnapshotCache.Get(room);
            RoomType mine = SelectModRoom(snapshot, PreviousRoomId(room));
            bool currentIsMine = current != null && IsModRoomId(current.Id);

            if (currentIsMine)
            {
                if (mine != null && !ReferenceEquals(mine, current))
                {
                    return mine;
                }

                if (mine != null)
                {
                    return null;
                }

                RoomType fallback = SelectOriginalRoom(room);
                return fallback != null && fallback.Id != "Neutral" ? fallback : null;
            }

            if (current != null && current.Id != "Neutral")
            {
                return mine != null && RoomEvaluator.Beats(DefinitionOf(mine.Id), current.Id) ? mine : null;
            }

            RoomType baseline = SelectOriginalRoom(room);
            if (baseline != null && baseline.Id != "Neutral")
            {
                return mine != null && RoomEvaluator.Beats(DefinitionOf(mine.Id), baseline.Id) ? mine : baseline;
            }

            return mine;
        }

        /// <summary>
        /// 只考虑原版房间类型，复刻 <c>RoomTypes.GetRoomType</c> 的优先级／升级／冲突规则。
        /// 本 Mod 房间不参与，避免改变原版歧义的判定结果。
        /// </summary>
        private static RoomType SelectOriginalRoom(Room room)
        {
            Database.RoomTypes types = Db.Get().RoomTypes;
            List<RoomType> resources = types.resources;
            for (int i = 0; i < resources.Count; i++)
            {
                RoomType candidate = resources[i];
                if (candidate == null || candidate.Id == "Neutral" || IsModRoomId(candidate.Id))
                {
                    continue;
                }

                if (candidate.isSatisfactory(room) != RoomType.RoomIdentificationResult.all_satisfied)
                {
                    continue;
                }

                bool ambiguous = false;
                for (int j = 0; j < resources.Count; j++)
                {
                    RoomType other = resources[j];
                    if (other == null || other == candidate || other.Id == "Neutral" || IsModRoomId(other.Id))
                    {
                        continue;
                    }

                    if (types.HasAmbiguousRoomType(room, candidate, other))
                    {
                        ambiguous = true;
                        break;
                    }
                }

                if (!ambiguous)
                {
                    return candidate;
                }
            }

            return types.Neutral;
        }

        /// <param name="previousRoomId">房间重判前的类型 id；与候选相同时按保持回差判定。</param>
        private static RoomType SelectModRoom(RoomSnapshot snapshot, string previousRoomId)
        {
            RoomDefinition best = null;
            for (int i = 0; i < ActiveRoomsValue.Count; i++)
            {
                RoomDefinition definition = ActiveRoomsValue[i];
                bool retain = string.Equals(definition.Id, previousRoomId, StringComparison.Ordinal);
                if (!RoomEvaluator.IsSatisfied(definition, snapshot, retain))
                {
                    continue;
                }

                if (best == null
                    || definition.Priority > best.Priority
                    || (definition.Priority == best.Priority && definition.SortKey > best.SortKey))
                {
                    best = definition;
                }
            }

            return best == null ? null : FindRoomType(best.Id);
        }

        /// <summary>房间重判前的类型 id；新建房间对象尚无类型时为 null。</summary>
        private static string PreviousRoomId(Room room)
        {
            return room == null || room.roomType == null ? null : room.roomType.Id;
        }

        /// <summary>
        /// 房间是否可视为给定原版房间：本身就是，或是本 Mod 明确顶替（<see cref="RoomDefinition.Beats"/>）该房间的新房间。
        /// 供原版 <c>RoomTracker</c> 的「所需房间」判定放行。
        /// </summary>
        public static bool CountsAsRoom(Room room, string requiredRoomId)
        {
            if (room == null || room.roomType == null || string.IsNullOrEmpty(requiredRoomId))
            {
                return false;
            }

            if (string.Equals(room.roomType.Id, requiredRoomId, StringComparison.Ordinal))
            {
                return true;
            }

            RoomDefinition definition = DefinitionOf(room.roomType.Id);
            return definition != null && RoomEvaluator.Beats(definition, requiredRoomId);
        }

        private static RoomDefinition DefinitionOf(string roomId)
        {
            for (int i = 0; i < ActiveRoomsValue.Count; i++)
            {
                if (string.Equals(ActiveRoomsValue[i].Id, roomId, StringComparison.Ordinal))
                {
                    return ActiveRoomsValue[i];
                }
            }

            return null;
        }

        private static bool IsModRoomId(string roomId)
        {
            return DefinitionOf(roomId) != null;
        }

        /// <summary>该房间类型是否由本 Mod 登记（用于限制效果触发只作用于新增房间）。</summary>
        public static bool IsModRoom(RoomType roomType)
        {
            return roomType != null && IsModRoomId(roomType.Id);
        }

        /// <summary>对原版房间追加环境条件：主条件满足但附加条件不足时降级为「主条件满足」。</summary>
        public static RoomType.RoomIdentificationResult ApplyModifications(
            RoomType roomType,
            Room room,
            RoomType.RoomIdentificationResult result)
        {
            if (result != RoomType.RoomIdentificationResult.all_satisfied || roomType == null || room == null)
            {
                return result;
            }

            RoomDefinition modification = RoomCatalogue.ModificationFor(roomType.Id);
            if (modification == null)
            {
                return result;
            }

            bool retain = string.Equals(PreviousRoomId(room), roomType.Id, StringComparison.Ordinal);
            List<RoomRequirement> missing = RoomEvaluator.Missing(modification, RoomSnapshotCache.Get(room), retain);
            if (missing.Count > 0)
            {
                return RoomType.RoomIdentificationResult.primary_satisfied;
            }

            return result;
        }

        public static RoomType FindRoomType(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            return Db.Get().RoomTypes.TryGet(id);
        }

        private static RoomConstraints.Constraint[] BuildAdditional(RoomDefinition definition)
        {
            RoomConstraints.Constraint[] additional =
                new RoomConstraints.Constraint[definition.Additional.Length];
            for (int i = 0; i < definition.Additional.Length; i++)
            {
                RoomRequirement requirement = definition.Additional[i];
                additional[i] = new RoomConstraints.Constraint(
                    building_criteria: null,
                    room_criteria: (Room room) => RoomEvaluator.IsSatisfied(
                        definition,
                        requirement,
                            RoomSnapshotCache.Get(room),
                            string.Equals(PreviousRoomId(room), definition.Id, StringComparison.Ordinal)),
                    times_required: 1,
                    name: RoomText.RequirementName(definition, requirement),
                    description: RoomText.RequirementName(definition, requirement));
            }

            return additional;
        }

        private static RoomType CreateRoomType(RoomDefinition definition)
        {
            RoomConstraints.Constraint primary = new RoomConstraints.Constraint(
                building_criteria: (KPrefabID entity) => MatchesPrimary(definition, entity),
                room_criteria: null,
                times_required: 1,
                name: RoomText.PrimaryName(definition),
                description: RoomText.PrimaryName(definition));

            RoomConstraints.Constraint[] additional = BuildAdditional(definition);

            List<RoomDetails.Detail> details = new List<RoomDetails.Detail>
            {
                RoomDetails.SIZE,
                RoomDetails.BUILDING_COUNT,
                RoomDetails.PLANT_COUNT
            };
            if (definition.UsesEnvironment)
            {
                details.Add(new RoomDetails.Detail(room => RoomText.AverageDecorLine(room)));
                details.Add(new RoomDetails.Detail(room => RoomText.AverageTemperatureLine(room)));
            }

            if (definition.EffectIds != null && definition.EffectIds.Length > 0)
            {
                details.Add(RoomDetails.EFFECTS);
            }

            if (definition.Id == RoomCatalogue.IndustrialAreaId
                || definition.Id == RoomCatalogue.DataCenterId)
            {
                details.Add(new RoomDetails.Detail(room => RoomText.ProductionLine(room)));
            }

            return new RoomType(
                definition.Id,
                RoomText.RoomName(definition),
                RoomText.RoomDescription(definition),
                RoomText.RoomTooltip(definition),
                string.Empty,
                Category(definition.CategoryKey),
                primary,
                additional,
                details.ToArray(),
                definition.Priority,
                null,
                false,
                false,
                ExistingEffectIds(definition.EffectIds),
                definition.SortKey);
        }

        /// <summary>过滤出已经登记到 <c>Db.Get().effects</c> 的效果 id；缺失项留可定位日志。</summary>
        private static string[] ExistingEffectIds(string[] effectIds)
        {
            if (effectIds == null || effectIds.Length == 0)
            {
                return new string[0];
            }

            List<string> existing = new List<string>();
            for (int i = 0; i < effectIds.Length; i++)
            {
                if (Db.Get().effects.TryGet(effectIds[i]) != null)
                {
                    existing.Add(effectIds[i]);
                }
                else
                {
                    ModLog.WarnOnce("room|effect|missing|" + effectIds[i],
                        "房间效果：" + effectIds[i] + " 尚未登记到 Db.effects，本次不挂到房间上。");
                }
            }

            return existing.ToArray();
        }

        private static bool MatchesPrimary(RoomDefinition definition, KPrefabID entity)
        {
            if (entity == null || definition.PrimaryPrefabIds == null)
            {
                return false;
            }

            string prefabId = entity.PrefabID().ToString();
            for (int i = 0; i < definition.PrimaryPrefabIds.Length; i++)
            {
                if (string.Equals(definition.PrimaryPrefabIds[i], prefabId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static RoomTypeCategory Category(string key)
        {
            Database.RoomTypeCategories categories = Db.Get().RoomTypeCategories;
            switch (key)
            {
                case "Bathroom":
                    return categories.Bathroom;
                case "Park":
                    return categories.Park;
                case "Industrial":
                    return categories.Industrial;
                case "Hospital":
                    return categories.Hospital;
                case "Recreation":
                    return categories.Recreation;
                case "Science":
                    return categories.Science;
                default:
                    return categories.None;
            }
        }

        private static bool DlcActive(RoomDefinition definition)
        {
            if (definition.RequiredDlcIds == null || definition.RequiredDlcIds.Length == 0)
            {
                return true;
            }

            return DlcManager.IsAllContentSubscribed(definition.RequiredDlcIds);
        }

        private static string Join(string[] values)
        {
            return values == null || values.Length == 0 ? "（无）" : string.Join("、", values);
        }

        private static string JoinRequirementNames(RoomDefinition definition)
        {
            if (definition.Additional == null || definition.Additional.Length == 0)
            {
                return "（无）";
            }

            string[] names = new string[definition.Additional.Length];
            for (int i = 0; i < definition.Additional.Length; i++)
            {
                names[i] = RoomText.RequirementName(definition, definition.Additional[i]);
            }

            return string.Join("、", names);
        }
    }
}
