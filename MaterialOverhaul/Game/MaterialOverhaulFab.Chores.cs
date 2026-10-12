using MaterialOverhaul.Core;
using MaterialOverhaul.Zones;

namespace MaterialOverhaul.Game
{
    public partial class MaterialOverhaulFab
    {
        /// <summary>
        /// 在原版 UpdateChore 的入口检查当前批次的实时温度许可。
        /// StartWorkingOrder 已设置 workingOrderIdx 并投料，故只拦操作任务，不拦批次准备。
        /// 没有温区的配方及非复制人建筑沿用原版，其他供电／自动化条件仍由原版检查。
        /// </summary>
        public bool TryBlockOperationChore()
        {
            if (!Spawned || fabricator == null || !fabricator.duplicantOperated)
            {
                return false;
            }

            ComplexRecipe recipe = CurrentWorkingRecipe;
            if (recipe == null)
            {
                return false;
            }

            ProductionDecision decision = EvaluateFor(recipe);
            if (!decision.ZoneConfigured || decision.AllowsProduction)
            {
                return false;
            }

            PauseOperationForTemperature();
            return true;
        }

        private void PauseOperationForTemperature()
        {
            // 暂停标记只用于恢复与日志，不能代替原版任务当前是否存在。
            // 已暂停时外部事件仍可能重建任务；这里始终按实际 chore 清理。
            if (FabricatorAccess.ChoreField(fabricator) != null)
            {
                FabricatorAccess.CancelChore(fabricator);
            }

            bool firstPause = !productionPausedByTemperature;
            productionPausedByTemperature = true;
            SetOperationalActive(false);
            if (firstPause)
            {
                ModLog.Info(PrefabId + "：温度条件不允许生产，已阻止操作任务并释放复制人（批次与进度保留）。");
            }
        }
    }
}
