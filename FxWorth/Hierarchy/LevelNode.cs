using System;
using System.Collections.Generic;
using System.Linq;
using FxApi.Connection;
using NLog;

namespace FxWorth.Hierarchy
{
    /// <summary>
    /// Represents a single node in the hierarchical trading tree.
    /// Each node maintains its own TradingParameters instance and parent-child relationships.
    /// This is the foundation of the true hierarchical trading state machine.
    /// </summary>
    public class LevelNode
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        
        // ===== IDENTITY =====
        public string LevelId { get; set; }              // "1.2.3" (Level 3 of Layer 2)
        public int Depth { get; set; }                   // Number of dots in ID (e.g., "1.2.3" = depth 2)
        public int LayerNumber { get; set; }             // Derived from depth (Layer 1 = depth 1)
        public int LevelNumber { get; set; }             // Last number in ID (e.g., "1.2.3" = level 3)
        
        // ===== TREE STRUCTURE (THE KEY FIX) =====
        public LevelNode Parent { get; set; }                      // Reference to parent node
        public List<LevelNode> Children { get; private set; }     // Child nodes (nested levels)
        
        // ===== TRADING STATE (CRITICAL: Each node has its OWN TradingParameters) =====
        public TradingParameters TradingParams { get; set; }       // THIS node's independent trading state
        
        // ===== CONFIGURATION (Metadata from HierarchyLevel) =====
        public decimal AmountToRecover { get; set; }      // Profit target for this level
        public decimal InitialStake { get; set; }         // Base stake for this level
        public int? MartingaleLevel { get; set; }         // Martingale configuration
        public decimal? MaxDrawdown { get; set; }         // Maximum drawdown before nesting
        public decimal? BarrierOffset { get; set; }       // ROI barrier percentage
        
        // ===== STATUS =====
        public bool IsCompleted { get; set; }             // Has this level reached its profit target?
        public bool IsActive { get; set; }                // Is this level currently being traded?
        public DateTime CreatedAt { get; set; }           // When was this node created?
        public DateTime? CompletedAt { get; set; }        // When did this level complete?
        
        // ===== NAVIGATION HELPERS =====
        public bool IsRoot => Parent == null;
        public bool IsLeaf => Children.Count == 0;
        public int ChildCount => Children.Count;
        public bool HasSibling => Parent != null && Parent.Children.Count > 1;
        
        /// <summary>
        /// Constructor for creating a new level node
        /// </summary>
        public LevelNode(string levelId, decimal amountToRecover, decimal initialStake,
                         int? martingaleLevel, decimal? maxDrawdown, decimal? barrierOffset)
        {
            LevelId = levelId;
            AmountToRecover = amountToRecover;
            InitialStake = initialStake;
            MartingaleLevel = martingaleLevel;
            MaxDrawdown = maxDrawdown;
            BarrierOffset = barrierOffset;
            
            Children = new List<LevelNode>();
            IsCompleted = false;
            IsActive = false;
            CreatedAt = DateTime.Now;
            
            // Parse level ID to extract depth, layer, and level number
            ParseLevelId(levelId);
        }
        
        /// <summary>
        /// Parses the level ID to extract structural information
        /// Examples: "1.1" -> depth=1, layer=1, level=1
        ///          "1.2.3" -> depth=2, layer=2, level=3
        /// </summary>
        private void ParseLevelId(string levelId)
        {
            if (string.IsNullOrEmpty(levelId) || levelId == "0")
            {
                Depth = 0;
                LayerNumber = 0;
                LevelNumber = 0;
                return;
            }
            
            string[] parts = levelId.Split('.');
            Depth = parts.Length - 1;              // "1.2.3" has 3 parts, depth = 2
            LayerNumber = parts.Length - 1;        // Layer number = depth
            LevelNumber = int.Parse(parts[parts.Length - 1]); // Last number
            
            logger.Debug($"Parsed level ID '{levelId}': Depth={Depth}, Layer={LayerNumber}, Level={LevelNumber}");
        }
        
        /// <summary>
        /// Adds a child node and establishes bidirectional parent-child relationship
        /// </summary>
        public void AddChild(LevelNode child)
        {
            if (child == null)
                throw new ArgumentNullException(nameof(child));
            
            child.Parent = this;
            Children.Add(child);
            
            logger.Info($"Added child {child.LevelId} to parent {this.LevelId} (Parent now has {Children.Count} children)");
        }
        
        /// <summary>
        /// Gets the next sibling node (same depth, same parent)
        /// Returns null if this is the last child
        /// </summary>
        public LevelNode GetNextSibling()
        {
            if (Parent == null)
            {
                logger.Debug($"Level {LevelId} has no parent, cannot get next sibling");
                return null;
            }
            
            int myIndex = Parent.Children.IndexOf(this);
            if (myIndex < 0)
            {
                logger.Error($"Level {LevelId} not found in parent's children list");
                return null;
            }
            
            if (myIndex >= Parent.Children.Count - 1)
            {
                logger.Debug($"Level {LevelId} is last child, no next sibling");
                return null;
            }
            
            var nextSibling = Parent.Children[myIndex + 1];
            logger.Debug($"Next sibling of {LevelId} is {nextSibling.LevelId}");
            return nextSibling;
        }
        
        /// <summary>
        /// Creates a new child node under this parent with specified configuration
        /// </summary>
        public LevelNode CreateChild(string childLevelId, decimal amountToRecover, decimal initialStake,
                                      int? martingaleLevel, decimal? maxDrawdown, decimal? barrierOffset)
        {
            var childNode = new LevelNode(
                childLevelId,
                amountToRecover,
                initialStake,
                martingaleLevel,
                maxDrawdown,
                barrierOffset
            );
            
            AddChild(childNode);
            return childNode;
        }
        
        /// <summary>
        /// Marks this level as completed and records completion time
        /// </summary>
        public void MarkCompleted()
        {
            IsCompleted = true;
            CompletedAt = DateTime.Now;
            IsActive = false;
            
            var duration = CompletedAt.Value - CreatedAt;
            logger.Info($"Level {LevelId} marked as completed (Duration: {duration.TotalMinutes:F2} minutes, " +
                       $"TotalProfit: {TradingParams?.TotalProfit:F2})");
        }
        
        /// <summary>
        /// Activates this level for trading
        /// </summary>
        public void Activate()
        {
            IsActive = true;
            logger.Info($"Level {LevelId} activated for trading (AmountToRecover: {AmountToRecover:F2}, " +
                       $"InitialStake: {InitialStake:F2})");
        }
        
        /// <summary>
        /// Deactivates this level (pauses trading, preserves state)
        /// </summary>
        public void Deactivate()
        {
            IsActive = false;
            logger.Info($"Level {LevelId} deactivated (TotalProfit: {TradingParams?.TotalProfit:F2})");
        }
        
        /// <summary>
        /// Gets all children recursively (depth-first traversal)
        /// </summary>
        public List<LevelNode> GetAllDescendants()
        {
            var descendants = new List<LevelNode>();
            
            foreach (var child in Children)
            {
                descendants.Add(child);
                descendants.AddRange(child.GetAllDescendants());
            }
            
            return descendants;
        }
        
        /// <summary>
        /// Calculates the total profit accumulated by all child nodes recursively
        /// </summary>
        public decimal GetChildrenAccumulatedProfit()
        {
            decimal total = 0;
            
            foreach (var child in Children)
            {
                if (child.TradingParams != null)
                {
                    total += child.TradingParams.TotalProfit;
                }
                
                // Recursively add grandchildren's profit
                total += child.GetChildrenAccumulatedProfit();
            }
            
            logger.Debug($"Total accumulated profit from children of {LevelId}: {total:F2}");
            return total;
        }
        
        /// <summary>
        /// Checks if this level has exceeded its maximum drawdown
        /// </summary>
        public bool HasExceededMaxDrawdown()
        {
            if (TradingParams == null || MaxDrawdown == null)
                return false;
            
            return TradingParams.AmountToBeRecoverd > MaxDrawdown.Value;
        }
        
        /// <summary>
        /// Checks if this level has reached its profit target
        /// </summary>
        public bool HasReachedProfitTarget()
        {
            if (TradingParams == null)
                return false;
            
            return TradingParams.TotalProfit >= AmountToRecover;
        }
        
        /// <summary>
        /// Gets a string representation of this node's position in the tree
        /// </summary>
        public string GetTreePosition()
        {
            var path = new List<string>();
            var current = this;
            
            while (current != null)
            {
                path.Insert(0, current.LevelId);
                current = current.Parent;
            }
            
            return string.Join(" -> ", path);
        }
        
        /// <summary>
        /// Returns a detailed string representation of this node
        /// </summary>
        public override string ToString()
        {
            return $"LevelNode[{LevelId}] Depth={Depth}, Layer={LayerNumber}, Level={LevelNumber}, " +
                   $"Active={IsActive}, Completed={IsCompleted}, " +
                   $"Children={Children.Count}, " +
                   $"TotalProfit={TradingParams?.TotalProfit:F2}, " +
                   $"Target={AmountToRecover:F2}";
        }
    }
}
