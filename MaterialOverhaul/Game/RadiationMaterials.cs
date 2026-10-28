using MaterialOverhaul.Radiation;

namespace MaterialOverhaul.Game
{
    /// <summary>按原版元素判定显现分类、相态与是否本身就是来源；建筑、格子、管道、物品共用。</summary>
    public static class RadiationMaterials
    {
        /// <summary>元素的显现参数；缺元素时按矿物。</summary>
        public static ManifestProfile ProfileOf(Element element)
        {
            if (element == null)
            {
                return ManifestMaterial.MineralDefault;
            }

            return ManifestMaterial.For(element.id.ToString(), CategoryOf(element));
        }

        public static MaterialCategory CategoryOf(Element element)
        {
            if (element == null)
            {
                return MaterialCategory.Mineral;
            }

            if (element.IsLiquid || element.IsGas)
            {
                return MaterialCategory.Fluid;
            }

            if (element.HasTag(GameTags.Metal) || element.HasTag(GameTags.RefinedMetal))
            {
                return MaterialCategory.Metal;
            }

            if (element.HasTag(GameTags.Plastic) || element.id == SimHashes.WoodLog)
            {
                return MaterialCategory.Organic;
            }

            return MaterialCategory.Mineral;
        }

        public static CellPhase PhaseOf(Element element)
        {
            if (element == null || element.IsVacuum)
            {
                return CellPhase.Vacuum;
            }

            if (element.IsGas)
            {
                return CellPhase.Gas;
            }

            return element.IsLiquid ? CellPhase.Liquid : CellPhase.Solid;
        }

        /// <summary>能否累积：真空不累积；原版放射物质（铀矿、核废料等）本身就是来源，不累积。</summary>
        public static bool CanAccumulate(Element element)
        {
            return element != null && !element.IsVacuum && ContaminationModel.CanContaminate(element.radiationPer1000Mass);
        }

        /// <summary>元素在累积表中的身份。</summary>
        public static int IdentityOf(Element element)
        {
            return element == null ? 0 : AccumulationTable.ElementIdentity((int)element.id);
        }
    }
}
