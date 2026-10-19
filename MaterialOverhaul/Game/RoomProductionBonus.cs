namespace MaterialOverhaul.Game
{
    /// <summary>
    /// 运行时判定建筑是否处于带生产加成的房间。
    ///
    /// 只在生产进度入口调用，不缓存跨帧结果：房间可能因拆墙、改装饰或温度变化而改变，
    /// 缓存需要额外的失效渠道，当前按需查询更能保证与房间显示一致。
    /// </summary>
    public static class RoomProductionBonus
    {
        public static float MultiplierFor(UnityEngine.Component component)
        {
            if (component == null)
            {
                return 1f;
            }

            KPrefabID prefab = component.GetComponent<KPrefabID>();
            if (prefab == null)
            {
                return 1f;
            }

            global::Game game = global::Game.Instance;
            RoomProber prober = game == null ? null : game.roomProber;
            if (prober == null)
            {
                return 1f;
            }

            Room room = prober.GetRoomOfGameObject(component.gameObject);
            if (room == null || room.roomType == null)
            {
                return 1f;
            }

            return Rooms.RoomProduction.Multiplier(room.roomType.Id, prefab.PrefabID().ToString());
        }
    }
}
