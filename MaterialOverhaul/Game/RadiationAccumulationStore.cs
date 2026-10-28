using System.Runtime.Serialization;
using KSerialization;

namespace MaterialOverhaul.Game
{
    /// <summary>
    /// 每格累积表的存档与驱动。挂在原版 SaveGame 对象上，随存档保存；
    /// 只写出有累积量的格子（存档键 = 格子 × 4 + 层、累积量、物质身份三个数组）。
    ///
    /// 读档时先留着存档数据，等第一次模拟更新（世界格子已就绪）再交给 <see cref="RadiationInducedMonitor"/>；
    /// 交出之前存档会原样写回，不会丢。每 0.2 秒的模拟更新驱动扫描，暂停时原版不调用。
    /// 卸载本 Mod 后原版读档会跳过本组件。
    /// </summary>
    [SerializationConfig(MemberSerialization.OptIn)]
    public class RadiationAccumulationStore : KMonoBehaviour, ISim200ms
    {
        [Serialize] private int[] keys;
        [Serialize] private float[] values;
        [Serialize] private int[] identities;

        private bool handedOver;

        [OnSerializing]
        private void OnSerializing()
        {
            if (!handedOver)
            {
                return;
            }

            int[] packedKeys;
            float[] packedValues;
            int[] packedIdentities;
            if (RadiationInducedMonitor.TryPack(out packedKeys, out packedValues, out packedIdentities))
            {
                keys = packedKeys;
                values = packedValues;
                identities = packedIdentities;
            }
        }

        [OnSerialized]
        private void OnSerialized()
        {
            if (handedOver)
            {
                keys = null;
                values = null;
                identities = null;
            }
        }

        public void Sim200ms(float dt)
        {
            if (!handedOver)
            {
                handedOver = true;
                RadiationInducedMonitor.LoadSaved(keys, values, identities);
                keys = null;
                values = null;
                identities = null;
            }

            RadiationInducedMonitor.Tick(dt);
        }
    }
}
