using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FxApi;
using FxApi.Connection;
using FxWorth.Hierarchy;
using Newtonsoft.Json;
using NLog;
using static FxWorth.Hierarchy.HierarchyNavigator;
using System.Timers;

namespace FxWorth
{
    /// <summary>
    /// The `TokenStorage` class is the core component of the FxWorth application. 
    /// It manages multiple trading accounts (represented by API tokens and App IDs(Masked as Acc ID/Client ID)), handles connections to 
    /// the Deriv API, subscribes to market data, implements trading logic based on the RSI technical indicator, 
    /// and executes trades. It also monitors internet latency and ensures responsible API usage.
    /// </summary>

    public class TokenStorage
    {
        // Logging messages related to token management, trading actions, and network status.
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        // A global flag indicating whether trading is currently allowed across all managed accounts.
        // Used to halt trading attempts when either the Take-Profit or Stop-Loss condition is reached on all accounts.
        private bool isTradingGloballyAllowed = true;

        // The maximum number of API tokens (and therefore, trading accounts) that the application can manage.
        public static int MaxTokensCount = 50;

        // Latency threshold (in milliseconds) above which the internet connection is considered slow.
        // Trading attempts are blocked if the latency exceeds this value.
        private static int slowInternetConst = 650;

        public Dictionary<int, CustomLayerConfig> customLayerConfigs = new Dictionary<int, CustomLayerConfig>();

        // Minimum time (in seconds) to pause trading after a loss trade.
        // This helps prevent rapid consecutive losses and allows the market to potentially stabilize.
        private static TimeSpan tradesTimeoutThreshold = TimeSpan.FromSeconds(0.001);

        // Minimum time (in seconds) to pause between any trades, regardless of outcome.
        // This ensures responsible API usage and prevents overloading the Deriv server with requests.
        private static TimeSpan anyTradeTimeoutThreshold = TimeSpan.FromSeconds(0.001);

        // The path to the file where API tokens and App IDs are stored.
        private readonly string path;

        // A dictionary that stores `AuthClient` instances, each associated with a specific `Credentials` object.
        // Each `AuthClient` represents a connection to a Deriv trading account.
        private Dictionary<Credentials, AuthClient> clients = new Dictionary<Credentials, AuthClient>();

        // A `PingClient` instance used to measure and monitor the latency to the Deriv API server.
        private PingClient pinger = new PingClient(new Credentials() { AppId = "70216" });

        // A `MarketDataClient` instance used to subscribe to market data (e.g., price quotes, candlesticks) for selected symbols.
        // new Credentials() { AppId = "70216" } : The App ID used to authenticate with the Deriv API.
        private MarketDataClient marketDataClient = new MarketDataClient(new Credentials() { AppId = "70216" });

        // Event raised when the state of any managed `AuthClient` changes (e.g., connection status, balance updates).
        public EventHandler<EventArgs> ClientsStateChanged;

        // Event raised when the measured internet latency changes.
        // This allows the UI to update the displayed latency value.
        public EventHandler<EventArgs> InternetSpeedChanged;

        // Event raised when authentication with a Deriv account fails, indicating a problem with the API token or Client ID.
        public EventHandler<AuthFailedArgs> AuthFailed;

        // Event raised when a trade is updated (e.g., trade state changes, profit/loss updates), allowing the UI to update the displayed trade information.
        public EventHandler<TradeEventArgs> TradeUpdated;

        // This flag  ensures that the trading logic for a given crossover signal is completed
        // before the system starts evaluating another signal, preventing concurrent trade attempts.
        private bool isTradePending = false;

        /// <summary>
        /// An `Rsi` instance representing the Relative Strength Index indicator.
        /// It's used to generate trading signals based on overbought/oversold conditions.
        /// </summary
        public Rsi rsi;

        // The `AuthClient` instance that will be used for eventing (e.g., receiving trade updates).
        // This is typically the first online client in the `clients` dictionary.
        private AuthClient eventingClinet;

        public bool IsHierarchyMode => hierarchyNavigator != null && hierarchyNavigator.IsInHierarchyMode;

        // Lock object for TradeUpdated event
        private readonly object tradeUpdateLock = new object();

        public HierarchyNavigator hierarchyNavigator;
        public Dictionary<string, HierarchyLevel> hierarchyLevels = new Dictionary<string, HierarchyLevel>();
        public string currentLevelId;
        public AuthClient hierarchyClient;
        public PhaseParameters phase1Parameters;
        public PhaseParameters phase2Parameters;
        public int MaxHierarchyDepth => hierarchyNavigator?.maxHierarchyDepth ?? 0;
        private Timer clientStateCheckTimer;
        private Dictionary<Credentials, bool> previousClientStates = new Dictionary<Credentials, bool>();
        public decimal InitialStakeLayer1 { get; set; }


        public void SetHierarchyParameters(PhaseParameters phase1Params, PhaseParameters phase2Params, Dictionary<int, CustomLayerConfig> customLayerConfigs)
        {
            this.phase1Parameters = phase1Params;
            this.phase2Parameters = phase2Params;
            this.customLayerConfigs = customLayerConfigs;
        }

        /// <summary>
        /// Constructor for the `TokenStorage` class.
        /// Initializes the `PingClient`, `MarketDataClient`, loads credentials from the specified file, and 
        /// sets up event handlers for monitoring network status and trading activity.
        /// <param name="path">The path to the file where API tokens and Client IDs are stored.</param>
        /// </summary>
        public TokenStorage(string path)
        {
            // Start the PingClient to begin monitoring network latency.
            pinger.Start();
            // Start the MarketDataClient to enable market data subscriptions.
            marketDataClient.Start();

            // Attach an event handler to be notified when the ping latency changes.
            pinger.PingChanged += PingChanged;

            // Store the path to the credentials file.
            this.path = path;

            // Initialize the clientStateCheckTimer
            clientStateCheckTimer = new Timer(12000); // Set the interval to 12 seconds (12000 milliseconds)
            clientStateCheckTimer.Elapsed += ClientStateCheckTimer_Elapsed;
            clientStateCheckTimer.Start();

            // If the credentials file exists, load and process the credentials.
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                if (!string.IsNullOrWhiteSpace(json)) // Check if file is empty or whitespace
                {
                    var creds = JsonConvert.DeserializeObject<List<Credentials>>(json);

                    if (creds != null)
                    {
                        foreach (var cred in creds)
                        {
                            Add(cred);
                        }
                    }
                }
            }
        }

        private void ClientStateCheckTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            foreach (var pair in Clients)
            {
                bool currentState = pair.Value.IsOnline;
                bool previousState;


                if (!previousClientStates.TryGetValue(pair.Key, out previousState))
                {
                    previousClientStates[pair.Key] = currentState;
                    ClientsStateChanged?.Raise(pair.Value, EventArgs.Empty);
                    continue;
                }


                if (currentState != previousState)
                {
                    ClientsStateChanged?.Raise(pair.Value, EventArgs.Empty);
                    previousClientStates[pair.Key] = currentState;
                }
            }
        }

        /// <summary>
        /// Event handler triggered when the ping latency changes.
        /// Raises the `InternetSpeedChanged` event to notify listeners.
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="e">Event arguments.</param>
        /// </summary>
        public void PingChanged(object sender, EventArgs e)
        {
            // Raise the InternetSpeedChanged event to notify the UI or other components about the latency change.
            InternetSpeedChanged?.Raise(sender, EventArgs.Empty);
        }

        /// <summary>
        /// Determines if the current internet connection is considered slow based on the measured latency.
        /// <returns>True if the latency exceeds the slow internet threshold, otherwise false.</returns>
        /// </summary>
        public bool IsInternetSlow()
        {
            // Compare the measured latency to the slow internet threshold.
            return pinger.Latency >= slowInternetConst;
        }

        // Publicly accessible reference to the `PingClient` instance.
        public PingClient PingClient => pinger;

        // Publicly accessible reference to the `MarketDataClient` instance.
        public MarketDataClient MarketDataClient => marketDataClient;

        // Publicly accessible reference to the dictionary of managed `AuthClient` instances.
        public Dictionary<Credentials, AuthClient> Clients => clients;

        // When called, it disposes of the `PingClient` and `MarketDataClient` instances, stopping network monitoring and market data subscriptions.
        public void Dispose()
        {
            // Stop the PingClient to prevent further latency measurements.
            pinger.Stop();

            // Detach the PingChanged event handler.
            pinger.PingChanged -= PingChanged;

            // Stop the MarketDataClient to unsubscribe from market data because the application is closing.
            marketDataClient.Stop();

            clientStateCheckTimer.Stop();
            clientStateCheckTimer.Dispose();
        }

        /// <summary>
        /// Adds a new trading account to the list of managed lucky clients.
        /// <param name="creds">The API credentials (token and APP ID) for the trading account.</param>
        /// <returns>True if the credentials were successfully added, otherwise false.</returns>
        /// </summary>
        public bool Add(Credentials creds)
        {
            // Logging which clients were added
            logger.Info("<=> Adding client credentials. Client ID - {0}, Key - {1}.", creds.AppId, creds.Token);

            // Dummy proffing! Prevent adding duplicate credentials for the sake 
            if (Credentials.Any(x => x.AppId == creds.AppId && x.Token == creds.Token))
            {
                return false;
            }

            // Add and save the credentials to the list of managed credentials.
            Credentials.Add(creds);
            Save();

            // Create a new AuthClient instance using the provided credentials.
            var client = new AuthClient(creds, 0);
            // Add the AuthClient to the dictionary, associating it with the credentials.
            clients.Add(creds, client);

            // Attach event handlers to monitor the client's state, balance, trade updates, and authentication failures.
            client.StateChanged += HandleStateChanged;
            client.BalanceChanged += OnBalanceChanged;
            client.TradeChanged += OnTradeChanged;
            client.AuthFailed += OnAuthFailed;

            // Indicate that the credentials were added successfully.
            return true;
        }

        /// <summary>
        /// Event handler triggered when authentication with a Deriv account fails.
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="e">Event arguments containing the credentials that failed authentication.</param>
        /// </summary>
        private void OnAuthFailed(object sender, EventArgs e)
        {
            // Get the credentials associated with the AuthClient that failed authentication.
            var pair = Clients.First(x => x.Value == sender);

            // If the account was selected/approved for trading, raise the AuthFailed event to notify the UI.
            if (pair.Key.IsChecked)
            {
                AuthFailed.Raise(this, new AuthFailedArgs(pair.Key));
            }
        }

        /// <summary>
        /// Event handler triggered when a trade on a managed account is updated.
        /// Raises the TradeUpdated event to propagate the trade update to listeners.
        /// <param name="sender">The object that raised the event (the AuthClient associated with the trade).</param>
        /// <param name="e">Trade event arguments containing details about the trade update.</param>
        /// </summary>
        private void OnTradeChanged(object sender, TradeEventArgs e)
        {
            // Only process trade updates from the designated eventing client.
            if (sender != eventingClinet)
            {
                return;
            }

            // Raise the TradeUpdated event to notify the UI or other components about the trade update.
            TradeUpdated?.Raise(this, e);
        }

        /// <summary>
        /// Event handler triggered when the balance of a managed account changes.
        /// Raises the ClientsStateChanged event to notify listeners.
        /// <param name="sender">The object that raised the event (the AuthClient associated with the account).</param>
        /// <param name="e">Event arguments.</param>
        /// </summary>
        private void OnBalanceChanged(object sender, EventArgs e)
        {
            // Raise the ClientsStateChanged event to notify the UI or any other components about the account balance change.
            ClientsStateChanged?.Raise(sender, EventArgs.Empty);
        }

        // Starts all managed `AuthClient` instances, initiating connections to Deriv accounts.
        public void StartAll()
        {
            // Iterate through each AuthClient in the clients dictionary.
            foreach (var client in clients.Values)
            {
                // Raise the ClientsStateChanged event to potentially update the UI before starting the client.
                ClientsStateChanged?.Raise(client, EventArgs.Empty);
                // Start the AuthClient, which initiates the connection to the Deriv API.
                client.Start();
            }
        }

        /// <summary>
        /// Subscribes to market data for a specific symbol and configures technical indicators (RSI).
        /// The parameters are specified by the caller and self explanatory.
        /// <returns>A `MarketDataParameters` object containing the configured RSI instances.</returns>
        /// </summary>
        public MarketDataParameters SubscribeMarketData(
            int rsiPeriod,
            double rsiOverbought,
            double rsiOversold,
            int rsiTimeframe,
            string symbol)
        {
            // Unsubscribe from any existing market data subscriptions.
            MarketDataClient.UnsubscribeAll();

            // If there's an existing RSI instance, detach the Crossover event handler.
            if (rsi != null)
            {
                rsi.Crossover -= OnCrossover;
            }

            // Create a new RSI instance with the specified parameters.
            rsi = new Rsi
            {
                Period = rsiPeriod,
                Overbought = rsiOverbought,
                Oversold = rsiOversold,
                TimeFrame = rsiTimeframe
            };

            // Attach the OnCrossover event handler to be notified when the RSI crosses overbought/oversold thresholds.
            rsi.Crossover += OnCrossover;

            // Subscribe to market data for the specified symbol using the configured RSI indicator.
            MarketDataClient.Subscribe(symbol, rsiTimeframe, rsi);

            // Return a MarketDataParameters object containing the configured RSI instance.
            // it is returned to the method: 
            return new MarketDataParameters { Rsi = rsi, Symbol = symbol };
        }



        /* ---------------------------------------------------------------------------------------------- */

        /// <summary>
        /// Event handler triggered when the RSI indicator crosses overbought or oversold thresholds.
        /// This method contains the core trading logic, evaluating various conditions before attempting to execute trades.
        /// <param name="sender">The object that raised the event (the RSI instance).</param>
        /// <param name="e">Event arguments.</param>
        /// </summary>
        private void OnCrossover(object sender, EventArgs e)
        {
            try
            {
                if (isTradePending)
                {
                    logger.Debug("<=> Ignoring RSI crossover signal because a trade is already pending.");
                    return;
                }

                isTradePending = true;

                if (!isTradingGloballyAllowed)
                {
                    logger.Info("<=> Global trading halted. Take-Profit/Stop-Loss condition reached on all accounts.");
                    return;
                }

                if (IsInternetSlow())
                {
                    return;
                }

                if (!IsTradingAllowed)
                {
                    logger.Info("<=> Entry blocked by **--IsTradingAllowed = false--** condition.");
                    return;
                }

                var tradingParameters = Clients.FirstOrDefault(x => x.Key.IsChecked && x.Value.IsOnline).Value?.TradingParameters;

                if (tradingParameters == null)
                {
                    logger.Warn("No suitable client found for trading. Skipping trade.");
                    return;
                }


                foreach (var pair in Clients)
                {
                    var value = pair.Value;
                    if (!pair.Key.IsChecked || !value.IsOnline)
                    {
                        continue;
                    }

                    if (eventingClinet == null || !eventingClinet.IsOnline)
                    {
                        eventingClinet = value;
                    }

                    if (value.IsTrading)
                    {
                        continue;
                    }

                    if (tradingParameters.AmountToBeRecoverd > tradingParameters.MaxDrawdown && (hierarchyNavigator == null || !hierarchyNavigator.IsInHierarchyMode))
                    {
                        hierarchyClient = value;

                        hierarchyNavigator = new HierarchyNavigator(tradingParameters.AmountToBeRecoverd, tradingParameters, phase1Parameters, phase2Parameters, customLayerConfigs, InitialStakeLayer1, this);
                        currentLevelId = "1.1";
                        hierarchyNavigator.LoadLevelTradingParameters(currentLevelId, value, tradingParameters);
                        tradingParameters.DynamicStake = tradingParameters.Stake;
                    }

                    if (IsHierarchyMode)
                    {
                        HierarchyLevel currentLevel = hierarchyNavigator.GetCurrentLevel();

                        if (currentLevel != null)
                        {
                            hierarchyNavigator.LoadLevelTradingParameters(currentLevel.LevelId, value, tradingParameters);

                            logger.Info($"Hierarchy Trade - Level: {currentLevel.LevelId}, AmountToRecover: {currentLevel.AmountToBeRecovered}, Stake: {tradingParameters.DynamicStake}, PreviousProfit: {tradingParameters.PreviousProfit}, MartingaleLevel: {tradingParameters.MartingaleLevel}, Barrier: {tradingParameters.Barrier}");

                            Task.Factory.StartNew(() =>
                            {
                                value.Buy(tradingParameters.Symbol.symbol, tradingParameters.Duration,
                                    tradingParameters.DurationType, tradingParameters.DynamicStake);
                            });

                            Task.Factory.StartNew(() =>
                            {
                                value.Sell(tradingParameters.Symbol.symbol, tradingParameters.Duration,
                                    tradingParameters.DurationType, tradingParameters.DynamicStake);
                            });
                        }
                        else
                        {
                            logger.Error("Current level is null in hierarchy mode.");
                            // Handle error appropriately
                        }
                    }
                    else
                    {
                        if (value.Pnl >= tradingParameters.TakeProfit)
                        {
                            continue;
                        }

                        if (value.Pnl <= -tradingParameters.Stoploss)
                        {
                            continue;
                        }

                        if (value.Balance < 2 * tradingParameters.DynamicStake)
                        {
                            logger.Warn($"<=> Margin call for Client ID: {pair.Key.Name}. Available balance ({value.Balance}) is insufficient to cover the required stake {2 * tradingParameters.DynamicStake}). Trading paused for this account.");
                            continue;
                        }

                        var timeout = DateTime.Now - value.LossTradeTime;
                        if (timeout < tradesTimeoutThreshold)
                        {
                            continue;
                        }

                        var anyTimeout = DateTime.Now - value.AnyTradeTime;
                        if (anyTimeout < anyTradeTimeoutThreshold)
                        {
                            continue;
                        }

                        Task.Factory.StartNew(() =>
                        {
                            value.Buy(tradingParameters.Symbol.symbol, tradingParameters.Duration, tradingParameters.DurationType, tradingParameters.DynamicStake);
                        });

                        Task.Factory.StartNew(() =>
                        {
                            value.Sell(tradingParameters.Symbol.symbol, tradingParameters.Duration, tradingParameters.DurationType, tradingParameters.DynamicStake);
                        });
                    }
                }

                UpdateGlobalTradingStatus();
            }
            finally
            {
                isTradePending = false;
            }
        }

        /// <summary>
        /// Updates the `isTradingGloballyAllowed` flag based on the current P&L status of all managed accounts.
        /// If all accounts have reached either the Take-Profit or Stop-Loss condition, trading is globally halted.
        /// </summary>
        private void UpdateGlobalTradingStatus()
        {
            // Assume trading should be stopped, then check if any account requires continued trading.
            isTradingGloballyAllowed = false;

            // Iterate through each managed AuthClient.
            foreach (var clientPair in Clients)
            {
                var client = clientPair.Value;
                var tradingParameters = client.TradingParameters;

                // If any account hasn't reached Take-Profit or Stop-Loss, allow trading to continue.
                if (client.Pnl < tradingParameters.TakeProfit &&
                    client.Pnl > -tradingParameters.Stoploss)
                {
                    isTradingGloballyAllowed = true;
                    return;
                }
            }

            // Logging when all accounts have met Take-Profit or Stop-Loss.
            logger.Info("<=> All accounts have met Take-Profit/Stop-Loss condition. Trading attempts halted.");

            // Detach the RSI Crossover event handler to prevent further trade attempts.
            if (rsi != null)
            {
                rsi.Crossover -= OnCrossover;
            }
        }

        /// <summary>
        /// Stops all managed `AuthClient` instances, disconnecting from Deriv accounts and halting trading activity.
        /// </summary>
        public void StopAll()
        {
            // Iterate through each AuthClient.
            foreach (var client in clients.Values)
            {
                // Stop the AuthClient, disconnecting from the Deriv API.
                client.Stop();
                // Raise the ClientsStateChanged event to notify the UI or other components.
                ClientsStateChanged?.Raise(client, EventArgs.Empty);
            }

            // Reset the global trading flag, allowing trading to resume if StartAll()("start button is pressed") is called again.
            isTradingGloballyAllowed = true;

            // Log the reset of the global trading flag.
            logger.Info("<=> Global trading flag has been reset!");
        }

        /// <summary>
        /// Event handler triggered when the state of a managed AuthClient changes.
        /// Raises the ClientsStateChanged event to notify listeners.
        /// <param name="sender">The object that raised the event (the AuthClient whose state has changed).</param>
        /// <param name="args">State changed event arguments.</param>
        /// </summary>
        private void HandleStateChanged(object sender, StateChangedArgs args)
        {
            // Raise the ClientsStateChanged event to notify the UI or other components about the client's state change.
            ClientsStateChanged?.Raise(sender, EventArgs.Empty);
        }

        /// <summary>
        /// Removes a trading account from the list of managed clients based on its API token and App ID.
        /// <param name="appId">The App ID of the trading account to remove.</param>
        /// <param name="token">The API token of the trading account to remove.</param>
        /// </summary>
        public void Remove(string appId, string token)
        {
            // Find the credentials matching the provided App ID and token.
            var found = Credentials.FirstOrDefault(x => x.AppId == appId && x.Token == token);

            // If the credentials are not found, return.
            if (found == null)
            {
                return;
            }
            // Otherwise:
            // Remove the credentials from the list of managed credentials.
            Credentials.Remove(found);

            // If an AuthClient exists for the credentials, stop it and remove it from the clients dictionary.
            if (clients.TryGetValue(found, out var client))
            {
                // Stop the AuthClient to disconnect from the Deriv API.
                client.Stop();
                // Remove the AuthClient from the dictionary.
                clients.Remove(found);

                // Detach event handlers from the client.
                client.StateChanged -= HandleStateChanged;
                client.BalanceChanged -= OnBalanceChanged;
                client.TradeChanged -= OnTradeChanged;
                client.AuthFailed -= OnAuthFailed;
            }

            // Log the removal of the credentials.
            logger.Info("<=> Removing client credentials. Client ID - {0}, Key - {1}", found.AppId, found.Token);

            // Save the updated credentials to the file.
            Save();
        }

        /// <summary>
        /// Enables or disables a trading account for trading based on its API token and App ID.
        /// <param name="enable">True to enable the account for trading, false to disable it.</param>
        /// <param name="appId">The App ID of the trading account.</param>
        /// <param name="token">The API token of the trading account.</param>
        /// </summary>
        public void EnableCredentials(bool enable, string appId, string token)
        {
            // Find the credentials matching the provided App ID and token.
            var found = Credentials.FirstOrDefault(x => x.AppId == appId && x.Token == token);

            // If the credentials are not found, return.
            if (found == null)
            {
                return;
            }

            // Update the IsChecked property of the credentials, indicating whether the account is enabled for trading.
            found.IsChecked = enable;
            // Save the updated credentials to the file.
            Save();
        }

        // Saves the current list of managed credentials to the file.
        private void Save()
        {
            // Serialize the credentials to JSON and write them to the file.
            File.WriteAllText(path, JsonConvert.SerializeObject(Credentials));
        }

        // The list of managed API credentials (tokens and Client IDs).
        public List<Credentials> Credentials { get; set; } = new List<Credentials>();

        // A flag that can be set externally to explicitly allow or disallow trading across all managed accounts.
        public bool IsTradingAllowed { get; set; }


        /// <summary>
        /// Sets the trading parameters for all managed `AuthClient` instances.
        /// <param name="parameters">The trading parameters to apply to all accounts.</param>
        /// </summary>
        public void SetTradingParameters(TradingParameters parameters)
        {
            // Iterate through each AuthClient and set its trading parameters.
            foreach (var value in Clients.Values)
            {
                // Clone the trading parameters to avoid modifying the original object.
                value.TradingParameters = (TradingParameters)parameters.Clone();
                // Initialize a new list for recovery results for each account.
                // value.TradingParameters.recoveryResults = new List<decimal>();
            }
        }
    }

    /// <summary>
    /// Event arguments for the AuthFailed event. Contains the credentials that failed authentication.
    /// </summary>
    public class AuthFailedArgs : EventArgs
    {
        // The API credentials that failed authentication.
        public Credentials Credentials { get; }

        /// <summary>
        /// Constructor for AuthFailedArgs.
        /// <param name="credentials">The credentials that failed authentication.</param>
        /// </summary>
        public AuthFailedArgs(Credentials credentials)
        {
            this.Credentials = credentials;
        }
    }

    /// <summary>
    /// Data structure to hold parameters related to market data and technical indicators.
    /// </summary>
    public class MarketDataParameters
    {
        // The trading symbol for which market data is subscribed.
        public string Symbol { get; set; }

        // The configured RSI indicator instance.
        public Rsi Rsi { get; set; }

    }

    /// <summary>
    /// Data structure to hold both market data parameters and trading parameters.
    /// This is used to store and load application settings and configurations.
    /// </summary>
    public class Layout
    {
        public MarketDataParameters MarketDataParameters { get; set; }
        public TradingParameters TradingParameters { get; set; }
        public PhaseParameters Phase2Parameters { get; set; }
        public Dictionary<int, CustomLayerConfig> CustomLayerConfigs { get; set; }
    }
}