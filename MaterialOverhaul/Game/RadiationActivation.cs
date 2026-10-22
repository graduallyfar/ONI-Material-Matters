using System;
using KSerialization;
using MaterialOverhaul.Core;
using MaterialOverhaul.Radiation;
using UnityEngine;

namespace MaterialOverhaul.Game
{
    /// <summary>
    /// 建筑（含建造砖块）的活化度：长期处在超过门槛的辐射中会累积，离开后按半衰期衰减，
    /// 达到下限后建筑自身成为一个小辐射源（设计方案 §8.2、§9）。只增加放射，不改变材料性能。
    ///
    /// 组件本身不参加模拟更新：受照采样与步进由 <see cref="RadiationInducedMonitor"/> 统一完成，
    /// 只有处在强辐射中或仍带活化度的建筑会被处理，不在每次更新中遍历全部建筑。
    ///
    /// 活化度随存档保存。自身放射源是运行时添加的原版 <c>RadiationEmitter</c>，不进存档，
    /// 读档后按活化度重建。原版自带辐射源的建筑（研究反应堆、辐射灯等）不参与活化。
    /// 旧存档没有本组件数据时活化度为 0；卸载本 Mod 后原版读档会按长度跳过本组件。
    /// </summary>
    [SerializationConfig(MemberSerialization.OptIn)]
    public class RadiationActivation : KMonoBehaviour
    {
        [Serialize] private float activation;

        private RadiationEmitter emitter;
        private float currentEmitRads;
        private int sourceCell = -1;
        private Guid statusHandle = Guid.Empty;

        /// <summary>当前活化度（rads/周期）。</summary>
        public float Activation
        {
            get { return activation; }
        }

        /// <summary>当前自身放射强度（rads/周期）；低于下限时为 0。</summary>
        public float CurrentEmitRads
        {
            get { return currentEmitRads; }
        }

        /// <summary>是否参与活化（原版自带辐射源的建筑不参与）。</summary>
        public bool Eligible { get; private set; }

        /// <summary>主建造材料的活化系数。</summary>
        public float Fraction { get; private set; }

        /// <summary>占用格数；多格建筑的受照按格平均。</summary>
        public int CellCount { get; private set; }

        protected override void OnSpawn()
        {
            base.OnSpawn();

            // 运行时添加的放射源不进存档，此刻存在的 RadiationEmitter 一定是原版配置带来的。
            Eligible = GetComponent<RadiationEmitter>() == null;
            Fraction = ResolveFraction();
            Building building = GetComponent<Building>();
            int cells = building != null && building.PlacementCells != null ? building.PlacementCells.Length : 1;
            CellCount = cells > 0 ? cells : 1;

            if (!Eligible)
            {
                activation = 0f;
                return;
            }

            if (activation > 0f)
            {
                RadiationInducedMonitor.Register(this);
                UpdateEmission(force: true);
            }
        }

        protected override void OnCleanUp()
        {
            RadiationInducedMonitor.Unregister(this);
            if (currentEmitRads > 0f)
            {
                RadiationInducedMonitor.MoveSource(sourceCell, currentEmitRads, -1, 0f);
            }

            currentEmitRads = 0f;
            SetStatus(false);
            base.OnCleanUp();
        }

        /// <summary>按本轮受到的超额辐射（已扣门槛与活化物自身贡献）步进活化度。</summary>
        public void ApplyStep(float excessRads, float dtSeconds)
        {
            if (!Eligible)
            {
                return;
            }

            activation = ActivationModel.Step(activation, excessRads, Fraction, dtSeconds);
            if (ActivationModel.CanDrop(activation, excessRads))
            {
                activation = 0f;
                RadiationInducedMonitor.Unregister(this);
            }
            else
            {
                RadiationInducedMonitor.Register(this);
            }

            UpdateEmission(force: false);
        }

        private void UpdateEmission(bool force)
        {
            float next = ActivationModel.EmitRads(activation);
            int cell = Grid.PosToCell(this);
            if (!force && cell == sourceCell && !ActivationModel.NeedsRefresh(currentEmitRads, next))
            {
                return;
            }

            RadiationInducedMonitor.MoveSource(sourceCell, currentEmitRads, cell, next);
            sourceCell = next > 0f ? cell : -1;
            currentEmitRads = next;
            ApplyEmitter(next);
            SetStatus(next > 0f);
        }

        private void ApplyEmitter(float rads)
        {
            try
            {
                if (emitter == null)
                {
                    if (!(rads > 0f))
                    {
                        return;
                    }

                    emitter = gameObject.AddComponent<RadiationEmitter>();
                    emitter.emitType = RadiationEmitter.RadiationEmitterType.Constant;
                    emitter.radiusProportionalToRads = false;
                    emitter.emitRadiusX = (short)RadiationBalance.EmitRadiusCells;
                    emitter.emitRadiusY = (short)RadiationBalance.EmitRadiusCells;
                    emitter.emissionOffset = Vector3.zero;
                    emitter.emitRads = rads;
                    ModLog.InfoOnce("radiation|emitter", "感生放射：已为活化建筑挂上自身放射源（" + name + "，"
                        + rads.ToString("0") + " rads/周期）。");
                    return;
                }

                emitter.emitRads = rads;
                emitter.Refresh();
            }
            catch (Exception e)
            {
                ModLog.WarnOnce("radiation|emitter|error", "感生放射：设置自身放射源失败，活化建筑不会放射：" + e.Message);
            }
        }

        private void SetStatus(bool show)
        {
            KSelectable selectable = GetComponent<KSelectable>();
            if (selectable == null || RadiationStatusItems.Induced == null)
            {
                return;
            }

            if (show && statusHandle == Guid.Empty)
            {
                statusHandle = selectable.AddStatusItem(RadiationStatusItems.Induced, this);
            }
            else if (!show && statusHandle != Guid.Empty)
            {
                selectable.RemoveStatusItem(statusHandle);
                statusHandle = Guid.Empty;
            }
        }

        private float ResolveFraction()
        {
            PrimaryElement primary = GetComponent<PrimaryElement>();
            Element element = primary == null ? null : primary.Element;
            if (element == null)
            {
                return ActivationMaterial.MineralDefault;
            }

            return ActivationMaterial.FractionFor(element.id.ToString(), CategoryOf(element));
        }

        private static ActivationCategory CategoryOf(Element element)
        {
            if (element.HasTag(GameTags.Metal) || element.HasTag(GameTags.RefinedMetal))
            {
                return ActivationCategory.Metal;
            }

            if (element.HasTag(GameTags.Plastic) || element.id == SimHashes.WoodLog)
            {
                return ActivationCategory.Organic;
            }

            return ActivationCategory.Mineral;
        }
    }
}
