using MaterialOverhaul.Core;
using MaterialOverhaul.Heating;

namespace MaterialOverhaul.Game
{
    public partial class MaterialOverhaulFab
    {
        public bool ModeConnectionReady { get { return modeConnectionReady; } }

        /// <summary>
        /// LogicCircuitManager.Refresh 先重建脏网络再传播信号；只在系统不脏时读取最终连接。
        /// 初始化期间不把缺少网络误当拆线。只查询本实例的指定输出，不读取红绿值。
        /// </summary>
        private void RefreshModeFromOutputConnection()
        {
            if (!Spawned) return;
            if (logicPorts == null)
            {
                ModLog.WarnOnce("mode|no-ports|" + PrefabId + "|" + Grid.PosToCell(this),
                    PrefabId + "：缺少 LogicPorts 组件，无法按输出接线选择模式，回退默认模式。");
                modeConnectionReady = true;
                SetControlMode(false);
                return;
            }
            if (logicPorts.outputPorts == null || global::Game.Instance == null
                || global::Game.Instance.logicCircuitManager == null
                || global::Game.Instance.logicCircuitSystem == null
                || global::Game.Instance.logicCircuitSystem.IsDirty)
            {
                return;
            }

            bool found = false;
            if (logicPorts.outputPortInfo != null)
            {
                foreach (LogicPorts.Port port in logicPorts.outputPortInfo)
                {
                    if (port.id == TemperatureZoneSignal.PortId)
                    {
                        found = true;
                        break;
                    }
                }
            }
            // 燃料发电机按设计没有温区输出，直接按未接线（默认模式）处理，不报警。
            if (!found && !AutomationControlScope.IsWithoutOutput(PrefabId))
            {
                ModLog.WarnOnce("mode|no-output|" + PrefabId + "|" + Grid.PosToCell(this),
                    PrefabId + "：缺少温区输出端口，无法按接线选择模式，回退默认模式。");
            }

            int cell = found ? logicPorts.GetPortCell(TemperatureZoneSignal.PortId) : -1;
            if (found && !Grid.IsValidCell(cell))
            {
                ModLog.WarnOnce("mode|invalid-cell|" + PrefabId + "|" + Grid.PosToCell(this),
                    PrefabId + "：温区输出格子无效，等待端口就绪。");
                return;
            }

            bool connected = found && logicPorts.IsPortConnected(TemperatureZoneSignal.PortId);
            bool first = !modeConnectionReady;
            bool previous = controlMode;
            modeConnectionReady = true;
            if (previous != connected)
            {
                SetControlMode(Policy.WithOutputConnection(connected).ControlMode);
            }
            else if (first)
            {
                if (IsNonBatch)
                {
                    runState = RunController.OnLoaded(BodyTemperature,cachedZone,Policy,runState);
                    SyncRunStateFields();
                }
                // 原版读档时 workingOrderIdx 尚未恢复；用保存的锁定订单解析同批温区。
                string recipeId = CurrentWorkingRecipe?.id ?? orders.LockedBatch?.RecipeId;
                if (recipeId != null)
                {
                    UpdateCachedZone(recipeId);
                    runState = RunController.OnLoaded(BodyTemperature, cachedZone, Policy, runState);
                    SyncRunStateFields();
                }
            }

            if (first || previous != connected)
            {
                ModLog.Info(PrefabId + " @格子 " + Grid.PosToCell(this) + "：温区输出 "
                    + TemperatureZoneSignal.PortId + " @" + cell + "，连接 " + connected
                    + "，" + (previous ? "控制" : "默认") + "→" + (controlMode ? "控制" : "默认")
                    + "（" + (first ? "网络就绪后初始化" : "输出连接变化") + "）。");
                UpdateModeStatusItem();
            }
        }

        private void UpdateModeStatusItem()
        {
            if (selectable == null || RunModeStatusItem.Item == null || RunModeStatusItem.Category == null)
            {
                return;
            }
            string text = RunModeStatusItem.Name(this);
            if (text != lastAppliedModeText)
            {
                lastAppliedModeText = text;
                selectable.SetStatusItem(RunModeStatusItem.Category, RunModeStatusItem.Item, this);
            }
        }

        /// <summary>原版推进工作之前更新模式与温度门控，避免生产路径读到旧模式。</summary>
        public void PrepareRunStep()
        {
            if (!Spawned || fabricator == null)
            {
                return;
            }
            RefreshModeFromOutputConnection();
            if (!modeConnectionReady)
            {
                if (!modeInitializationLogged)
                {
                    modeInitializationLogged = true;
                    ModLog.Info(PrefabId + " @格子 " + Grid.PosToCell(this)
                        + "：等待自动化网络重建，保留存档模式与温控回差。");
                }
                EnforceProductionGate();
                return;
            }
            UpdateRunState();
            EnforceProductionGate();
        }
    }
}
