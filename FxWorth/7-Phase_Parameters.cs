namespace FxWorth
{
    /// <summary>
    /// Class representing the parameters for a trading phase. Distingishes between the first phase and subsequent phase.
    public class PhaseParameters
    {
        public decimal Barrier { get; set; }
        public int MartingaleLevel { get; set; }
        public decimal MaxDrawdown { get; set; }
    }
}