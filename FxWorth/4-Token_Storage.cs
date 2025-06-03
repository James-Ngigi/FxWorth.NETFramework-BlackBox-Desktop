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
{    /// <summary>
    /// The `TokenStorage` class is the core component of the FxWorth application. 
    /// It manages multiple trading accounts (represented by API tokens and App IDs(Masked as Acc ID/Client ID)), handles connections to 
    /// the Deriv API, subscribes to market data, implements trading logic based on the RSI technical indicator, 
    /// and executes trades. It also monitors internet latency and ensures responsible API usage.
    /// </summary>

    public class TokenStorage
    {        
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        private bool isTradingGloballyAllowed = true;
        public static int MaxTokensCount = 150;
        private static int slowInternetConst = 6650;
        
        // Delegate to get UI trading parameters
        public Func<TradingParameters> GetUITradingParameters { get; set; }

        public Dictionary<int, CustomLayerConfig> customLayerConfigs = new Dictionary<int, CustomLayerConfig>();

        private static TimeSpan tradesTimeoutThreshold = TimeSpan.FromSeconds(0.001);
        private static TimeSpan anyTradeTimeoutThreshold = TimeSpan.FromSeconds(0.001);
        private readonly string path;

        private Dictionary<Credentials, AuthClient> clients = new Dictionary<Credentials, AuthClient>();
        private PingClient pinger = new PingClient(new Credentials() { AppId = "70216" });
        private MarketDataClient marketDataClient = new MarketDataClient(new Credentials() { AppId = "70216" });
        public EventHandler<EventArgs> ClientsStateChanged;
        public EventHandler<EventArgs> InternetSpeedChanged;
        public EventHandler<AuthFailedArgs> AuthFailed;
        public EventHandler<TradeEventArgs> TradeUpdated;

        private bool isTradePending = false;
        public Rsi rsi;
        private AuthClient eventingClinet;

        public bool IsHierarchyMode => hierarchyNavigator != null && hierarchyNavigator.IsInHierarchyMode;
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

        public void SetHierarchyParameters(PhaseParameters phase1, PhaseParameters phase2, Dictionary<int, CustomLayerConfig> configs)
        {
            phase1Parameters = phase1;
            phase2Parameters = phase2;
            
            // Validate and set custom layer configs
            if (configs != null)
            {
                customLayerConfigs.Clear();

                foreach (var kvp in configs)
                {
                    if (kvp.Key > 1) // Only process configs for layers beyond Layer 1
                    {
                        if (kvp.Value.BarrierOffset == null)
                        {
                            logger.Warn($"Layer {kvp.Key} missing barrier offset. Using phase1 barrier as default.");
                            kvp.Value.BarrierOffset = phase1.Barrier;
                        }
                        if (kvp.Value.InitialStake == null)
                        {
                            logger.Warn($"Layer {kvp.Key} missing initial stake.");
                        }
                        if (kvp.Value.MartingaleLevel == null)
                        {
                            logger.Warn($"Layer {kvp.Key} missing martingale level. Using phase1 martingale level as default.");
                            kvp.Value.MartingaleLevel = phase1.MartingaleLevel;
                        }
                        if (kvp.Value.MaxDrawdown == null)
                        {
                            logger.Warn($"Layer {kvp.Key} missing max drawdown. Using phase1 max drawdown as default.");
                            kvp.Value.MaxDrawdown = phase1.MaxDrawdown;
                        }
                        customLayerConfigs[kvp.Key] = kvp.Value;
                    }
                }
            }
            else
            {
                customLayerConfigs.Clear();
            }
        }

        /// <summary>
        /// Constructor for the `TokenStorage` class.
        /// Initializes the `PingClient`, `MarketDataClient`, loads credentials from the specified file, and 
        /// sets up event handlers for monitoring network status and trading activity.
        /// <param name="path">The path to the file where API tokens and Client IDs are stored.</param>
        /// </summary>
        public TokenStorage(string path)
        {
            pinger.Start();
            marketDataClient.Start();
            pinger.PingChanged += PingChanged;

            this.path = path;
            clientStateCheckTimer = new Timer(12000);
            clientStateCheckTimer.Elapsed += ClientStateCheckTimer_Elapsed;
            clientStateCheckTimer.Start();

            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);

                if (!string.IsNullOrWhiteSpace(json))
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

        /// Event handler triggered when the ping latency changes.
        public void PingChanged(object sender, EventArgs e)
        {
            InternetSpeedChanged?.Raise(sender, EventArgs.Empty);
        }

        /// Determines if the current internet connection is considered slow based on the measured latency.
        public bool IsInternetSlow()
        {
            return pinger.Latency >= slowInternetConst;
        }

        public PingClient PingClient => pinger;
        public MarketDataClient MarketDataClient => marketDataClient;
        public Dictionary<Credentials, AuthClient> Clients => clients;

        // When called, it disposes of the `PingClient` and `MarketDataClient` instances, stopping network monitoring and market data subscriptions.
        public void Dispose()
        {
            pinger.Stop();
            pinger.PingChanged -= PingChanged;
            marketDataClient.Stop();

            clientStateCheckTimer.Stop();
            clientStateCheckTimer.Dispose();
        }

        /// Adds a new trading account to the list of managed lucky clients.
        public bool Add(Credentials creds)
        {
            logger.Info("<=> Adding client credentials. Client ID - {0}, Key - {1}, Profit Target - {2}", creds.AppId, creds.Token, creds.ProfitTarget);

            // Dummy proffing! Prevent adding duplicate credentials for the sake 
            if (Credentials.Any(x => x.AppId == creds.AppId && x.Token == creds.Token))
            {
                return false;
            }

            Credentials.Add(creds);
            Save();

            var client = new AuthClient(creds, 0);
            clients.Add(creds, client);
            client.StateChanged += HandleStateChanged;
            client.BalanceChanged += OnBalanceChanged;
            client.TradeChanged += OnTradeChanged;
            client.AuthFailed += OnAuthFailed;
            return true;
        }

        /// Event handler triggered when authentication with a Deriv account fails.
        private void OnAuthFailed(object sender, EventArgs e)
        {
            var pair = Clients.First(x => x.Value == sender);
            if (pair.Key.IsChecked)
            {
                AuthFailed.Raise(this, new AuthFailedArgs(pair.Key));
            }
        }

        /// Event handler triggered when a trade on a managed account is updated & raises the TradeUpdated event to propagate the trade update to listeners.
        private void OnTradeChanged(object sender, TradeEventArgs e)
        {
            if (sender != eventingClinet)
            {
                return;
            }

            TradeUpdated?.Raise(this, e);
        }

        /// Event handler triggered when the balance of a managed account changes & raises the ClientsStateChanged event to notify listeners.
        private void OnBalanceChanged(object sender, EventArgs e)
        {
            ClientsStateChanged?.Raise(sender, EventArgs.Empty);
        }

        // Starts all managed `AuthClient` instances, initiating connections to Deriv accounts.
        public void StartAll()
        {
            // Iterate through each AuthClient in the clients dictionary.
            foreach (var client in clients.Values)
            {
                ClientsStateChanged?.Raise(client, EventArgs.Empty);
                client.Start();
            }
        }

        /// Subscribes to market data for a specific symbol and configures technical indicators (RSI)
        public MarketDataParameters SubscribeMarketData(
            int rsiPeriod,
            double rsiOverbought,
            double rsiOversold,
            int rsiTimeframe,
            string symbol)
        {
            MarketDataClient.UnsubscribeAll();

            if (rsi != null)
            {
                rsi.Crossover -= OnCrossover;
            }

            rsi = new Rsi
            {
                Period = rsiPeriod,
                Overbought = rsiOverbought,
                Oversold = rsiOversold,
                TimeFrame = rsiTimeframe
            };

            rsi.Crossover += OnCrossover;
            MarketDataClient.Subscribe(symbol, rsiTimeframe, rsi);

            return new MarketDataParameters { Rsi = rsi, Symbol = symbol };
        }



        /* -----------------------------------------SENSITIVE AREA----------------------------------------------------- */

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
                    isTradePending = false;
                    return;
                }

                if (IsInternetSlow())
                {
                    isTradePending = false;
                    return;
                }

                if (!IsTradingAllowed)
                {
                    logger.Info("<=> Entry blocked by **--IsTradingAllowed = false--** condition.");
                    isTradePending = false;
                    return;
                }

                foreach (var pair in Clients)
                {
                    var value = pair.Value;
                    var credentials = pair.Key;

                    if (!credentials.IsChecked || !value.IsOnline)
                    {
                        continue; 
                    }

                    var clientParams = value.TradingParameters;
                    if (clientParams == null)
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

                    if (clientParams.AmountToBeRecoverd > clientParams.MaxDrawdown && (hierarchyNavigator == null || !hierarchyNavigator.IsInHierarchyMode))
                    {
                        hierarchyClient = value;
                        decimal initialStakeForHierarchy = clientParams.InitialStake4Layer1 > 0 ? clientParams.InitialStake4Layer1 : clientParams.Stake;                        
                        hierarchyNavigator = new HierarchyNavigator(clientParams.AmountToBeRecoverd, clientParams, phase1Parameters, phase2Parameters, customLayerConfigs, initialStakeForHierarchy, this);
                        
                        currentLevelId = "1.1";

                        hierarchyNavigator.AssignClientToLevel(currentLevelId, value);

                        // Use SetTradingParameters method approach for initial hierarchy entry and level assignment
                        SetHierarchyLevelTradingParameters(value);
                        clientParams.DynamicStake = hierarchyNavigator.GetCurrentLevel()?.InitialStake ?? clientParams.Stake;
                    } 
                    
                    if (IsHierarchyMode)
                    {
                        if (hierarchyClient == value)
                        {
                            HierarchyLevel currentLevel = hierarchyNavigator.GetCurrentLevel();
                            if (currentLevel != null)
                            {
                                logger.Info($"Hierarchy Trade - Client: {credentials.AppId}, Level: {currentLevel.LevelId}, AmountToRecover: {currentLevel.AmountToRecover}, Stake: {clientParams.DynamicStake}, Barrier: {clientParams.TempBarrier}");
                            }
                        }

                        // Execute trades for this client (regardless of whether it's the hierarchy client)
                        Task.Factory.StartNew(() =>
                        {
                            if (clientParams.Symbol != null)
                            {
                                value.Buy(clientParams.Symbol.symbol, clientParams.Duration, clientParams.DurationType, clientParams.DynamicStake);
                            }
                            else { logger.Error($"Symbol is null for client {credentials.AppId} in hierarchy mode."); }
                        });

                        Task.Factory.StartNew(() =>
                        {
                            if (clientParams.Symbol != null)
                            {
                                value.Sell(clientParams.Symbol.symbol, clientParams.Duration, clientParams.DurationType, clientParams.DynamicStake);
                            }
                            else { logger.Error($"Symbol is null for client {credentials.AppId} in hierarchy mode."); }
                        });
                    }
                    else 
                    {
                        if (value.Pnl >= clientParams.TakeProfit)
                        {
                            continue;
                        }

                        if (value.Pnl <= -clientParams.Stoploss)
                        {
                            continue;
                        }

                        if (value.Balance < 2 * clientParams.DynamicStake)
                        {
                            logger.Warn($"<=> Margin call for Client ID: {credentials.Name}. Available balance ({value.Balance}) is insufficient to cover the required stake ({2 * clientParams.DynamicStake}). Trading paused for this account.");
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
                            if (clientParams.Symbol != null)
                            {
                                value.Buy(clientParams.Symbol.symbol, clientParams.Duration, clientParams.DurationType, clientParams.DynamicStake);
                            }
                            else 
                            { 
                                logger.Error($"Symbol is null for client {credentials.AppId} in normal mode."); 
                            }
                        });

                        Task.Factory.StartNew(() =>
                        {
                            if (clientParams.Symbol != null)
                            {
                                value.Sell(clientParams.Symbol.symbol, clientParams.Duration, clientParams.DurationType, clientParams.DynamicStake);
                            }
                            else 
                            { 
                                logger.Error($"Symbol is null for client {credentials.AppId} in normal mode."); 
                            }
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
        /// In hierarchy mode, only stop trading when completely exiting hierarchy (back to root level).
        /// </summary>
        private void UpdateGlobalTradingStatus()
        {
            // In hierarchy mode, keep trading until we exit hierarchy completely
            if (IsHierarchyMode)
            {
                isTradingGloballyAllowed = true;
                return;
            }

            isTradingGloballyAllowed = false;

            foreach (var clientPair in Clients)
            {
                var client = clientPair.Value;
                var tradingParameters = client.TradingParameters;

                if (tradingParameters == null)
                {
                    continue; 
                }

                if (client.Pnl == 0 || (client.Pnl < tradingParameters.TakeProfit &&
                    client.Pnl > -tradingParameters.Stoploss))
                {
                    isTradingGloballyAllowed = true;
                    return;
                }
            }

            logger.Info("<=> All active accounts have met Take-Profit/Stop-Loss condition. Trading attempts halted.");

            if (rsi != null)
            {
                rsi.Crossover -= OnCrossover;
            }
        }

        /// Stops all managed `AuthClient` instances, disconnecting from Deriv accounts and halting trading activity.
        public void StopAll()
        {
            foreach (var client in clients.Values)
            {
                client.Stop();
                
                // Reset trading parameters
                if (client.TradingParameters != null)
                {
                    client.TradingParameters.IsRecoveryMode = false;
                    client.TradingParameters.DynamicStake = client.TradingParameters.Stake;
                    client.TradingParameters.TempBarrier = 0;
                    client.TradingParameters.RecoveryResults.Clear();
                    client.TradingParameters.AmountToBeRecoverd = 0;
                }
                
                ClientsStateChanged?.Raise(client, EventArgs.Empty);
            }

            // Reset hierarchy state
            hierarchyNavigator = null;
            hierarchyClient = null;
            currentLevelId = null;
            hierarchyLevels.Clear();
            isTradingGloballyAllowed = true;
            logger.Info("<=> Global trading flag has been reset and hierarchy state cleared!");
        }

        /// Event handler triggered when the state of a managed AuthClient changes and notifies listeners
        private void HandleStateChanged(object sender, StateChangedArgs args)
        {
            ClientsStateChanged?.Raise(sender, EventArgs.Empty);
        }

        /// Removes a trading account from the list of managed clients based on its API token and App ID.
        public void Remove(string appId, string token)
        {
            var found = Credentials.FirstOrDefault(x => x.AppId == appId && x.Token == token);

            if (found == null)
            {
                return;
            }

            Credentials.Remove(found);

            if (clients.TryGetValue(found, out var client))
            {
                client.Stop();
                clients.Remove(found);
                client.StateChanged -= HandleStateChanged;
                client.BalanceChanged -= OnBalanceChanged;
                client.TradeChanged -= OnTradeChanged;
                client.AuthFailed -= OnAuthFailed;
            }

            logger.Info("<=> Removing client credentials. Client ID - {0}, Key - {1}.", found.AppId, found.Token);
            Save();
        }

        /// Enables or disables a trading account for trading based on its API token and App ID.
        public void EnableCredentials(bool enable, string appId, string token)
        {
            var found = Credentials.FirstOrDefault(x => x.AppId == appId && x.Token == token);

            if (found == null)
            {
                return;
            }

            found.IsChecked = enable;
            Save();
        }

        // Saves the current list of managed credentials to the file.
        private void Save()
        {
            File.WriteAllText(path, JsonConvert.SerializeObject(Credentials));
        }

        public List<Credentials> Credentials { get; set; } = new List<Credentials>();
        public bool IsTradingAllowed { get; set; }        
          
        /// Sets the trading parameters for each managed `AuthClient` instance based on the provided base parameters.
        /// In hierarchy mode, configures parameters for the current hierarchy level.
        public void SetTradingParameters(TradingParameters baseParameters)
        {
            foreach (var clientPair in clients)
            {
                var credentials = clientPair.Key;
                var client = clientPair.Value;

                // Unsubscribe from the old TradingParameters event if we are replacing it
                if (client.TradingParameters != null)
                {
                    client.TradingParameters.TakeProfitReached -= OnTakeProfitReached;
                }

                var clientParameters = (TradingParameters)baseParameters.Clone();
                
                // Check if this client is in hierarchy mode
                if (IsHierarchyMode && client == hierarchyClient)
                {
                    SetHierarchyLevelParameters(clientParameters, client);
                }
                else
                {
                    // Normal mode (root level) - use credentials profit target
                    clientParameters.TakeProfit = credentials.ProfitTarget;
                }
                
                clientParameters.TakeProfitReached += OnTakeProfitReached;

                client.TradingParameters = clientParameters;

                logger.Debug($"<=> Applied TakeProfit {clientParameters.TakeProfit} to api token client {credentials.Token}");
            }
        }        
        
        /// Configures trading parameters for the current hierarchy level
        private void SetHierarchyLevelParameters(TradingParameters clientParameters, AuthClient client)
        {
            HierarchyLevel currentLevel = hierarchyNavigator.GetCurrentLevel();
            if (currentLevel == null)
            {
                logger.Error("Cannot set hierarchy parameters - current level is null");
                return;
            }

            // CRITICAL: Set take profit to the level's AmountToRecover (this is the level's target)
            clientParameters.TakeProfit = currentLevel.AmountToRecover;
            
            // Configure level-specific stake parameters that Process() method will use
            clientParameters.Stake = currentLevel.InitialStake;
            clientParameters.LevelInitialStake = currentLevel.InitialStake;
            
            // Only reset DynamicStake if not in active recovery to preserve recovery calculations
            if (!clientParameters.IsRecoveryMode || clientParameters.DynamicStake <= 0)
            {
                clientParameters.DynamicStake = currentLevel.InitialStake;
            }
            
            // Apply level-specific or phase-specific parameters based on layer
            if (currentLevel.LevelId.StartsWith("1."))
            {
                // Phase 2 parameters (level 1.x)
                clientParameters.Barrier = currentLevel.BarrierOffset ?? phase2Parameters.Barrier;
                clientParameters.MaxDrawdown = currentLevel.MaxDrawdown ?? phase2Parameters.MaxDrawdown;
                clientParameters.MartingaleLevel = currentLevel.MartingaleLevel ?? phase2Parameters.MartingaleLevel;
            }
            else
            {
                // Phase 1 parameters (level 2.x, 3.x, etc.)
                clientParameters.Barrier = currentLevel.BarrierOffset ?? phase1Parameters.Barrier;
                clientParameters.MaxDrawdown = currentLevel.MaxDrawdown ?? phase1Parameters.MaxDrawdown;
                clientParameters.MartingaleLevel = currentLevel.MartingaleLevel ?? phase1Parameters.MartingaleLevel;
            }

            // Set temp barrier for the level (used by Process method to know we're in hierarchy)
            clientParameters.TempBarrier = clientParameters.Barrier;
            
            logger.Info($"Configured hierarchy level {currentLevel.LevelId} - TakeProfit: ${clientParameters.TakeProfit:F2}, " +
                       $"Stake: ${clientParameters.Stake:F2}, Barrier: {clientParameters.Barrier}, " +
                       $"MartingaleLevel: {clientParameters.MartingaleLevel}, MaxDrawdown: {clientParameters.MaxDrawdown}");
        }
        
        /// Sets trading parameters specifically for a hierarchy level transition
        public void SetHierarchyLevelTradingParameters(AuthClient client)
        {
            if (!IsHierarchyMode || client != hierarchyClient || hierarchyNavigator == null)
                return;

            var currentLevel = hierarchyNavigator.GetCurrentLevel();

            if (currentLevel == null)
            {
                logger.Error("Cannot set hierarchy level parameters - current level is null");
                return;
            }

            // Only perform full parameter setup for new levels or null parameters
            if (client.TradingParameters == null)
            {
                logger.Info($"Initializing trading parameters for new hierarchy level {hierarchyNavigator.currentLevelId}");
               
                new TradingParameters();
            }
            else
            {
                // For level transitions with existing parameters, only update the essential hierarchy-specific values
                logger.Info($"Updating existing parameters for level transition to {hierarchyNavigator.currentLevelId}");
                
                // Update only the hierarchy-specific parameters without resetting recovery state
                SetHierarchyLevelParameters(client.TradingParameters, client);
                
                // Ensure the take profit is updated for the new level
                client.TradingParameters.TakeProfit = currentLevel.AmountToRecover;
                
                logger.Info($"Updated TakeProfit to {currentLevel.AmountToRecover:F2} for level {hierarchyNavigator.currentLevelId}");
            }
        }
        
        /// Event handler triggered when the take profit target is reached for a managed `AuthClient` instance.
        private void OnTakeProfitReached(object sender, decimal totalProfit)
        {
            var tradingParameters = (TradingParameters)sender;
            
            var client = clients.Values.FirstOrDefault(c => c.TradingParameters == tradingParameters);
            if (client == null)
            {
                return;
            }

            var credentials = clients.FirstOrDefault(x => x.Value == client).Key;
            if (credentials == null)
            {
                return;
            }            
            logger.Info($"<=> Take profit target reached for client : {credentials.Token}! Total Profit: {totalProfit:C}");
            
            // In hierarchy mode, don't clear trading parameters - let the hierarchy system handle transitions
            if (IsHierarchyMode && client == hierarchyClient)
            {
                logger.Info("Take profit reached in hierarchy mode - attempting level transition");
                
                // First, get the current level and log its state
                var currentLevel = hierarchyNavigator?.GetCurrentLevel();

                if (currentLevel != null)
                {
                    // Log comprehensive level state for debugging
                    logger.Info($"Level state before transition: Level={currentLevel.LevelId}, " +
                               $"IsCompleted={currentLevel.IsCompleted}, " +
                               $"RecoveryResults.Count={currentLevel.RecoveryResults.Count}, " +
                               $"TotalProfit={currentLevel.GetTotalProfit():F2}, " +
                               $"AmountToRecover={currentLevel.AmountToRecover:F2}");
                    
                    // Mark the level as completed when take profit is reached
                    currentLevel.IsCompleted = true;
                    logger.Info($"Level {currentLevel.LevelId} marked as completed due to take profit target reached");
                }
                
                // Try to move to the next level in the hierarchy
                if (hierarchyNavigator != null && hierarchyNavigator.MoveToNextLevel(client))
                {
                    // Check if we've exited hierarchy mode (returned to root level)
                    if (hierarchyNavigator.currentLevelId == "0" || !hierarchyNavigator.IsInHierarchyMode)
                    {
                        logger.Info("Hierarchy recovery completed - returning to root level trading");
                        // Reset hierarchy state
                        hierarchyNavigator = null;
                        hierarchyClient = null;
                        currentLevelId = null;
                        // Clear trading parameters to stop trading (root level behavior)
                        client.TradingParameters = null;
                    }
                    else
                    {
                        logger.Info("Successfully transitioned to next hierarchy level");
                        SetHierarchyLevelTradingParameters(client);
                    }
                }
                else
                {
                    logger.Info("Could not transition to next level - staying in current level");
                }
            }
            else
            {
                // Normal mode: clear trading parameters to stop trading
                client.TradingParameters = null;
            }
            
            ClientsStateChanged?.Raise(client, EventArgs.Empty);
        }

        /// Event handler triggered when a trade update is received from a managed `AuthClient` instance.
        private void OnTradeUpdate(object sender, TradeEventArgs e)
        {
            lock (tradeUpdateLock)
            {
                try
                {
                    TradeModel model = e.Model;
                    var client = e.Client;

                    if (client == null)
                    {
                        logger.Warn("Trade update received with null client");
                        return;
                    }                    
               
                    TradeUpdated?.Invoke(this, e);
                    HandleNormalTradeUpdate(model, client);
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Error processing trade update");
                }
            }        
        }

        // Creates a new layer in the hierarchy based on the current level and assigns the client to it.
        private void CreateNewLayer(HierarchyLevel currentLevel, AuthClient client)
        {
            int nextLayer = currentLevel.LevelId.Split('.').Length + 1;

            decimal initialStakeForNextLayer;
            if (nextLayer == 2)
            {
                initialStakeForNextLayer = customLayerConfigs.ContainsKey(nextLayer) ?
                    (customLayerConfigs[nextLayer].InitialStake ?? InitialStakeLayer1) :
                    InitialStakeLayer1;
            }
            else
            {
                initialStakeForNextLayer = customLayerConfigs.ContainsKey(nextLayer) ?
                    (customLayerConfigs[nextLayer].InitialStake ?? currentLevel.InitialStake) :
                    currentLevel.InitialStake;
            }

            hierarchyNavigator.CreateLayer(nextLayer, currentLevel.AmountToRecover, client.TradingParameters, customLayerConfigs, initialStakeForNextLayer);

            string nextLevelId = $"{currentLevel.LevelId}.1";
            hierarchyNavigator.currentLevelId = nextLevelId;
            hierarchyNavigator.AssignClientToLevel(nextLevelId, client);
            logger.Info($"Created new layer {nextLayer} and moved to level: {nextLevelId}");
        }        
        
        // Handles trade updates in normal mode, processing the trade model and updating the trading parameters.
        private void HandleNormalTradeUpdate(TradeModel model, AuthClient client)
        {
            // Avoid processing the same trade multiple times
            if (model.IsClosed)
            {
                decimal maxPayout = model.Payouts.Max();
                
                if (IsHierarchyMode && client == hierarchyClient && hierarchyNavigator != null)
                {
                    var currentLevel = hierarchyNavigator.GetCurrentLevel();
                    if (currentLevel != null)
                    {
                        // Sync hierarchy level state with trading parameters
                        currentLevel.UpdateFromTradingParameters(client.TradingParameters);
                        
                        if (model.Profit != 0) 
                        {
                            // Make sure we're not duplicating the result
                            if (!currentLevel.RecoveryResults.Contains(model.Profit))
                            {
                                // Add this result to the level's tracking
                                currentLevel.RecoveryResults.Add(model.Profit);
                                
                                // Also update the trading parameters with this result to keep them in sync
                                if (!client.TradingParameters.RecoveryResults.Contains(model.Profit))
                                {
                                    client.TradingParameters.RecoveryResults.Add(model.Profit);
                                }

                                logger.Info($"Added profit {model.Profit:F2} to level {currentLevel.LevelId} recovery results");
                                client.TradingParameters.RecalculateTotalProfit();
                            }
                            
                            // Check if level has reached its recovery target
                            decimal profitAmount = currentLevel.GetTotalProfit();
                            if (profitAmount >= currentLevel.AmountToRecover)
                            {
                                currentLevel.IsCompleted = true;
                                logger.Info($"Level {currentLevel.LevelId} marked as completed - recovered ${profitAmount:F2} of required ${currentLevel.AmountToRecover:F2}");
                                
                                // Attempt to move to next level immediately after a profitable trade
                                // that meets the target - don't wait for next RSI signal
                                if (hierarchyNavigator.MoveToNextLevel(client))
                                {
                                    logger.Info($"Immediately transitioned to next level after reaching target");
                                }
                            }
                            
                            logger.Info($"Updated hierarchy level {hierarchyNavigator.currentLevelId} with trade result: Profit={model.Profit:F2}, TotalProfit={profitAmount:F2}, IsCompleted={currentLevel.IsCompleted}, TotalRecoveryItems={currentLevel.RecoveryResults.Count}");
                        }
                        else
                        {
                            logger.Debug($"Ignoring invalid trade with ID {model.Id} - possible test or placeholder trade");
                        }
                    }
                }

                // Enter hierarchy mode when AmountToBeRecovered exceeds MaxDrawdown
                if (client.TradingParameters.AmountToBeRecoverd > client.TradingParameters.MaxDrawdown && !IsHierarchyMode)
                {
                    EnterHierarchyMode(client);
                }
            }
        }

        // Enters hierarchy mode for a specific client, initializing the hierarchy navigator and assigning the client to the first level.
        private void EnterHierarchyMode(AuthClient client)
        {
            hierarchyClient = client;
            hierarchyNavigator = new HierarchyNavigator(
                client.TradingParameters.AmountToBeRecoverd,
                client.TradingParameters,
                phase1Parameters,
                phase2Parameters,
                customLayerConfigs,
                InitialStakeLayer1,
                this
            );
            currentLevelId = "1.1";
            hierarchyNavigator.AssignClientToLevel(currentLevelId, client);
            logger.Info("Entered hierarchy mode");
        }
    }

    /// Event arguments for the AuthFailed event. Contains the credentials that failed authentication.
    public class AuthFailedArgs : EventArgs
    {
        public Credentials Credentials { get; }

        public AuthFailedArgs(Credentials credentials)
        {
            this.Credentials = credentials;
        }
    }

    /// Data structure to hold parameters related to market data and technical indicators.
    public class MarketDataParameters
    {
        public string Symbol { get; set; }
        public Rsi Rsi { get; set; }

    }

    /// Data structure to hold both market data parameters and trading parameters used to store and load application settings and configurations.
    public class Layout
    {
        public MarketDataParameters MarketDataParameters { get; set; }
        public TradingParameters TradingParameters { get; set; }
        public PhaseParameters Phase2Parameters { get; set; }
        public Dictionary<int, CustomLayerConfig> CustomLayerConfigs { get; set; }
    }
}