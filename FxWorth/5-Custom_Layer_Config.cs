namespace FxWorth
{
    /// <summary>
    /// The `CustomLayerConfig` class represents the configuration settings for a custom layer in a trading strategy.
    /// </summary>
    public class CustomLayerConfig
    {
        public int LayerNumber { get; set; }
        public int? HierarchyLevels { get; set; }
        public int? MaxHierarchyDepth { get; set; }
        public decimal? InitialStake { get; set; }
        public int? MartingaleLevel { get; set; }
        public decimal? MaxDrawdown { get; set; }
        /// <summary>
        /// Desired ROI percentage for this layer (legacy barrier offset input).
        /// </summary>
        public decimal? BarrierOffset { get; set; } 
    }
}