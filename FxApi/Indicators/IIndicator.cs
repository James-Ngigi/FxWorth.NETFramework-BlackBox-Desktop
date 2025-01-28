using System.Collections.Generic;

namespace FxApi
{
    /// <summary>
    /// The `IIndicator` interface defines a common set of methods and properties that must be implemented by 
    /// all technical indicators used in FxWorth. This interface ensures that all indicators 
    /// can be used consistently for market data analysis, signal generation, and integration with the trading logic.
    /// </summary>
    public interface IIndicator
    {
        /// The period (number of data points) used for indicator calculation. 
        int Period { get; set; }

        /// The previous calculated value of the indicator.
        /// This is used for comparison with the current value to detect trends and crossovers.
        double PreviousValue { get; set; }

        /// The current calculated value of the indicator. 
        /// This represents the latest indicator value based on the most recent market data.
        double Value { get; set; }

        /// The timestamp of the last data point used for indicator calculation. 
        /// This is usually the timestamp of the most recent candlestick or bar.
        int Timestamp { get; set; }

        /// The timeframe (in seconds) for which the indicator is calculated. 
        /// This represents the time interval of each data point (e.g., 1 minute, 5 minutes, 1 hour).
        int TimeFrame { get; set; }

        /// <summary>
        /// Calculates and returns the number of data points required for indicator calculation.
        /// This might be a multiple of the `Period` property or a fixed value, depending on the indicator's logic.
        /// <returns>The number of data points needed for indicator calculation.</returns>
        /// </summary>
        int GetPeriod();

        /// <summary>
        /// Handles the initial snapshot of historical market data (e.g., a list of candlesticks). 
        /// This method is called to initialize the indicator with historical data before real-time updates are received.
        /// <param name="candlesResponse">A list of `Candle` objects representing historical market data.</param>
        /// </summary>
        void HandleSnapshot(List<Candle> candlesResponse);

        /// <summary>
        /// Handles a real-time update with a new data point (e.g., a new candlestick). 
        /// This method updates the indicator's internal state and recalculates the indicator value based on the latest data.
        /// <param name="candle">A `Candle` object representing the latest market data point.</param>
        /// </summary>
        void HandleUpdate(Candle candle);

        /// Resets the indicator's internal state, clearing any cached data and resetting the indicator values. 
        /// This method is typically called when starting a new trading session or when switching to a different symbol.
        void Reset();
    }
}