using Klei.AI;
using MaterialOverhaul.Core;
using MaterialOverhaul.Rooms;
using TUNING;

namespace MaterialOverhaul.Game
{
    /// <summary>
    /// 创建六项娱乐偏好特质并加入原版先天性特质池。
    ///
    /// 工作口径（D13 未确认）：目标游戏没有这些同名特质，因此由本 Mod 创建为空特质，
    /// 只作为房间加成的判定标记；加入 <c>DUPLICANTSTATS.CONGENITALTRAITS</c> 后可由
    /// 新复制人生成获得。特质本身不提供永久属性，加成由 <see cref="RoomTraitEffects"/>
    /// 在对应房间内发放。
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
                Db.Get().CreateTrait(
                    spec.TraitId,
                    name,
                    description,
                    null,
                    true,
                    null,
                    true,
                    true);
                DUPLICANTSTATS.CONGENITALTRAITS.Add(new DUPLICANTSTATS.TraitVal
                {
                    id = spec.TraitId
                });
                created++;
            }

            ModLog.Info("房间特质：创建 " + created + " 项娱乐偏好特质并加入先天性特质池"
                + "（工作口径，D13 未确认；特质本身不提供永久属性）。");
        }
    }
}
