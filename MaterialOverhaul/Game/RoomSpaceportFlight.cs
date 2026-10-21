using KSerialization;

namespace MaterialOverhaul.Game
{
    /// <summary>
    /// 航天发射场的飞行标记：挂在群星火箭（<c>Clustercraft</c> 实体）上，随存档保存。
    ///
    /// 发射时平台位于航天发射场则置位，飞行期间（含落地前最后一格）燃料与速度加成都读取本标记；
    /// 火箭落地（<c>Grounded</c>）时清除。读档后在途火箭继续保留加成，不需要重新判定平台房间。
    /// 旧存档没有本组件数据时默认为未受益；卸载本 Mod 后原版读档会按长度跳过本组件。
    /// </summary>
    [SerializationConfig(MemberSerialization.OptIn)]
    public class RoomSpaceportFlight : KMonoBehaviour
    {
        [Serialize] private bool fromSpaceport;

        public bool FromSpaceport
        {
            get { return fromSpaceport; }
            set { fromSpaceport = value; }
        }

        /// <summary>读档后按保留的飞行标记恢复火箭状态栏的房间加成条目。</summary>
        protected override void OnSpawn()
        {
            base.OnSpawn();
            if (fromSpaceport)
            {
                RoomBonusStatus.RefreshRocket(GetComponent<Clustercraft>());
            }
        }
    }
}
