using Klei.AI;
using MaterialOverhaul.Core;
using MaterialOverhaul.Rooms;
using TUNING;

namespace MaterialOverhaul.Game
{
    /// <summary>
    /// 创建六项娱乐偏好特质并加入原版正面特质池。
    ///
    /// 目标游戏没有这些同名特质，因此由本 Mod 创建为空特质，只作为房间加成的判定标记。
    /// 原版 <c>MinionStartingStats.GenerateTraits</c> 只从 <c>GOODTRAITS</c> 与 <c>BADTRAITS</c>
    /// 随机抽取，<c>CONGENITALTRAITS</c> 只用于固定人物，因此加入 <c>GOODTRAITS</c>
    /// （稀有度见 <see cref="RoomTraitCatalogue.PoolRarity"/>）。特质本身不提供永久属性，
    /// 加成由 <see cref="RoomTraitEffects"/> 在对应房间内发放。
    /// </summary>
    public static class RoomTraitRegistration
    {
        private const string Prefix = "STRINGS.ROOMS.MATERIALOVERHAUL.TRAITS.";

        private static bool registered;

        public static void Register()
        {
            if (registered)
            {
                return;
            }

            registered = true;
            RoomTraitSpec[] specs = RoomTraitCatalogue.All;
            int created = 0;
            for (int i = 0; i < specs.Length; i++)
            {
                RoomTraitSpec spec = specs[i];
                if (Db.Get().traits.TryGet(spec.TraitId) != null)
                {
                    continue;
                }

                string name = Strings.Get(Prefix + spec.StringToken + ".NAME");
                string description = Strings.Get(Prefix + spec.StringToken + ".DESC");
                Trait trait = Db.Get().CreateTrait(
                    spec.TraitId,
                    name,
                    description,
                    null,
                    true,
                    null,
                    true,
                    true);

                // 特质没有属性修饰，原版特质列表下方不会自动生成说明行；用 ShortDesc 补一行，
                // 悬停与特质名提示给出房间与效果。
                string shortDescKey = Prefix + spec.StringToken + ".SHORT_DESC";
                string shortTooltipKey = shortDescKey + "_TOOLTIP";
                trait.ShortDescCB = () => Strings.Get(shortDescKey);
                trait.ShortDescTooltipCB = () => Strings.Get(shortTooltipKey);
                trait.ExtendedTooltip = () => Strings.Get(shortTooltipKey);
                DUPLICANTSTATS.GOODTRAITS.Add(new DUPLICANTSTATS.TraitVal
                {
                    id = spec.TraitId,
                    rarity = RoomTraitCatalogue.PoolRarity
                });
                created++;
            }

            ModLog.Info("房间特质：创建 " + created + " 项娱乐偏好特质并加入正面特质池（稀有度 "
                + RoomTraitCatalogue.PoolRarity + "，暂定；特质本身不提供永久属性）。");
        }
    }
}
