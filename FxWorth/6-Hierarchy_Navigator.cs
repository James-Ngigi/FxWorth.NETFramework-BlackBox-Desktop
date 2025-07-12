using System;
using System.Collections.Generic;
using System.Linq;
using FxApi;
using FxApi.Connection;
using NLog;

namespace FxWorth.Hierarchy
{
    /// <summary>
    /// Unified level state management for hierarchy navigation
    /// Encapsulates all state preservation and restoration logic
    /// </summary>
    public class LevelStateManager
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        private readonly Dictionary<string, TradingParameters> savedStates = new Dictionary<string, TradingParameters>();

        /// <summary>
        /// Saves the current trading state for a level
        /// </summary>
        public void SaveLevelState(string levelId, AuthClient client)
        {
            if (client?.TradingParameters == null)
            {
                logger.Warn($"Cannot save state for {levelId} - client or trading parameters are null");
                return;
            }

            try
            {
                var stateBackup = (TradingParameters)client.TradingParameters.Clone();
                savedStates[levelId] = stateBackup;
                
                logger.Info($"Saved state for level {levelId}: " +
                           $"RecoveryResults={stateBackup.RecoveryResults.Count}, " +
                           $"DynamicStake=${stateBackup.DynamicStake:F2}, " +
                           $"AmountToBeRecovered=${stateBackup.AmountToBeRecoverd:F2}, " +
                           $"TakeProfit=${stateBackup.TakeProfit:F2}");
            }
            catch (Exception ex)
            {
                logger.Error($"Failed to save state for level {levelId}: {ex.Message}");
            }
        }

        /// <summary>
        /// Resets TotalProfit to prevent cross-level contamination while preserving other trading state
        /// This ensures each level starts with clean profit tracking
        /// </summary>
        public void ResetTotalProfitForNewLevel(AuthClient client, string levelId)
        {
            if (client?.TradingParameters == null) return;

            decimal previousTotalProfit = client.TradingParameters.TotalProfit;
            client.TradingParameters.ResetForHierarchyTransition();
            
            logger.Info($"Reset TotalProfit for level {levelId}: {previousTotalProfit:F2} -> 0.00 to prevent cross-level contamination");
        }

        /// <summary>
        /// Restores the saved trading state for a level
        /// </summary>
        public bool RestoreLevelState(string levelId, AuthClient client)
        {
            if (!savedStates.TryGetValue(levelId, out TradingParameters savedState))
            {
                logger.Debug($"No saved state found for level {levelId}");
                return false;
            }

            try
            {
                client.TradingParameters = (TradingParameters)savedState.Clone();
                savedStates.Remove(levelId); // Clean up after restoration
                
                logger.Info($"Restored state for level {levelId}: " +
                           $"RecoveryResults={savedState.RecoveryResults.Count}, " +
                           $"DynamicStake=${savedState.DynamicStake:F2}, " +
                           $"AmountToBeRecovered=${savedState.AmountToBeRecoverd:F2}, " +
                           $"TakeProfit=${savedState.TakeProfit:F2}");
                
                return true;
            }
            catch (Exception ex)
            {
                logger.Error($"Failed to restore state for level {levelId}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Checks if a saved state exists for a level
        /// </summary>
        public bool HasSavedState(string levelId)
        {
            return savedStates.ContainsKey(levelId);
        }

        /// <summary>
        /// Clears all saved states (for cleanup)
        /// </summary>
        public void ClearAllStates()
        {
            logger.Info($"Clearing {savedStates.Count} saved level states");
            savedStates.Clear();
        }
    }

    /// <summary>
    /// Level relationship analyzer for navigation logic
    /// Encapsulates all parent-child relationship identification
    /// </summary>
    public class LevelRelationshipAnalyzer
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Determines the relationship between current level and potential next level
        /// </summary>
        public enum LevelRelationship
        {
            SameLayer,      // Moving to next level in same layer (1.1 -> 1.2)
            ChildLevel,     // Moving to child level (1.1 -> 1.1.1)
            ParentLevel,    // Moving to parent level (1.1.1 -> 1.1)
            RootLevel,      // Moving to root level (any -> 0)
            Invalid         // Invalid transition
        }

        /// <summary>
        /// Gets the parent level ID for a given level
        /// </summary>
        public string GetParentLevelId(string levelId)
        {
            if (string.IsNullOrEmpty(levelId) || !levelId.Contains("."))
                return "0"; // Root level

            string[] parts = levelId.Split('.');
            if (parts.Length <= 2)
                return "0"; // Already at Layer 1, parent is root

            return string.Join(".", parts.Take(parts.Length - 1));
        }

        /// <summary>
        /// Gets the next level ID in the same layer
        /// </summary>
        public string GetNextLevelInSameLayer(string currentLevelId)
        {
            if (string.IsNullOrEmpty(currentLevelId) || !currentLevelId.Contains("."))
                return null;

            string[] parts = currentLevelId.Split('.');
            int currentNumber = int.Parse(parts[parts.Length - 1]);
            int nextNumber = currentNumber + 1;

            parts[parts.Length - 1] = nextNumber.ToString();
            return string.Join(".", parts);
        }

        /// <summary>
        /// Gets the first child level ID for a given parent
        /// </summary>
        public string GetFirstChildLevelId(string parentLevelId)
        {
            return $"{parentLevelId}.1";
        }

        /// <summary>
        /// Gets the depth of a level (0 = root, 1 = Layer 1, 2 = Layer 2, etc.)
        /// </summary>
        public int GetLevelDepth(string levelId)
        {
            if (levelId == "0")
                return 0;

            if (string.IsNullOrEmpty(levelId) || !levelId.Contains("."))
                return 0;

            return levelId.Split('.').Length - 1;
        }
    }

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
        private Dictionary<int, int> layerLevelCounts = new Dictionary<int, int>(); // Track level count per layer
        private PhaseParameters phase1Params;
        private PhaseParameters phase2Params;
        private readonly TokenStorage storage;
        public bool IsInHierarchyMode { get; private set; } = false;
        internal int layer1CompletedLevels = 0;
        private Dictionary<string, AuthClient> levelClients = new Dictionary<string, AuthClient>();
        // Unified state management and level relationship analysis
        private readonly LevelStateManager stateManager = new LevelStateManager();
        private readonly LevelRelationshipAnalyzer relationshipAnalyzer = new LevelRelationshipAnalyzer();

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
                
                // Check if we're returning to a parent level with saved state
                if (RestoreParentLevelState(levelId, client))
                {
                    logger.Info($"Successfully restored saved state for parent level {levelId}");
                    return; // Don't reset profit when restoring saved state
                }
                
                // Create fresh trading parameters for this level following the same pattern as root level
                // Get the base parameters from the current client's configuration
                if (client.TradingParameters != null)
                {
                    LoadLevelTradingParameters(levelId, client, client.TradingParameters);
                    logger.Info($"Created fresh trading parameters and reset profit for new level {levelId}");
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
            // Check if hierarchy creation is allowed
            if (maxHierarchyDepth == 0)
            {
                logger.Warn($"Cannot create hierarchy: Maximum hierarchy depth is set to 0. No levels can be created from root level.");
                return;
            }

            IsInHierarchyMode = true; // Enter hierarchy mode

            // Only create Layer 1 initially
            CreateLayer(1, amountToBeRecovered, tradingParameters, customLayerConfigs, initialStakeLayer1);
            currentLevelId = "1.1";
        }

        /// This Create Layer method is called when the maximum drawdown in a level is reached.
        public void CreateLayer(int layerNumber, decimal amountToBeRecovered, TradingParameters tradingParameters, Dictionary<int, CustomLayerConfig> customLayerConfigs, decimal initialStake)
        {
            logger.Info($"Creating Layer {layerNumber} for amount {amountToBeRecovered:F2} (Max depth allowed: {maxHierarchyDepth})");
            
            // Check if this layer exceeds max depth
            if (LayerExceedsMaxDepth(layerNumber))
            {
                logger.Error($"Cannot create Layer {layerNumber}: Exceeds maximum hierarchy depth of {maxHierarchyDepth}");
                return;
            }
            
            CustomLayerConfig customConfig = GetCustomConfigForLayer(layerNumber, customLayerConfigs);

            // Determine number of levels for this layer and store per layer
            int layerLevelsCount = Math.Max(2, customConfig?.HierarchyLevels ?? hierarchyLevelsCount);
            layerLevelCounts[layerNumber] = layerLevelsCount; // Store level count for this specific layer
            decimal amountPerLevel = Math.Round(amountToBeRecovered / layerLevelsCount, 2);

            logger.Info($"Layer {layerNumber} will have {layerLevelsCount} levels, {amountPerLevel:F2} per level when fully created");

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
            if (amountPerLevel > (maxDrawdown ?? decimal.MaxValue) && !LayerExceedsMaxDepth(layerNumber + 1))
                {
                logger.Info($"Level {levelId} amount ({amountPerLevel:F2}) exceeds MaxDrawdown ({maxDrawdown:F2}). Creating new layer.");
                    CreateLayer(layerNumber + 1, amountPerLevel, tradingParameters, customLayerConfigs, levelInitialStake);
                }
            else if (amountPerLevel > (maxDrawdown ?? decimal.MaxValue) && LayerExceedsMaxDepth(layerNumber + 1))
            {
                logger.Warn($"Level {levelId} amount ({amountPerLevel:F2}) exceeds MaxDrawdown ({maxDrawdown:F2}), but cannot create new layer - would exceed max depth {maxHierarchyDepth}. Level will trade with higher risk.");
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
            
            // Check if we're at the max level count for this specific layer
            int layerMaxLevels = GetLevelCountForLayer(currentLayer);
            if (currentLevelNumber >= layerMaxLevels)
            {
                logger.Info($"Cannot create next level in layer {currentLayer}: Already at max level {currentLevelNumber} of {layerMaxLevels}");
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

        /// <summary>
        /// Creates the next level in a nested layer (e.g., from "1.1.1" to "1.1.2")
        /// </summary>
        public string CreateNextNestedLevelInLayer(string currentLevelId)
        {
            if (string.IsNullOrEmpty(currentLevelId) || !currentLevelId.Contains("."))
            {
                logger.Error($"Cannot create next nested level: Invalid level ID format {currentLevelId}");
                return null;
            }

            // Check if creating another nested level would exceed max depth
            if (WouldExceedMaxDepth(currentLevelId))
            {
                logger.Warn($"Cannot create next nested level from {currentLevelId}: Would exceed maximum hierarchy depth {maxHierarchyDepth}");
                return null;
            }

            string[] parts = currentLevelId.Split('.');
            if (parts.Length < 3)
            {
                // This is not a nested level, use the regular method
                return CreateNextLevelInLayer(currentLevelId);
            }

            // For nested levels like "1.1.1", increment the last part
            int lastLevelNumber = int.Parse(parts[parts.Length - 1]);
            int nextLevelNumber = lastLevelNumber + 1;
            
            // Build the next level ID
            string nextLevelId = string.Join(".", parts.Take(parts.Length - 1)) + "." + nextLevelNumber;
            
            // Don't create if it already exists
            if (hierarchyLevels.ContainsKey(nextLevelId))
            {
                logger.Info($"Nested level {nextLevelId} already exists, not creating it again");
                return nextLevelId;
            }
            
            // Find the parameters from the current level to use as a template
            HierarchyLevel currentLevel = hierarchyLevels[currentLevelId];
            
            // Determine the layer number from the depth
            // For nested levels: "1.1.1" = Layer 2, "1.1.1.1" = Layer 3, etc.
            int layerDepth = parts.Length;
            int actualLayerForConfig = layerDepth - 1;
            
            CustomLayerConfig customConfig = GetCustomConfigForLayer(actualLayerForConfig, storage.customLayerConfigs);
            
            // Create new nested level with the same parameters as current level
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
            
            logger.Info($"Created next nested Level {nextLevelId}: AmountToRecover={newLevel.AmountToRecover:F2}, " +
                       $"InitialStake={newLevel.InitialStake:F2}, MartingaleLevel={newLevel.MartingaleLevel}, " +
                       $"MaxDrawdown={newLevel.MaxDrawdown:F2}, BarrierOffset={newLevel.BarrierOffset:F2}");
                       
            return nextLevelId;
        }

        /// <summary>
        /// UNIFIED NAVIGATION METHOD - Handles ALL level transitions
        /// This is the ONLY method that should be used for level navigation
        /// </summary>
        public bool NavigateToLevel(AuthClient client, string targetLevelId, string reason = "")
        {
            if (string.IsNullOrEmpty(targetLevelId))
            {
                logger.Error("Cannot navigate: target level ID is null or empty");
                return false;
            }

            string fromLevel = currentLevelId ?? "0";
            logger.Info($"Navigation requested: {fromLevel} -> {targetLevelId} ({reason})");

            // Handle root level transition
            if (targetLevelId == "0")
            {
                return NavigateToRootLevel(client);
            }

            // Analyze the relationship between current and target level
            var relationship = AnalyzeLevelTransition(fromLevel, targetLevelId);
            
            switch (relationship)
            {
                case "parent":
                    return NavigateToParentLevel(client, targetLevelId);
                case "child":
                    return NavigateToChildLevel(client, targetLevelId);
                case "sibling":
                    return NavigateToSiblingLevel(client, targetLevelId);
                default:
                    logger.Error($"Invalid level transition: {fromLevel} -> {targetLevelId}");
                    return false;
            }
        }

        /// <summary>
        /// Analyzes the relationship between two levels
        /// </summary>
        private string AnalyzeLevelTransition(string fromLevel, string toLevel)
        {
            if (fromLevel == "0" || toLevel == "0")
                return "root";

            string[] fromParts = fromLevel.Split('.');
            string[] toParts = toLevel.Split('.');

            // Parent relationship: toLevel has fewer parts and is a prefix of fromLevel
            if (toParts.Length < fromParts.Length)
            {
                bool isParent = true;
                for (int i = 0; i < toParts.Length; i++)
                {
                    if (fromParts[i] != toParts[i])
                    {
                        isParent = false;
                        break;
                    }
                }
                if (isParent) return "parent";
            }

            // Child relationship: toLevel has more parts and fromLevel is a prefix
            if (toParts.Length > fromParts.Length)
            {
                bool isChild = true;
                for (int i = 0; i < fromParts.Length; i++)
                {
                    if (fromParts[i] != toParts[i])
                    {
                        isChild = false;
                        break;
                    }
                }
                if (isChild) return "child";
            }

            // Sibling relationship: same depth, same parent prefix
            if (toParts.Length == fromParts.Length)
            {
                bool sameLevelGroup = true;
                for (int i = 0; i < fromParts.Length - 1; i++)
                {
                    if (fromParts[i] != toParts[i])
                    {
                        sameLevelGroup = false;
                        break;
                    }
                }
                if (sameLevelGroup) return "sibling";
            }

            return "invalid";
        }

        /// <summary>
        /// Navigates to root level (exits hierarchy)
        /// </summary>
        private bool NavigateToRootLevel(AuthClient client)
        {
            logger.Info("Navigating to root level - exiting hierarchy mode");
            IsInHierarchyMode = false;
            currentLevelId = "0";
            
            // Restore root level trading parameters
            storage.RestoreRootLevelTradingParameters(client);
            
            // Clean up all saved states
            stateManager.ClearAllStates();
            
            return true;
        }

        /// <summary>
        /// Navigates to a parent level (moving up in hierarchy)
        /// </summary>
        private bool NavigateToParentLevel(AuthClient client, string parentLevelId)
        {
            logger.Info($"Navigating to parent level: {currentLevelId} -> {parentLevelId}");
            
            // Try to restore saved state for the parent level
            if (stateManager.RestoreLevelState(parentLevelId, client))
            {
                currentLevelId = parentLevelId;
                levelClients[parentLevelId] = client;
                logger.Info($"Successfully restored parent level {parentLevelId} from saved state");
                return true;
            }
            
            // If no saved state, create fresh parameters for the parent level
            if (hierarchyLevels.ContainsKey(parentLevelId))
            {
                currentLevelId = parentLevelId;
                AssignClientToLevel(parentLevelId, client);
                logger.Info($"Created fresh parameters for parent level {parentLevelId}");
                return true;
            }
            
            logger.Error($"Cannot navigate to parent level {parentLevelId} - level does not exist");
            return false;
        }

        /// <summary>
        /// Navigates to a child level (moving down in hierarchy)
        /// </summary>
        private bool NavigateToChildLevel(AuthClient client, string childLevelId)
        {
            logger.Info($"Navigating to child level: {currentLevelId} -> {childLevelId}");
            
            // Save current level state before moving to child
            if (!string.IsNullOrEmpty(currentLevelId) && currentLevelId != "0")
            {
                stateManager.SaveLevelState(currentLevelId, client);
            }
            
            // Check if child level exists, create if needed
            if (!hierarchyLevels.ContainsKey(childLevelId))
            {
                logger.Error($"Child level {childLevelId} does not exist and cannot be created automatically");
                return false;
            }
            
            currentLevelId = childLevelId;
            AssignClientToLevel(childLevelId, client);
            
            // Reset profit tracking for new level to prevent cross-level contamination
            stateManager.ResetTotalProfitForNewLevel(client, childLevelId);
            
            logger.Info($"Successfully navigated to child level {childLevelId}");
            return true;
        }

        /// <summary>
        /// Navigates to a sibling level (same hierarchy depth)
        /// </summary>
        private bool NavigateToSiblingLevel(AuthClient client, string siblingLevelId)
        {
            logger.Info($"Navigating to sibling level: {currentLevelId} -> {siblingLevelId}");
            
            // Check if sibling level exists, create if needed
            if (!hierarchyLevels.ContainsKey(siblingLevelId))
            {
                // Try to create the sibling level
                string createdLevelId = CreateNextLevelInLayer(currentLevelId);
                if (createdLevelId != siblingLevelId)
                {
                    logger.Error($"Failed to create sibling level {siblingLevelId}");
                    return false;
                }
            }
            
            currentLevelId = siblingLevelId;
            AssignClientToLevel(siblingLevelId, client);
            
            // Reset profit tracking for new level to prevent cross-level contamination
            stateManager.ResetTotalProfitForNewLevel(client, siblingLevelId);
            
            logger.Info($"Successfully navigated to sibling level {siblingLevelId}");
            return true;
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
                AmountToBeRecoverd = 0,  // Start fresh, no recovery needed initially
                IsRecoveryMode = false,  // Always start fresh, never in recovery mode initially
                DynamicStake = level.InitialStake,
                LevelInitialStake = level.InitialStake,
                
                // Start with empty recovery results - fresh trading object
                RecoveryResults = new List<decimal>()
            };

            // Apply hierarchy level parameters with proper precedence
            // Calculate the logical layer number for nested levels
            string[] levelParts = levelId.Split('.');
            
            // For nested levels:
            // "1.1" = Layer 1 (depth 2, layer 1)
            // "1.1.1" = Layer 2 (depth 3, layer 2) 
            // "1.1.1.1" = Layer 3 (depth 4, layer 3)
            // etc.
            int actualLayerForConfig = levelParts.Length - 1;
            
            logger.Info($"Level {levelId}: parts={levelParts.Length}, using config for Layer {actualLayerForConfig}");
            
            CustomLayerConfig customConfig = GetCustomConfigForLayer(actualLayerForConfig, storage.customLayerConfigs);

            if (actualLayerForConfig == 1)
            {
                // Layer 1 - use phase 2 parameters (hierarchy recovery parameters)
                freshParameters.MartingaleLevel = customConfig?.MartingaleLevel ?? level.MartingaleLevel ?? phase2Params.MartingaleLevel;
                freshParameters.MaxDrawdown = customConfig?.MaxDrawdown ?? level.MaxDrawdown ?? phase2Params.MaxDrawdown;
                freshParameters.TempBarrier = customConfig?.BarrierOffset ?? level.BarrierOffset ?? phase2Params.Barrier;
                
                logger.Info($"Applied Layer 1 config: MartingaleLevel={freshParameters.MartingaleLevel}, MaxDrawdown={freshParameters.MaxDrawdown}, BarrierOffset={freshParameters.TempBarrier}");
            }
            else if (customConfig != null)
            {
                // Layer 2+ with custom configuration - prioritize custom config
                freshParameters.MartingaleLevel = customConfig.MartingaleLevel ?? level.MartingaleLevel ?? phase1Params.MartingaleLevel;
                freshParameters.MaxDrawdown = customConfig.MaxDrawdown ?? level.MaxDrawdown ?? phase1Params.MaxDrawdown;
                freshParameters.TempBarrier = customConfig.BarrierOffset ?? level.BarrierOffset ?? phase1Params.Barrier;
                
                logger.Info($"Applied Layer {actualLayerForConfig} custom config: MartingaleLevel={freshParameters.MartingaleLevel}, MaxDrawdown={freshParameters.MaxDrawdown}, BarrierOffset={freshParameters.TempBarrier}");
            }
            else
            {
                // Layer 2+ without custom configuration - use phase 1 parameters as default
                freshParameters.MartingaleLevel = level.MartingaleLevel ?? phase1Params.MartingaleLevel;
                freshParameters.MaxDrawdown = level.MaxDrawdown ?? phase1Params.MaxDrawdown;
                freshParameters.TempBarrier = level.BarrierOffset ?? phase1Params.Barrier;
                
                logger.Info($"Applied Layer {actualLayerForConfig} default config (Phase 1): MartingaleLevel={freshParameters.MartingaleLevel}, MaxDrawdown={freshParameters.MaxDrawdown}, BarrierOffset={freshParameters.TempBarrier}");
            }

            // Use TokenStorage.SetTradingParameters to apply the fresh parameters following the established pattern
            // This creates proper parameter clones and sets up event handlers
            var tempParameters = freshParameters;
            storage.SetTradingParameters(tempParameters);
            
            // Ensure clean profit tracking for the new level by resetting TotalProfit
            // This prevents profit accumulation from previous levels from contaminating the new level
            if (client.TradingParameters != null)
            {
                client.TradingParameters.ResetForHierarchyTransition();
                logger.Info($"Reset TotalProfit for fresh level {levelId} to prevent cross-level contamination");
            }

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

        /// <summary>
        /// Creates a nested level under the specified parent level when max drawdown is exceeded.
        /// This creates levels like "1.1.1" under parent "1.1"
        /// </summary>
        public void CreateNestedLevel(string parentLevelId, AuthClient client, decimal amountToBeRecovered, TradingParameters tradingParameters, Dictionary<int, CustomLayerConfig> customLayerConfigs, decimal initialStake)
        {
            if (!hierarchyLevels.ContainsKey(parentLevelId))
            {
                logger.Error($"Cannot create nested level: Parent level {parentLevelId} not found");
                return;
            }

            // Check if creating a nested level would exceed max depth
            if (WouldExceedMaxDepth(parentLevelId))
            {
                logger.Warn($"Cannot create nested level under {parentLevelId}: Would exceed maximum hierarchy depth {maxHierarchyDepth}");
                return;
            }

            // IMPORTANT: Save the parent level's current state before entering nested level
            SaveParentLevelState(parentLevelId, client);

            var parentLevel = hierarchyLevels[parentLevelId];
            string nestedLevelId = $"{parentLevelId}.1";
            
            logger.Info($"Creating nested level {nestedLevelId} under parent {parentLevelId} for amount {amountToBeRecovered:F2}");

            // Determine the layer number from the nested level depth
            // For nested levels:
            // "1.1" = Layer 1, "1.1.1" = Layer 2, "1.1.1.1" = Layer 3, etc.
            string[] parentParts = parentLevelId.Split('.');
            int layerDepth = parentParts.Length + 1; // Adding one more level of nesting
            int actualLayerForConfig = layerDepth - 1; // Convert to layer number
            
            logger.Info($"Creating nested level under {parentLevelId}, depth={layerDepth}, using config for Layer {actualLayerForConfig}");
            
            CustomLayerConfig customConfig = GetCustomConfigForLayer(actualLayerForConfig, customLayerConfigs);

            // Determine number of levels for this nested layer and store it
            int nestedLevelsCount = Math.Max(2, customConfig?.HierarchyLevels ?? hierarchyLevelsCount);
            layerLevelCounts[actualLayerForConfig] = nestedLevelsCount; // Store level count for this nested layer
            decimal amountPerLevel = Math.Round(amountToBeRecovered / nestedLevelsCount, 2);

            logger.Info($"Nested layer at depth {layerDepth} (Layer {actualLayerForConfig}) will have {nestedLevelsCount} levels, {amountPerLevel:F2} per level when fully created");

            // Get parameters, prioritizing custom config over phase parameters
            int? martingaleLevel = customConfig?.MartingaleLevel ?? 
                (actualLayerForConfig == 1 ? phase2Params.MartingaleLevel : phase1Params.MartingaleLevel);
            
            decimal? maxDrawdown = customConfig?.MaxDrawdown ?? 
                (actualLayerForConfig == 1 ? phase2Params.MaxDrawdown : phase1Params.MaxDrawdown);
            
            decimal? barrierOffset = customConfig?.BarrierOffset ?? 
                (actualLayerForConfig == 1 ? phase2Params.Barrier : phase1Params.Barrier);

            // Determine initial stake for this nested level
            decimal levelInitialStake = DetermineLevelInitialStake(actualLayerForConfig, customConfig, initialStake);

            // Create and configure the new nested level
            HierarchyLevel newLevel = new HierarchyLevel(
                nestedLevelId,
                amountPerLevel,
                levelInitialStake,
                martingaleLevel,
                maxDrawdown,
                barrierOffset
            );

            hierarchyLevels[nestedLevelId] = newLevel;
            levelOrder.Add(nestedLevelId);

            logger.Info($"Created nested Level {nestedLevelId}: AmountToRecover={amountPerLevel:F2}, " +
                       $"InitialStake={levelInitialStake:F2}, MartingaleLevel={martingaleLevel}, " +
                       $"MaxDrawdown={maxDrawdown:F2}, BarrierOffset={barrierOffset:F2}");

            // Check if this nested level's amount exceeds its MaxDrawdown and needs further nesting
            if (amountPerLevel > (maxDrawdown ?? decimal.MaxValue))
            {
                if (!WouldExceedMaxDepth(nestedLevelId))
                {
                    logger.Info($"Nested Level {nestedLevelId} amount ({amountPerLevel:F2}) exceeds MaxDrawdown ({maxDrawdown:F2}). Can create deeper nesting if needed.");
                }
                else
                {
                    logger.Warn($"Nested Level {nestedLevelId} amount ({amountPerLevel:F2}) exceeds MaxDrawdown ({maxDrawdown:F2}), but cannot create deeper nesting - would exceed max depth {maxHierarchyDepth}.");
                }
            }
        }

        /// <summary>
        /// Gets the layer number from a level ID (e.g., "1.2" returns 1, "2.1" returns 2)
        /// </summary>
        private int GetLayerNumberFromLevelId(string levelId)
        {
            if (string.IsNullOrEmpty(levelId) || !levelId.Contains("."))
                return 1;
            
            string[] parts = levelId.Split('.');
            return int.Parse(parts[0]);
        }

        /// <summary>
        /// Gets the number of levels for a specific layer
        /// </summary>
        private int GetLevelCountForLayer(int layerNumber)
        {
            int count = layerLevelCounts.TryGetValue(layerNumber, out int levelCount) ? levelCount : hierarchyLevelsCount;
            logger.Debug($"GetLevelCountForLayer({layerNumber}) = {count}");
            return count;
        }

        /// <summary>
        /// Calculates the actual layer depth from a level ID 
        /// "1.1" = depth 1, "1.1.1" = depth 2, "1.1.1.1" = depth 3, etc.
        /// </summary>
        private int GetDepthFromLevelId(string levelId)
        {
            if (string.IsNullOrEmpty(levelId))
                return 0;
            
            string[] parts = levelId.Split('.');
            return parts.Length - 1; // "1.1" has 2 parts = depth 1, "1.1.1" has 3 parts = depth 2
        }

        /// <summary>
        /// Checks if creating a new nested level would exceed the maximum hierarchy depth
        /// </summary>
        private bool WouldExceedMaxDepth(string currentLevelId)
        {
            int currentDepth = GetDepthFromLevelId(currentLevelId);
            int newDepth = currentDepth + 1; // Creating a nested level adds one more depth
            bool wouldExceed = newDepth > maxHierarchyDepth;
            
            logger.Debug($"Depth check for {currentLevelId}: current depth={currentDepth}, new depth would be={newDepth}, max allowed={maxHierarchyDepth}, would exceed={wouldExceed}");
            return wouldExceed;
        }

        /// <summary>
        /// Checks if a layer number exceeds the maximum hierarchy depth
        /// </summary>
        private bool LayerExceedsMaxDepth(int layerNumber)
        {
            bool exceeds = layerNumber > maxHierarchyDepth;
            logger.Debug($"Layer {layerNumber} exceeds max depth {maxHierarchyDepth}: {exceeds}");
            return exceeds;
        }

        /// <summary>
        /// Public method to check if nested levels can be created from a given level
        /// </summary>
        public bool CanCreateNestedLevel(string parentLevelId)
        {
            if (maxHierarchyDepth == 0)
            {
                logger.Debug($"Cannot create nested levels: Max hierarchy depth is 0");
                return false;
            }
            
            bool canCreate = !WouldExceedMaxDepth(parentLevelId);
            logger.Debug($"CanCreateNestedLevel({parentLevelId}): {canCreate} (max depth: {maxHierarchyDepth})");
            return canCreate;
        }

        /// <summary>
        /// Handles moving to the next appropriate level when max drawdown is exceeded 
        /// but depth limits prevent creating nested levels
        /// Uses the unified navigation system
        /// </summary>
        public bool HandleMaxDrawdownExceeded(AuthClient client, string currentLevelId)
        {
            if (!hierarchyLevels.TryGetValue(currentLevelId, out HierarchyLevel currentLevel))
            {
                logger.Error($"Cannot handle max drawdown exceeded: Level {currentLevelId} not found");
                return false;
            }

            logger.Info($"Handling max drawdown exceeded for level {currentLevelId} (depth limits prevent nesting)");

            // Determine the next appropriate level when nesting is not possible
            string nextLevelId = DetermineNextLevelForMaxDrawdown(currentLevelId);
            if (string.IsNullOrEmpty(nextLevelId))
            {
                logger.Warn($"No alternative level available from {currentLevelId} due to max drawdown");
                return false;
            }

            // Use unified navigation to move to the determined level
            return NavigateToLevel(client, nextLevelId, "Max drawdown exceeded, depth limits prevent nesting");
        }

        /// <summary>
        /// Determines the next level when max drawdown is exceeded but nesting is not possible
        /// </summary>
        private string DetermineNextLevelForMaxDrawdown(string currentLevelId)
        {
            string[] parts = currentLevelId.Split('.');
            int currentLayer = int.Parse(parts[0]);
            int currentLevelNumber = int.Parse(parts[1]);

            // Try to create next level in the same layer first
            int layerMaxLevels = GetLevelCountForLayer(currentLayer);
            if (currentLevelNumber < layerMaxLevels)
            {
                // Move to next level in same layer
                return CreateNextLevelInLayer(currentLevelId);
            }

            // No more levels in current layer
            if (parts.Length > 2)
            {
                // We're in a nested level, move up to parent
                return string.Join(".", parts.Take(parts.Length - 1));
            }
            else if (currentLayer == 1)
            {
                // We're in Layer 1 and no more levels available - complete hierarchy
                return "0";
            }
            else
            {
                // We're in a higher layer with no more levels - move back to Layer 1
                return "1." + (layer1CompletedLevels + 1);
            }
        }

        /// <summary>
        /// Saves the current trading parameters state for a level before entering nested levels
        /// </summary>
        private void SaveParentLevelState(string levelId, AuthClient client)
        {
            stateManager.SaveLevelState(levelId, client);
        }

        /// <summary>
        /// Restores the saved trading parameters state when returning to a parent level
        /// </summary>
        private bool RestoreParentLevelState(string levelId, AuthClient client)
        {
            return stateManager.RestoreLevelState(levelId, client);
        }

        /// <summary>
        /// Simplified MoveToNextLevel that automatically determines the next level
        /// and uses the unified navigation system
        /// </summary>
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

            // Check if level is completed
            if (!currentLevel.IsCompleted)
            {
                // Check completion status based on recovery results
                if (currentLevel.RecoveryResults.Any())
                {
                    decimal profitAmount = currentLevel.GetTotalProfit();
                    if (profitAmount >= currentLevel.AmountToRecover)
                    {
                        logger.Info($"Level {currentLevelId} completed: ${profitAmount:F2} >= ${currentLevel.AmountToRecover:F2}");
                        currentLevel.IsCompleted = true;
                    }
                }

                if (!currentLevel.IsCompleted)
                {
                    logger.Info($"Cannot move from level {currentLevelId}: Level not completed");
                    return false;
                }
            }

            // Determine the next level based on hierarchy rules
            string nextLevelId = DetermineNextLevel(currentLevelId);
            if (string.IsNullOrEmpty(nextLevelId))
            {
                logger.Info($"No next level available from {currentLevelId}");
                return false;
            }

            // Use unified navigation to move to the next level
            return NavigateToLevel(client, nextLevelId, "Level completed");
        }

        /// <summary>
        /// Determines the next level ID based on current level and hierarchy rules
        /// </summary>
        private string DetermineNextLevel(string currentLevelId)
        {
            string[] parts = currentLevelId.Split('.');
            int currentLayer = int.Parse(parts[0]);
            int currentLevelNumber = int.Parse(parts[1]);

            // For nested levels (more than 2 parts)
            if (parts.Length > 2)
            {
                int lastLevelNumber = int.Parse(parts[parts.Length - 1]);
                int layerDepth = parts.Length;
                int actualLayerForConfig = layerDepth - 1;
                int nestedLayerMaxLevels = GetLevelCountForLayer(actualLayerForConfig);

                // Try next level in same nested layer
                if (lastLevelNumber < nestedLayerMaxLevels)
                {
                    return CreateNextNestedLevelInLayer(currentLevelId);
                }
                else
                {
                    // Move up to parent level
                    return string.Join(".", parts.Take(parts.Length - 1));
                }
            }

            // For Layer 1 levels
            if (currentLayer == 1)
            {
                layer1CompletedLevels++;
                int layer1TotalLevels = GetLevelCountForLayer(1);

                if (layer1CompletedLevels >= layer1TotalLevels)
                {
                    // All Layer 1 levels completed - exit to root
                    layer1CompletedLevels = 0;
                    return "0";
                }
                else
                {
                    // Move to next level in Layer 1
                    return CreateNextLevelInLayer(currentLevelId);
                }
            }

            // For other layers, try to move to next level in same layer
            int layerMaxLevels = GetLevelCountForLayer(currentLayer);
            if (currentLevelNumber < layerMaxLevels)
            {
                return CreateNextLevelInLayer(currentLevelId);
            }

            // No more levels available
            return null;
        }
    }
}