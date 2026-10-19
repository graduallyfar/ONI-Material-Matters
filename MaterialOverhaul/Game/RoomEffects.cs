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
    /// <c>QualityOfLife</c> 属性，压力按 <c>Stress.deltaAttribute</c> 的乘数修正。
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
                false);

            if (spec.HasMorale)
            {
                effect.Add(new AttributeModifier(Db.Get().Attributes.QualityOfLife.Id, spec.Morale, name));
            }

            if (spec.HasStressReduction)
            {
                effect.Add(new AttributeModifier(
                    Db.Get().Amounts.Stress.deltaAttribute.Id,
                    -spec.StressReductionPercent / 100f,
                    name,
                    is_multiplier: true));
            }

            if (spec.HasHealthRecovery)
            {
                effect.Add(new AttributeModifier(
                    Db.Get().Amounts.HitPoints.deltaAttribute.Id,
                    spec.HealthRecoveryPercent / 100f,
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
                + (spec.HasAttributeBonus ? "、" + spec.AttributeId + " +" + spec.AttributeBonus.ToString("0") : string.Empty)
                + "，持续 " + spec.DurationSeconds.ToString("0") + " 秒（工作初值，D08／D10／D13 未确认）。");
        }

        private static string Signed(float value)
        {
            return (value >= 0f ? "+" : string.Empty) + value.ToString("0");
        }

        private static string Stress(float percent)
        {
            return percent == 0f ? "不变" : "−" + percent.ToString("0") + "%";
        }

        private static string Health(float percent)
        {
            return percent == 0f ? "不变" : "+" + percent.ToString("0") + "%";
        }
    }
}
