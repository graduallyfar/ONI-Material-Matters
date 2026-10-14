using MaterialOverhaul.Heating;

namespace MaterialOverhaul.Game
{
    // 复用损坏原因的存档锁存；不添加可由开关或切配方重置的第二套安全状态。
    public partial class MaterialOverhaulFab
    {
        /// <summary>是否已经锁存过热原因。</summary>
        public bool HasOverheatDamage
        {
            get { return damageState.OverheatLatched || damageState.ForcedOverheatLatched; }
        }

        /// <summary>
        /// 锁存过热损坏原因。强制运行期间发生的过热损坏会额外锁存一条「附加机械显示原因」，
        /// 使硬度合格的材料也同时显示两种原因；这只是显示，不额外制造机械伤害。
        /// 锁存只看**事件发生当时**的强制开关，关闭开关不会丢原因，也不会伪造一次强制过热。
        /// </summary>
        public void LatchOverheatDamage()
        {
            if (damageState.OverheatLatched && (!forcedRun || damageState.ForcedOverheatLatched))
            {
                return;
            }

            damageState.OverheatLatched = true;
            if (forcedRun)
            {
                damageState.ForcedOverheatLatched = true;
            }

            SyncDamageFields();
            // 伤害事件内立即撤掉加热需求；dt=0 不额外生产或注热。
            if (Spawned)
            {
                UpdatePowerAndHeating(0f);
            }
        }

        private void ClearDamageWhenFullyRepaired()
        {
            if (buildingHP != null && !buildingHP.NeedsRepairs && !damageState.IsClean)
            {
                damageState.Clear();
                SyncDamageFields();
            }
        }

        private HeatingContext CreateHeatingContext()
        {
            return new HeatingContext
            {
                HasLockedBatch = HasLockedBatch,
                BatchMaterialsReady = HasLockedBatch, // 已投料无需输入仓再备一份。
                Powered = IsPowered,
                ManuallyDisabled = IsManuallyDisabled,
                BrokenDown = IsBrokenDown,
                OverheatDamaged = HasOverheatDamage
            };
        }
    }
}
