using System;
using HarmonyLib;
using MaterialOverhaul.Core;
using MaterialOverhaul.Heating;
using MaterialOverhaul.Zones;
using UnityEngine;

namespace MaterialOverhaul.Game
{
    /// <summary>非批次工业接入。只复用温控、加热、损坏和显示，不创建任何订单或批次。</summary>
    public partial class MaterialOverhaulFab : ISim200ms
    {
        private static readonly Operational.Flag TemperatureFlag = new Operational.Flag("MaterialOverhaulTemperature", Operational.Flag.Type.Requirement);
        internal static readonly AccessTools.FieldRef<ElementConverter, float> ConverterSpeed =
            AccessTools.FieldRefAccess<ElementConverter,float>("workSpeedMultiplier");
        private ElementConverter[] converters = Array.Empty<ElementConverter>();
        private Action<float>[] conversionHandlers = Array.Empty<Action<float>>();
        private EnergyGenerator generator;
        private Storage industrialStorage;
        private Workable industrialWorkable;
        private BuildingElementEmitter industrialEmitter;
        private float originalEmitRate;
        private bool industrialInputs;
        private bool preparingIndustrial;
        private bool industrialInitialized;
        private bool industrialLoaded;
        private bool lastPumpPermission = true;
        private float lastGeneratorHeatFactor = -1f;
        private readonly ProductionIntervalCredit conversionCredit = new ProductionIntervalCredit();
        internal bool GeneratorAdvancing { get; set; }
        public bool IsNonBatch { get { return NumericProfile != null && NumericProfile.Kind != ProductionKind.Batch; } }
        public bool IsFuelGenerator { get { return NumericProfile != null && NumericProfile.Kind == ProductionKind.Generator; } }
        public bool IsOilWell { get { return NumericProfile != null && NumericProfile.Kind == ProductionKind.OilWell; } }
        public bool IsOreScrubber { get { return NumericProfile != null && NumericProfile.Kind == ProductionKind.OreScrubber; } }
        internal bool IndustrialHasInputs { get { return industrialInputs; } }
        internal bool IndustrialTemperatureAllowed { get { return modeConnectionReady && runDecision.AllowsProduction; } }
        internal bool IndustrialProductionActive
        {
            get
            {
                if (!Spawned || !industrialInputs || !IndustrialTemperatureAllowed || IsBrokenDown || IsManuallyDisabled
                    || !AutomationAllowsProduction || !IsPowered) return false;
                return IsOreScrubber ? industrialWorkable != null && industrialWorkable.worker != null
                    : operational != null && operational.IsActive;
            }
        }

        private void InitializeIndustrial()
        {
            if (!IsNonBatch) return;
            converters = GetComponents<ElementConverter>();
            generator = GetComponent<EnergyGenerator>();
            industrialStorage = GetComponent<Storage>();
            industrialWorkable = IsOreScrubber ? (Workable)GetComponent<OreScrubber.Work>() : GetComponent<OilRefinery.WorkableTarget>();
            industrialEmitter = PrefabId == "FertilizerMaker" ? GetComponent<BuildingElementEmitter>() : null;
            if (industrialEmitter != null) originalEmitRate = industrialEmitter.emitRate;
            cachedRecipeId = PrefabId;
            cachedRecipeZone = TempZone.Celsius(NumericProfile.BestMinCelsius,NumericProfile.BestMaxCelsius);
            ResolveCachedZone(PrefabId);
            conversionHandlers = new Action<float>[converters.Length];
            for (int i=0;i<converters.Length;i++)
            {
                ElementConverter converter = converters[i];
                conversionHandlers[i] = mass => NoteConversion(converter,mass);
                converter.onConvertMass += conversionHandlers[i];
            }
            industrialInitialized = true;
            if (!IsOreScrubber && !IsFuelGenerator && converters.Length == 0 || IsFuelGenerator && generator == null)
                ModLog.Error(PrefabId+"：缺少原版生产组件，无法完成工业接入。");
        }

        private void CleanupIndustrial()
        {
            for (int i=0;i<conversionHandlers.Length;i++)
                if (converters[i] != null) converters[i].onConvertMass -= conversionHandlers[i];
        }

        void ISim200ms.Sim200ms(float dt)
        {
            if (!Spawned || !IsNonBatch) return;
            // 接入自检（I01 用）：非批次建筑的温控需求、加热、磨损与状态栏全部由这条
            // ISim200ms 驱动。若日志里始终没有这一行，说明原版没有把组件注册进调度，
            // 后续游戏验收必须按「组件未调度」排查，而不是当成温控数值问题。
            ModLog.InfoOnce("industrial|sim|" + PrefabId,
                PrefabId + "：非批次工业组件已收到 Sim200ms，温度门控、加热需求、磨损与状态刷新已开始工作。");
            PrepareIndustrialStep();
            ClearDamageWhenFullyRepaired();
            UpdateCoolingTrend(dt);
            if (modeConnectionReady) UpdatePowerAndHeating(dt);
            else { powerDemand=default(PowerDemand); currentRequestedWatts=0f; currentUsedWatts=0f; }
            TickDamage();
            UpdateRunStatusItem();
            UpdateMaterialRiskStatusItems();
            UpdateModeStatusItem();
        }

        /// <summary>各实际生产入口也调用，防止SIM更新顺序造成一帧越温生产；此方法不注热、不计磨损。</summary>
        public void PrepareIndustrialStep()
        {
            if (!Spawned || !IsNonBatch || !industrialInitialized || preparingIndustrial) return;
            preparingIndustrial = true;
            try
            {
                RefreshZoneForMaterial();
                RefreshModeFromOutputConnection();
                RefreshIndustrialInputs();
                if (modeConnectionReady && !industrialLoaded)
                {
                    industrialLoaded = true;
                    runState = RunController.OnLoaded(BodyTemperature,cachedZone,Policy,runState);
                }
                UpdateIndustrialRunState();
                bool allow = IndustrialTemperatureAllowed;
                productionPausedByTemperature = !allow;
                if (IsOilWell)
                {
                    // 只触发原抽油条件重检，绝不设置全机温度Flag，以便原泄压任务继续工作。
                    if (!allow) SetOperationalActive(false);
                    if (lastPumpPermission != allow)
                    {
                        lastPumpPermission = allow;
                        Trigger((int)GameHashes.OnStorageChange);
                    }
                }
                else if (operational != null)
                {
                    operational.SetFlag(TemperatureFlag,allow);
                    if (!allow) SetOperationalActive(false);
                }
                if (IsFuelGenerator && Math.Abs(lastGeneratorHeatFactor-CurrentEfficiency) > .001f)
                {
                    lastGeneratorHeatFactor = CurrentEfficiency;
                    MarkStructureTemperatureDirty();
                }
                UpdateTemperatureZoneSignal();
            }
            finally { preparingIndustrial = false; }
        }

        private void RefreshIndustrialInputs()
        {
            industrialInputs = false;
            if (IsFuelGenerator)
            {
                if (generator == null || industrialStorage == null || generator.formula.inputs == null) return;
                bool any = false;
                foreach (var input in generator.formula.inputs)
                {
                    any = true;
                    if (industrialStorage.GetMassAvailable(input.tag) < input.consumptionRate * .2f * IndustrialEfficiency) return;
                }
                industrialInputs = any;
            }
            else if (IsOreScrubber)
            {
                industrialInputs = industrialWorkable != null && industrialWorkable.worker != null && industrialStorage != null
                    && industrialStorage.GetMassAvailable(SimHashes.ChlorineGas.CreateTag()) > 0f;
            }
            else
            {
                foreach (ElementConverter converter in converters)
                    if (converter.HasEnoughMassToStartConverting()) { industrialInputs=true; break; }
            }
        }

        private void UpdateIndustrialRunState()
        {
            runDecision = RunController.Update(BodyTemperature,cachedZone,Policy,runState);
            if (industrialInputs && modeConnectionReady && runDecision.ZoneConfigured)
            {
                runState = runDecision.Next;
                SyncRunStateFields();
            }
            CurrentState = runDecision.State;
            CurrentEfficiency = runDecision.Efficiency;
        }

        /// <summary>仅温度系数，不改写原版WorkSpeed或机器属性，不因暂停返回0而伪造“材料齐全”。</summary>
        public float IndustrialEfficiency
        {
            get
            {
                if (!Spawned || !IsNonBatch || !cachedZoneConfigured) return 1f;
                return RunController.Update(BodyTemperature,cachedZone,Policy,runState).Efficiency;
            }
        }

        private void NoteConversion(ElementConverter converter,float consumedMass)
        {
            if (!IndustrialTemperatureAllowed || IsBrokenDown || IsManuallyDisabled) return;
            float nominal=0f;
            foreach (var input in converter.consumedElements)
                if (input.IsActive) nominal+=input.MassConsumptionRate;
            if (industrialEmitter != null && nominal > 0f)
            {
                // 独立排放器改为跟随实际耗料结算，缺料、堵塞和部分转换时不额外生成副产物。
                int cell=Grid.OffsetCell(Grid.PosToCell(this),new CellOffset((int)industrialEmitter.modifierOffset.x,(int)industrialEmitter.modifierOffset.y));
                SimMessages.AddRemoveSubstance(cell,ElementLoader.GetElementIndex(industrialEmitter.element),null,
                    consumedMass/nominal*originalEmitRate,industrialEmitter.temperature,industrialEmitter.emitDiseaseIdx,0);
            }
            // GetSpeedMultiplier的实际结果包含原版机械属性和其他修改，用同一结果反解真实时间。
            float speed = IndustrialPatchAccess.GetConverterSpeed(converter);
            float seconds = ProductionAccounting.ConversionSeconds(consumedMass,nominal,speed);
            double now = GameClock.Instance.GetTime();
            AccumulateProduction(conversionCredit.Add(now,seconds));
        }

        public void NoteGeneratorProduction(float realDt)
        {
            if (IndustrialProductionActive) AccumulateProduction(realDt);
        }

        public string IndustrialStatusTooltip()
        {
            string key="STRINGS.UI.BUILDINGEFFECTS.MATERIALOVERHAUL.INDUSTRIAL_STATUS";
            string text=string.Format(LocalizedOr(key,"建筑温度：{0}\n温度效率：{1}\n耗电：{2}（需求：{3}）"),
                GameUtil.GetFormattedTemperature(BodyTemperature),GameUtil.GetFormattedPercent(CurrentEfficiency*100f),
                GameUtil.GetFormattedWattage(CurrentUsedWatts),GameUtil.GetFormattedWattage(CurrentRequestedWatts));
            if (generator != null)
                text += "\n"+string.Format(LocalizedOr("STRINGS.UI.BUILDINGEFFECTS.MATERIALOVERHAUL.GENERATION_STATUS","额定发电：{0} · 当前发电：{1}"),
                    GameUtil.GetFormattedWattage(generator.BaseWattageRating),GameUtil.GetFormattedWattage(IndustrialProductionActive ? generator.WattageRating : 0f));
            return text+"\n"+RunStatusText.ZoneDetailText(this)+"\n"+HardnessText();
        }
    }

    internal static class IndustrialPatchAccess
    {
        internal static readonly Func<ElementConverter,float> GetConverterSpeed =
            AccessTools.MethodDelegate<Func<ElementConverter,float>>(AccessTools.Method(typeof(ElementConverter),"GetSpeedMultiplier"));
    }
}
