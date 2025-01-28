namespace FxApi
{
    /// <summary>
    /// The `Indices` class acts as a container for multiple technical indicators used in the trading system. 
    /// Currently, it holds instances of the `Rsi` (Relative Strength Index) indicator.
    /// This class provides a convenient way to group and manage these indicators together, simplifying their access and usage within the application's trading logic.
    /// </summary>
    public class Indices
    {
        /// An instance of the `Rsi` indicator, used to measure momentum and identify overbought/oversold conditions.
        public Rsi Rsi { get; set; } = new Rsi();
    }
}