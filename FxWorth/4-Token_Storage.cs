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
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        private bool isTradingGloballyAllowed = true;
        public static int MaxTokensCount = 150;
        private static int slowInternetConst = 650;

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

            TradeUpdated?.Raise(this, e); // Raise the TradeUpdated event to notify the UI or other components about the trade update.
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
                        hierarchyNavigator.LoadLevelTradingParameters(currentLevelId, value, clientParams);
                        clientParams.DynamicStake = hierarchyNavigator.GetCurrentLevel()?.InitialStake ?? clientParams.Stake;
                    }

                    if (IsHierarchyMode)
                    {
                        if (hierarchyClient != value) 
                        {
                            continue;
                        }

                        HierarchyLevel currentLevel = hierarchyNavigator.GetCurrentLevel();
                        if (currentLevel != null)
                        {
                            hierarchyNavigator.LoadLevelTradingParameters(currentLevel.LevelId, value, clientParams);

                            logger.Info($"Hierarchy Trade - Client: {credentials.AppId}, Level: {currentLevel.LevelId}, AmountToRecover: {currentLevel.AmountToRecover}, Stake: {clientParams.DynamicStake}, Barrier: {clientParams.TempBarrier}");

                            Task.Factory.StartNew(() =>
                            {
                                if (clientParams.Symbol != null)
                                {
                                    value.Buy(clientParams.Symbol.symbol, clientParams.Duration,
                                        clientParams.DurationType, clientParams.DynamicStake);
                                }
                                else { logger.Error($"Symbol is null for client {credentials.AppId} in hierarchy mode."); }
                            });

                            Task.Factory.StartNew(() =>
                            {
                                if (clientParams.Symbol != null)
                                {
                                    value.Sell(clientParams.Symbol.symbol, clientParams.Duration,
                                        clientParams.DurationType, clientParams.DynamicStake);
                                }
                                else { logger.Error($"Symbol is null for client {credentials.AppId} in hierarchy mode."); }
                            });
                        }
                        else
                        {
                            logger.Error($"Current hierarchy level ({hierarchyNavigator?.currentLevelId}) is null or not found for client {credentials.AppId}.");
                        }
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
                            else { logger.Error($"Symbol is null for client {credentials.AppId} in normal mode."); }
                        });

                        Task.Factory.StartNew(() =>
                        {
                            if (clientParams.Symbol != null)
                            {
                                value.Sell(clientParams.Symbol.symbol, clientParams.Duration, clientParams.DurationType, clientParams.DynamicStake);
                            }
                            else { logger.Error($"Symbol is null for client {credentials.AppId} in normal mode."); }
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
                
                clientParameters.TakeProfit = credentials.ProfitTarget;
                
                clientParameters.TakeProfitReached += OnTakeProfitReached;

                client.TradingParameters = clientParameters;

                logger.Debug($"<=> Applied TakeProfit {clientParameters.TakeProfit} to client {credentials.AppId}");
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

            client.TradingParameters = null;
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

                    if (IsHierarchyMode)
                    {
                        HandleHierarchyTradeUpdate(model, client);
                    }
                    else
                    {
                        HandleNormalTradeUpdate(model, client);
                    }
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Error processing trade update");
                }
            }
        }

        // Handles trade updates in hierarchy mode, processing the trade model and updating the hierarchy level.
        private void HandleHierarchyTradeUpdate(TradeModel model, AuthClient client)
        {
            HierarchyLevel currentLevel = hierarchyNavigator.GetCurrentLevel();

            if (currentLevel != null)
            {
                // Use the maximum payout from the model's Payouts list
                decimal maxPayout = model.Payouts.Max();

                // Store original recovery state for diagnostic logging
                bool wasInRecoveryMode = client.TradingParameters.IsRecoveryMode;
                decimal originalDynamicStake = client.TradingParameters.DynamicStake;
                decimal originalAmountToRecover = client.TradingParameters.AmountToBeRecoverd;

                // Process the trade result - this handles recovery state automatically
                client.TradingParameters.Process(model.Profit, maxPayout, int.Parse(client.GetToken()), model.Id, 0);

                // Log recovery state changes
                if (!wasInRecoveryMode && client.TradingParameters.IsRecoveryMode)
                {
                    logger.Info($"Entered recovery mode: Amount to recover {client.TradingParameters.AmountToBeRecoverd}, Dynamic stake {client.TradingParameters.DynamicStake}");
                }
                else if (wasInRecoveryMode && !client.TradingParameters.IsRecoveryMode)
                {
                    logger.Info($"Exited recovery mode: Recovery successful");
                }
                else if (wasInRecoveryMode && client.TradingParameters.IsRecoveryMode)
                {
                    logger.Info($"Updated recovery: Amount change {originalAmountToRecover} -> {client.TradingParameters.AmountToBeRecoverd}, Stake change {originalDynamicStake} -> {client.TradingParameters.DynamicStake}");
                }

                // Update the level with the current trading parameters (including recovery state)
                currentLevel.UpdateFromTradingParameters(client.TradingParameters);

                // Check if level needs to create new layer due to MaxDrawdown
                if (currentLevel.HasExceededMaxDrawdown)
                {
                    decimal maxDrawdown = currentLevel.MaxDrawdown ?? 
                        (currentLevel.LevelId.StartsWith("1.") ? phase2Parameters.MaxDrawdown : phase1Parameters.MaxDrawdown);

                    if (currentLevel.CurrentRecoveryAmount > maxDrawdown && 
                        currentLevel.LevelId.Split('.').Length < hierarchyNavigator.maxHierarchyDepth)
                    {
                        CreateNewLayer(currentLevel, client);
                    }
                    return; // Don't proceed with level transition if we created a new layer
                }

                // Handle level transitions when recovery is complete
                bool shouldAttemptLevelTransition = false;
                
                // ALWAYS track trades for the current level, regardless of recovery mode
                // This ensures profits are tracked even when IsRecoveryMode=False
                if (model.Profit != 0)
                {
                    // Update the level's recovery results with this trade
                    if (!currentLevel.RecoveryResults.Contains(model.Profit))
                    {
                        currentLevel.RecoveryResults.Add(model.Profit);
                        logger.Info($"Added trade result ${model.Profit:F2} to level {currentLevel.LevelId}'s results");
                    }
                    
                    // Force the level into recovery mode if needed
                    if (!client.TradingParameters.IsRecoveryMode && model.Profit < 0)
                    {
                        logger.Info($"Forcing level {currentLevel.LevelId} into recovery mode to track trades properly");
                        client.TradingParameters.IsRecoveryMode = true;
                        client.TradingParameters.AmountToBeRecoverd = currentLevel.AmountToRecover;
                    }
                }
                
                // Check if we should try moving to the next level
                decimal totalProfit = currentLevel.GetTotalProfit();
                decimal totalLoss = currentLevel.GetTotalLoss();
                decimal amountNeeded = currentLevel.AmountToRecover;
                
                // Log recovery status for debugging
                logger.Info($"Recovery status for level {currentLevel.LevelId}: Profit=${totalProfit:F2}, Loss=${totalLoss:F2}, Target=${amountNeeded:F2}, Level completions: {hierarchyNavigator.layer1CompletedLevels}/{hierarchyNavigator.hierarchyLevelsCount}");
                
                // Level should be completed if profit meets or exceeds needed amount
                if (totalProfit >= amountNeeded)
                {
                    logger.Info($"Level {currentLevel.LevelId} should be completed - target amount recovered (${totalProfit:F2} >= ${amountNeeded:F2})");
                    currentLevel.IsCompleted = true;
                    shouldAttemptLevelTransition = true;
                }
                
                // Try moving to next level if recovery is complete
                if (shouldAttemptLevelTransition || 
                    (currentLevel.IsRecoverySuccessful && !client.TradingParameters.IsRecoveryMode))
                {
                    // Store the current level ID before attempting to move
                    string previousLevelId = hierarchyNavigator.currentLevelId;
                    
                    // Log attempt to move levels
                    logger.Info($"Attempting to move from level {previousLevelId} (IsCompleted: {currentLevel.IsCompleted}, IsRecoverySuccessful: {currentLevel.IsRecoverySuccessful})");
                    
                    // Attempt to move to next level
                    bool moved = hierarchyNavigator.MoveToNextLevel(client);

                    // Only proceed with parameter loading if the level actually changed
                    if (moved && hierarchyNavigator.currentLevelId != previousLevelId)
                    {
                        if (hierarchyNavigator.currentLevelId == "0")
                        {
                            logger.Info("Returned to root level trading.");
                        }
                        else
                        {
                            hierarchyNavigator.LoadLevelTradingParameters(hierarchyNavigator.currentLevelId, client, client.TradingParameters);
                            logger.Info($"Successfully moved to next level: {hierarchyNavigator.currentLevelId}");
                        }
                    }
                    else
                    {
                        logger.Info($"Failed to move from level {previousLevelId}. Current level remains {hierarchyNavigator.currentLevelId}");
                    }
                }
                
                // Always ensure we're using the correct parameters for the current level
                // but only if we haven't created a new layer (which would have already loaded parameters)
                HierarchyLevel updatedLevel = hierarchyNavigator.GetCurrentLevel();
                if (updatedLevel != null && updatedLevel.LevelId == currentLevel.LevelId)
                {
                    // No need to reload parameters if we just processed a trade and are in recovery mode
                    // This is the key - let the recovery system work without interference
                    if (!client.TradingParameters.IsRecoveryMode)
                    {
                        hierarchyNavigator.LoadLevelTradingParameters(hierarchyNavigator.currentLevelId, client, client.TradingParameters);
                    }
                    
                    logger.Info($"Trading in level {hierarchyNavigator.currentLevelId}, " +
                                $"Amount to recover: {updatedLevel.CurrentRecoveryAmount}, " +
                                $"IsRecoveryMode: {client.TradingParameters.IsRecoveryMode}, " +
                                $"DynamicStake: {client.TradingParameters.DynamicStake}");
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
            decimal maxPayout = model.Payouts.Max();
            client.TradingParameters.Process(model.Profit, maxPayout, int.Parse(client.GetToken()), model.Id, 0);

            if (client.TradingParameters.AmountToBeRecoverd > client.TradingParameters.MaxDrawdown && !IsHierarchyMode)
            {
                EnterHierarchyMode(client);
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