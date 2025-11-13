namespace FxApi
{
    /// <summary>
    /// The `Indices` class acts as a container for multiple technical indicators used in the trading system. 
    /// Currently, it holds instances of the `ATR_Dual_ROC` (Average True Range Dual Rate of Change) indicator.
    /// This class provides a convenient way to group and manage these indicators together, simplifying their access and usage within the application's trading logic.
    /// </summary>
    public class Indices
    {
        /// An instance of the `ATR_Dual_ROC` indicator, used to measure volatility patterns and identify expansion/compression conditions.
        public ATR_Dual_ROC AtrDualRoc { get; set; } = new ATR_Dual_ROC();
    }
}