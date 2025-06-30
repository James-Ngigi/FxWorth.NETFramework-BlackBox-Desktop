using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Caching;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NLog;
using WebSocket4Net;
using System.Threading.Tasks;

namespace FxApi
{
    /// <summary>
    /// The `MarketDataClient` class is responsible for retrieving and managing market data from the Deriv API. 
    /// It handles subscriptions to market data streams, processes incoming data messages, and provides 
    /// mechanisms for accessing historical data and real-time updates. It uses the RSI technical indicators 
    /// to analyze market trends and generate trading signals. It also utilizes a local cache to store 
    /// market data, reducing the need for frequent API requests and improving performance avoiding Potential API rate limits.
    /// </summary>
    public class MarketDataClient : BinaryClientBase
    {
        /// A counter used to generate unique request IDs for market data subscriptions.
        private int counter = 0;

        /// Logger for recording debug, informational, and error messages related to market data retrieval, processing, and caching.
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        /// A dictionary that maps trading symbols to their corresponding `ActiveSymbol` objects, which contain detailed information about the symbols.
        private Dictionary<string, ActiveSymbol> symbolsDictionary = new Dictionary<string, ActiveSymbol>();

        /// A dictionary that maps display names of trading symbols (e.g., "Volatility 50 Index (1s) - (1Hz50v)") to their corresponding `ActiveSymbol` objects.
        private Dictionary<string, ActiveSymbol> namesDictionary = new Dictionary<string, ActiveSymbol>();

        /// A memory cache used to store market data locally, reducing the need for frequent API requests. It also serves to improve performance by avoiding potential API rate limits.
        private static MemoryCache cache = MemoryCache.Default;

        /// A cache item policy defining the expiration and removal behavior for items stored in the memory cache.
        private readonly CacheItemPolicy cachePolicy = new CacheItemPolicy
        {
            // Set the absolute expiration time to 24 hours from now, meaning cached items will expire after this duration.
            AbsoluteExpiration = DateTimeOffset.Now.AddHours(24),
            // Define a callback method to be executed when a cache entry is removed.
            // The `CacheEntryRemovedCallback` method logs information about the removed entry.
            RemovedCallback = new CacheEntryRemovedCallback(CacheEntryRemovedCallback)
        };

        /// <summary>
        /// A dictionary that stores local subscription descriptions, mapping local IDs to `SubscriptionDescription` objects. 
        /// Each `SubscriptionDescription` represents an active market data subscription.
        /// </summary>
        private readonly Dictionary<int, SubscriptionDescription> localSubscriptions = new Dictionary<int, SubscriptionDescription>();


        /// <summary>
        /// Constructor for the `MarketDataClient` class used to create a new instance of the client with the specified API credentials.
        /// <param name="credentials">API credentials required for connecting to the Deriv API.</param>
        /// </summary>
        public MarketDataClient(Credentials credentials) : base(credentials)
        {
        }

        /// <summary>
        /// Callback method executed when a cache entry is removed from the memory cache.
        /// Logs information about the removed entry, including its key and the reason for removal.
        /// It is used to track cache usage and provide insights into cache behavior.
        /// <param name="arguments">Cache entry removed arguments containing details about the removed entry.</param>
        /// </summary>
        private static void CacheEntryRemovedCallback(CacheEntryRemovedArguments arguments)
        {
            logger.Info($"Cache entry removed: Key = {arguments.CacheItem.Key}, Reason = {arguments.RemovedReason}");
        }

        /// <summary>
        /// Event handler called when the WebSocket connection is successfully opened.
        /// Sends a request to retrieve the list of active symbols from the Deriv API.
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="e">Event arguments.</param>
        /// </summary>
        protected override void SockOnOpened(object sender, EventArgs e)
        {
            // Call the base class's `SockOnOpened` method to handle general connection setup.
            base.SockOnOpened(sender, e);

            // Always set IsOnline = true for operator/monitoring use
            IsOnline = true;

            // Send a request to the server to get the full list of active symbols.
            Send(new GetActiveSymbolsFullMessage());
        }

        /// <summary>
        /// Unsubscribes from all active market data subscriptions. 
        /// This clears the local subscriptions dictionary and sends "forget" messages to the server for both candles and ticks.
        /// </summary>
        public void UnsubscribeAll()
        {
            // Clear the localSubscriptions dictionary, effectively unsubscribing from all locally tracked subscriptions.
            localSubscriptions.Clear();

            // Send "forget" messages to the server to cancel existing subscriptions for candles and ticks.
            Send(new ForgetAllMessage() { Forget = "candles" });
            Send(new ForgetAllMessage() { Forget = "ticks" });
        }

        /// <summary>
        /// Retrieves the `ActiveSymbol` object for the specified trading symbol name.
        /// <param name="name">The display name of the trading symbol (e.g., "1HZ10V") to search for.</param>
        /// <returns>The `ActiveSymbol` object for the specified symbol, or null if the symbol is not found.</returns>
        /// </summary>
        public ActiveSymbol GetInstrument(string name)
        {
            // Attempt to get the `ActiveSymbol` from the namesDictionary using the provided symbol name.
            if (!namesDictionary.TryGetValue(name, out var description))
            {
                // If the symbol is not found, return null.
                return null;
            }

            // Return the found `ActiveSymbol`.
            return description;
        }

        /// <summary>
        /// Subscribes to market data for the specified symbol, duration, and indicator.
        /// This method handles sending subscription requests to the Deriv API and managing local subscription data.
        /// It serves to initiate the retrieval and processing of market data for technical analysis.
        /// <param name="name">The display name of the trading symbol (e.g., "1HZ100V").</param>
        /// <param name="duration">The duration (in seconds) for the market data subscription.</param>
        /// <param name="indicator">The indicator to be used for processing the market data.</param>
        /// </summary>
        public void Subscribe(string name, int duration, IIndicator indicator)
        {
            // Attempt to get the `ActiveSymbol` from the namesDictionary.
            if (!namesDictionary.TryGetValue(name, out var description))
            {
                // If the symbol is not found, return.
                return;
            }

            // Create a new `GetTicksHistoryMessage` object to request historical tick data.
            var req = new GetTicksHistoryMessage();
            // Set the symbol for the request.
            req.TicksHistory = description.symbol;
            // Set the number of ticks to retrieve (up to 5000).
            req.Count = 5000;
            // Set the granularity (time interval in seconds) for the data.
            req.Granularity = duration;
            // Generate a unique request ID using the counter and increment the counter.
            req.RequestId = counter++;
            // Create a new `SubscriptionDescription` to track the local subscription details.
            var lSub = new SubscriptionDescription() { LocalId = req.RequestId };

            //Custom timeframes offer a unique look into different timeframes not natively supported by the Deriv API offering a more granular view of the market.

            // If the duration is a custom timeframe (not a standard timeframe supported by the indicators)...
            if (TimeFrameValidator.IsSupportedCustomTimeFrame(duration))
            {
                // Set the granularity to 60 seconds (1 minute).
                req.Granularity = 60;
                // Set the number of ticks to 5000.
                req.Count = 5000;
                // Set the style to "ticks" to retrieve raw tick data.
                req.Style = "ticks";
                // Create a new `ChartsCache` instance to handle caching and aggregation of tick data into custom timeframes.
                lSub.Cache = new ChartsCache(duration);
            }

            // Set the indicator and active symbol for the subscription description.
            lSub.Token = indicator;
            lSub.ActiveSymbol = description;

            // Add the subscription description to the localSubscriptions dictionary.
            localSubscriptions.Add(lSub.LocalId, lSub);
            // Send the GetTicksHistoryMessage request to the server.
            Send(req);
        }

        /// <summary>
        /// Generates a unique cache key for candles data based on the request ID.
        /// It is used to store and retrieve candles data from the memory cache.
        /// <param name="candles">The `CandlesResponse` object containing the candles data.</param>
        /// <returns>A string representing the unique cache key for the candles data.</returns>
        /// </summary>
        private string GenerateCandlesCacheKey(CandlesResponse candles)
        {
            return $"candles_{candles.req_id}_{candles.pip_size}";
        }

        /// <summary>
        /// Generates a unique cache key for ticks data based on the request ID.
        /// Its used to store and retrieve ticks data from the memory cache.
        /// <param name="ticks">The `TicksResponse` object containing the ticks data.</param>
        /// <returns>A string representing the unique cache key for the ticks data.</returns>
        /// </summary>
        private string GenerateTicksCacheKey(TicksResponse ticks)
        {
            return $"ticks_{ticks.req_id}";
        }

        /// <summary>
        /// Retrieves cached candles data from the memory cache using the provided cache key.
        /// Handles potential exceptions during cache retrieval and logging.
        /// <param name="cacheKey">The cache key for the candles data.</param>
        /// <returns>The cached `CandlesResponse` object, or null if the data is not found or an error occurs.</returns>
        /// </summary>
        private CandlesResponse GetCachedCandles(string cacheKey)
        {
            try
            {
                // Attempt to get the cached CandlesResponse from the memory cache.
                return cache.Get(cacheKey) as CandlesResponse;
            }

            catch (Exception ex)
            {
                // If an exception occurs, log the error and return null.
                logger.Error(ex, $"Error retrieving candles from cache with key: {cacheKey}");
                return null;
            }
        }

        /// <summary>
        /// Caches candles data in the memory cache using the provided cache key and cache policy.
        /// Handles potential exceptions during caching.
        /// <param name="cacheKey">The cache key for the candles data.</param>
        /// <param name="candles">The `CandlesResponse` object to be cached.</param>
        /// </summary>
        private void CacheCandles(string cacheKey, CandlesResponse candles)
        {
            try
            {
                // Attempt to add the CandlesResponse to the memory cache using the provided key and cache policy.
                cache.Add(cacheKey, candles, cachePolicy);
            }

            catch (Exception ex)
            {
                // If an exception occurs, log the error.
                logger.Error(ex, $"Error adding candles to cache with key: {cacheKey}");
            }
        }

        /// <summary>
        /// Retrieves cached ticks data from the memory cache using the provided cache key.
        /// Handles potential exceptions during cache retrieval.
        /// <param name="cacheKey">The cache key for the ticks data.</param>
        /// <returns>The cached `TicksResponse` object, or null if the data is not found or an error occurs.</returns>
        /// </summary>
        private TicksResponse GetCachedTicks(string cacheKey)
        {
            try
            {
                // Attempt to get the cached TicksResponse from the memory cache.
                return cache.Get(cacheKey) as TicksResponse;
            }

            catch (Exception ex)
            {
                // If an exception occurs, log the error and return null.
                logger.Error(ex, $"Error retrieving ticks from cache with key: {cacheKey}");
                return null;
            }
        }

        /// <summary>
        /// Caches ticks data in the memory cache using the provided cache key and cache policy.
        /// Handles potential exceptions during caching.
        /// <param name="cacheKey">The cache key for the ticks data.</param>
        /// <param name="ticks">The `TicksResponse` object to be cached.</param>
        /// </summary>
        private void CacheTicks(string cacheKey, TicksResponse ticks)
        {
            try
            {
                // Attempt to add the TicksResponse to the memory cache using the provided key and cache policy.
                cache.Add(cacheKey, ticks, cachePolicy);
            }

            catch (Exception ex)
            {
                // If an exception occurs, log the error.
                logger.Error(ex, $"Error adding ticks to cache with key: {cacheKey}");
            }
        }

        /// <summary>
        /// Event handler called when a message is received from the Deriv API WebSocket server. 
        /// It processes different message types related to market data, such as active symbols, candles, ticks, and OHLC (Open-High-Low-Close) data.
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="e">Message received event arguments containing the received message data.</param>
        /// </summary>
        protected override void SockOnMessageReceived(object sender, MessageReceivedEventArgs e)
        {
            try
            {
                // Deserialize the received JSON message into a JObject.
                var jMessage = JsonConvert.DeserializeObject<JObject>(e.Message);
                
                if (jMessage == null)
                {
                    logger.Error("Failed to deserialize message - jMessage is null");
                    return;
                }

                if (!jMessage.ContainsKey("msg_type"))
                {
                    logger.Error("Message does not contain msg_type field");
                    return;
                }

                // Declare a `SubscriptionDescription` variable to hold subscription information.
                SubscriptionDescription sub;

                // Process different message types based on the "msg_type" field.
                switch (jMessage["msg_type"].Value<string>())
                {
                    case "active_symbols":
                        // This message type contains the list of active trading symbols available on the Deriv platform.

                        // Check if the message contains an error
                        if (jMessage.ContainsKey("error"))
                        {
                            logger.Error($"Error in active_symbols response: {jMessage["error"]}");
                            break;
                        }

                        // Deserialize the message into an `ActiveSymbolsResponse` object.
                        var symbols = jMessage.ToObject<ActiveSymbolsResponse>();

                        // Check if symbols and active_symbols list are not null
                        if (symbols == null || symbols.active_symbols == null)
                        {
                            logger.Error("Received null active_symbols data from API");
                            break;
                        }

                        // Update the symbolsDictionary and namesDictionary with the received symbol information.
                        try
                        {
                            symbolsDictionary = symbols.active_symbols.ToDictionary(x => x.symbol);
                            namesDictionary = symbols.active_symbols.ToDictionary(x => x.display_name);
                            //logger.Info($"Successfully loaded {symbols.active_symbols.Count} active symbols");
                        }
                        catch (ArgumentException ex)
                        {
                            logger.Error(ex, "Error creating dictionaries from active symbols - possible duplicate keys");
                            // Create dictionaries using GroupBy to handle duplicates
                            symbolsDictionary = symbols.active_symbols
                                .GroupBy(x => x.symbol)
                                .ToDictionary(g => g.Key, g => g.First());
                            namesDictionary = symbols.active_symbols
                                .GroupBy(x => x.display_name)
                                .ToDictionary(g => g.Key, g => g.First());
                            //logger.Info($"Successfully loaded {symbols.active_symbols.Count} active symbols with duplicate handling");
                        }

                        // Handle re-subscriptions after receiving the active symbols list.
                        var tmp = localSubscriptions.ToList();
                        localSubscriptions.Clear();

                        // Re-subscribe to previously subscribed symbols.
                        foreach (var pair in tmp)
                        {
                            logger.Info("resubscribe data {0} {1}", pair.Value.ActiveSymbol.display_name, pair.Value.Token.TimeFrame);
                            pair.Value.Token.Reset();
                            Subscribe(pair.Value.ActiveSymbol.display_name, pair.Value.Token.TimeFrame, pair.Value.Token);
                        }

                        break;

                    case "forget":
                        // This message type is a confirmation that a forget request (unsubscribe) has been processed.
                        // No specific action is required here.
                        break;

                    case "candles":
                        // This message type contains candlestick data for the subscribed symbol.
                        var candles = jMessage.ToObject<CandlesResponse>();
                        string candlesCacheKey = GenerateCandlesCacheKey(candles);
                        var cachedCandles = GetCachedCandles(candlesCacheKey);

                        if (cachedCandles != null)
                        {
                            sub = localSubscriptions[candles.req_id];
                            sub.RemoteId = cachedCandles.subscription.id;
                            sub.Token.HandleSnapshot(cachedCandles.candles);
                        }
                        else
                        {
                            if (localSubscriptions.TryGetValue(candles.req_id, out sub))
                            {
                                sub.RemoteId = candles.subscription.id;
                                sub.Token.HandleSnapshot(candles.candles);
                                CacheCandles(candlesCacheKey, candles);
                            }
                        }
                        break;

                    case "history":
                        // This message type contains historical tick data for the subscribed symbol.
                        var ticks = jMessage.ToObject<TicksResponse>();
                        string ticksCacheKey = GenerateTicksCacheKey(ticks);
                        var cachedTicks = GetCachedTicks(ticksCacheKey);

                        if (cachedTicks != null)
                        {
                            sub = localSubscriptions[ticks.req_id];
                            sub.RemoteId = cachedTicks.subscription.id;
                            var customCandles = sub.Cache.HandleSnapshot(cachedTicks.history);
                            sub.Token.HandleSnapshot(customCandles);
                        }
                        else
                        {
                            if (localSubscriptions.TryGetValue(ticks.req_id, out sub))
                            {
                                sub.RemoteId = ticks.subscription.id;
                                var customCandles = sub.Cache.HandleSnapshot(ticks.history);
                                sub.Token.HandleSnapshot(customCandles);
                                CacheTicks(ticksCacheKey, ticks);
                            }
                        }
                        break;

                    case "ohlc":
                        // This message type contains the real-time OHLC (Open-High-Low-Close) data for the subscribed symbol.
                        var ohlc = jMessage.ToObject<OhlcMessage>();
                        var c = new Candle();
                        c.close = ohlc.ohlc.close;
                        c.open = ohlc.ohlc.open;
                        c.high = ohlc.ohlc.high;
                        c.low = ohlc.ohlc.low;
                        c.epoch = ohlc.ohlc.open_time;

                        if (localSubscriptions.TryGetValue(ohlc.req_id, out sub))
                        {
                            sub.Token.HandleUpdate(c);
                        }
                        break;

                    case "tick":
                        // This message type contains real-time tick data for the subscribed symbol.
                        var tick = jMessage.ToObject<TickMessage>();

                        if (localSubscriptions.TryGetValue(tick.req_id, out sub))
                        {
                            var customCandle = sub.Cache.HandleUpdate(tick.tick);
                            sub.Token.HandleUpdate(customCandle);
                        }
                        break;

                    default:
                        // Ignore any other message types.
                        break;
                }
            }
            catch (JsonException jsonEx)
            {
                logger.Error(jsonEx, $"JSON deserialization error in SockOnMessageReceived: {e.Message}");
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Unexpected error in SockOnMessageReceived: {e.Message}");
            }
        }
    }

    /// <summary>
    /// The `TimeFrameValidator` class is used to determine whether a given timeframe is supported 
    /// by the RSI technical indicators used in the application.
    /// </summary>
    public class TimeFrameValidator
    {
        /// A list of custom timeframes (in minutes) supported by the RSI indicator.
        private static List<int> customTimeFrames = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 12, 15, 18, 20, 22, 25, 27, 30, 35, 40, 45, 50, 55, 60, 65, 75, 80, 90, 96, 95, 100 };

        /// <summary>
        /// Checks if the given duration (in minutes) is a custom timeframe supported by the RSI indicator.
        /// <param name="duration">The duration in minutes.</param>
        /// <returns>True if the duration is supported, otherwise false.</returns>
        /// </summary>
        public static bool IsSupportedCustomTimeFrame(int duration)
        {
            return customTimeFrames.Contains(Math.Abs(duration));
        }
    }
}