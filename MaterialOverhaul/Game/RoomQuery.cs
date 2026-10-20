using Klei.AI;
using UnityEngine;

namespace MaterialOverhaul.Game
{
    /// <summary>
    /// 房间效果接入层的共用查询：对象所在房间类型与复制人科学值。
    ///
    /// 只在效果结算入口按需调用，不缓存跨帧结果，与 <see cref="RoomProductionBonus"/> 一致。
    /// </summary>
    public static class RoomQuery
    {
        /// <summary>对象所在房间的类型 id；不在房间内或房间探测器未就绪时为 null。</summary>
        public static string RoomTypeIdOf(GameObject go)
        {
            if (go == null)
            {
                return null;
            }

            global::Game game = global::Game.Instance;
            RoomProber prober = game == null ? null : game.roomProber;
            if (prober == null)
            {
                return null;
            }

            Room room = prober.GetRoomOfGameObject(go);
            return room == null || room.roomType == null ? null : room.roomType.Id;
        }

        public static string RoomTypeIdOf(Component component)
        {
            return component == null ? null : RoomTypeIdOf(component.gameObject);
        }

        /// <summary>复制人的科学（<c>Learning</c>）总值；无法读取时为 NaN，按不满足翻倍处理。</summary>
        public static float ScienceOf(WorkerBase worker)
        {
            if (worker == null)
            {
                return float.NaN;
            }

            Attributes attributes = worker.GetAttributes();
            AttributeInstance learning = attributes == null ? null : attributes.Get(Db.Get().Attributes.Learning.Id);
            return learning == null ? float.NaN : learning.GetTotalValue();
        }
    }
}
