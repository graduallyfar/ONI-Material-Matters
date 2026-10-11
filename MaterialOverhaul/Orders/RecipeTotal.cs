namespace MaterialOverhaul.Orders
{
    /// <summary>
    /// 某个配方在订单表里的总需求，用于与原版「按配方的计数字典」对齐
    /// （原版界面的数量、预取与可用性判断都读原版计数字典，因此本 Mod 的订单表必须能折算回它）。
    ///
    /// 有限订单求和；存在无限订单时按无限口径处理，不再看有限数量。
    /// </summary>
    public struct RecipeTotal
    {
        /// <summary>有限订单的批次总数。</summary>
        public int Batches;

        /// <summary>该配方是否存在无限订单。</summary>
        public bool Infinite;

        /// <summary>该配方在本 Mod 订单表中是否没有需求。</summary>
        public bool IsEmpty
        {
            get { return !Infinite && Batches <= 0; }
        }

        public override string ToString()
        {
            return Infinite ? "RecipeTotal[无限]" : "RecipeTotal[" + Batches + " 批]";
        }
    }
}
