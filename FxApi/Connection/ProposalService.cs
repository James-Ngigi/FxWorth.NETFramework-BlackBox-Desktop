using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NLog;

namespace FxApi.Connection
{
    /// <summary>
    /// Resolves Deriv barriers that match a desired return-on-investment target by iterating over proposals.
    /// </summary>
    public class ProposalService
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        private const decimal DefaultTolerancePercent = 2m;

        private readonly AuthClient authClient;

        public ProposalService(AuthClient client)
        {
            authClient = client ?? throw new ArgumentNullException(nameof(client));
        }

        public async Task<BarrierResolutionResult> ResolveBarrierAsync(TradingParameters parameters, CancellationToken cancellationToken)
        {
            if (parameters == null)
            {
                return BarrierResolutionResult.Failed("Trading parameters are not available.");
            }

            if (!parameters.RequiresReturnCalibration)
            {
                return BarrierResolutionResult.Skipped();
            }

            if (parameters.Symbol == null)
            {
                return BarrierResolutionResult.Failed("Symbol is not configured.");
            }

            decimal stake = parameters.DynamicStake > 0 ? parameters.DynamicStake : parameters.Stake;
            if (stake <= 0)
            {
                return BarrierResolutionResult.Failed("Stake must be greater than zero for ROI calibration.");
            }

            var durationUnit = AuthClient.GetBarrierUnit(parameters.DurationType);
            var currency = authClient.AccountCurrency;
            var targetRoi = parameters.DesiredReturnPercent;

            BarrierResolutionResult bestResult = null;

            foreach (var candidate in GenerateCandidates(parameters))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var formattedBarrier = candidate.ToString("+#0.0#;-#0.0#;0", CultureInfo.InvariantCulture);

                var request = new ProposalRequest
                {
                    amount = stake,
                    basis = "stake",
                    barrier = formattedBarrier,
                    contract_type = "CALL",
                    currency = currency,
                    duration = parameters.Duration,
                    duration_unit = durationUnit,
                    symbol = parameters.Symbol.symbol
                };

                ProposalResponse response;
                try
                {
                    response = await authClient.RequestProposalAsync(request, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    logger.Warn(ex, $"Proposal request failed for barrier {formattedBarrier}");
                    continue;
                }

                if (response?.proposal == null)
                {
                    if (response?.error != null)
                    {
                        logger.Warn($"Proposal error for barrier {formattedBarrier}: {response.error.code} - {response.error.message}");
                    }
                    continue;
                }

                decimal actualReturn = CalculateReturnPercent(stake, response.proposal.payout);
                var currentResult = BarrierResolutionResult.CreateSuccess(candidate, actualReturn);

                if (bestResult == null || currentResult.GetDifference(targetRoi) < bestResult.GetDifference(targetRoi))
                {
                    bestResult = currentResult;
                }

                if (currentResult.IsWithinTolerance(targetRoi, DefaultTolerancePercent))
                {
                    break;
                }
            }

            return bestResult ?? BarrierResolutionResult.Failed("No barrier candidate matched the desired ROI target.");
        }

        private IEnumerable<decimal> GenerateCandidates(TradingParameters parameters)
        {
            decimal min = Math.Max(0.1m, parameters.BarrierSearchMin);
            decimal max = Math.Max(min, parameters.BarrierSearchMax);
            decimal step = parameters.BarrierSearchStep > 0 ? parameters.BarrierSearchStep : 1m;

            decimal seed = parameters.Barrier > 0 ? parameters.Barrier : parameters.TempBarrier;
            if (seed <= 0)
            {
                seed = Math.Min(Math.Max(10m, min), max);
            }
            else
            {
                seed = Math.Min(Math.Max(seed, min), max);
            }

            yield return decimal.Round(seed, 2);

            decimal span = max - min;
            if (span == 0)
            {
                yield break;
            }

            for (decimal offset = step; offset <= span; offset += step)
            {
                var higher = seed + offset;
                if (higher <= max)
                {
                    yield return decimal.Round(higher, 2);
                }

                var lower = seed - offset;
                if (lower >= min)
                {
                    yield return decimal.Round(lower, 2);
                }
            }
        }

        private static decimal CalculateReturnPercent(decimal stake, decimal payout)
        {
            if (stake <= 0)
            {
                return 0;
            }

            var profit = payout - stake;
            return profit / stake * 100m;
        }
    }

    public class BarrierResolutionResult
    {
        private BarrierResolutionResult(bool success, decimal barrier, decimal actualReturnPercent, string error)
        {
            Success = success;
            Barrier = barrier;
            ActualReturnPercent = actualReturnPercent;
            Error = error;
        }

        public bool Success { get; }
        public decimal Barrier { get; }
        public decimal ActualReturnPercent { get; }
        public string Error { get; }

        public static BarrierResolutionResult CreateSuccess(decimal barrier, decimal actualReturnPercent) =>
            new BarrierResolutionResult(true, barrier, actualReturnPercent, null);

        public static BarrierResolutionResult Failed(string error) =>
            new BarrierResolutionResult(false, 0m, 0m, error);

        public static BarrierResolutionResult Skipped() =>
            new BarrierResolutionResult(true, 0m, 0m, null);

        public decimal GetDifference(decimal target) => Math.Abs(ActualReturnPercent - target);

        public bool IsWithinTolerance(decimal target, decimal tolerancePercent) =>
            GetDifference(target) <= tolerancePercent;
    }
}
