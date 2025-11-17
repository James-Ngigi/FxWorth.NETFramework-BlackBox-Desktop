namespace FxWorth
{
    /// <summary>
    /// Class representing the parameters for a trading phase. Distingishes between the first phase and subsequent phase.
    public class PhaseParameters
    {
        /// <summary>
        /// Stores the desired win return percentage for this phase (formerly barrier offset input).
        /// </summary>
        public decimal Barrier { get; set; }
        public int MartingaleLevel { get; set; }
        public decimal MaxDrawdown { get; set; }
    }
}