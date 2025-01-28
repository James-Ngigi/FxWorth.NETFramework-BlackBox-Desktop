namespace FxWorth
{
    public class CustomLayerConfig
    {
        public int LayerNumber { get; set; }
        public int? HierarchyLevels { get; set; }
        public int? MaxHierarchyDepth { get; set; }
        public decimal? InitialStake { get; set; }
        public int? MartingaleLevel { get; set; }
        public decimal? MaxDrawdown { get; set; }
        public decimal? BarrierOffset { get; set; } 
    }
}