using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NLog;

namespace FxApi.Connection
{
    /// <summary>
    /// The `TradingParameters` class encapsulates the parameters that govern the trading strategy, 
    /// including initial stake, Martingale settings, profit targets, stop-loss limits, and dynamic stake adjustments 
    /// based on the Martingale recovery system. It provides a mechanism for processing trade outcomes, 
    /// adjusting stake amounts, and entering or exiting recovery mode as needed.
    /// </summary>

    public class TradingParameters : ICloneable
    {
        /// Logger for recording trading parameters and actions.
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// The price barrier offset used for both "Higher" and "Lower" contracts in a pair trade.
        /// This represents the price movement required to trigger a win for either contract.
        /// </summary>
        public decimal Barrier { get; set; }

        /// <summary>
        /// The formatted barrier value used for the "Buy" (Higher) contract, including the positive sign. 
        /// TempBarrier check to determine its place in the trade model.
        /// Example: "+1.50".
        /// </summary>
        public string BuyBarrier => string.Format(CultureInfo.InvariantCulture, "{0:+#0.0#;-#0.0#;0}", TempBarrier != 0 ? TempBarrier : Barrier);

        /// <summary>
        /// The formatted barrier value used for the "Sell" (Lower) contract, including the negative sign.
        /// Example: "-1.50".
        /// </summary>
        public string SellBarrier => string.Format(CultureInfo.InvariantCulture, "{0:+#0.0#;-#0.0#;0}", TempBarrier != 0 ? -TempBarrier : -Barrier);

        /// The trading symbol (e.g., "1HZ50V" - Volatility 50(1s) Index) on which the pair trades are executed.
        public ActiveSymbol Symbol { get; set; }

        /// The duration of each pair trade in seconds.
        public int Duration { get; set; }

        /// The Base stake amount for each pair trade, before any Martingale adjustments.
        public decimal Stake { get; set; }

        /// The type of duration used for the pair trades, which can be "t" (tick), "s" (second), "m" (minute), or "h" (hour).
        public string DurationType { get; set; }

        /// <summary>
        /// The maximum allowable drawdown. This is the trigger used to enter hierarchyy recovery mode.
        /// This helps limit potential losses.
        /// </summary>
        public decimal MaxDrawdown { get; set; }

        /// <summary>
        /// The Martingale level, which controls the aggressiveness of the recovery system.
        /// Providing a way to balance the desire for quicker recovery with the need to manage risk and avoid excessive stake increase.
        /// Higher values lead to slower, less risky recovery. 
        /// A value of 1 means doubling the stake after each loss, basically using the classic basic Martingale strategy of doubling the stake after each loss.
        /// Higher values mean further diversifying the stake increase, which can help reduce the risk of large losses but also slow down the recovery process.
        /// </summary>
        public int MartingaleLevel { get; set; }

        /// <summary>
        /// Number of maximum hierarchy levels to be used per layer.
        /// </summary>
        public int HierarchyLevels { get; set; }

        /// <summary>
        /// Maximum hierarchy layer depth extent.
        /// </summary>
        public int MaxHierarchyDepth { get; set; }

        /// The target profit level at which trading will be paused.
        public decimal TakeProfit { get; set; }

        /// The stop-loss limit at which trading will be paused.
        public decimal Stoploss { get; set; } = 10000000;

        /// A flag indicating whether the system is currently in Martingale recovery mode.
        public bool IsRecoveryMode { get; set; }

        /// The total amount of losses to be recovered during the current Martingale recovery attempt.
        public decimal AmountToBeRecoverd { get; set; }

        /// The dynamically calculated stake amount for the current trade, adjusted based on the Godfathers martingale logic.
        public decimal DynamicStake { get; set; }

        /// The profit or loss from the previous trade. Used to determine if recovery mode should be triggered.
        public decimal PreviousProfit { get; set; }

        /// The number of remaining recovery attempts in the current Martingale recovery cycle.
        public int RecoveryAttemptsLeft { get; set; }

        /// Temporarily overrides the Barrier property when in hierarchy mode.
        public decimal TempBarrier { get; set; }

        public int InitialStake4Layer1 { get; set; }

        private List<decimal> recoveryResults = new List<decimal>();

        protected List<decimal> RecoveryResults
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
            return $"{nameof(BuyBarrier)}: {BuyBarrier}, {nameof(SellBarrier)}: {SellBarrier}, {nameof(Symbol)}: {Symbol}, {nameof(Duration)}: {Duration}, {nameof(Stake)}: {Stake}, {nameof(DurationType)}: {DurationType}, {nameof(MaxDrawdown)}: {MaxDrawdown}, {nameof(MartingaleLevel)}: {MartingaleLevel}, {nameof(TakeProfit)}: {TakeProfit}, {nameof(IsRecoveryMode)}: {IsRecoveryMode}, {nameof(AmountToBeRecoverd)}: {AmountToBeRecoverd}, {nameof(DynamicStake)}: {DynamicStake}, {nameof(PreviousProfit)}: {PreviousProfit}, {nameof(RecoveryAttemptsLeft)}: {RecoveryAttemptsLeft}";
        }

        /// <summary>
        /// Creates a deep copy of the `TradingParameters` object ensuring that each trading account has its own set of trading parameters that can be modified independently
        /// preventing unintended side effects.
        /// <returns>A deep copy of the `TradingParameters` object.</returns>
        /// </summary>
        public object Clone()
        {
            return MemberwiseClone();
        }
    }
}