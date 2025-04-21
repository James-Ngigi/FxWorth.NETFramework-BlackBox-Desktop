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
        public int InitialStake4Layer1 { get; set; }
        public decimal TotalProfit { get; private set; }

        public event EventHandler<decimal> TakeProfitReached;

        public List<decimal> recoveryResults = new List<decimal>();
        public List<decimal> RecoveryResults
        {
            get => recoveryResults;
            set => recoveryResults = value;
        }

        /*------------------------------------------------------------------------------------------------------------------------*/


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

            // If not in recovery mode and the trade was profitable, update the PreviousProfit.
            if (!IsRecoveryMode && mlp > 0)
            {
                PreviousProfit = mlp;
            }

            // If the PreviousProfit is zero, use the estimated profit value for recovery calculations.
            if (PreviousProfit == 0)
            {
                PreviousProfit = estimate;
            }

            // If the trade resulted in a loss...
            if (mlp < 0)
            {
                // Add the loss to the recoveryResults list.
                recoveryResults.Add(mlp);

                // If not already in recovery mode, enter recovery mode and set the initial amount to be recovered.
                if (!IsRecoveryMode)
                {
                    IsRecoveryMode = true;

                    // We multiply the AmountToBeRecoverd by 2 to respect the pair trade strategy logic.
                    AmountToBeRecoverd = 2 * Stake;
                }

                else
                {
                    // If already in recovery mode, update the amount to be recovered.
                    AmountToBeRecoverd = -recoveryResults.Sum();
                }

                /// Calculate the stake required to recover the losses, considering previous profit.
                /// <summary>   
                /// Stake to be used is calculated to recover all previous losses, aiming to return to a net profit state.
                /// This is done based on the historical performance represented by the PreviousProfit(mlp/estimate).
                ///  By dividing by the PreviousProfit, the formula automatically adjusts the stake based  on the average win payout observerd in previous trades.
                /// </summary>
                var stakeToBeUsed = AmountToBeRecoverd * Stake / PreviousProfit;

                /// Determine the Martingale multiplier based on the stake required and the base stake.
                /// <summary>
                /// The Martingale value represents the multiplier needed to achieve this recovery.
                /// By saying "this" i mean the stakeToBeUsed determined above, which is the stake needed to return to a net profit state.
                /// The formula calculates a multiplier tht represents how much the base stake needs to be multiplied to reach the stakeToBeUsed.
                /// It essentially qunatifies the "Martingale effect" and provides a clear measure of how aggressive the system is responding to losses.
                /// </summary>
                var martingaleValue = stakeToBeUsed / Stake;

                // Calculate the dynamic stake, rounded to two decimal places.
                DynamicStake = Math.Round(Stake * martingaleValue / MartingaleLevel, 2);

                // Apply lower limit to the DynamicStake (Derivs stake lower limit of 0.35 )
                if (IsRecoveryMode)
                {
                    DynamicStake = Math.Max(DynamicStake, 0.35m);
                }

            }

            // If currently in recovery mode and the trade was profitable...
            else if (IsRecoveryMode)
            {
                // Add the profit to the recoveryResults list.
                recoveryResults.Add(mlp);

                // Update the amount to be recovered.
                AmountToBeRecoverd = -recoveryResults.Sum();

                // Decrement the remaining recovery attempts counter.
                RecoveryAttemptsLeft--;

                // Calculate the total profit achieved during the recovery attempt.
                decimal recoveryProfit = mlp + recoveryResults.Sum();

                // If the recovery profit meets or exceeds the target, or if all recovery attempts have been used, exit recovery mode.
                if (recoveryProfit >= AmountToBeRecoverd || RecoveryAttemptsLeft == 0)
                {
                    // Reset the dynamic stake to the initial stake.
                    DynamicStake = Stake;
                    // Exit recovery mode.
                    IsRecoveryMode = false;
                    // Clear the recovery results list.
                    recoveryResults.Clear();
                }
            }

            // Reset TempBarrier to 0 after each trade to ensure it doesn't interfere with subsequent trades outside the hierarchy
            TempBarrier = 0;

            // Format the transaction time
            string formattedTransactionTime = DateTimeOffset.FromUnixTimeSeconds(transactionTime).ToString("dd-MM-yyyy @ HH:mm:ss");

            // Log the processed trade outcome and relevant trading parameters. STT = Server Transaction Time.
            //logger.Info($"<=> Deal Processed.... => Client-ID-> {appId} :: Contract ID-> {contractId} :: STT-> {formattedTransactionTime} :: Profit/Loss-> {mlp:C}    :: Recovery Mode-> {(IsRecoveryMode ? "Yes" : "No")}.");
        }

        /// Returns a string representation of the trading parameters, including all relevant values.
        public override string ToString()
        {
            // Format the trading parameters as a string, including all relevant values.
            return $"{nameof(BuyBarrier)}: {BuyBarrier}, {nameof(SellBarrier)}: {SellBarrier}, {nameof(Symbol)}: {Symbol}, {nameof(Duration)}: {Duration}, {nameof(Stake)}: {Stake}, {nameof(DurationType)}: {DurationType}, {nameof(MaxDrawdown)}: {MaxDrawdown}, {nameof(MartingaleLevel)}: {MartingaleLevel}, {nameof(TakeProfit)}: {TakeProfit}, {nameof(IsRecoveryMode)}: {IsRecoveryMode}, {nameof(AmountToBeRecoverd)}: {AmountToBeRecoverd}, {nameof(DynamicStake)}: {DynamicStake}, {nameof(PreviousProfit)}: {PreviousProfit}, {nameof(RecoveryAttemptsLeft)}: {RecoveryAttemptsLeft}, {nameof(TotalProfit)}: {TotalProfit}";
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