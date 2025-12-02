using System;
using System.Collections.Generic;
using System.Linq;
using FxApi;
using FxApi.Connection;
using NLog;

namespace FxWorth.Hierarchy
{
    /// <summary>
    /// Refactored HierarchyNavigator that manages a true tree-based hierarchy.
    /// Each level is represented by a LevelNode with its own TradingParameters instance.
    /// Navigation preserves parent state while trading child levels.
    /// </summary>
    public class HierarchyNavigator_Refactored
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        
        // ===== TREE STRUCTURE =====
        private LevelNode rootNode;                    // Virtual root (Level "0")
        private LevelNode currentActiveNode;           // Currently active trading node
        
        // ===== CONFIGURATION =====
        private readonly PhaseParameters phase1Params;
        private readonly PhaseParameters phase2Params;
        private readonly TokenStorage storage;
        private readonly TokenStorage tokenStorage;
        private readonly int maxHierarchyDepth;
        private readonly int defaultLevelsPerLayer;
        private readonly Dictionary<int, CustomLayerConfig> customLayerConfigs;
        
        // ===== STATE TRACKING =====
        private readonly Dictionary<int, int> layerLevelCounts = new Dictionary<int, int>();
        
        // ===== PROPERTIES =====
        public LevelNode CurrentLevel => currentActiveNode;
        public bool IsInHierarchyMode => currentActiveNode != null && !currentActiveNode.IsRoot;
        public string CurrentLevelId => currentActiveNode?.LevelId ?? "0";
        public int MaxHierarchyDepth => maxHierarchyDepth;
        
        // Legacy compatibility
        public string currentLevelId
        {
            get => CurrentLevelId;
            set
            {
                // For backward compatibility - navigate to this level if it exists
                var node = FindNodeById(value);
                if (node != null)
                {
                    currentActiveNode = node;
                }
            }
        }
        
        public int maxHierarchyDepth_Legacy => maxHierarchyDepth;
        
        /// <summary>
        /// Initializes the hierarchical trading state machine
        /// </summary>
        public HierarchyNavigator_Refactored(
            decimal amountToBeRecovered,
            TradingParameters baseTradingParams,
            PhaseParameters phase1,
            PhaseParameters phase2,
            Dictionary<int, CustomLayerConfig> customLayerConfigs,
            decimal initialStakeLayer1,
            TokenStorage storage)
        {
            this.phase1Params = phase1 ?? throw new ArgumentNullException(nameof(phase1));
            this.phase2Params = phase2 ?? throw new ArgumentNullException(nameof(phase2));
            this.storage = storage ?? throw new ArgumentNullException(nameof(storage));
            this.tokenStorage = storage;
            this.maxHierarchyDepth = baseTradingParams.MaxHierarchyDepth;
            this.defaultLevelsPerLayer = baseTradingParams.HierarchyLevels;
            this.customLayerConfigs = customLayerConfigs ?? new Dictionary<int, CustomLayerConfig>();
            
            // Create virtual root node
            rootNode = new LevelNode("0", 0, 0, null, null, null)
            {
                IsActive = false
            };
            
            logger.Info($"Initializing HierarchyNavigator: MaxDepth={maxHierarchyDepth}, " +
                       $"DefaultLevelsPerLayer={defaultLevelsPerLayer}, AmountToRecover={amountToBeRecovered:F2}");
            
            // Create first layer if amount exceeds threshold
            if (amountToBeRecovered > phase1.MaxDrawdown && maxHierarchyDepth > 0)
            {
                CreateFirstLayer(amountToBeRecovered, baseTradingParams, initialStakeLayer1);
            }
        }
        
        /// <summary>
        /// Creates the first layer of the hierarchy (Layer 1 with its levels)
        /// </summary>
        private void CreateFirstLayer(decimal amountToBeRecovered, TradingParameters baseTradingParams, decimal initialStake)
        {
            logger.Info($"Creating Layer 1 for recovery amount {amountToBeRecovered:F2}");
            
            // Get configuration for Layer 1
            var layer1Config = GetCustomConfigForLayer(1);
            int layer1LevelCount = layer1Config?.HierarchyLevels ?? defaultLevelsPerLayer;
            layerLevelCounts[1] = layer1LevelCount;
            
            decimal amountPerLevel = Math.Round(amountToBeRecovered / layer1LevelCount, 2);
            
            logger.Info($"Layer 1 will have {layer1LevelCount} levels with {amountPerLevel:F2} per level");
            
            // Create first level node (1.1)
            var firstLevel = rootNode.CreateChild(
                childLevelId: "1.1",
                amountToRecover: amountPerLevel,
                initialStake: initialStake,
                martingaleLevel: layer1Config?.MartingaleLevel ?? phase2Params.MartingaleLevel,
                maxDrawdown: layer1Config?.MaxDrawdown ?? phase2Params.MaxDrawdown,
                barrierOffset: layer1Config?.BarrierOffset ?? phase2Params.Barrier
            );
            
            // Create TradingParameters for this node
            firstLevel.TradingParams = CreateTradingParametersForNode(firstLevel, baseTradingParams);
            
            // Set as current active node
            currentActiveNode = firstLevel;
            firstLevel.Activate();
            
            logger.Info($"Created first level 1.1: Target={amountPerLevel:F2}, Stake={initialStake:F2}");
        }
        
        /// <summary>
        /// Creates a TradingParameters instance for a specific node
        /// KEY FIX: Each node gets its own independent TradingParameters
        /// </summary>
        private TradingParameters CreateTradingParametersForNode(LevelNode node, TradingParameters baseParams)
        {
            var nodeParams = new TradingParameters
            {
                // Copy base configuration
                Symbol = baseParams.Symbol,
                Duration = baseParams.Duration,
                DurationType = baseParams.DurationType,
                HierarchyLevels = baseParams.HierarchyLevels,
                MaxHierarchyDepth = baseParams.MaxHierarchyDepth,
                
                // Node-specific configuration
                TakeProfit = node.AmountToRecover,
                Stake = node.InitialStake,
                DynamicStake = node.InitialStake,
                LevelInitialStake = node.InitialStake,
                DesiredReturnPercent = node.BarrierOffset ?? phase1Params.Barrier,
                MaxDrawdown = node.MaxDrawdown ?? phase1Params.MaxDrawdown,
                MartingaleLevel = node.MartingaleLevel ?? phase1Params.MartingaleLevel,
                TempBarrier = 0,
                
                // Fresh state for this level - TotalProfit is read-only, defaults to 0
                IsRecoveryMode = false,
                AmountToBeRecoverd = 0
            };
            
            // Subscribe to events for coordination
            nodeParams.TakeProfitReached += OnTakeProfitReached;
            nodeParams.MaxDrawdownExceeded += OnMaxDrawdownExceeded;
            nodeParams.RecoveryStateChanged += OnRecoveryStateChanged;
            nodeParams.TradeProcessed += OnTradeProcessed;
            
            logger.Debug($"Created TradingParameters for {node.LevelId}: TakeProfit={nodeParams.TakeProfit:F2}, " +
                        $"Stake={nodeParams.Stake:F2}, MaxDrawdown={nodeParams.MaxDrawdown:F2}");
            
            return nodeParams;
        }
        
        // ===== EVENT HANDLERS (Forwarded from TokenStorage) =====
        
        private void OnTakeProfitReached(object sender, TakeProfitReachedEventArgs e)
        {
            var tradingParams = (TradingParameters)sender;
            var node = FindNodeByTradingParams(tradingParams);
            
            if (node == null)
            {
                logger.Error("TakeProfitReached event received but node not found");
                return;
            }
            
            // Prevent duplicate processing
            if (node.IsCompleted)
            {
                logger.Warn($"Level {node.LevelId} already marked as completed - ignoring duplicate TakeProfitReached event");
                return;
            }
            
            logger.Info($"Level {node.LevelId} reached profit target: {e.TotalProfit:F2}/{e.TargetProfit:F2}");
            node.MarkCompleted();
        }
        
        private void OnMaxDrawdownExceeded(object sender, MaxDrawdownExceededEventArgs e)
        {
            var tradingParams = (TradingParameters)sender;
            var node = FindNodeByTradingParams(tradingParams);
            
            if (node == null)
            {
                logger.Error("MaxDrawdownExceeded event received but node not found");
                return;
            }
            
            logger.Warn($"Level {node.LevelId} exceeded max drawdown: {e.CurrentDrawdown:F2}/{e.MaxDrawdownLimit:F2}");
        }
        
        private void OnRecoveryStateChanged(object sender, RecoveryStateChangedEventArgs e)
        {
            var tradingParams = (TradingParameters)sender;
            var node = FindNodeByTradingParams(tradingParams);
            
            if (node != null)
            {
                if (e.EnteredRecovery)
                {
                    logger.Info($"Level {node.LevelId} entered recovery mode: AmountToRecover={e.AmountToRecover:F2}");
                }
                else if (e.ExitedRecovery)
                {
                    logger.Info($"Level {node.LevelId} exited recovery mode");
                }
            }
        }
        
        private void OnTradeProcessed(object sender, TradeProfitEventArgs e)
        {
            var tradingParams = (TradingParameters)sender;
            var node = FindNodeByTradingParams(tradingParams);
            
            if (node != null)
            {
                logger.Debug($"Trade processed for {node.LevelId}: P/L={e.ProfitLoss:F2}, Total={e.TotalProfit:F2}");
            }
        }
        
        /// <summary>
        /// Assigns a client to a specific level node
        /// KEY FIX: Points client to the node's TradingParameters, doesn't replace it
        /// </summary>
        public void AssignClientToLevel(string levelId, AuthClient client)
        {
            var targetNode = FindNodeById(levelId);
            if (targetNode == null)
            {
                logger.Error($"Cannot assign client to level {levelId} - node not found");
                return;
            }
            
            // Deactivate current node (preserve its state)
            if (currentActiveNode != null && currentActiveNode != targetNode)
            {
                currentActiveNode.Deactivate();
            }
            
            // Activate target node
            targetNode.Activate();
            currentActiveNode = targetNode;
            
            // KEY FIX: Assign this node's TradingParameters to the client
            // Parent nodes keep their TradingParameters intact!
            client.TradingParameters = targetNode.TradingParams;
            
            // CRITICAL: Re-subscribe TokenStorage event handlers to the new level's TradingParameters
            // This ensures TokenStorage can handle level transitions
            tokenStorage?.ResubscribeToTradingParameters(client);
            
            logger.Info($"Client assigned to level {levelId} - TakeProfit: ${targetNode.TradingParams.TakeProfit:F2}, " +
                       $"CurrentProfit: ${targetNode.TradingParams.TotalProfit:F2}, Path: {targetNode.GetTreePosition()}");
        }
        
        /// <summary>
        /// Navigates to a sibling level (horizontal move within same layer)
        /// </summary>
        public bool NavigateToSiblingLevel(AuthClient client)
        {
            if (currentActiveNode == null || currentActiveNode.IsRoot)
            {
                logger.Error("Cannot navigate to sibling from root");
                return false;
            }
            
            // Try to get next sibling
            var nextSibling = currentActiveNode.GetNextSibling();
            if (nextSibling != null)
            {
                logger.Info($"Moving to sibling: {currentActiveNode.LevelId} ? {nextSibling.LevelId}");
                
                // If sibling doesn't exist yet, create it
                if (nextSibling.TradingParams == null)
                {
                    nextSibling.TradingParams = CreateTradingParametersForNode(
                        nextSibling, 
                        currentActiveNode.TradingParams);
                }
                
                currentActiveNode.Deactivate();
                AssignClientToLevel(nextSibling.LevelId, client);
                return true;
            }
            
            // No more siblings - need to move up to parent
            logger.Info($"No more siblings in layer, moving up to parent");
            return NavigateToParentLevel(client);
        }
        
        /// <summary>
        /// Creates the next sibling level if it doesn't exist
        /// </summary>
        private LevelNode CreateNextSiblingLevel(LevelNode currentNode)
        {
            if (currentNode.Parent == null)
                return null;
            
            // Determine the layer's max level count
            int layerNumber = currentNode.LayerNumber;
            int maxLevels = layerLevelCounts.TryGetValue(layerNumber, out int count) 
                ? count 
                : defaultLevelsPerLayer;
            
            if (currentNode.LevelNumber >= maxLevels)
            {
                logger.Info($"Already at max level {currentNode.LevelNumber} of {maxLevels} in layer {layerNumber}");
                return null;
            }
            
            // Build next sibling ID
            string[] parts = currentNode.LevelId.Split('.');
            int nextLevelNumber = currentNode.LevelNumber + 1;
            parts[parts.Length - 1] = nextLevelNumber.ToString();
            string nextSiblingId = string.Join(".", parts);
            
            logger.Info($"Creating sibling level {nextSiblingId}");
            
            // Create sibling with same configuration as current level
            var siblingNode = currentNode.Parent.CreateChild(
                childLevelId: nextSiblingId,
                amountToRecover: currentNode.AmountToRecover,
                initialStake: currentNode.InitialStake,
                martingaleLevel: currentNode.MartingaleLevel,
                maxDrawdown: currentNode.MaxDrawdown,
                barrierOffset: currentNode.BarrierOffset
            );
            
            return siblingNode;
        }
        
        /// <summary>
        /// Navigates to a child level (dive down - max drawdown exceeded)
        /// </summary>
        public bool NavigateToChildLevel(AuthClient client, string childLevelId)
        {
            var childNode = FindNodeById(childLevelId);
            if (childNode == null)
            {
                logger.Error($"Child level {childLevelId} does not exist");
                return false;
            }
            
            // Verify it's actually a child of current node
            if (childNode.Parent != currentActiveNode)
            {
                logger.Error($"{childLevelId} is not a child of {currentActiveNode.LevelId}");
                return false;
            }
            
            logger.Info($"Diving down: {currentActiveNode.LevelId} ? {childLevelId}");
            
            // Current node becomes inactive (but keeps its state!)
            currentActiveNode.Deactivate();
            
            // Activate child node
            AssignClientToLevel(childLevelId, client);
            
            return true;
        }
        
        /// <summary>
        /// Navigates back to parent level (climb up - child level completed)
        /// Handles excess profit scenario where children recovered more than parent needs
        /// </summary>
        public bool NavigateToParentLevel(AuthClient client)
        {
            if (currentActiveNode.Parent == null || currentActiveNode.Parent.IsRoot)
            {
                logger.Info("Already at top level, exiting hierarchy");
                return NavigateToRoot(client);
            }
            
            var parentNode = currentActiveNode.Parent;
            logger.Info($"Climbing up: {currentActiveNode.LevelId} ? {parentNode.LevelId}");
            
            // Calculate children's contribution to parent's profit
            decimal childrenProfit = parentNode.GetChildrenAccumulatedProfit();
            
            // Update parent's TakeProfit to reflect remaining amount needed
            decimal originalTarget = parentNode.AmountToRecover;
            decimal parentCurrentProfit = parentNode.TradingParams.TotalProfit;
            decimal effectiveProfit = parentCurrentProfit + childrenProfit;
            decimal remainingNeeded = originalTarget - effectiveProfit;
            
            logger.Info($"Parent {parentNode.LevelId}: OriginalTarget={originalTarget:F2}, " +
                       $"ParentProfit={parentCurrentProfit:F2}, ChildrenProfit={childrenProfit:F2}, " +
                       $"Effective={effectiveProfit:F2}, Remaining={remainingNeeded:F2}");
            
            // EXCESS PROFIT SCENARIO: Children recovered so much that parent is already satisfied
            if (remainingNeeded <= 0)
            {
                logger.Info($"EXCESS PROFIT: Parent level {parentNode.LevelId} already satisfied by children (remaining: {remainingNeeded:F2})");
                parentNode.MarkCompleted();

                // CRITICAL: Properly transition currentActiveNode from child to parent
                string childLevelId = currentActiveNode.LevelId;
                
                // Deactivate current child node
                if (currentActiveNode != null && currentActiveNode != parentNode)
                {
                    currentActiveNode.Deactivate();
                    logger.Debug($"Deactivated child level {childLevelId}");
                }
                
                // Set parent as current active node (but don't activate for trading since it's completed)
                currentActiveNode = parentNode;
                logger.Info($"Transitioning navigation context: {childLevelId} → {parentNode.LevelId} (parent completed by children)");
                
                // Now navigate from the completed parent to its next sibling or grandparent
                return MoveToNextLevel(client);
            }
            
            // Update parent's TakeProfit to remaining amount
            parentNode.TradingParams.TakeProfit = remainingNeeded;
            
            // Reset recovery state (children did the recovery work)
            parentNode.TradingParams.IsRecoveryMode = false;
            parentNode.TradingParams.AmountToBeRecoverd = 0;
            parentNode.TradingParams.DynamicStake = parentNode.TradingParams.Stake;
            
            // Activate parent node
            AssignClientToLevel(parentNode.LevelId, client);
            
            return true;
        }
        
        /// <summary>
        /// Navigates to root level (exits hierarchy mode)
        /// </summary>
        private bool NavigateToRoot(AuthClient client)
        {
            logger.Info("Exiting hierarchy mode - returning to root level");
            
            currentActiveNode?.Deactivate();
            currentActiveNode = rootNode;
            
            // Restore root level trading parameters
            storage.RestoreRootLevelTradingParameters(client);
            
            return true;
        }
        
        /// <summary>
        /// Moves to the next level after current level completes
        /// Implements the navigation logic based on hierarchy rules
        /// </summary>
        public bool MoveToNextLevel(AuthClient client)
        {
            if (currentActiveNode == null || currentActiveNode.IsRoot)
            {
                logger.Error("Cannot move to next level from root");
                return false;
            }
            
            if (!currentActiveNode.IsCompleted)
            {
                logger.Info($"Level {currentActiveNode.LevelId} not completed yet");
                return false;
            }
            
            // Capture level ID before any state changes for consistent logging
            string levelBeingLeft = currentActiveNode.LevelId;
            logger.Info($"Moving from completed level {levelBeingLeft}");
            
            // Try next sibling first (horizontal move within layer)
            var nextSibling = currentActiveNode.GetNextSibling();
            if (nextSibling == null)
            {
                // Try to create next sibling
                nextSibling = CreateNextSiblingLevel(currentActiveNode);
                
                if (nextSibling == null)
                {
                    logger.Info($"No more siblings available for {currentActiveNode.LevelId} - will navigate to parent");
                }
            }
            
            if (nextSibling != null)
            {
                logger.Info($"Successfully found/created next sibling: {nextSibling.LevelId}");
                
                // Ensure sibling has TradingParameters
                if (nextSibling.TradingParams == null)
                {
                    nextSibling.TradingParams = CreateTradingParametersForNode(
                        nextSibling, 
                        currentActiveNode.TradingParams);
                }
                
                AssignClientToLevel(nextSibling.LevelId, client);
                logger.Info($"Level transition complete: {levelBeingLeft} → {nextSibling.LevelId}");
                return true;
            }
            
            // No sibling, climb up to parent
            logger.Info($"No more siblings for {levelBeingLeft}, climbing up to parent");
            return NavigateToParentLevel(client);
        }
        
        /// <summary>
        /// Creates a nested level when max drawdown is exceeded
        /// </summary>
        public void CreateNestedLevel(AuthClient client, decimal amountToBeRecovered, TradingParameters baseParams)
        {
            if (currentActiveNode == null || currentActiveNode.IsRoot)
            {
                logger.Error("Cannot create nested level from root");
                return;
            }
            
            // Check depth limit
            if (currentActiveNode.Depth >= maxHierarchyDepth)
            {
                logger.Warn($"Cannot create nested level - max depth {maxHierarchyDepth} reached");
                return;
            }
            
            int nextLayer = currentActiveNode.Depth + 1;
            var layerConfig = GetCustomConfigForLayer(nextLayer);
            int levelCount = layerConfig?.HierarchyLevels ?? defaultLevelsPerLayer;
            layerLevelCounts[nextLayer] = levelCount;
            
            decimal amountPerLevel = Math.Round(amountToBeRecovered / levelCount, 2);
            
            string nestedLevelId = $"{currentActiveNode.LevelId}.1";
            
            logger.Info($"Creating nested level {nestedLevelId} under {currentActiveNode.LevelId} " +
                       $"for amount {amountPerLevel:F2}");
            
            // Create child node
            var childNode = currentActiveNode.CreateChild(
                childLevelId: nestedLevelId,
                amountToRecover: amountPerLevel,
                initialStake: layerConfig?.InitialStake ?? currentActiveNode.InitialStake,
                martingaleLevel: layerConfig?.MartingaleLevel ?? phase1Params.MartingaleLevel,
                maxDrawdown: layerConfig?.MaxDrawdown ?? phase1Params.MaxDrawdown,
                barrierOffset: layerConfig?.BarrierOffset ?? phase1Params.Barrier
            );
            
            // Create TradingParameters for child
            childNode.TradingParams = CreateTradingParametersForNode(childNode, baseParams);
            
            // Navigate to child level
            NavigateToChildLevel(client, nestedLevelId);
        }
        
        /// <summary>
        /// Legacy method for compatibility - returns current level as HierarchyLevel
        /// </summary>
        public HierarchyLevel GetCurrentLevel()
        {
            if (currentActiveNode == null || currentActiveNode.IsRoot)
                return null;
            
            return new HierarchyLevel(
                currentActiveNode.LevelId,
                currentActiveNode.AmountToRecover,
                currentActiveNode.InitialStake,
                currentActiveNode.MartingaleLevel,
                currentActiveNode.MaxDrawdown,
                currentActiveNode.BarrierOffset
            )
            {
                IsCompleted = currentActiveNode.IsCompleted
            };
        }
        
        /// <summary>
        /// Checks if a nested level can be created from current level
        /// </summary>
        public bool CanCreateNestedLevel(string parentLevelId)
        {
            var parentNode = FindNodeById(parentLevelId);
            if (parentNode == null)
                return false;
            
            return parentNode.Depth < maxHierarchyDepth;
        }
        
        /// <summary>
        /// Finds a node by its level ID using tree traversal
        /// </summary>
        private LevelNode FindNodeById(string levelId)
        {
            return FindNodeByIdRecursive(rootNode, levelId);
        }
        
        private LevelNode FindNodeByIdRecursive(LevelNode node, string levelId)
        {
            if (node.LevelId == levelId)
                return node;
            
            foreach (var child in node.Children)
            {
                var found = FindNodeByIdRecursive(child, levelId);
                if (found != null)
                    return found;
            }
            
            return null;
        }
        
        /// <summary>
        /// Gets custom configuration for a specific layer
        /// </summary>
        private CustomLayerConfig GetCustomConfigForLayer(int layerNumber)
        {
            return customLayerConfigs.TryGetValue(layerNumber, out var config) ? config : null;
        }
        
        /// <summary>
        /// Finds a node by its TradingParameters instance (for event handling)
        /// </summary>
        public LevelNode FindNodeByTradingParams(TradingParameters tradingParams)
        {
            return FindNodeByTradingParamsRecursive(rootNode, tradingParams);
        }
        
        private LevelNode FindNodeByTradingParamsRecursive(LevelNode node, TradingParameters tradingParams)
        {
            if (node.TradingParams == tradingParams)
                return node;
            
            foreach (var child in node.Children)
            {
                var found = FindNodeByTradingParamsRecursive(child, tradingParams);
                if (found != null)
                    return found;
            }
            
            return null;
        }
        
        /// <summary>
        /// Prints the entire hierarchy tree for debugging
        /// </summary>
        public void PrintHierarchyTree()
        {
            logger.Info("=== HIERARCHY TREE ===");
            PrintNodeRecursive(rootNode, 0);
        }
        
        private void PrintNodeRecursive(LevelNode node, int indent)
        {
            string indentation = new string(' ', indent * 2);
            string activeMarker = node.IsActive ? " [ACTIVE]" : "";
            string completedMarker = node.IsCompleted ? " [COMPLETED]" : "";
            
            logger.Info($"{indentation}{node.LevelId}{activeMarker}{completedMarker} " +
                       $"(Profit: {node.TradingParams?.TotalProfit:F2}/{node.AmountToRecover:F2})");
            
            foreach (var child in node.Children)
            {
                PrintNodeRecursive(child, indent + 1);
            }
        }
    }
}
