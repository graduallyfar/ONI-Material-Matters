using System;
using KSerialization;
using MaterialOverhaul.Core;
using MaterialOverhaul.Radiation;
using UnityEngine;

namespace MaterialOverhaul.Game
{
    /// <summary>
    /// 建筑（含建造砖块）的累积量：受到的照射累积起来，按半衰期衰减；超过主建造材料的显现限度后
    /// 建筑自身开始放射，成为一个小辐射源。只增加放射，不改变材料性能。
    ///
    /// 组件本身不参加模拟更新：受照采样与步进由 <see cref="RadiationInducedMonitor"/> 统一完成，
    /// 只有正在受照或仍带累积量的建筑会被处理，不在每次更新中遍历全部建筑。
    /// 建筑自身的放射不计入它自己的累积；显现建筑之间可以互相照射。
    ///
    /// 累积量随存档保存。自身放射源是运行时添加的原版 <c>RadiationEmitter</c>，不进存档，
    /// 读档后按累积量重建。原版自带辐射源的建筑（研究反应堆、辐射灯等）本身就是来源，不累积。
    /// 类名沿用第一版，旧存档里的活化度（自身放射强度）读档时折算成累积量。
    /// </summary>
    [SerializationConfig(MemberSerialization.OptIn)]
    public class RadiationActivation : KMonoBehaviour
    {
        [Serialize] private float accumulation;

        /// <summary>第一版存档的活化度（rads/周期，即当时的自身放射）；读档时折算成累积量后清零。</summary>
        [Serialize] private float activation;

        private RadiationEmitter emitter;
        private float currentEmitRads;
        private int emitCell = -1;
        private Guid manifestHandle = Guid.Empty;
        private Guid accumulatingHandle = Guid.Empty;

        /// <summary>当前累积量（rads）。</summary>
        public float Accumulation
        {
            get { return accumulation; }
        }

        /// <summary>当前自身放射强度（rads/周期）；未显现或不到下限时为 0。</summary>
        public float CurrentEmitRads
        {
            get { return currentEmitRads; }
        }

        /// <summary>自身放射源所在格；不放射时为 -1。</summary>
        public int EmitCell
        {
            get { return emitCell; }
        }

        /// <summary>是否参与累积（原版自带辐射源的建筑不参与）。</summary>
        public bool Eligible { get; private set; }

        /// <summary>主建造材料的显现限度（rads）。</summary>
        public float Limit { get; private set; }

        /// <summary>主建造材料显现后的放射比例。</summary>
        public float Fraction { get; private set; }

        /// <summary>占用格数；多格建筑的受照按格平均。</summary>
        public int CellCount { get; private set; }

        protected override void OnSpawn()
        {
            base.OnSpawn();

            // 运行时添加的放射源不进存档，此刻存在的 RadiationEmitter 一定是原版配置带来的。
            Eligible = GetComponent<RadiationEmitter>() == null;
            ManifestProfile profile = RadiationMaterials.ProfileOf(PrimaryElementOf(gameObject));
            Limit = profile.Limit;
            Fraction = profile.Fraction;
            Building building = GetComponent<Building>();
            int cells = building != null && building.PlacementCells != null ? building.PlacementCells.Length : 1;
            CellCount = cells > 0 ? cells : 1;

            if (!Eligible)
            {
                accumulation = 0f;
                activation = 0f;
                return;
            }

            if (activation > 0f)
            {
                if (!(accumulation > 0f))
                {
                    accumulation = AccumulationModel.AccumulationForEmission(activation, Limit, Fraction);
                    ModLog.InfoOnce("radiation|migrate", "辐射：旧存档的活化度已按自身放射折算成累积量。");
                }

                activation = 0f;
            }

            accumulation = AccumulationModel.Clamp(accumulation);
            if (accumulation > 0f)
            {
                RadiationInducedMonitor.Register(this);
                UpdateEmission(force: true);
            }
        }

        protected override void OnCleanUp()
        {
            RadiationInducedMonitor.Unregister(this);
            currentEmitRads = 0f;
            emitCell = -1;
            SetStatus(ref manifestHandle, RadiationStatusItems.Manifest, false);
            SetStatus(ref accumulatingHandle, RadiationStatusItems.Accumulating, false);
            base.OnCleanUp();
        }

        /// <summary>按本轮受到的平均照射（已扣掉自身放射）步进累积量。</summary>
        public void ApplyStep(float rads, float dtSeconds)
        {
            if (!Eligible)
            {
                return;
            }

            accumulation = AccumulationModel.Step(accumulation, rads, dtSeconds);
            if (AccumulationModel.CanDrop(accumulation, rads))
            {
                accumulation = 0f;
                RadiationInducedMonitor.Unregister(this);
            }
            else
            {
                RadiationInducedMonitor.Register(this);
            }

            UpdateEmission(force: false);
        }

        /// <summary>辐射粒子撞击：一次性存入累积量。</summary>
        public void AddAccumulation(float rads)
        {
            if (!Eligible || !(rads > 0f))
            {
                return;
            }

            accumulation = AccumulationModel.Deposit(accumulation, rads);
            RadiationInducedMonitor.Register(this);
            UpdateEmission(force: false);
        }

        /// <summary>辐射粒子发生器付账：累积量按「吸收量 ÷ 放射比例」扣减。</summary>
        public void Drain(float absorbedRads)
        {
            if (!Eligible || !(absorbedRads > 0f))
            {
                return;
            }

            accumulation = AccumulationModel.Drain(accumulation, absorbedRads, Fraction);
            UpdateEmission(force: false);
        }

        private void UpdateEmission(bool force)
        {
            float next = AccumulationModel.EmitRads(accumulation, Limit, Fraction);
            int cell = Grid.PosToCell(this);
            if (force || (next > 0f && cell != emitCell) || AccumulationModel.NeedsRefresh(currentEmitRads, next))
            {
                currentEmitRads = next;
                emitCell = next > 0f ? cell : -1;
                ApplyEmitter(next);
            }

            SetStatus(ref manifestHandle, RadiationStatusItems.Manifest, currentEmitRads > 0f);
            SetStatus(ref accumulatingHandle, RadiationStatusItems.Accumulating,
                !(currentEmitRads > 0f) && accumulation >= Limit * RadiationBalance.AccumulatingStatusFraction);
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
                    ModLog.InfoOnce("radiation|emitter", "辐射显现：已为显现建筑挂上自身放射源（" + name + "，"
                        + rads.ToString("0.#") + " rads/周期）。");
                    return;
                }

                emitter.emitRads = rads;
                emitter.Refresh();
            }
            catch (Exception e)
            {
                ModLog.WarnOnce("radiation|emitter|error", "辐射显现：设置自身放射源失败，显现建筑不会放射：" + e.Message);
            }
        }

        private void SetStatus(ref Guid handle, StatusItem item, bool show)
        {
            KSelectable selectable = GetComponent<KSelectable>();
            if (selectable == null || item == null)
            {
                return;
            }

            if (show && handle == Guid.Empty)
            {
                handle = selectable.AddStatusItem(item, this);
            }
            else if (!show && handle != Guid.Empty)
            {
                selectable.RemoveStatusItem(handle);
                handle = Guid.Empty;
            }
        }

        private static Element PrimaryElementOf(GameObject go)
        {
            PrimaryElement primary = go == null ? null : go.GetComponent<PrimaryElement>();
            return primary == null ? null : primary.Element;
        }
    }
}
