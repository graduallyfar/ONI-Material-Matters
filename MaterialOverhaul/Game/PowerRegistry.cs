using System.Collections.Generic;

namespace MaterialOverhaul.Game
{
    /// <summary>
    /// <see cref="EnergyConsumer"/> 与本 Mod 建筑组件的对应表。
    ///
    /// 电网补丁会拦截**所有** <c>EnergyConsumer</c> 的功率读数（原版属性不可覆写，
    /// 只能做后置补丁），因此需要一个按消费者的快速反查，避免每次读功率都
    /// <c>GetComponent</c> 一次；同时保证只有本 Mod 明确处理的建筑会被改写。
    ///
    /// 组件在 <c>OnSpawn</c> 注册、<c>OnCleanUp</c> 注销，不持有已被销毁的对象。
    /// </summary>
    internal static class PowerRegistry
    {
        private static readonly Dictionary<EnergyConsumer, MaterialOverhaulFab> FabByConsumer =
            new Dictionary<EnergyConsumer, MaterialOverhaulFab>();

        public static void Register(EnergyConsumer consumer, MaterialOverhaulFab fab)
        {
            if (consumer == null || fab == null)
            {
                return;
            }

            FabByConsumer[consumer] = fab;
        }

        public static void Unregister(EnergyConsumer consumer)
        {
            if (consumer != null)
            {
                FabByConsumer.Remove(consumer);
            }
        }

        public static bool TryGet(EnergyConsumer consumer, out MaterialOverhaulFab fab)
        {
            fab = null;
            return consumer != null && FabByConsumer.TryGetValue(consumer, out fab) && fab != null;
        }
    }
}
