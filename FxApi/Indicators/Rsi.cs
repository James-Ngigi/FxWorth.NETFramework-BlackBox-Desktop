using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using NLog;
using TicTacTec.TA.Library;

namespace FxApi
{
    /// <summary>
    /// The `Rsi` class implements the Relative Strength Index (RSI) technical indicator. It calculates the RSI value
    /// based on historical price data (candlesticks) and provides methods for handling real-time updates. 
    /// The RSI is a momentum oscillator that measures the magnitude of recent price changes to 
    /// evaluate overbought or oversold conditions in the price of a stock or other asset.
    /// It is used in this program to detect potential trading opportunities based on RSI crossovers of overbought and oversold thresholds.
    /// </summary>
    public class Rsi : IIndicator
    {
        /// Logger for recording RSI calculation details and crossover events.
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        /// The previous RSI value, used for comparison with the current value to detect crossovers.
        public double PreviousValue { get; set; }

        /// <summary>
        /// The period (number of candles) used for RSI calculation. 
        /// A typical period is 14, but it can be adjusted based on trading preferences.
        /// </summary>
        public int Period { get; set; }

        /// <summary>
        /// The overbought threshold for the RSI. 
        /// When the RSI value crosses above this threshold, it indicates a potentially overbought condition.
        /// </summary>
        public double Overbought { get; set; }

        /// <summary>
        /// The oversold threshold for the RSI. 
        /// When the RSI value crosses below this threshold, it indicates a potentially oversold condition.
        /// </summary>
        public double Oversold { get; set; }

        /// The current RSI value, calculated based on the specified period and historical price data.
        public double Value { get; set; } = double.NaN;

        /// The timestamp of the last candle used for RSI calculation. 
        public int Timestamp { get; set; }

        /// <summary>
        /// The delay (in periods) used for confirming RSI crossovers. 
        /// This helps filter out false signals and ensure that the crossover is sustained.
        /// This property is no longer used as the delay functionality has been removed.
        /// </summary>
        public int Delay { get; set; } // This property is retained for compatibility but is not used.

        /// <summary>
        /// A queue that stored the most recent RSI values, used for calculating the delayed crossover confirmation.
        /// This queue is no longer used as the delay functionality has been removed.
        /// </summary>
        Queue<double> values = new Queue<double>(); // This queue is retained for compatibility but is not used.

        /// <summary>
        /// The timeframe (in seconds) for the RSI calculation. 
        /// This is the time interval represented by each candle in the historical price data.
        /// </summary>
        public int TimeFrame { get; set; }

        /// <summary>
        /// A list that caches the most recent candles used for RSI calculation. 
        /// The number of candles cached is determined by the `GetPeriod` method.
        /// </summary>
        List<Candle> cache = new List<Candle>();

        /// <summary>
        /// Event raised when the RSI value crosses the overbought or oversold thresholds.
        /// This event signals a potential trading opportunity.
        /// </summary>
        [JsonIgnore]
        public EventHandler<EventArgs> Crossover;

        /// <summary>
        /// Calculates the number of candles required for RSI calculation based on the `Period` and a fixed multiplier (6).
        /// <returns>The number of candles needed for RSI calculation.</returns>
        /// </summary>
        public int GetPeriod()
        {
            return 6 * Period;
        }

        /// <summary>
        /// Handles the initial snapshot of historical candle data. 
        /// Caches the relevant candles and calculates the initial RSI value.
        /// Also raises the `Crossover` event if a crossover occurs immediately based on the initial data.
        /// <param name="candlesResponse">A list of candles representing historical price data.</param>
        /// </summary>
        public void HandleSnapshot(List<Candle> candlesResponse)
        {
            var per = GetPeriod();
            int since = Math.Max(0, candlesResponse.Count - per);
            int count = Math.Min(per, candlesResponse.Count);
            cache = candlesResponse.GetRange(since, count);
            Calculate();
        }

        /// <summary>
        /// Calculates the RSI value based on the cached candle data.
        /// Updates the current RSI value, previous value, and timestamp.
        /// Checks for crossovers and raises the `Crossover` event if a crossover occurs.
        /// The delay functionality has been removed, so crossovers are detected immediately.
        /// </summary>
        private void Calculate()
        {
            int totalCount = cache.Count;
            var closes = cache.Select(x => x.close).ToArray();

            var startIndex = 0;
            var endIndex = totalCount - 1;

            double[] rsiValues = new double[totalCount];
            Core.Rsi(
                startIndex,
                endIndex,
                closes,
                Period,
                out var _,
                out var _,
                rsiValues);

            PreviousValue = Value;
            Value = rsiValues[endIndex - Period];

            // Update the timestamp with the epoch of the last candle in the cache.
            Timestamp = cache.Last().epoch;

            // If the previous RSI value is NaN, skip crossover checks.
            if (double.IsNaN(PreviousValue))
            {
                return;
            }

            // Check for overbought crossover.
            if (Value >= Overbought && PreviousValue < Overbought)
            {
                Crossover?.Raise(this, EventArgs.Empty);
            }
            // Check for oversold crossover.
            else if (Value <= Oversold && PreviousValue > Oversold)
            {
                Crossover?.Raise(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Handles a real-time update with a new candle. 
        /// Updates the cached candles list, removing the oldest candle and adding the new candle. 
        /// Recalculates the RSI value after the update.
        /// <param name="candle">The new candle representing the latest price data.</param>
        /// </summary>
        public void HandleUpdate(Candle candle)
        {
            if (cache[cache.Count - 1].epoch == candle.epoch)
            {
                cache[cache.Count - 1] = candle;
            }
            else
            {
                cache.RemoveAt(0);
                cache.Add(candle);
            }
            Calculate();
        }

        /// <summary>
        /// Resets the RSI indicator by clearing the cached candles, values queue, and resetting the current and previous RSI values to NaN.
        /// </summary>
        public void Reset()
        {
            cache.Clear();
            values.Clear(); // Clearing the queue is not strictly necessary as it's not used, but it's good practice for consistency.
            Value = double.NaN;
            PreviousValue = double.NaN;
        }
    }
}