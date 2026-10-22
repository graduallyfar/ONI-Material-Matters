using Klei.AI;
using MaterialOverhaul.Core;
using MaterialOverhaul.Rooms;

namespace MaterialOverhaul.Game
{
    /// <summary>
    /// 房间效果的登记与创建。
    ///
    /// 数值与持续时间在纯配置层 <see cref="RoomEffectCatalogue"/> 维护；这里只负责把
    /// <see cref="RoomEffectSpec"/> 翻译成原版 <c>Klei.AI.Effect</c>：士气挂
    /// <c>QualityOfLife</c> 属性，压力按 <c>Stress.deltaAttribute</c> 的加法修正（每周期百分点）。
    /// 效果对象在 <c>Db.Initialize</c> 后置处理里加入 <c>Db.Get().effects</c>，房间类型只保存 id；
    /// 实际发放由原版 <c>RoomType.TriggerRoomEffects</c> 在设施被使用／进入时完成。
    /// </summary>
    public static class RoomEffects
    {
        private const string Prefix = "STRINGS.ROOMS.MATERIALOVERHAUL.EFFECTS.";

        public static void Register()
        {
            RoomEffectSpec[] specs = RoomEffectCatalogue.All;
            for (int i = 0; i < specs.Length; i++)
            {
                AddEffect(specs[i]);
            }

            ApplyBanquetMorale();
        }

        /// <summary>
        /// 宴会厅士气 +6（D08 工作口径）：把原版 <c>RoomBanquetHall</c> 效果的 QualityOfLife 加算修正
        /// 统一为设计值。该效果只由宴会厅发放；原值已相同时不改，找不到时只记录警告。
        /// </summary>
        private static void ApplyBanquetMorale()
        {
            Effect effect = Db.Get().effects.TryGet(RoomAdvancedEffects.BanquetEffectId);
            if (effect == null || effect.SelfModifiers == null)
            {
                ModLog.WarnOnce("room|banquet|effect", "宴会厅士气：找不到原版效果 "
                    + RoomAdvancedEffects.BanquetEffectId + "，士气 +6 未生效。");
                return;
            }

            string morale = Db.Get().Attributes.QualityOfLife.Id;
            AttributeModifier first = null;
            float total = 0f;
            for (int i = 0; i < effect.SelfModifiers.Count; i++)
            {
                AttributeModifier modifier = effect.SelfModifiers[i];
                if (modifier == null || modifier.IsMultiplier || modifier.AttributeId != morale)
                {
                    continue;
                }

                if (first == null)
                {
                    first = modifier;
                }

                total += modifier.Value;
            }

            if (first == null)
            {
                ModLog.WarnOnce("room|banquet|modifier", "宴会厅士气：原版效果没有士气修正，士气 +6 未生效。");
                return;
            }

            if (total == RoomAdvancedEffects.BanquetMorale)
            {
                ModLog.Info("宴会厅士气：原版已为 " + Signed(total) + "，保持不变。");
                return;
            }

            first.SetValue(first.Value + RoomAdvancedEffects.BanquetMorale - total);
            ModLog.Info("宴会厅士气：原版 " + Signed(total) + " 调整为 " + Signed(RoomAdvancedEffects.BanquetMorale) + "。");
        }

        private static void AddEffect(RoomEffectSpec spec)
        {
            if (spec == null || string.IsNullOrEmpty(spec.Id))
            {
                return;
            }

            if (Db.Get().effects.TryGet(spec.Id) != null)
            {
                return;
            }

            string name = Strings.Get(Prefix + spec.StringToken + ".NAME");
            string tooltip = Strings.Get(Prefix + spec.StringToken + ".TOOLTIP");
            Effect effect = new Effect(
                spec.Id,
                name,
                tooltip,
                spec.DurationSeconds,
                true,
                false,
                spec.IsBad);

            if (spec.HasMorale)
            {
                effect.Add(new AttributeModifier(Db.Get().Attributes.QualityOfLife.Id, spec.Morale, name));
            }

            if (spec.HasStressReduction)
            {
                // 压力本身是百分数，按加减法结算（V1.0.2 #21）。
                effect.Add(new AttributeModifier(
                    Db.Get().Amounts.Stress.deltaAttribute.Id,
                    RoomEffectCatalogue.StressDeltaPerSecond(spec.StressReductionPercent),
                    name));
            }

            if (spec.HasHealthRecovery)
            {
                effect.Add(new AttributeModifier(
                    Db.Get().Amounts.HitPoints.deltaAttribute.Id,
                    spec.EffectiveHealthModifierPercent / 100f,
                    name,
                    is_multiplier: true));
            }

            if (spec.HasAttributeBonus)
            {
                effect.Add(new AttributeModifier(spec.AttributeId, spec.AttributeBonus, name));
            }

            Db.Get().effects.Add(effect);
            ModLog.Info("房间效果：" + spec.Id + "（" + name + "）士气 " + Signed(spec.Morale)
                + "、压力缓解 " + Stress(spec.StressReductionPercent)
                + "、生命回复 " + Health(spec.HealthRecoveryPercent)
                + (spec.HasAttributeBonus ? "、" + spec.AttributeId + " " + Signed(spec.AttributeBonus) : string.Empty)
                + (spec.Permanent ? "，永久" : "，持续 " + spec.DurationSeconds.ToString("0") + " 秒")
                + "（工作初值，平衡待验收）。");
        }

        private static string Signed(float value)
        {
            return (value >= 0f ? "+" : string.Empty) + value.ToString("0");
        }

        private static string Stress(float percent)
        {
            return percent == 0f ? "不变" : "−" + percent.ToString("0") + "%／周期";
        }

        private static string Health(float percent)
        {
            return percent == 0f ? "不变" : "+" + percent.ToString("0") + "%";
        }
    }
}
