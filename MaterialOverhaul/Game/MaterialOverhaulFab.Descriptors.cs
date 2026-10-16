using System.Collections.Generic;
using MaterialOverhaul.Patches;
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
            return descriptors;
        }
    }
}
