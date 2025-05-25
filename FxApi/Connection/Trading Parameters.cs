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
    /// </summary>

    public class TradingParameters : ICloneable
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        public decimal Barrier { get; set; }
        public string BuyBarrier => string.Format(CultureInfo.InvariantCulture, "{0:+#0.0#;-#0.0#;0}", TempBarrier != 0 ? TempBarrier : Barrier);
        public string SellBarrier => string.Format(CultureInfo.InvariantCulture, "{0:+#0.0#;-#0.0#;0}", TempBarrier != 0 ? -TempBarrier : -Barrier);
        public ActiveSymbol Symbol { get; set; }
        public int Duration { get; set; }
        public decimal Stake { get; set; }
        public string DurationType { get; set; }
        public decimal MaxDrawdown { get; set; }
        public int MartingaleLevel { get; set; }
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

        public event EventHandler<decimal> TakeProfitReached;

        private List<decimal> recoveryResults = new List<decimal>();
        public List<decimal> RecoveryResults
        {
            get => recoveryResults;
            set => recoveryResults = value;       
        }

        /// <summary>
        /// Processes the outcome of a completed trade, adjusting the `DynamicStake`, entering or exiting recovery mode,
        /// and updating relevant parameters based on the Godfathers recovery system.
        /// <param name="mlp">The actual profit or loss of the completed trade (Most Recent Profit/Loss).</param>
        /// <param name="estimate">An estimated profit value, typically used when `mlp` is zero to determine the initial stake for recovery.</param>
        /// <param name="recoveryResults">The list of recovery results for the *current level*.</param>
        /// </summary>
        public void Process(decimal mlp, decimal estimate, int appId, long contractId, int transactionTime)
        {
            // Update total profit
            TotalProfit += mlp;

            // Check if take profit target is reached
            if (TotalProfit >= TakeProfit)
            {
                TakeProfitReached?.Invoke(this, TotalProfit);
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
                    AmountToBeRecoverd = 2 * Stake;
                    logger.Debug($"Entering recovery mode. Initial amount to recover: {AmountToBeRecoverd}");
                }
                else
                {
                    // Update amount to be recovered based on accumulated losses
                    AmountToBeRecoverd = -recoveryResults.Sum();
                    logger.Debug($"Updated amount to recover: {AmountToBeRecoverd}");
                }

                // Calculate recovery stake using Martingale strategy
                var stakeToBeUsed = AmountToBeRecoverd * Stake / PreviousProfit;
                var martingaleValue = stakeToBeUsed / Stake;

                // Apply Martingale level to control progression
                DynamicStake = Math.Round(Stake * martingaleValue / MartingaleLevel, 2);

                logger.Debug($"Calculated new dynamic stake: {DynamicStake} (Martingale value: {martingaleValue}, Level: {MartingaleLevel})");

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
                AmountToBeRecoverd = -recoveryResults.Sum();
                RecoveryAttemptsLeft--;

                decimal recoveryProfit = mlp + recoveryResults.Sum();

                // Check if recovery is complete
                if (recoveryProfit >= AmountToBeRecoverd || RecoveryAttemptsLeft == 0)
                {
                    // Only exit recovery mode if we're not in a hierarchy level
                    if (TempBarrier == 0)
                    {
                        DynamicStake = Stake;
                        IsRecoveryMode = false;
                        recoveryResults.Clear();
                        logger.Info("Exiting recovery mode - recovery complete");
                    }
                    else
                    {
                        // In hierarchy mode, keep recovery mode active but reset stake to the level's initial stake
                        // Use LevelInitialStake if it's set, otherwise fall back to Stake
                        decimal resetStake = LevelInitialStake > 0 ? LevelInitialStake : Stake;
                        DynamicStake = resetStake;
                        logger.Info($"Recovery target met in hierarchy mode. Reset dynamic stake to level's initial stake: {resetStake}");
                    }
                }
            }
            else if (!IsRecoveryMode)
            {
                TempBarrier = 0;
            }

            // Format and log the transaction
            string formattedTransactionTime = DateTimeOffset.FromUnixTimeSeconds(transactionTime).ToString("dd-MM-yyyy @ HH:mm:ss");
            logger.Debug($"Trade processed - Client: {appId}, Contract: {contractId}, Time: {formattedTransactionTime}, P/L: {mlp:C}, Recovery: {(IsRecoveryMode ? "Yes" : "No")}");
        }

        /// Returns a string representation of the trading parameters, including all relevant values.
        public override string ToString()
        {
            // Format the trading parameters as a string, including all relevant values.
            return $"{nameof(BuyBarrier)}: {BuyBarrier}, {nameof(SellBarrier)}: {SellBarrier}, {nameof(Symbol)}: {Symbol}, {nameof(Duration)}: {Duration}, {nameof(Stake)}: {Stake}, {nameof(DurationType)}: {DurationType}, {nameof(MaxDrawdown)}: {MaxDrawdown}, {nameof(MartingaleLevel)}: {MartingaleLevel}, {nameof(TakeProfit)}: {TakeProfit}, {nameof(IsRecoveryMode)}: {IsRecoveryMode}, {nameof(AmountToBeRecoverd)}: {AmountToBeRecoverd}, {nameof(DynamicStake)}: {DynamicStake}, {nameof(PreviousProfit)}: {PreviousProfit}, {nameof(RecoveryAttemptsLeft)}: {RecoveryAttemptsLeft}, {nameof(TotalProfit)}: {TotalProfit}";
        }        /// <summary>
        /// Updates the TotalProfit without triggering the full Process logic.
        /// This is used in hierarchy mode to track profit without interference.
        /// </summary>
        /// <param name="profitAmount">The profit amount to add to total profit</param>
        public void UpdateTotalProfit(decimal profitAmount)
        {
            TotalProfit += profitAmount;

            // Check if take profit target is reached
            if (TotalProfit >= TakeProfit)
            {
                TakeProfitReached?.Invoke(this, TotalProfit);
            }
        }

        /// <summary>
        /// Resets the TotalProfit to zero for hierarchy level transitions.
        /// This is used when starting a new hierarchy level with a clean profit state.
        /// </summary>
        public void ResetTotalProfit()
        {
            TotalProfit = 0;
            logger.Info("Reset TotalProfit to 0 for hierarchy level transition");
        }

        /// <summary>
        /// Completely resets the trading parameters when transitioning between hierarchy levels.
        /// This ensures a clean state for the new level without any residual data from the previous level.
        /// </summary>
        public void ResetForHierarchyTransition()
        {
            ResetTotalProfit();
            IsRecoveryMode = false;
            AmountToBeRecoverd = 0;
            PreviousProfit = 0;
            RecoveryAttemptsLeft = 0;
            
            // Don't clear recovery results here - that should be done explicitly when needed
            // to avoid losing important trade history
            
            logger.Info("Reset trading parameters for hierarchy level transition");
        }

        /// <summary>
        /// Recalculates the TotalProfit based on the positive entries in RecoveryResults.
        /// This ensures that the TotalProfit accurately reflects the current state
        /// of recovery results, which is crucial for hierarchy level transitions.
        /// </summary>
        public void RecalculateTotalProfit()
        {
            decimal positiveSum = recoveryResults.Where(r => r > 0).Sum();
            TotalProfit = positiveSum;
            
            logger.Info($"Recalculated TotalProfit: {TotalProfit:F2} from {recoveryResults.Count} recovery results");
            
            // Check if take profit target is reached after recalculation
            if (TotalProfit >= TakeProfit)
            {
                logger.Info($"Take profit target reached after recalculation: {TotalProfit:F2} >= {TakeProfit:F2}");
                TakeProfitReached?.Invoke(this, TotalProfit);
            }
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
}