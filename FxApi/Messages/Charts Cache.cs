using System;
using System.Collections.Generic;
using System.Linq;

namespace FxApi
{
    /// <summary>
    /// The `ChartsCache` class is responsible for aggregating and caching market data (ticks or candles) 
    /// into custom timeframes. It handles both initial snapshots of historical data and real-time updates, 
    /// converting raw tick data into candles of the specified timeframe or aggregating existing candles 
    /// into larger timeframes. This class is crucial for providing consistent data to technical indicators 
    /// that require specific timeframes for analysis.
    /// </summary>
    public class ChartsCache
    {
        /// <summary>
        /// The desired timeframe (in seconds) for aggregating the market data. 
        /// If the timeframe is negative, it indicates that raw tick data should be used for aggregation.
        /// </summary>
        private readonly int timeframe;

        /// <summary>
        /// A list of `Candle` objects representing the aggregated and cached market data.
        /// </summary>
        private List<Candle> cache = new List<Candle>();

        /// <summary>
        /// A flag indicating whether the aggregation is based on raw tick data (`true`) or existing candles (`false`).
        /// </summary>
        private bool isTickType;

        /// <summary>
        /// Constructor for the `ChartsCache` class.
        /// <param name="timeframe">The desired timeframe (in seconds) for data aggregation. 
        /// Negative values indicate tick-based aggregation.</param>
        /// </summary>
        public ChartsCache(int timeframe)
        {
            // Store the absolute value of the timeframe.
            this.timeframe = Math.Abs(timeframe);
            // Set the `isTickType` flag based on whether the timeframe is negative.
            isTickType = timeframe < 0;
        }

        /// <summary>
        /// Handles an initial snapshot of historical tick data, converting it into candles of the specified timeframe.
        /// <param name="history">The `History` object containing historical tick data (prices and timestamps).</param>
        /// <returns>A list of `Candle` objects representing the aggregated candles.</returns>
        /// </summary>
        public List<Candle> HandleSnapshot(History history)
        {
            // Iterate through the historical tick data.
            for (int i = 0; i < history.prices.Count; i++)
            {
                // Get the timestamp and price of the current tick.
                var timestamp = history.times[i];
                var price = history.prices[i];

                // Process the tick to aggregate it into a candle.
                ProcessTick(price, timestamp);
            }

            // Return the list of aggregated candles.
            return cache;
        }

        /// <summary>
        /// Processes a single tick (or candle), aggregating it into the appropriate candle in the cache.
        /// This method handles both tick-based and candle-based aggregation.
        /// <param name="price">The price of the tick or the closing price of the candle.</param>
        /// <param name="timestamp">The timestamp of the tick or the opening time of the candle.</param>
        /// <returns>The `Candle` object that the tick was aggregated into.</returns>
        /// </summary>
        private Candle ProcessTick(double price, int timestamp)
        {
            // Calculate the opening time of the candle based on the specified timeframe.
            var openTime = (timestamp / timeframe) * timeframe;

            // Get the last candle in the cache.
            var candle = cache.LastOrDefault();

            // Check if a new candle needs to be created:
            // - If there are no candles in the cache yet.
            // - If aggregating based on candles and the current candle's epoch doesn't match the calculated openTime.
            // - If aggregating based on ticks and the current candle's tick count has reached the timeframe.
            if (candle == null ||
                !isTickType && candle.epoch != openTime ||
                isTickType && candle.tick_count == timeframe
            )
            {
                // Create a new candle with the appropriate initial values.
                var newCandle = new Candle()
                {
                    close = price,
                    high = price,
                    low = price,
                    open = price,
                    epoch = openTime,
                    tick_count = 1 // If tick-based, start with a tick count of 1.
                };

                // Add the new candle to the cache.
                cache.Add(newCandle);

                // Return the newly created candle.
                return newCandle;
            }

            // If a new candle is not needed, update the existing candle with the new tick data.
            candle.tick_count++;
            candle.close = price;
            candle.high = Math.Max(price, candle.high);
            candle.low = Math.Min(price, candle.low);

            // Return the updated candle.
            return candle;
        }

        /// <summary>
        /// Handles a real-time tick update, aggregating it into the appropriate candle in the cache.
        /// <param name="tick">The `Tick` object representing the real-time tick data.</param>
        /// <returns>The `Candle` object that the tick was aggregated into.</returns>
        /// </summary>
        public Candle HandleUpdate(Tick tick)
        {
            // Call `ProcessTick` to aggregate the tick data into a candle.
            return ProcessTick(tick.quote, tick.epoch);
        }
    }
}