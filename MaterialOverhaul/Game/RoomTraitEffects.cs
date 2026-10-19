using Klei.AI;
using MaterialOverhaul.Rooms;

namespace MaterialOverhaul.Game
{
    /// <summary>
    /// 娱乐偏好特质的房间加成发放。
    ///
    /// 工作口径（D13 未确认）：特质持有者进入/使用对应房间设施时获得专用效果；
    /// 同时拥有两项相关特质时只取映射表第一项，不叠加。普通复制人仍走原版房间效果。
    /// </summary>
    public static class RoomTraitEffects
    {
        /// <summary>尝试发放特质加成；返回 true 表示已发放、调用方不应再走普通房间效果。</summary>
        public static bool TryApply(RoomType roomType, WorkerBase worker)
        {
            if (roomType == null || worker == null)
            {
                return false;
            }

            Traits traits = worker.GetComponent<Traits>();
            if (traits == null)
            {
                return false;
            }

            RoomTraitSpec[] specs = RoomTraitCatalogue.All;
            for (int i = 0; i < specs.Length; i++)
            {
                RoomTraitSpec spec = specs[i];
                if (!string.Equals(spec.RoomId, roomType.Id, System.StringComparison.Ordinal)
                    || !traits.HasTrait(spec.TraitId))
                {
                    continue;
                }

                Effects effects = worker.GetComponent<Effects>();
                if (effects != null)
                {
                    effects.Add(spec.EffectId, false);
                }

                return true;
            }

            return false;
        }
    }
}
