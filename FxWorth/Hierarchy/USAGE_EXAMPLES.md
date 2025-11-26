# Hierarchy Navigator Usage Examples

## Table of Contents
1. [Basic Usage](#basic-usage)
2. [Navigation Patterns](#navigation-patterns)
3. [Event Handling](#event-handling)
4. [Advanced Scenarios](#advanced-scenarios)
5. [Monitoring & Debugging](#monitoring--debugging)

---

## Basic Usage

### Initializing Hierarchy Mode

```csharp
// Automatic entry when max drawdown exceeded
public void OnCrossover(object sender, EventArgs e)
{
    // ... trading logic ...
    
    if (clientParams.AmountToBeRecoverd > clientParams.MaxDrawdown && 
        (hierarchyNavigatorNew == null || !hierarchyNavigatorNew.IsInHierarchyMode))
    {
        // Hierarchy mode is automatically entered
        hierarchyClient = value;
        decimal initialStake = clientParams.InitialStake4Layer1 > 0 
            ? clientParams.InitialStake4Layer1 
            : clientParams.Stake;
        
        hierarchyNavigatorNew = new HierarchyNavigator_Refactored(
            clientParams.AmountToBeRecoverd,  // Amount to recover
            clientParams,                      // Base trading parameters
            phase1Parameters,                  // Phase 1 config (nested levels)
            phase2Parameters,                  // Phase 2 config (Layer 1)
            customLayerConfigs,                // Custom layer configurations
            initialStake,                      // Initial stake for Layer 1
            this                               // TokenStorage reference
        );
        
        currentLevelId = "1.1";
        hierarchyNavigatorNew.AssignClientToLevel(currentLevelId, value);
        
        logger.Info($"Entered hierarchy: {hierarchyNavigatorNew.CurrentLevel.GetTreePosition()}");
    }
}
```

### Checking Hierarchy Status

```csharp
// Check if in hierarchy mode
if (IsHierarchyMode)
{
    var currentNode = hierarchyNavigatorNew.CurrentLevel;
    logger.Info($"Trading at level {currentNode.LevelId}");
    logger.Info($"Target: ${currentNode.AmountToRecover:F2}");
    logger.Info($"Current: ${currentNode.TradingParams.TotalProfit:F2}");
}
```

---

## Navigation Patterns

### Pattern 1: Horizontal Navigation (Sibling Moves)

```csharp
// Automatic when level completes
// Example: 1.1 ? 1.2 ? 1.3

public void OnTakeProfitReached(object sender, TakeProfitReachedEventArgs e)
{
    var currentNode = hierarchyNavigatorNew?.CurrentLevel;
    if (currentNode != null)
    {
        currentNode.MarkCompleted();
        
        // MoveToNextLevel handles sibling navigation
        if (hierarchyNavigatorNew.MoveToNextLevel(client))
        {
            var newLevel = hierarchyNavigatorNew.CurrentLevel;
            logger.Info($"Moved to sibling: {currentNode.LevelId} ? {newLevel.LevelId}");
            
            // Client is automatically assigned to new level's TradingParameters
            // Old level's state is preserved
        }
    }
}
```

### Pattern 2: Vertical Navigation (Dive Down - Nesting)

```csharp
// Automatic when max drawdown exceeded
// Example: 1.1 ? 1.1.1 (create nested level)

public void OnMaxDrawdownExceeded(object sender, MaxDrawdownExceededEventArgs e)
{
    if (hierarchyNavigatorNew != null)
    {
        var currentNode = hierarchyNavigatorNew.CurrentLevel;
        
        if (hierarchyNavigatorNew.CanCreateNestedLevel(currentNode.LevelId))
        {
            logger.Info($"Creating nested level under {currentNode.LevelId}");
            
            // Parent's state is automatically saved
            hierarchyNavigatorNew.CreateNestedLevel(
                client, 
                e.AmountToBeRecovered,  // Amount nested level needs to recover
                client.TradingParameters
            );
            
            // Now at child level (e.g., 1.1.1)
            var childNode = hierarchyNavigatorNew.CurrentLevel;
            logger.Info($"Nested to: {childNode.LevelId}");
            logger.Info($"Parent {currentNode.LevelId} preserved with profit: ${currentNode.TradingParams.TotalProfit:F2}");
        }
        else
        {
            logger.Warn($"Max depth reached - cannot nest further from {currentNode.LevelId}");
        }
    }
}
```

### Pattern 3: Climb Up (Parent Return)

```csharp
// Automatic when all siblings complete
// Example: 1.1.3 complete ? return to 1.1

public bool MoveToNextLevel(AuthClient client)
{
    var currentNode = hierarchyNavigatorNew.CurrentLevel;
    
    // Try next sibling first
    var nextSibling = currentNode.GetNextSibling();
    if (nextSibling != null)
    {
        // Move horizontally
        AssignClientToLevel(nextSibling.LevelId, client);
    }
    else
    {
        // No more siblings - climb up to parent
        logger.Info($"No more siblings, returning to parent");
        
        var parentNode = currentNode.Parent;
        if (parentNode != null && !parentNode.IsRoot)
        {
            // Calculate what children contributed
            decimal childrenProfit = parentNode.GetChildrenAccumulatedProfit();
            decimal remainingNeeded = parentNode.AmountToRecover - 
                                     (parentNode.TradingParams.TotalProfit + childrenProfit);
            
            if (remainingNeeded <= 0)
            {
                // EXCESS PROFIT - skip parent
                logger.Info($"Parent {parentNode.LevelId} satisfied by children, skipping");
                parentNode.MarkCompleted();
                return MoveToNextLevel(client); // Recursively move to parent's sibling
            }
            else
            {
                // Parent still needs trading
                parentNode.TradingParams.TakeProfit = remainingNeeded;
                AssignClientToLevel(parentNode.LevelId, client);
                logger.Info($"Returned to parent {parentNode.LevelId}, remaining: ${remainingNeeded:F2}");
            }
        }
    }
}
```

### Pattern 4: Exit Hierarchy (Return to Root)

```csharp
// Automatic when all Layer 1 levels complete

public bool NavigateToRoot(AuthClient client)
{
    logger.Info("All hierarchy levels complete - exiting to root");
    
    currentActiveNode?.Deactivate();
    currentActiveNode = rootNode;
    
    // Restore root level trading with adjusted target
    storage.RestoreRootLevelTradingParameters(client);
    
    return true;
}
```

---

## Event Handling

### Event: TakeProfitReached

```csharp
private void OnTakeProfitReached(object sender, TakeProfitReachedEventArgs e)
{
    var tradingParams = (TradingParameters)sender;
    var node = FindNodeByTradingParams(tradingParams);
    
    if (node != null)
    {
        logger.Info($"? Level {node.LevelId} completed!");
        logger.Info($"   Profit: ${e.TotalProfit:F2} / ${e.TargetProfit:F2}");
        logger.Info($"   Duration: {(DateTime.Now - node.CreatedAt).TotalMinutes:F2} minutes");
        
        node.MarkCompleted();
        
        // Automatic navigation to next level
        // (handled by TokenStorage event handler)
    }
}
```

### Event: MaxDrawdownExceeded

```csharp
private void OnMaxDrawdownExceeded(object sender, MaxDrawdownExceededEventArgs e)
{
    var tradingParams = (TradingParameters)sender;
    var node = FindNodeByTradingParams(tradingParams);
    
    if (node != null)
    {
        logger.Warn($"?? Level {node.LevelId} exceeded max drawdown");
        logger.Warn($"   Current: ${e.CurrentDrawdown:F2} > Limit: ${e.MaxDrawdownLimit:F2}");
        logger.Warn($"   Depth: {node.Depth} / Max: {maxHierarchyDepth}");
        
        // Automatic nesting if depth allows
        // (handled by TokenStorage event handler)
    }
}
```

### Event: RecoveryStateChanged

```csharp
private void OnRecoveryStateChanged(object sender, RecoveryStateChangedEventArgs e)
{
    var tradingParams = (TradingParameters)sender;
    var node = FindNodeByTradingParams(tradingParams);
    
    if (node != null)
    {
        if (e.EnteredRecovery)
        {
            logger.Info($"?? Level {node.LevelId} entered recovery mode");
            logger.Info($"   Amount to recover: ${e.AmountToRecover:F2}");
        }
        else if (e.ExitedRecovery)
        {
            logger.Info($"? Level {node.LevelId} exited recovery mode");
        }
    }
}
```

---

## Advanced Scenarios

### Scenario: Complex Multi-Level Hierarchy

```csharp
// Starting state: Root level
// Root exceeds max drawdown ? Enter hierarchy

// LAYER 1
// Create: 1.1, 1.2, 1.3 (3 levels to recover root's max drawdown)

// LAYER 2 (nested under 1.1)
// 1.1 exceeds max drawdown ? Create: 1.1.1, 1.1.2, 1.1.3

// LAYER 3 (nested under 1.1.1)
// 1.1.1 exceeds max drawdown ? Create: 1.1.1.1, 1.1.1.2, 1.1.1.3

// Navigation path (if all succeed):
// 1.1.1.1 ? 1.1.1.2 ? 1.1.1.3 ? 1.1.1 (parent) ? 1.1.2 ? 1.1.3 ? 1.1 (parent) ? 1.2 ? 1.3 ? Root

// Tree structure:
/*
Root (0)
??? 1.1
?   ??? 1.1.1
?   ?   ??? 1.1.1.1 ?
?   ?   ??? 1.1.1.2 ?
?   ?   ??? 1.1.1.3 ?
?   ??? 1.1.2 ?
?   ??? 1.1.3 ?
??? 1.2 ?
??? 1.3 ?
*/
```

### Scenario: Excess Profit Skip

```csharp
// Parent: 1.1 needs to recover $100 (max drawdown $50, target profit $50)
// Children: 1.1.1, 1.1.2, 1.1.3 each target $50/3 = $16.67

// Execution:
// 1.1.1 completes with $20 profit (excess $3.33)
// 1.1.2 completes with $25 profit (excess $8.33)
// 1.1.3 completes with $30 profit (excess $13.33)

// Total children profit: $75
// Parent's TotalProfit at nesting: $0
// Effective profit: $0 + $75 = $75
// Parent's original target: $100
// Remaining needed: $100 - $75 = $25

// Since remaining > 0, parent 1.1 trades to recover $25
// If remaining <= 0, parent would be marked complete and skipped

// Special case - children overachieve:
// If children accumulate $100+, parent is skipped entirely
```

### Scenario: Max Depth Limit Enforcement

```csharp
// Configuration: MaxHierarchyDepth = 3
// Means: Layer 1, Layer 2, Layer 3 allowed
// Level IDs: "1.x", "1.x.x", "1.x.x.x"

// Attempt to create Layer 4 (depth 4):
if (currentNode.Depth >= maxHierarchyDepth)
{
    logger.Warn($"Cannot create nested level - max depth {maxHierarchyDepth} reached");
    // Level continues trading with higher risk
    // No nesting allowed
}

// Depth calculation:
// "1.1"     ? Depth 1 (Layer 1)
// "1.1.1"   ? Depth 2 (Layer 2)
// "1.1.1.1" ? Depth 3 (Layer 3)
// "1.1.1.1.1" ? Would be Depth 4 - BLOCKED if MaxDepth = 3
```

---

## Monitoring & Debugging

### Print Full Hierarchy Tree

```csharp
tokenStorage.PrintHierarchyTree();

// Output:
/*
=== HIERARCHY TREE ===
0
  1.1 [ACTIVE] (Profit: 10.50/35.00)
    1.1.1 [COMPLETED] (Profit: 20.00/20.00)
    1.1.2 (Profit: 0.00/20.00)
  1.2 (Profit: 0.00/35.00)
  1.3 (Profit: 0.00/35.00)
*/
```

### Get Detailed Status

```csharp
string status = tokenStorage.GetHierarchyStatus();
logger.Info(status);

// Output:
/*
Hierarchy Mode Active
Current Level: 1.1
Path: 0 -> 1.1
Depth: 1, Layer: 1, Level: 1
Target: $35.00
Current Profit: $10.50
Remaining: $24.50
Children: 2
Max Depth: 5
*/
```

### Access Node Properties

```csharp
var currentNode = hierarchyNavigatorNew.CurrentLevel;

// Identity
logger.Info($"Level ID: {currentNode.LevelId}");
logger.Info($"Depth: {currentNode.Depth}");
logger.Info($"Layer: {currentNode.LayerNumber}");
logger.Info($"Level: {currentNode.LevelNumber}");

// Status
logger.Info($"Active: {currentNode.IsActive}");
logger.Info($"Completed: {currentNode.IsCompleted}");
logger.Info($"Is Root: {currentNode.IsRoot}");
logger.Info($"Is Leaf: {currentNode.IsLeaf}");

// Relationships
logger.Info($"Parent: {currentNode.Parent?.LevelId ?? "None"}");
logger.Info($"Children: {currentNode.ChildCount}");
logger.Info($"Has Sibling: {currentNode.HasSibling}");

// Trading state
logger.Info($"Amount to Recover: ${currentNode.AmountToRecover:F2}");
logger.Info($"Current Profit: ${currentNode.TradingParams?.TotalProfit:F2}");
logger.Info($"In Recovery: {currentNode.TradingParams?.IsRecoveryMode}");

// Timestamps
logger.Info($"Created: {currentNode.CreatedAt}");
if (currentNode.CompletedAt.HasValue)
{
    logger.Info($"Completed: {currentNode.CompletedAt.Value}");
    logger.Info($"Duration: {(currentNode.CompletedAt.Value - currentNode.CreatedAt).TotalMinutes:F2} min");
}

// Path
logger.Info($"Full Path: {currentNode.GetTreePosition()}");
```

### Monitor Children Profit

```csharp
var parentNode = hierarchyNavigatorNew.CurrentLevel.Parent;
if (parentNode != null)
{
    decimal childrenProfit = parentNode.GetChildrenAccumulatedProfit();
    logger.Info($"Parent {parentNode.LevelId} - Children contributed: ${childrenProfit:F2}");
    
    // Check each child individually
    foreach (var child in parentNode.Children)
    {
        logger.Info($"  {child.LevelId}: ${child.TradingParams?.TotalProfit:F2} " +
                   $"({(child.IsCompleted ? "Complete" : "Incomplete")})");
    }
}
```

### Find Any Node By ID

```csharp
var node = hierarchyNavigatorNew.FindNodeById("1.1.2");
if (node != null)
{
    logger.Info($"Found node: {node}");
}
```

---

## Best Practices

1. **Always check IsHierarchyMode** before accessing `hierarchyNavigatorNew`
   ```csharp
   if (IsHierarchyMode && hierarchyNavigatorNew != null)
   {
       // Safe to access
   }
   ```

2. **Use events for coordination** - don't manually move between levels
   - Events are already wired up in the navigator
   - TokenStorage handles high-level orchestration

3. **Log hierarchy state transitions** for debugging
   ```csharp
   logger.Info($"Transition: {oldLevel} ? {newLevel} ({reason})");
   ```

4. **Monitor depth** to prevent unexpected nesting limits
   ```csharp
   if (currentNode.Depth >= maxHierarchyDepth - 1)
   {
       logger.Warn("Approaching max depth limit");
   }
   ```

5. **Print tree periodically** during development
   ```csharp
   if (hierarchyNavigatorNew.CurrentLevel.LevelNumber == 1)
   {
       tokenStorage.PrintHierarchyTree(); // Print at start of each layer
   }
   ```

---

## Troubleshooting

### Issue: Parent state lost
**Cause**: Old code manually replacing TradingParameters  
**Solution**: Use `AssignClientToLevel()` - it preserves parent state

### Issue: Incorrect profit calculations
**Cause**: Not accounting for children's accumulated profit  
**Solution**: Use `GetChildrenAccumulatedProfit()` when returning to parent

### Issue: Navigation stuck
**Cause**: Level not marked as completed  
**Solution**: Ensure `MarkCompleted()` is called when target reached

### Issue: Unexpected nesting
**Cause**: Max drawdown threshold too low  
**Solution**: Review max drawdown configuration

### Issue: Cannot find node
**Cause**: Looking in wrong place in tree  
**Solution**: Use `PrintHierarchyTree()` to visualize structure

---

## Performance Tips

1. **Cache frequently accessed nodes**
   ```csharp
   private LevelNode currentNode = hierarchyNavigatorNew.CurrentLevel;
   ```

2. **Avoid repeated tree traversals**
   - Store direct references when possible
   - Use parent/child references instead of FindNodeById

3. **Clean up completed nodes** (optional optimization)
   - Remove completed subtrees to reduce memory
   - Only implement if managing hundreds of levels

4. **Use lazy evaluation** for children profit calculation
   - Only calculate when needed (returning to parent)
   - Cache result if called multiple times

---

This completes the comprehensive usage guide for the refactored hierarchy navigator!
