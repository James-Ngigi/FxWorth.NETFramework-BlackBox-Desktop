using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using NLog;
using TicTacTec.TA.Library;

namespace FxApi
{
    /// <summary>
    /// The `ATR_Dual_ROC` class implements a volatility-based signal generator using the "Sniper Model" strategy.
    /// It calculates ATR (Average True Range) values and then applies two Rate of Change (ROC) calculations
    /// to identify periods of volatility compression followed by volatility expansion.
    /// The system generates trading signals only when a short-term volatility expansion occurs
    /// immediately following a period of long-term volatility compression.
    /// </summary>
    public class ATR_Dual_ROC : IIndicator
    {
        /// Logger for recording ATR calculation details and crossover events.
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        /// The previous ATR value, used for comparison and ROC calculations.
        public double PreviousValue { get; set; }

        /// <summary>
        /// The period (number of candles) used for base ATR calculation. 
        /// Default is 2, but can be adjusted based on trading preferences.
        /// </summary>
        public int Period { get; set; } = 2;

        /// <summary>
        /// The percentage threshold for long-term ROC (ROC1) compression detection.
        /// When ROC1 falls below this threshold, the trigger becomes "armed".
        /// Default is 25.00%.
        /// </summary>
        public double CompressionThreshold { get; set; } = 25.00;

        /// <summary>
        /// The lookback period for calculating long-term ROC (ROC1).
        /// This defines how far back to look when calculating the compression ROC.
        /// Default is 10 periods.
        /// </summary>
        public int LongRocPeriod { get; set; } = 10;

        /// <summary>
        /// The percentage threshold for short-term ROC (ROC2) expansion detection.
        /// When ROC2 rises above this threshold while trigger is armed, a signal is fired.
        /// Default is 25.00%.
        /// </summary>
        public double ExpansionThreshold { get; set; } = 25.00;

        /// <summary>
        /// The lookback period for calculating short-term ROC (ROC2).
        /// This defines how far back to look when calculating the expansion ROC.
        /// Default is 3 periods.
        /// </summary>
        public int ShortRocPeriod { get; set; } = 3;

        /// The current ATR value, calculated based on the specified period and historical price data.
        public double Value { get; set; } = double.NaN;

        /// The timestamp of the last candle used for ATR calculation. 
        public int Timestamp { get; set; }

        /// <summary>
        /// The timeframe (in seconds) for the ATR calculation. 
        /// This is the time interval represented by each candle in the historical price data.
        /// </summary>
        public int TimeFrame { get; set; }

        /// <summary>
        /// A list that caches the most recent candles used for ATR calculation. 
        /// The number of candles cached is determined by the `GetPeriod` method.
        /// </summary>
        List<Candle> cache = new List<Candle>();

        /// <summary>
        /// Event raised when the ATR Dual ROC "Sniper Model" conditions are met.
        /// This event signals a potential trading opportunity based on volatility patterns.
        /// </summary>
        [JsonIgnore]
        public EventHandler<EventArgs> Crossover;

        // ATR Enhanced robustness fields
        private DateTime lastCalculationAttempt = DateTime.MinValue;
        private int calculationFailureCount = 0;
        private const int MAX_CALCULATION_FAILURES = 5;
        private const int RETRY_DELAY_MS = 3000;
        private bool isCalculating = false;

        // Sniper Model state management
        private bool isTriggerArmed = false;
        private double[] atrValues;
        public double currentROC1 = double.NaN;
        public double currentROC2 = double.NaN;

        /// <summary>
        /// Calculates the number of candles required for ATR and ROC calculations.
        /// Based on the largest lookback period needed plus buffer for ATR calculation.
        /// <returns>The number of candles needed for complete calculation.</returns>
        /// </summary>
        public int GetPeriod()
        {
            // Need enough data for ATR calculation plus the longest ROC lookback
            int maxRocPeriod = Math.Max(LongRocPeriod, ShortRocPeriod);
            return (Period + maxRocPeriod + 10) * 3; // 3x multiplier for safety buffer
        }

        /// <summary>
        /// Handles the initial snapshot of historical candle data. 
        /// Caches the relevant candles and calculates the initial ATR and ROC values.
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
                
                // Validate we have enough data for ATR + ROC calculations
                int minDataNeeded = Period + Math.Max(LongRocPeriod, ShortRocPeriod) + 1;
                if (count < minDataNeeded)
                {
                    logger.Debug($"ATR_Dual_ROC HandleSnapshot: Insufficient data - have {count}, need {minDataNeeded}");
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
                logger.Error(ex, "Error in ATR_Dual_ROC HandleSnapshot");
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

            // Check for valid OHLC data (ATR needs high, low, close)
            foreach (var candle in cache)
            {
                if (candle == null || 
                    candle.high <= 0 || double.IsNaN(candle.high) || double.IsInfinity(candle.high) ||
                    candle.low <= 0 || double.IsNaN(candle.low) || double.IsInfinity(candle.low) ||
                    candle.close <= 0 || double.IsNaN(candle.close) || double.IsInfinity(candle.close) ||
                    candle.high < candle.low) // High should be >= Low
                {
                    logger.Warn($"ATR_Dual_ROC: Invalid candle data detected at epoch {candle?.epoch}");
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
                        logger.Warn($"ATR_Dual_ROC: Invalid timestamp progression at index {i}");
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Calculates the ATR values and implements the "Sniper Model" dual ROC logic.
        /// Step A: Calculate base ATR using TA-Lib
        /// Step B: Calculate ROC1 (long-term) and ROC2 (short-term) from ATR values
        /// Step C: Implement sniper firing logic (compression arms, expansion fires)
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
                int minDataNeeded = Period + Math.Max(LongRocPeriod, ShortRocPeriod) + 1;
                
                // Ensure we have enough data points for ATR + ROC calculations
                if (totalCount < minDataNeeded)
                { 
                    logger.Debug($"ATR_Dual_ROC Calculate: Insufficient data - have {totalCount}, need {minDataNeeded}");
                    return;
                }

                // Step A: Calculate Base ATR
                var highs = cache.Select(x => x.high).ToArray();
                var lows = cache.Select(x => x.low).ToArray();
                var closes = cache.Select(x => x.close).ToArray();
                
                // Validate input arrays
                if (highs.Any(h => double.IsNaN(h) || double.IsInfinity(h) || h <= 0) ||
                    lows.Any(l => double.IsNaN(l) || double.IsInfinity(l) || l <= 0) ||
                    closes.Any(c => double.IsNaN(c) || double.IsInfinity(c) || c <= 0))
                {
                    logger.Warn("ATR_Dual_ROC Calculate: Invalid price data detected");
                    return;
                }

                var startIndex = 0;
                var endIndex = totalCount - 1;

                atrValues = new double[totalCount];
                
                // Call TA-Lib ATR function
                var retCode = Core.Atr(
                    startIndex,
                    endIndex,
                    highs,
                    lows,
                    closes,
                    Period,
                    out var outBegIdx,
                    out var outNbElement,
                    atrValues);

                // Check TA-Lib return code
                if (retCode != Core.RetCode.Success)
                {
                    logger.Error($"ATR_Dual_ROC Calculate: TA-Lib ATR calculation failed with code {retCode}");
                    calculationFailureCount++;
                    return;
                }

                // Validate output parameters
                if (outNbElement <= 0 || atrValues == null)
                {
                    logger.Error("ATR_Dual_ROC Calculate: Invalid ATR output");
                    calculationFailureCount++;
                    return;
                }

                // Get the current ATR value
                int atrIndex = outNbElement - 1;
                if (atrIndex < 0 || atrIndex >= atrValues.Length)
                {
                    logger.Error($"ATR_Dual_ROC Calculate: ATR index out of bounds: {atrIndex}");
                    calculationFailureCount++;
                    return;
                }

                PreviousValue = Value;
                Value = atrValues[atrIndex];

                // Validate calculated ATR value
                if (double.IsNaN(Value) || double.IsInfinity(Value) || Value < 0)
                {
                    logger.Error($"ATR_Dual_ROC Calculate: Invalid ATR value: {Value}");
                    calculationFailureCount++;
                    return;
                }

                // Step B: Calculate ROC1 and ROC2
                bool roc1Calculated = CalculateROC1(outBegIdx, outNbElement);
                bool roc2Calculated = CalculateROC2(outBegIdx, outNbElement);

                if (!roc1Calculated || !roc2Calculated)
                {
                    logger.Debug("ATR_Dual_ROC Calculate: ROC calculations incomplete");
                    return;
                }

                // Step C: Implement Sniper Firing Logic
                ImplementSniperLogic();

                // Update the timestamp with the epoch of the last candle in the cache
                Timestamp = cache.Last().epoch;

                // Reset failure count on successful calculation
                calculationFailureCount = 0;

            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error in ATR_Dual_ROC Calculate method");
                calculationFailureCount++;
                
                // If we have too many failures, reset the indicator
                if (calculationFailureCount >= MAX_CALCULATION_FAILURES)
                {
                    logger.Error($"ATR_Dual_ROC Calculate: Too many calculation failures ({calculationFailureCount}), resetting indicator");
                    Reset();
                }
            }
            finally
            {
                isCalculating = false;
            }
        }

        /// <summary>
        /// Calculates the long-term Rate of Change (ROC1) for compression detection
        /// </summary>
        private bool CalculateROC1(int outBegIdx, int outNbElement)
        {
            try
            {
                // Need at least LongRocPeriod + 1 ATR values for ROC1 calculation
                if (outNbElement < LongRocPeriod + 1)
                {
                    return false;
                }

                // Get current and past ATR values for ROC1
                int currentIndex = outNbElement - 1;
                int pastIndex = currentIndex - LongRocPeriod;
                
                if (pastIndex < 0)
                {
                    return false;
                }

                double currentATR = atrValues[currentIndex];
                double pastATR = atrValues[pastIndex];

                if (pastATR == 0 || double.IsNaN(pastATR) || double.IsInfinity(pastATR))
                {
                    return false;
                }

                // Calculate ROC1: ((current_ATR - past_ATR) / past_ATR) * 100
                currentROC1 = ((currentATR - pastATR) / pastATR) * 100.0;

                logger.Debug($"ATR_Dual_ROC ROC1: {currentROC1:F2}% (Current ATR: {currentATR:F6}, Past ATR: {pastATR:F6})");
                return true;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error calculating ROC1");
                return false;
            }
        }

        /// <summary>
        /// Calculates the short-term Rate of Change (ROC2) for expansion detection
        /// </summary>
        private bool CalculateROC2(int outBegIdx, int outNbElement)
        {
            try
            {
                // Need at least ShortRocPeriod + 1 ATR values for ROC2 calculation
                if (outNbElement < ShortRocPeriod + 1)
                {
                    return false;
                }

                // Get current and past ATR values for ROC2
                int currentIndex = outNbElement - 1;
                int pastIndex = currentIndex - ShortRocPeriod;
                
                if (pastIndex < 0)
                {
                    return false;
                }

                double currentATR = atrValues[currentIndex];
                double pastATR = atrValues[pastIndex];

                if (pastATR == 0 || double.IsNaN(pastATR) || double.IsInfinity(pastATR))
                {
                    return false;
                }

                // Calculate ROC2: ((current_ATR - past_ATR) / past_ATR) * 100
                currentROC2 = ((currentATR - pastATR) / pastATR) * 100.0;

                logger.Debug($"ATR_Dual_ROC ROC2: {currentROC2:F2}% (Current ATR: {currentATR:F6}, Past ATR: {pastATR:F6})");
                return true;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error calculating ROC2");
                return false;
            }
        }

        /// <summary>
        /// Implements the "Sniper Model" firing logic using dual ROC conditions
        /// </summary>
        private void ImplementSniperLogic()
        {
            try
            {
                if (double.IsNaN(currentROC1) || double.IsNaN(currentROC2))
                {
                    return;
                }

                // Check for Compression (Arming Condition)
                if (currentROC1 <= CompressionThreshold && !isTriggerArmed)
                {
                    isTriggerArmed = true;
                    logger.Info($"ATR_Dual_ROC ARMED: ROC1 {currentROC1:F2}% <= Compression Threshold {CompressionThreshold:F2}%");
                }

                // Check for Expansion (Firing Condition)
                if (isTriggerArmed && currentROC2 > ExpansionThreshold)
                {
                    logger.Info($"ATR_Dual_ROC SIGNAL FIRED: ROC2 {currentROC2:F2}% > Expansion Threshold {ExpansionThreshold:F2}% (ATR: {Value:F6})");
                    
                    // Fire the crossover event
                    Crossover?.Raise(this, EventArgs.Empty);
                    
                    // Immediately disarm to prevent multiple signals
                    isTriggerArmed = false;
                    logger.Debug("ATR_Dual_ROC: Trigger disarmed after signal fire");
                }

                // Handle Disarming (compression no longer valid)
                if (currentROC1 > CompressionThreshold && isTriggerArmed)
                {
                    isTriggerArmed = false;
                    logger.Debug($"ATR_Dual_ROC DISARMED: ROC1 {currentROC1:F2}% > Compression Threshold {CompressionThreshold:F2}%");
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error in ATR_Dual_ROC Sniper Logic");
            }
        }

        /// <summary>
        /// Handles a real-time update with a new candle. 
        /// Updates the cached candles list, removing the oldest candle and adding the new candle. 
        /// Recalculates the ATR and ROC values after the update.
        /// <param name="candle">The new candle representing the latest price data.</param>
        /// </summary>
        public void HandleUpdate(Candle candle)
        {
            try
            {
                if (candle == null)
                {
                    return;
                }

                // Validate candle data (ATR needs high, low, close)
                if (candle.high <= 0 || double.IsNaN(candle.high) || double.IsInfinity(candle.high) ||
                    candle.low <= 0 || double.IsNaN(candle.low) || double.IsInfinity(candle.low) ||
                    candle.close <= 0 || double.IsNaN(candle.close) || double.IsInfinity(candle.close) ||
                    candle.high < candle.low)
                { 
                    logger.Warn($"ATR_Dual_ROC HandleUpdate: Invalid candle data at epoch {candle.epoch}");
                    return;
                }

                if (cache == null || cache.Count == 0)
                {
                    logger.Debug("ATR_Dual_ROC HandleUpdate: Cache is empty");
                    return;
                }

                // Check if this is an update to the last candle or a new candle
                if (cache[cache.Count - 1].epoch == candle.epoch)
                {
                    // Update existing candle
                    cache[cache.Count - 1] = candle;
                    logger.Debug($"ATR_Dual_ROC HandleUpdate: Updated existing candle at epoch {candle.epoch}");
                }
                else
                {
                    // Add new candle and remove oldest if cache is at capacity
                    if (cache.Count >= GetPeriod())
                    {
                        cache.RemoveAt(0);
                    }
                    cache.Add(candle);
                    logger.Debug($"ATR_Dual_ROC HandleUpdate: Added new candle at epoch {candle.epoch}, cache size: {cache.Count}");
                }

                // Only calculate if we have sufficient data
                int minDataNeeded = Period + Math.Max(LongRocPeriod, ShortRocPeriod) + 1;
                if (cache.Count >= minDataNeeded)
                {
                    Calculate();
                }
                else
                {
                    logger.Debug($"ATR_Dual_ROC HandleUpdate: Insufficient data for calculation - have {cache.Count}, need {minDataNeeded}");
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error in ATR_Dual_ROC HandleUpdate");
            }
        }

        /// <summary>
        /// Resets the ATR_Dual_ROC indicator by clearing all cached data and state.
        /// </summary>
        public void Reset()
        {
            try
            {
                logger.Info("ATR_Dual_ROC Reset: Clearing indicator state");
                
                cache.Clear();
                Value = double.NaN;
                PreviousValue = double.NaN;
                Timestamp = 0;
                
                // Reset ROC values and sniper state
                currentROC1 = double.NaN;
                currentROC2 = double.NaN;
                isTriggerArmed = false;
                atrValues = null;
                
                // Reset failure tracking
                calculationFailureCount = 0;
                lastCalculationAttempt = DateTime.MinValue;
                isCalculating = false;
                
                logger.Debug("ATR_Dual_ROC Reset: Complete");
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error in ATR_Dual_ROC Reset");
            }
        }

        /// <summary>
        /// Forces a recalculation attempt if ATR is currently NaN and enough time has passed
        /// </summary>
        public void ForceRecalculationIfNeeded()
        {
            try
            {
                int minDataNeeded = Period + Math.Max(LongRocPeriod, ShortRocPeriod) + 1;
                
                if (double.IsNaN(Value) && 
                    cache != null && 
                    cache.Count >= minDataNeeded && 
                    (DateTime.Now - lastCalculationAttempt).TotalMilliseconds > RETRY_DELAY_MS)
                {
                    logger.Debug("ATR_Dual_ROC: Forcing recalculation attempt");
                    Calculate();
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error in ATR_Dual_ROC ForceRecalculationIfNeeded");
            }
        }

        /// <summary>
        /// Gets diagnostic information about the ATR_Dual_ROC state
        /// </summary>
        public string GetDiagnosticInfo()
        {
            return $"ATR_Dual_ROC Diagnostic - ATR Value: {Value:F6}, Cache Count: {cache?.Count ?? 0}, " +
                   $"Period: {Period}, ROC1: {currentROC1:F2}%, ROC2: {currentROC2:F2}%, " +
                   $"Trigger Armed: {isTriggerArmed}, Failures: {calculationFailureCount}, " +
                   $"Compression Threshold: {CompressionThreshold:F2}%, Expansion Threshold: {ExpansionThreshold:F2}%, " +
                   $"Long ROC Period: {LongRocPeriod}, Short ROC Period: {ShortRocPeriod}, " +
                   $"Last Calculation: {lastCalculationAttempt}, Is Calculating: {isCalculating}";
        }
    }
}
