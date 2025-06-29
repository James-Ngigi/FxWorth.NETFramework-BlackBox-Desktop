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

        // RSI Enhanced robustness fields
        private DateTime lastCalculationAttempt = DateTime.MinValue;
        private int calculationFailureCount = 0;
        private const int MAX_CALCULATION_FAILURES = 5;
        private const int RETRY_DELAY_MS = 3000;
        private bool isCalculating = false;

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
            try
            {
                if (candlesResponse == null || candlesResponse.Count == 0)
                {
                    return;
                }

                var per = GetPeriod();
                int since = Math.Max(0, candlesResponse.Count - per);
                int count = Math.Min(per, candlesResponse.Count);
                
                // Validate we have enough data
                if (count < Period + 1) // Need at least Period + 1 for RSI calculation
                {
                    return;
                }

                cache = candlesResponse.GetRange(since, count);
                
                // Validate cache data quality
                if (!ValidateCacheData())
                {
                    return;
                }
                Calculate();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error in RSI HandleSnapshot");
                calculationFailureCount++;
            }
        }

        /// <summary>
        /// Validates the quality of cached candle data
        /// </summary>
        private bool ValidateCacheData()
        {
            if (cache == null || cache.Count == 0)
                return false;

            // Check for valid price data
            foreach (var candle in cache)
            {
                if (candle == null || candle.close <= 0 || double.IsNaN(candle.close) || double.IsInfinity(candle.close))
                {
                    return false;
                }
            }

            // Check for reasonable timestamp progression
            if (cache.Count > 1)
            {
                for (int i = 1; i < cache.Count; i++)
                {
                    if (cache[i].epoch <= cache[i - 1].epoch)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Calculates the RSI value based on the cached candle data.
        /// Updates the current RSI value, previous value, and timestamp.
        /// Checks for crossovers and raises the `Crossover` event if a crossover occurs.
        /// Enhanced with comprehensive error handling and validation.
        /// </summary>
        private void Calculate()
        {
            if (isCalculating)
            {
                return;
            }

            try
            {
                isCalculating = true;
                lastCalculationAttempt = DateTime.Now;

                // Enhanced validation
                if (cache == null || cache.Count == 0)
                {
                    return;
                }

                int totalCount = cache.Count;
                
                // Ensure we have enough data points for RSI calculation
                if (totalCount < Period + 1)
                { 
                    return;
                }

                var closes = cache.Select(x => x.close).ToArray();
                
                // Validate closes array
                if (closes.Any(c => double.IsNaN(c) || double.IsInfinity(c) || c <= 0))
                {
                    return;
                }

                var startIndex = 0;
                var endIndex = totalCount - 1;

                // Ensure we don't exceed array bounds
                if (endIndex < Period)
                {
                    return;
                }

                double[] rsiValues = new double[totalCount];
                
                // Call TA-Lib RSI function with enhanced error checking
                var retCode = Core.Rsi(
                    startIndex,
                    endIndex,
                    closes,
                    Period,
                    out var outBegIdx,
                    out var outNbElement,
                    rsiValues);

                // Check TA-Lib return code
                if (retCode != Core.RetCode.Success)
                {
                    calculationFailureCount++;
                    return;
                }

                // Validate output parameters
                if (outNbElement <= 0 || rsiValues == null)
                {
                    calculationFailureCount++;
                    return;
                }

                // Calculate the correct index for the RSI value
                int rsiIndex = outNbElement - 1; // Last calculated RSI value

                // Validate RSI index bounds
                if (rsiIndex < 0 || rsiIndex >= rsiValues.Length)
                {
                    calculationFailureCount++;
                    return;
                }

                PreviousValue = Value;
                Value = rsiValues[rsiIndex];

                // Validate calculated RSI value
                if (double.IsNaN(Value) || double.IsInfinity(Value))
                {
                    calculationFailureCount++;
                    return;
                }

                // Additional range validation for RSI (should be 0-100)
                if (Value < 0 || Value > 100)
                {
                    logger.Warn($"RSI Calculate: RSI value out of expected range: {Value}");
                    // Don't return here as some edge cases might produce values slightly outside 0-100
                }

                // Update the timestamp with the epoch of the last candle in the cache.
                Timestamp = cache.Last().epoch;

                // Reset failure count on successful calculation
                calculationFailureCount = 0;

                // If the previous RSI value is NaN, skip crossover checks.
                if (double.IsNaN(PreviousValue))
                {
                    return;
                }

                // Check for overbought crossover.
                if (Value >= Overbought && PreviousValue < Overbought)
                {
                    //logger.Info($"RSI Crossover: Overbought signal - RSI: {Value:F2} crossed above {Overbought}");
                    Crossover?.Raise(this, EventArgs.Empty);
                }
                // Check for oversold crossover.
                else if (Value <= Oversold && PreviousValue > Oversold)
                {
                    //logger.Info($"RSI Crossover: Oversold signal - RSI: {Value:F2} crossed below {Oversold}");
                    Crossover?.Raise(this, EventArgs.Empty);
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error in RSI Calculate method");
                calculationFailureCount++;
                
                // If we have too many failures, reset the indicator
                if (calculationFailureCount >= MAX_CALCULATION_FAILURES)
                {
                    logger.Error($"RSI Calculate: Too many calculation failures ({calculationFailureCount}), resetting indicator");
                    Reset();
                }
            }
            finally
            {
                isCalculating = false;
            }
        }

        /// <summary>
        /// Handles a real-time update with a new candle. 
        /// Updates the cached candles list, removing the oldest candle and adding the new candle. 
        /// Recalculates the RSI value after the update.
        /// Enhanced with better validation and error handling.
        /// <param name="candle">The new candle representing the latest price data.</param>
        public void HandleUpdate(Candle candle)
        {
            try
            {
                if (candle == null)
                {
                    return;
                }

                // Validate candle data
                if (candle.close <= 0 || double.IsNaN(candle.close) || double.IsInfinity(candle.close))
                { 
                    return;
                }

                if (cache == null || cache.Count == 0)
                {
                    return;
                }

                // Check if this is an update to the last candle or a new candle
                if (cache[cache.Count - 1].epoch == candle.epoch)
                {
                    // Update existing candle
                    cache[cache.Count - 1] = candle;
                }
                else
                {
                    // Add new candle and remove oldest if cache is at capacity
                    if (cache.Count >= GetPeriod())
                    {
                        cache.RemoveAt(0);
                    }
                    cache.Add(candle);
                }

                // Only calculate if we have sufficient data
                if (cache.Count >= Period + 1)
                {
                    Calculate();
                }
                else
                {
                    logger.Debug($"RSI HandleUpdate: Insufficient data for calculation - have {cache.Count}, need {Period + 1}");
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error in RSI HandleUpdate");
            }
        }

        /// <summary>
        /// Resets the RSI indicator by clearing the cached candles, values queue, and resetting the current and previous RSI values to NaN.
        /// Enhanced with better logging and state management.
        /// </summary>
        public void Reset()
        {
            try
            {
                logger.Info("RSI Reset: Clearing indicator state");
                
                cache.Clear();
                values.Clear();
                Value = double.NaN;
                PreviousValue = double.NaN;
                Timestamp = 0;
                
                // Reset failure tracking
                calculationFailureCount = 0;
                lastCalculationAttempt = DateTime.MinValue;
                isCalculating = false;
                
                logger.Debug("RSI Reset: Complete");
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error in RSI Reset");
            }
        }

        /// <summary>
        /// NEW: Forces a recalculation attempt if RSI is currently NaN and enough time has passed
        /// </summary>
        public void ForceRecalculationIfNeeded()
        {
            try
            {
                if (double.IsNaN(Value) && 
                    cache != null && 
                    cache.Count >= Period + 1 && 
                    (DateTime.Now - lastCalculationAttempt).TotalMilliseconds > RETRY_DELAY_MS)
                {
                    Calculate();
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error in RSI ForceRecalculationIfNeeded");
            }
        }

        /// <summary>
        /// NEW: Gets diagnostic information about the RSI state
        /// </summary>
        public string GetDiagnosticInfo()
        {
            return $"RSI Diagnostic - Value: {Value}, Cache Count: {cache?.Count ?? 0}, " +
                   $"Period: {Period}, Failures: {calculationFailureCount}, " +
                   $"Last Calculation: {lastCalculationAttempt}, Is Calculating: {isCalculating}";
        }
    }
}