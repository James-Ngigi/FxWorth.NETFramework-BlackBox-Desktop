using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NLog;

namespace FxApi.Connection
{
    /// <summary>
    /// This class encapsulates the parameters that govern the trading strategy,providing a mechanism for processing trade outcomes, 
    /// adjusting stake amounts, and entering or exiting recovery mode as needed.
    /// Its the Pure operational class - handles trading logic only, emits events for external coordination.
    /// </summary>

    public class TradingParameters : ICloneable
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        public decimal Barrier { get; set; }
        public decimal DesiredReturnPercent { get; set; }
        public decimal LastCalibratedReturnPercent { get; private set; }
        public decimal BarrierSearchMin { get; set; } = 1m;
        public decimal BarrierSearchMax { get; set; } = 120m;
        public decimal BarrierSearchStep { get; set; } = 1m;
        public DateTime? LastBarrierCalibrationUtc { get; private set; }
        public string BuyBarrier => string.Format(CultureInfo.InvariantCulture, "{0:+#0.0#;-#0.0#;0}", TempBarrier != 0 ? TempBarrier : Barrier);
        public string SellBarrier => string.Format(CultureInfo.InvariantCulture, "{0:+#0.0#;-#0.0#;0}", TempBarrier != 0 ? -TempBarrier : -Barrier);
        public ActiveSymbol Symbol { get; set; }
        public int Duration { get; set; }
        public decimal Stake { get; set; }
        public string DurationType { get; set; }
        public decimal MaxDrawdown { get; set; }
        public int MartingaleLevel { get; set; }
        
        private int currentMartingaleLevel = 1;
        public int HierarchyLevels { get; set; }
        public int MaxHierarchyDepth { get; set; }
        public decimal TakeProfit { get; set; }
        public decimal Stoploss { get; set; } = 10000000;
        public bool IsRecoveryMode { get; set; }
        public decimal AmountToBeRecoverd { get; set; }
        public decimal DynamicStake { get; set; }
        public decimal PreviousProfit { get; set; }
        public int RecoveryAttemptsLeft { get; set; }
        public decimal TempBarrier { get; set; }
        public decimal InitialStake4Layer1 { get; set; }
        public decimal TotalProfit { get; private set; }
        public decimal LevelInitialStake { get; set; }
        public bool RequiresReturnCalibration => DesiredReturnPercent > 0 && Stake > 0;

        public int CurrentMartingaleLevel
        {
            get => currentMartingaleLevel;
            private set => currentMartingaleLevel = value;
        }

        public void UpdateBarrierCalibration(decimal calibratedBarrier, decimal actualReturnPercent)
        {
            Barrier = calibratedBarrier;
            TempBarrier = calibratedBarrier;
            LastCalibratedReturnPercent = actualReturnPercent;
            LastBarrierCalibrationUtc = DateTime.UtcNow;
        }

        // ===== EVENT-DRIVEN ARCHITECTURE =====
        // Events allow external coordinators (like HierarchyNavigator) to listen and respond
        // without TradingParameters knowing about hierarchy logic
        
        /// <summary>Raised when take profit target is reached</summary>
        public event EventHandler<TakeProfitReachedEventArgs> TakeProfitReached;
        
        /// <summary>Raised when max drawdown is exceeded (signal for hierarchy escalation)</summary>
        public event EventHandler<MaxDrawdownExceededEventArgs> MaxDrawdownExceeded;
        
        /// <summary>Raised when recovery mode state changes (enter/exit)</summary>
        public event EventHandler<RecoveryStateChangedEventArgs> RecoveryStateChanged;
        
        /// <summary>Raised after each trade is processed with profit details</summary>
        public event EventHandler<TradeProfitEventArgs> TradeProcessed;

        private List<decimal> recoveryResults = new List<decimal>();
        public List<decimal> RecoveryResults
        {
            get => recoveryResults;
            set => recoveryResults = value;       
        }

        /// <summary>
        /// Processes the outcome of a completed trade, adjusting the `DynamicStake`, entering or exiting recovery mode,
        /// and updating relevant parameters based on the Godfathers recovery system.
        /// The pure trading logic - emits events for external coordination (hierarchy, UI updates, etc.)
        /// <param name="mlp">The actual profit or loss of the completed trade (Most Recent Profit/Loss).</param>
        /// <param name="estimate">An estimated profit value, typically used when `mlp` is zero to determine the initial stake for recovery.</param>
        /// <param name="appId">The application ID for logging purposes.</param>
        /// <param name="contractId">The contract ID for logging purposes.</param>
        /// <param name="transactionTime">The transaction time for logging purposes.</param>
        /// </summary>
        public void Process(decimal mlp, decimal estimate, int appId, long contractId, long transactionTime)
        {
            // Update total profit
            TotalProfit += mlp;

            // Check if take profit target is reached
            if (TotalProfit >= TakeProfit)
            {
                TakeProfitReached?.Invoke(this, new TakeProfitReachedEventArgs(TotalProfit, TakeProfit, IsRecoveryMode));
                return;
            }

            // If not in recovery mode and the trade was profitable, update the PreviousProfit
            if (!IsRecoveryMode && mlp > 0)
            {
                PreviousProfit = mlp;
            }

            // If the PreviousProfit is zero, use the estimated profit value
            if (PreviousProfit == 0)
            {
                PreviousProfit = estimate;
                logger.Debug($"Using estimated profit: {estimate} for recovery calculations");
            }

            // If the trade resulted in a loss...
            if (mlp < 0)
            {
                // Add the loss to the recoveryResults list
                recoveryResults.Add(mlp);

                // If not already in recovery mode, enter recovery mode
                if (!IsRecoveryMode)
                {
                    IsRecoveryMode = true;

                    // Add the expected profit from the initial stake to ensure we exit recovery mode with profit
                    decimal firstLoss = Math.Abs(mlp);
                    decimal expectedProfit = PreviousProfit; // The profit the initial stake would have made - maintains original profit margin

                    AmountToBeRecoverd = firstLoss + expectedProfit;
                    logger.Debug($"Entering recovery mode. First loss: {firstLoss:F2}, Expected profit: {expectedProfit:F2}, Amount to recover: {AmountToBeRecoverd:F2}");
                    
                    // Emit recovery state changed event
                    RecoveryStateChanged?.Invoke(this, new RecoveryStateChangedEventArgs(
                        enteredRecovery: true,
                        exitedRecovery: false,
                        amountToRecover: AmountToBeRecoverd,
                        recoveryAttemptsLeft: RecoveryAttemptsLeft));
                }
                else
                {
                    // Update amount to be recovered: accumulated losses + maintain the original profit margin
                    decimal accumulatedLosses = -recoveryResults.Sum();
                    decimal originalProfitMargin = PreviousProfit; // Keep the original expected profit

                    AmountToBeRecoverd = accumulatedLosses + originalProfitMargin;
                    logger.Debug($"Updated amount to recover: {AmountToBeRecoverd:F2} (losses: {accumulatedLosses:F2} + profit margin: {originalProfitMargin:F2})");
                }
                
                // Check if max drawdown is exceeded - emit event for external coordination (hierarchy escalation)
                if (AmountToBeRecoverd > MaxDrawdown)
                {
                    logger.Warn($"Max drawdown exceeded: {AmountToBeRecoverd:F2} > {MaxDrawdown:F2}");
                    MaxDrawdownExceeded?.Invoke(this, new MaxDrawdownExceededEventArgs(
                        currentDrawdown: AmountToBeRecoverd,
                        maxDrawdownLimit: MaxDrawdown,
                        amountToBeRecovered: AmountToBeRecoverd,
                        isInRecoveryMode: IsRecoveryMode));
                }
                
                // Update dynamic Martingale level (always dynamic now)
                // Progressive dynamic Martingale based on fractions toward max drawdown
                // MartingaleLevel from UI determines the maximum progression levels
                int maxDynamicMartingaleLevel = Math.Max(1, MartingaleLevel); // Ensure minimum of 1
                
                // Calculate progressive thresholds: 1/max, 2/max, 3/max, etc. of max drawdown
                CurrentMartingaleLevel = 1; // Start with level 1
                
                for (int level = 2; level <= maxDynamicMartingaleLevel; level++)
                {
                    decimal threshold = MaxDrawdown * (level - 1) / maxDynamicMartingaleLevel;
                    if (AmountToBeRecoverd >= threshold)
                    {
                        CurrentMartingaleLevel = level;
                    }
                    else
                    {
                        break; // Stop at first threshold not met
                    }
                }
                
                logger.Debug($"Dynamic Martingale level set to {CurrentMartingaleLevel} " +
                           $"(Amount to recover: {AmountToBeRecoverd} vs Max drawdown: {MaxDrawdown}, Max levels: {maxDynamicMartingaleLevel})");

                // Calculate recovery stake using Martingale strategy
                var stakeToBeUsed = AmountToBeRecoverd * Stake / PreviousProfit; 
                var martingaleValue = stakeToBeUsed / Stake;
                var roundedMartingaleValue = Math.Round(martingaleValue, 3);

                // Apply Martingale level to control progression (always use current dynamic level)
                DynamicStake = Math.Round(Stake * martingaleValue / CurrentMartingaleLevel, 2);

                logger.Debug($"Calculated new dynamic stake: {DynamicStake} (Martingale value: {roundedMartingaleValue}, Level: {CurrentMartingaleLevel})");

                // Apply lower limit to the DynamicStake
                if (IsRecoveryMode)
                {
                    DynamicStake = Math.Max(DynamicStake, 0.35m);
                }
            }
            else if (IsRecoveryMode)
            {
                // Add the profit to recoveryResults
                recoveryResults.Add(mlp);
                decimal totalRecoverySum = recoveryResults.Sum();
                RecoveryAttemptsLeft--;

                // Check if recovery is complete:
                // 1. Primary: Full recovery achieved (sum is positive or zero)
                // 2. Secondary: Attempts exhausted AND remaining amount is negligible (< 0.01)
                bool isFullyRecovered = totalRecoverySum >= 0;
                bool isNegligibleAmountLeft = RecoveryAttemptsLeft == 0 && Math.Abs(totalRecoverySum) < 0.01m;
                
                if (isFullyRecovered || isNegligibleAmountLeft)
                {
                    DynamicStake = Stake;
                    IsRecoveryMode = false;
                    recoveryResults.Clear();
                    CurrentMartingaleLevel = 1;
                    
                    string exitReason = isFullyRecovered ? "full recovery achieved" : "attempts exhausted with negligible remaining amount";
                    logger.Info($"Exiting recovery mode - {exitReason} (Total recovery: {totalRecoverySum:F2})");
                    
                    // Emit recovery exit event
                    RecoveryStateChanged?.Invoke(this, new RecoveryStateChangedEventArgs(
                        enteredRecovery: false,
                        exitedRecovery: true,
                        amountToRecover: 0,
                        recoveryAttemptsLeft: RecoveryAttemptsLeft));
                }
                else
                {
                    // Still in recovery - update amount to be recovered
                    AmountToBeRecoverd = -totalRecoverySum;
                    logger.Debug($"Recovery ongoing - Amount left to recover: {AmountToBeRecoverd:F2}, Attempts left: {RecoveryAttemptsLeft}");
                }
            }
            else if (!IsRecoveryMode)
            {
                TempBarrier = 0;
            }

            // Emit trade processed event for external tracking (UI, hierarchy profit tracking, etc.)
            TradeProcessed?.Invoke(this, new TradeProfitEventArgs(
                profitLoss: mlp,
                totalProfit: TotalProfit,
                isInRecoveryMode: IsRecoveryMode,
                dynamicStake: DynamicStake,
                appId: appId,
                contractId: contractId,
                transactionTime: transactionTime));

            // Format and log the transaction
            string formattedTransactionTime = DateTimeOffset.FromUnixTimeSeconds(transactionTime).ToString("dd-MM-yyyy @ HH:mm:ss");
            logger.Debug($"Trade processed - Client: {appId}, Contract: {contractId}, Time: {formattedTransactionTime}, P/L: {mlp:C}, Recovery: {(IsRecoveryMode ? "Yes" : "No")}");
        }

        /// Returns a string representation of the trading parameters, including all relevant values.
        public override string ToString()
        {
            return $"{nameof(BuyBarrier)}: {BuyBarrier}, {nameof(SellBarrier)}: {SellBarrier}, {nameof(Symbol)}: {Symbol}, {nameof(Duration)}: {Duration}, {nameof(Stake)}: {Stake}, {nameof(DurationType)}: {DurationType}, {nameof(MaxDrawdown)}: {MaxDrawdown}, {nameof(MartingaleLevel)}: {MartingaleLevel}, {nameof(DesiredReturnPercent)}: {DesiredReturnPercent}, {nameof(LastCalibratedReturnPercent)}: {LastCalibratedReturnPercent}, {nameof(TakeProfit)}: {TakeProfit}, {nameof(IsRecoveryMode)}: {IsRecoveryMode}, {nameof(AmountToBeRecoverd)}: {AmountToBeRecoverd}, {nameof(DynamicStake)}: {DynamicStake}, {nameof(PreviousProfit)}: {PreviousProfit}, {nameof(RecoveryAttemptsLeft)}: {RecoveryAttemptsLeft}, {nameof(TotalProfit)}: {TotalProfit}";
        }

        /// <summary>
        /// Creates a deep copy of the `TradingParameters` object ensuring that each trading account has its own set of trading parameters that can be modified independently
        /// preventing unintended side effects.
        /// <returns>A deep copy of the `TradingParameters` object.</returns>
        /// </summary>
        public object Clone()
        {
            var clone = (TradingParameters)MemberwiseClone();
            clone.recoveryResults = new List<decimal>(recoveryResults);
            return clone;
        }
    }

    // ===== EVENT ARGS FOR EVENT-DRIVEN ARCHITECTURE =====
    
    /// <summary>
    /// Event arguments when take profit target is reached
    /// </summary>
    public class TakeProfitReachedEventArgs : EventArgs
    {
        public decimal TotalProfit { get; }
        public decimal TargetProfit { get; }
        public bool IsInRecoveryMode { get; }
        
        public TakeProfitReachedEventArgs(decimal totalProfit, decimal targetProfit, bool isInRecoveryMode)
        {
            TotalProfit = totalProfit;
            TargetProfit = targetProfit;
            IsInRecoveryMode = isInRecoveryMode;
        }
    }
    
    /// <summary>
    /// Event arguments when max drawdown is exceeded - signals need for hierarchy escalation
    /// </summary>
    public class MaxDrawdownExceededEventArgs : EventArgs
    {
        public decimal CurrentDrawdown { get; }
        public decimal MaxDrawdownLimit { get; }
        public decimal AmountToBeRecovered { get; }
        public bool IsInRecoveryMode { get; }
        
        public MaxDrawdownExceededEventArgs(decimal currentDrawdown, decimal maxDrawdownLimit, decimal amountToBeRecovered, bool isInRecoveryMode)
        {
            CurrentDrawdown = currentDrawdown;
            MaxDrawdownLimit = maxDrawdownLimit;
            AmountToBeRecovered = amountToBeRecovered;
            IsInRecoveryMode = isInRecoveryMode;
        }
    }
    
    /// <summary>
    /// Event arguments when recovery mode state changes
    /// </summary>
    public class RecoveryStateChangedEventArgs : EventArgs
    {
        public bool EnteredRecovery { get; }
        public bool ExitedRecovery { get; }
        public decimal AmountToRecover { get; }
        public int RecoveryAttemptsLeft { get; }
        
        public RecoveryStateChangedEventArgs(bool enteredRecovery, bool exitedRecovery, decimal amountToRecover, int recoveryAttemptsLeft)
        {
            EnteredRecovery = enteredRecovery;
            ExitedRecovery = exitedRecovery;
            AmountToRecover = amountToRecover;
            RecoveryAttemptsLeft = recoveryAttemptsLeft;
        }
    }
    
    /// <summary>
    /// Event arguments after each trade is processed
    /// </summary>
    public class TradeProfitEventArgs : EventArgs
    {
        public decimal ProfitLoss { get; }
        public decimal TotalProfit { get; }
        public bool IsInRecoveryMode { get; }
        public decimal DynamicStake { get; }
        public int AppId { get; }
        public long ContractId { get; }
        public long TransactionTime { get; }
        
        public TradeProfitEventArgs(decimal profitLoss, decimal totalProfit, bool isInRecoveryMode, 
            decimal dynamicStake, int appId, long contractId, long transactionTime)
        {
            ProfitLoss = profitLoss;
            TotalProfit = totalProfit;
            IsInRecoveryMode = isInRecoveryMode;
            DynamicStake = dynamicStake;
            AppId = appId;
            ContractId = contractId;
            TransactionTime = transactionTime;
        }
    }
}