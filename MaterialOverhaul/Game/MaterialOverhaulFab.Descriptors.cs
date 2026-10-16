using System.Collections.Generic;
using MaterialOverhaul.Patches;
using MaterialOverhaul.Core;
using UnityEngine;

namespace MaterialOverhaul.Game
{
    // 原版免疫过热的窑炉没有 Overheatable 描述器，工作范围由本组件提供。
    public partial class MaterialOverhaulFab : IGameObjectEffectDescriptor
    {
        public List<Descriptor> GetDescriptors(GameObject go)
        {
            var descriptors = new List<Descriptor>();
            float ceiling;
            if (Spawned && NumericProfile != null && NumericProfile.PreserveOverheatImmunity
                && TryGetHeatCeiling(out ceiling))
                Patch_Overheatable_GetDescriptors.AppendWorkTemperature(PrefabId, ceiling, descriptors, true);
            if (Spawned && IsNonBatch)
            {
                string line = string.Format(LocalizedOr("STRINGS.UI.BUILDINGEFFECTS.MATERIALOVERHAUL.PROCESS_BEST","最佳温度：{0} ~ {1}"),
                    GameUtil.GetFormattedTemperature(Temperatures.FromCelsius(NumericProfile.BestMinCelsius)),
                    GameUtil.GetFormattedTemperature(Temperatures.FromCelsius(NumericProfile.BestMaxCelsius)));
                descriptors.Add(new Descriptor(line,LocalizedOr("STRINGS.UI.BUILDINGEFFECTS.MATERIALOVERHAUL.PROCESS_BEST_TOOLTIP",
                    "建筑本体处于此范围时温度效率为100%。温度效率影响每秒处理量，实际运行仍需满足原版条件。"),Descriptor.DescriptorType.Effect));
            }
            return descriptors;
        }
    }
}
