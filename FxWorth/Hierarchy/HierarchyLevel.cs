using System;

namespace FxWorth.Hierarchy
{
    /// <summary>
    /// The `HierarchyLevel` class represents a single level in the hierarchy of the trading strategy.
    /// Pure data structure - no calculation logic, only configuration and metadata.
    /// This is a legacy compatibility class used for backward compatibility with existing code.
    /// </summary>
    public class HierarchyLevel
    {
        public string LevelId { get; set; }
        public decimal AmountToRecover { get; set; }
        public decimal InitialStake { get; set; }
        public int? MartingaleLevel { get; set; }
        public decimal? MaxDrawdown { get; set; }
        public decimal? BarrierOffset { get; set; }
        public bool IsCompleted { get; set; }

        public HierarchyLevel(string levelId, decimal amountToBeRecovered, decimal initialStake, 
                             int? martingaleLevel, decimal? maxDrawdown, decimal? barrierOffset)
        {
            LevelId = levelId;
            AmountToRecover = amountToBeRecovered;
            InitialStake = initialStake;
            MartingaleLevel = martingaleLevel;
            MaxDrawdown = maxDrawdown;
            BarrierOffset = barrierOffset;
            IsCompleted = false;
        }

        public void Reset()
        {
            IsCompleted = false;
        }
    }
}
