using System;
using System.Collections.Generic;
using System.Linq;
using FxApi;
using FxApi.Connection;
using NLog;

namespace FxWorth.Hierarchy
{
    /// <summary>
    /// The `HierarchyNavigator` class manages the hierarchy of trading levels for a recovery strategy.
    /// Its engaged once an api token exceeds its MaxDrawdown.
    /// </summary>
    public class HierarchyNavigator
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        private Dictionary<string, HierarchyLevel> hierarchyLevels;
        private List<string> levelOrder;
        public string currentLevelId { get; set; }
        public int hierarchyLevelsCount { get; private set; }
        public int maxHierarchyDepth { get; private set; }
        private PhaseParameters phase1Params;
        private PhaseParameters phase2Params;
        private readonly TokenStorage storage;
        public bool IsInHierarchyMode { get; private set; } = false;
        internal int layer1CompletedLevels = 0;
        private Dictionary<string, AuthClient> levelClients = new Dictionary<string, AuthClient>();

        /// <summary>
        /// Initializes a new instance of the `HierarchyNavigator` class to navigate through the layers and thier levels of the hierarchy instance engaged
        /// </summary>
        public HierarchyNavigator(decimal amountToBeRecovered, TradingParameters tradingParameters, PhaseParameters phase1Params, PhaseParameters phase2Params, Dictionary<int, CustomLayerConfig> customLayerConfigs, decimal initialStakeLayer1, TokenStorage storage)
        {
            this.hierarchyLevelsCount = tradingParameters.HierarchyLevels;
            this.maxHierarchyDepth = tradingParameters.MaxHierarchyDepth;
            this.phase1Params = phase1Params;
            this.phase2Params = phase2Params;
            this.storage = storage;

            hierarchyLevels = new Dictionary<string, HierarchyLevel>();
            levelOrder = new List<string>();

            if (amountToBeRecovered > phase1Params.MaxDrawdown)
            {
                CreateHierarchy(amountToBeRecovered, tradingParameters, customLayerConfigs, initialStakeLayer1);
            }
        }        
        
        // This method is used to assign a client to a specific level in the hierarchy.
        public void AssignClientToLevel(string levelId, AuthClient client)
        {
            if (hierarchyLevels.ContainsKey(levelId))
            {
                levelClients[levelId] = client;
                
                // Create fresh trading parameters for this level following the same pattern as root level
                // Get the base parameters from the current client's configuration
                if (client.TradingParameters != null)
                {
                    LoadLevelTradingParameters(levelId, client, client.TradingParameters);
                }
                else
                {
                    logger.Error($"Cannot assign client to level {levelId} - client has null trading parameters");
                }
            }
        }

        // This method is used to retrieve the client associated with a specific level in the hierarchy.
        public AuthClient GetClientForLevel(string levelId)
        {
            return levelClients.TryGetValue(levelId, out var client) ? client : null;
        }

        /// <summary>
        /// Method called when a clients api token exceeds its MaxDrawdown, creating a hierarchy of levels and layer to aid recovery.
        /// Exits Root level trading and enters the hierarchy mode.
        /// </summary>
        /// <param name="amountToBeRecovered"> This is the amount to be recovered from the previous level.</param>
        /// <param name="tradingParameters"> Takes in the trading parameters of the client.</param>
        /// <param name="customLayerConfigs"> Infers the custom layer configurations for the client from the storage.</param>
        /// <param name="initialStakeLayer1"> This is the initial stake to be used for the first layer.</param>
        private void CreateHierarchy(decimal amountToBeRecovered, TradingParameters tradingParameters, Dictionary<int, CustomLayerConfig> customLayerConfigs, decimal initialStakeLayer1)
        {
            IsInHierarchyMode = true; // Enter hierarchy mode

            // Only create Layer 1 initially
            CreateLayer(1, amountToBeRecovered, tradingParameters, customLayerConfigs, initialStakeLayer1);
            currentLevelId = "1.1";
        }

        /// This Create Layer method is called when the maximum drawdown in a level is reached.
        public void CreateLayer(int layerNumber, decimal amountToBeRecovered, TradingParameters tradingParameters, Dictionary<int, CustomLayerConfig> customLayerConfigs, decimal initialStake)
        {
            logger.Info($"Creating Layer {layerNumber} for amount {amountToBeRecovered:F2}");
            
            CustomLayerConfig customConfig = GetCustomConfigForLayer(layerNumber, customLayerConfigs);

            // Determine number of levels for this layer and store for later use
            hierarchyLevelsCount = Math.Max(2, customConfig?.HierarchyLevels ?? hierarchyLevelsCount);
            decimal amountPerLevel = Math.Round(amountToBeRecovered / hierarchyLevelsCount, 2);

            logger.Info($"Layer {layerNumber} will have {hierarchyLevelsCount} levels, {amountPerLevel:F2} per level when fully created");

            // Create just the first level of this layer - others will be created as needed
            string levelId = $"{layerNumber}.1";

            // Get parameters, prioritizing custom config over phase parameters
            int? martingaleLevel = customConfig?.MartingaleLevel ?? 
                (layerNumber == 1 ? phase2Params.MartingaleLevel : phase1Params.MartingaleLevel);
            
            decimal? maxDrawdown = customConfig?.MaxDrawdown ?? 
                (layerNumber == 1 ? phase2Params.MaxDrawdown : phase1Params.MaxDrawdown);
            
            decimal? barrierOffset = customConfig?.BarrierOffset ?? 
                (layerNumber == 1 ? phase2Params.Barrier : phase1Params.Barrier);

            // Determine initial stake for this level
            decimal levelInitialStake = DetermineLevelInitialStake(layerNumber, customConfig, initialStake);

            // Create and configure the new level
            HierarchyLevel newLevel = new HierarchyLevel(
                levelId,
                amountPerLevel,
                levelInitialStake,
                martingaleLevel,
                maxDrawdown,
                barrierOffset
            );

            hierarchyLevels[levelId] = newLevel;
            levelOrder.Add(levelId);

            logger.Info($"Created Level {levelId}: AmountToRecover={amountPerLevel:F2}, " +
                       $"InitialStake={levelInitialStake:F2}, MartingaleLevel={martingaleLevel}, " +
                       $"MaxDrawdown={maxDrawdown:F2}, BarrierOffset={barrierOffset:F2}");

            // Check if this level's amount exceeds its MaxDrawdown and needs to create a new layer
            if (amountPerLevel > (maxDrawdown ?? decimal.MaxValue) && layerNumber < maxHierarchyDepth)
                {
                logger.Info($"Level {levelId} amount ({amountPerLevel:F2}) exceeds MaxDrawdown ({maxDrawdown:F2}). Creating new layer.");
                    CreateLayer(layerNumber + 1, amountPerLevel, tradingParameters, customLayerConfigs, levelInitialStake);
                }
            }

        private decimal DetermineLevelInitialStake(int layerNumber, CustomLayerConfig customConfig, decimal defaultInitialStake)
        {
            // For Layer 1, use the provided defaultInitialStake if no custom config
            if (layerNumber == 1)
            {
                return customConfig?.InitialStake ?? defaultInitialStake;
            }

            // For layers beyond Layer 1, must use custom config's initial stake
            if (customConfig?.InitialStake != null)
            {
                return customConfig.InitialStake.Value;
            }

            // If no custom config for higher layers, log warning and use a safe default
            logger.Warn($"No custom configuration found for Layer {layerNumber}. This may lead to incorrect stake values.");
            return defaultInitialStake;
        }


        /// <summary>
        /// The `HierarchyLevel` class represents a single level in the hierarchy of the trading strategy.
        /// </summary>
        public class HierarchyLevel
        {
            public string LevelId { get; set; }
            public decimal AmountToRecover { get; set; }
            public decimal InitialStake { get; set; }
            public int? MartingaleLevel { get; set; }            
            public decimal? MaxDrawdown { get; set; }
            public decimal? BarrierOffset { get; set; }
            public List<decimal> RecoveryResults { get; set; } = new List<decimal>();
            public bool IsCompleted { get; set; }
            public bool HasExceededMaxDrawdown => GetTotalLoss() > (MaxDrawdown ?? decimal.MaxValue);
            public decimal CurrentRecoveryAmount => GetTotalLoss();
            public bool IsRecoverySuccessful => GetTotalProfit() >= AmountToRecover;
            public decimal GetTotalLoss()
            {
                return RecoveryResults.Where(r => r < 0).Sum(r => -r);
            }
            
            // Get total profit from positive trade results
            public decimal GetTotalProfit()
            {
                return RecoveryResults.Where(r => r > 0).Sum();
            }
            // Cache the latest dynamic stake used in recovery
            public decimal CurrentDynamicStake { get; set; }

            public HierarchyLevel(string levelId, decimal amountToBeRecovered, decimal initialStake, int? martingaleLevel, decimal? maxDrawdown, decimal? barrierOffset)
            {
                LevelId = levelId;
                AmountToRecover = amountToBeRecovered;
                InitialStake = initialStake;
                CurrentDynamicStake = initialStake;
                MartingaleLevel = martingaleLevel;
                MaxDrawdown = maxDrawdown;
                BarrierOffset = barrierOffset;
                IsCompleted = false;
            }            
            
            public void UpdateRecoveryResults(List<decimal> newResults)
            {
                if (newResults == null || !newResults.Any())
                    return;

                RecoveryResults = new List<decimal>(newResults);
                IsCompleted = IsRecoverySuccessful;
            }

            // Update level state from trading parameters to keep them synchronized
            public void UpdateFromTradingParameters(TradingParameters tradingParameters)
            {
                if (tradingParameters == null)
                    return;
                
                // Update recovery results
                if (tradingParameters.RecoveryResults.Any())
                {
                    RecoveryResults = new List<decimal>(tradingParameters.RecoveryResults);
                    
                    // Log recovery progress using correct metrics
                    decimal profitAmount = GetTotalProfit();
                    decimal lossAmount = GetTotalLoss();
                    decimal netAmount = profitAmount - lossAmount;
                    
                    logger.Info($"Level {LevelId} recovery progress: Total Profit=${profitAmount:F2}, Total Loss=${lossAmount:F2}, Net=${netAmount:F2}, Target=${AmountToRecover:F2}");
                    
                    // Check if recovery is successful (total profit exceeds target)
                    if (profitAmount >= AmountToRecover)
                    {
                        logger.Info($"Level {LevelId} recovery target met: ${profitAmount:F2} >= ${AmountToRecover:F2} (required amount)");
                        IsCompleted = true;
                    }                }
                
                // NOTE: Do NOT update AmountToRecover from trading parameters!
                // Each hierarchy level should maintain its fixed target amount.
                // The trading parameters' AmountToBeRecoverd is dynamic and changes during recovery,
                // but the level's AmountToRecover should remain constant as the level's target.
                
                // Update dynamic stake - keep track of the current stake being used
                if (tradingParameters.DynamicStake > 0)
                {
                    CurrentDynamicStake = tradingParameters.DynamicStake;
                }
                
                // Force update completion state
                IsCompleted = IsRecoverySuccessful;
            }            public void Reset()
            {
                RecoveryResults.Clear();
                IsCompleted = false;
                CurrentDynamicStake = InitialStake;
            }
        }

        // This method creates the next level in the same layer (e.g., from 1.1 to 1.2)
        public string CreateNextLevelInLayer(string currentLevelId)
        {
            if (string.IsNullOrEmpty(currentLevelId) || !currentLevelId.Contains("."))
            {
                logger.Error($"Cannot create next level: Invalid level ID format {currentLevelId}");
                return null;
            }

            string[] parts = currentLevelId.Split('.');
            int currentLayer = int.Parse(parts[0]);
            int currentLevelNumber = int.Parse(parts[1]);
            
            // Check if we're at the max level count for this layer
            if (currentLevelNumber >= hierarchyLevelsCount)
            {
                logger.Info($"Cannot create next level in layer {currentLayer}: Already at max level {currentLevelNumber} of {hierarchyLevelsCount}");
                return null;
            }
            
            string nextLevelId = $"{currentLayer}.{currentLevelNumber + 1}";
            
            // Don't create if it already exists
            if (hierarchyLevels.ContainsKey(nextLevelId))
            {
                logger.Info($"Level {nextLevelId} already exists, not creating it again");
                return nextLevelId;
            }
            
            // Find the parameters from the current level to use as a template
            HierarchyLevel currentLevel = hierarchyLevels[currentLevelId];
            
            // Get the appropriate custom config
            CustomLayerConfig customConfig = GetCustomConfigForLayer(currentLayer, storage.customLayerConfigs);
            
            // Create new level with the same parameters
            HierarchyLevel newLevel = new HierarchyLevel(
                nextLevelId,
                currentLevel.AmountToRecover, // Same amount as current level
                currentLevel.InitialStake,
                currentLevel.MartingaleLevel,
                currentLevel.MaxDrawdown,
                currentLevel.BarrierOffset
            );
            
            hierarchyLevels[nextLevelId] = newLevel;
            levelOrder.Add(nextLevelId);
            
            logger.Info($"Created next Level {nextLevelId}: AmountToRecover={newLevel.AmountToRecover:F2}, " +
                       $"InitialStake={newLevel.InitialStake:F2}, MartingaleLevel={newLevel.MartingaleLevel}, " +
                       $"MaxDrawdown={newLevel.MaxDrawdown:F2}, BarrierOffset={newLevel.BarrierOffset:F2}");
                       
            return nextLevelId;
        }

        // This method moves through the hierarchy levels based on the current level ID.        
        public bool MoveToNextLevel(AuthClient client)
        {
            if (string.IsNullOrEmpty(currentLevelId) || currentLevelId == "0")
            {
                logger.Error("Cannot move to next level: Invalid current level ID");
                return false;
            }

            var currentLevel = GetCurrentLevel();
            if (currentLevel == null)
            {
                logger.Error($"Cannot move to next level: Current level {currentLevelId} not found");
                return false;
            }
            
            // Log detailed level state for debugging
            logger.Info($"MoveToNextLevel check for level {currentLevelId}: " +
                      $"IsCompleted={currentLevel.IsCompleted}, " +
                      $"RecoveryResults.Count={currentLevel.RecoveryResults.Count}, " +
                      $"TotalProfit=${currentLevel.GetTotalProfit():F2}, " +
                      $"AmountToRecover=${currentLevel.AmountToRecover:F2}");
            
            // If the level is already marked as completed (e.g., in OnTakeProfitReached),
            // then we don't need additional checks - proceed with the transition
            if (!currentLevel.IsCompleted)
            {
                // Only recalculate completion status if not already completed
                if (currentLevel.RecoveryResults.Any())
                {
                    decimal profitAmount = currentLevel.GetTotalProfit();
                    decimal lossAmount = currentLevel.GetTotalLoss();
                    
                    logger.Info($"Level {currentLevelId} movement check: Profit=${profitAmount:F2}, Loss=${lossAmount:F2}, Target=${currentLevel.AmountToRecover:F2}");
                    
                    if (profitAmount >= currentLevel.AmountToRecover)
                    {
                        logger.Info($"Level {currentLevelId} has recovered ${profitAmount:F2} which exceeds needed amount ${currentLevel.AmountToRecover:F2}");
                        currentLevel.IsCompleted = true;
                    }
                }
                
                // Don't move if level hasn't processed any trades or isn't completed
                if (!currentLevel.IsCompleted)
                {
                    logger.Info($"Cannot move from level {currentLevelId}: Level has not been marked as completed");
                    return false;
                }
            }

            // Log exit from current level
            logger.Info($"Exiting Level: {currentLevelId} (Completed: {currentLevel.IsCompleted}, " +
                        $"Recovery Amount: {currentLevel.CurrentRecoveryAmount}, MaxDrawdown: {currentLevel.MaxDrawdown})");

            string[] parts = currentLevelId.Split('.');
            int currentLayer = int.Parse(parts[0]);
            int currentLevelNumber = int.Parse(parts[1]);
            string newLevelId = null;

            // If we're in a nested layer (layer > 1), first try to move up to parent level
            if (currentLayer > 1 && currentLevel.IsCompleted)
            {
                // Get parent level ID by removing the last part
                newLevelId = string.Join(".", parts.Take(parts.Length - 1));
                logger.Info($"Moving up from nested layer {currentLevelId} to parent level {newLevelId}");
            }
            // If in Layer 1
            else if (currentLayer == 1)
            {
                if (currentLevel.IsCompleted)
                {
                    layer1CompletedLevels++;
                    logger.Info($"Completed level {currentLevelId} - that's {layer1CompletedLevels} of {hierarchyLevelsCount} levels in Layer 1");
                    
                    if (layer1CompletedLevels >= hierarchyLevelsCount)                    
                    {
                        // All Layer 1 levels completed - exit hierarchy mode
                        IsInHierarchyMode = false;
                        newLevelId = "0";
                        layer1CompletedLevels = 0;
                        client.TradingParameters = null;
                        
                        logger.Info("Layer 1 fully recovered. Returning to root level trading.");
                    }
                    else
                    {
                        // Create and move to next level in Layer 1
                        newLevelId = CreateNextLevelInLayer(currentLevelId);
                        if (newLevelId != null)                        
                        {
                            logger.Info($"Moving to next Level 1 position. Entering Level: {newLevelId}");
                        }
                        else
                        {
                            logger.Error($"Failed to create next level after {currentLevelId}");
                            return false;
                        }
                    }
                }
            }
            // If we get here and the level is completed, create and move to next level in same layer
            else if (currentLevel.IsCompleted && currentLevelNumber < hierarchyLevelsCount)
            {
                newLevelId = CreateNextLevelInLayer(currentLevelId);
                if (newLevelId != null)
                {
                    logger.Info($"Moving to next level in same layer. Entering Level: {newLevelId}");
                }
                else
                {
                    logger.Error($"Failed to create next level after {currentLevelId}");
                    return false;
                }
            }

            // Only update the current level ID if we've determined a valid new level
            if (!string.IsNullOrEmpty(newLevelId))
            {
                currentLevelId = newLevelId;
                if (newLevelId != "0")  // Don't assign client for root level
                {
                    AssignClientToLevel(newLevelId, client);
                }
                return true;
            }

            // If we get here, we're staying in the current level
            logger.Info($"No movement: Staying in current level {currentLevelId}");
            return false;
        }        
        
        /// This method creates fresh trading parameters for the hierarchy level following the same pattern as root level initialization.
        /// It creates a new TradingParameters object instead of modifying existing ones, ensuring clean parameter initialization.
        public void LoadLevelTradingParameters(string levelId, AuthClient client, TradingParameters baseTradingParameters)
        {
            if (!hierarchyLevels.TryGetValue(levelId, out HierarchyLevel level))
            {
                logger.Error($"Level {levelId} not found in hierarchy");
                return;
            }

            logger.Info($"Creating fresh trading parameters for hierarchy level {levelId} following root level pattern");

            // Create fresh TradingParameters object using the same pattern as root level initialization
            // This ensures clean parameter state without interference from previous trading calculations
            var freshParameters = new TradingParameters()
            {
                // Copy base trading configuration from existing parameters
                Barrier = baseTradingParameters.Barrier,
                Symbol = baseTradingParameters.Symbol,
                Duration = baseTradingParameters.Duration,
                DurationType = baseTradingParameters.DurationType,
                Stake = baseTradingParameters.Stake,
                HierarchyLevels = baseTradingParameters.HierarchyLevels,
                MaxHierarchyDepth = baseTradingParameters.MaxHierarchyDepth,
                
                // Set level-specific recovery configuration
                TakeProfit = level.AmountToRecover,
                AmountToBeRecoverd = level.AmountToRecover,
                IsRecoveryMode = level.RecoveryResults.Any(),
                DynamicStake = level.InitialStake,
                LevelInitialStake = level.InitialStake,
                
                RecoveryResults = new List<decimal>(level.RecoveryResults)
            };

            // Apply hierarchy level parameters with proper precedence
            int layerNumber = int.Parse(levelId.Split('.')[0]);
            CustomLayerConfig customConfig = GetCustomConfigForLayer(layerNumber, storage.customLayerConfigs);

            if (layerNumber > 1 && customConfig != null)
            {
                // Layer 2+ with custom configuration
                freshParameters.MartingaleLevel = customConfig.MartingaleLevel ?? level.MartingaleLevel ?? phase1Params.MartingaleLevel;
                freshParameters.MaxDrawdown = customConfig.MaxDrawdown ?? level.MaxDrawdown ?? phase1Params.MaxDrawdown;
                freshParameters.TempBarrier = customConfig.BarrierOffset ?? level.BarrierOffset ?? phase1Params.Barrier;
            }
            else if (layerNumber == 1)
            {
                // Layer 1 - use phase 2 parameters
                freshParameters.MartingaleLevel = level.MartingaleLevel ?? phase2Params.MartingaleLevel;
                freshParameters.MaxDrawdown = level.MaxDrawdown ?? phase2Params.MaxDrawdown;
                freshParameters.TempBarrier = level.BarrierOffset ?? phase2Params.Barrier;
            }
            else
            {
                // Layer 2+ without custom configuration - use phase 1 parameters
                freshParameters.MartingaleLevel = level.MartingaleLevel ?? phase1Params.MartingaleLevel;
                freshParameters.MaxDrawdown = level.MaxDrawdown ?? phase1Params.MaxDrawdown;
                freshParameters.TempBarrier = level.BarrierOffset ?? phase1Params.Barrier;
            }

            // Use TokenStorage.SetTradingParameters to apply the fresh parameters following the established pattern
            // This creates proper parameter clones and sets up event handlers
            var tempParameters = freshParameters;
            storage.SetTradingParameters(tempParameters);

            logger.Info($"Created fresh trading parameters for level {levelId}: TakeProfit=${freshParameters.TakeProfit:F2}, " +
                        $"Stake=${freshParameters.Stake:F2}, DynamicStake=${freshParameters.DynamicStake:F2}, " +
                        $"MartingaleLevel={freshParameters.MartingaleLevel}, MaxDrawdown={freshParameters.MaxDrawdown}.");
        }

        public HierarchyLevel GetCurrentLevel()
        {
            if (string.IsNullOrEmpty(currentLevelId))
            {
                logger.Warn("currentLevelId is null or empty. Cannot get current level.");
                return null;
            }

            if (hierarchyLevels.TryGetValue(currentLevelId, out HierarchyLevel currentLevel))
            {
                return currentLevel;
            }
            else
            {
                logger.Error($"Level with ID '{currentLevelId}' not found in hierarchy.");
                return null;
            }
        }

        private CustomLayerConfig GetCustomConfigForLayer(int layerNumber, Dictionary<int, CustomLayerConfig> customLayerConfigs)
        {
            if (customLayerConfigs.TryGetValue(layerNumber, out CustomLayerConfig config))
            {
                return config;
            }
            return null;
        }

        private bool ExistsCustomConfigForLayer(int layerNumber, Dictionary<int, CustomLayerConfig> customLayerConfigs)
        {
            return customLayerConfigs.ContainsKey(layerNumber);
        }

        public decimal GetLayer1TotalAmountToBeRecovered()
        {
            decimal total = 0;
            foreach (var level in hierarchyLevels.Values)
            {
                if (level.LevelId.StartsWith("1."))
                {
                    total += level.AmountToRecover;
                }
            }
            return total;
        }
    }
}