using System;
using System.Globalization;
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
        private const decimal FinePrecisionStep = 0.001m;
        private const decimal CoarsePrecisionStep = 0.01m;
        private const decimal RoiUpperLimitPercent = 3900m;
        private const decimal AbsoluteMinOffset = FinePrecisionStep;
        private const decimal AbsoluteMaxOffset = 5000m;
        private const int MaxIterations = 60;

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

            if (parameters.DesiredReturnPercent <= 0)
            {
                return BarrierResolutionResult.Failed("Desired return must be greater than zero.");
            }

            decimal stake = parameters.DynamicStake > 0 ? parameters.DynamicStake : parameters.Stake;
            if (stake <= 0)
            {
                return BarrierResolutionResult.Failed("Stake must be greater than zero for ROI calibration.");
            }

            var durationUnit = AuthClient.GetBarrierUnit(parameters.DurationType);
            var currency = authClient.AccountCurrency;
            var targetRoi = Math.Min(parameters.DesiredReturnPercent, RoiUpperLimitPercent);
            if (targetRoi < parameters.DesiredReturnPercent)
            {
                logger.Warn($"Desired ROI {parameters.DesiredReturnPercent}% exceeds broker limit {RoiUpperLimitPercent}%. Clamped target to {targetRoi}%.");
            }

            var initialOffset = DetermineInitialOffset(parameters);
            var searchState = new AdaptiveSearchState(parameters, targetRoi, DefaultTolerancePercent, initialOffset);
            BarrierResolutionResult finalResult = null;

            for (int attempt = 1; attempt <= MaxIterations; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var candidate = searchState.GetNextCandidate();
                var formattedBarrier = FormatBarrier(candidate, searchState.DecimalPlaces);

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
                catch (InvalidOperationException ex) when (IsPrecisionError(ex) && searchState.TryPromoteCoarserPrecision())
                {
                    logger.Warn($"Broker rejected barrier precision {formattedBarrier}. Retrying with {searchState.DecimalPlaces}-decimal offsets.");
                    continue;
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
                logger.Debug($"Calibration sample {attempt}: barrier {formattedBarrier} => ROI {actualReturn:F2}% (target {targetRoi:F2}%, step {searchState.StepSize:F3}).");

                searchState.RecordSample(candidate, actualReturn);

                if (searchState.IsWithinTolerance(actualReturn))
                {
                    finalResult = BarrierResolutionResult.CreateSuccess(candidate, actualReturn);
                    break;
                }

                if (!searchState.Advance(actualReturn))
                {
                    logger.Warn("Adaptive barrier search hit bounds or minimal step. Returning closest observed value.");
                    break;
                }
            }

            return finalResult ?? searchState.GetBestResult() ?? BarrierResolutionResult.Failed("No barrier candidate matched the desired ROI target.");
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

        private static decimal DetermineInitialOffset(TradingParameters parameters)
        {
            var minCandidate = parameters.BarrierSearchMin > 0 ? parameters.BarrierSearchMin : FinePrecisionStep;
            minCandidate = Math.Max(minCandidate, FinePrecisionStep);

            var maxCandidate = parameters.BarrierSearchMax > 0 ? parameters.BarrierSearchMax : Math.Max(minCandidate, 120m);
            maxCandidate = Math.Max(maxCandidate, minCandidate);

            decimal seed = parameters.TempBarrier != 0
                ? Math.Abs(parameters.TempBarrier)
                : (parameters.Barrier > 0 ? Math.Abs(parameters.Barrier) : minCandidate);

            if (seed <= 0)
            {
                seed = minCandidate;
            }

            if (seed < minCandidate)
            {
                seed = minCandidate;
            }
            else if (seed > maxCandidate)
            {
                seed = maxCandidate;
            }

            return seed;
        }

        private static string FormatBarrier(decimal offset, int decimals)
        {
            offset = Math.Abs(Math.Round(offset, decimals));
            var pattern = "+#0." + new string('0', decimals) + ";-#0." + new string('0', decimals) + ";0";
            return offset.ToString(pattern, CultureInfo.InvariantCulture);
        }

        private static bool IsPrecisionError(Exception ex)
        {
            var message = ex?.Message;
            if (string.IsNullOrWhiteSpace(message))
            {
                return false;
            }

            message = message.ToLowerInvariant();
            return message.Contains("decimal") || message.Contains("fraction") || message.Contains("precision") || message.Contains("places");
        }

        private sealed class AdaptiveSearchState
        {
            private readonly decimal targetRoi;
            private readonly decimal tolerance;
            private decimal minOffset;
            private decimal maxOffset;
            private readonly decimal fineStep;
            private readonly decimal coarseStep;
            private bool? lastDirectionIncrease;
            private decimal? bestOffset;
            private decimal? bestReturn;
            private decimal bestDifference = decimal.MaxValue;

            public AdaptiveSearchState(TradingParameters parameters, decimal targetRoi, decimal tolerance, decimal initialOffset)
            {
                this.targetRoi = targetRoi;
                this.tolerance = tolerance;

                fineStep = FinePrecisionStep;
                coarseStep = CoarsePrecisionStep;

                var configuredMin = Math.Max(parameters.BarrierSearchMin > 0 ? parameters.BarrierSearchMin : fineStep, fineStep);
                var configuredMax = parameters.BarrierSearchMax > 0
                    ? Math.Max(parameters.BarrierSearchMax, configuredMin + fineStep)
                    : Math.Max(initialOffset * 4m, configuredMin + 50m);

                minOffset = Math.Min(initialOffset, configuredMin);
                minOffset = Math.Max(minOffset, AbsoluteMinOffset);
                maxOffset = Math.Max(initialOffset, configuredMax);
                maxOffset = Math.Min(maxOffset, AbsoluteMaxOffset);

                CurrentOffset = Clamp(initialOffset);
                DecimalPlaces = 3;

                var configuredStep = parameters.BarrierSearchStep > 0
                    ? parameters.BarrierSearchStep
                    : Math.Max(CurrentOffset / 2m, fineStep);

                StepSize = ClampStep(configuredStep);
            }

            public decimal CurrentOffset { get; private set; }
            public decimal StepSize { get; private set; }
            public int DecimalPlaces { get; private set; }

            public decimal GetNextCandidate()
            {
                return Math.Round(CurrentOffset, DecimalPlaces);
            }

            public void RecordSample(decimal offset, decimal actualReturn)
            {
                var difference = Math.Abs(actualReturn - targetRoi);
                if (difference < bestDifference)
                {
                    bestDifference = difference;
                    bestOffset = Math.Abs(Math.Round(offset, DecimalPlaces));
                    bestReturn = actualReturn;
                }
            }

            public bool IsWithinTolerance(decimal actualReturn)
            {
                return Math.Abs(actualReturn - targetRoi) <= tolerance;
            }

            public bool Advance(decimal actualReturn)
            {
                var shouldIncrease = actualReturn < targetRoi;
                var difference = Math.Abs(actualReturn - targetRoi);

                AdjustForDirectionChange(shouldIncrease);
                AdjustStepMagnitude(shouldIncrease);
                ApplyPrecisionCap(difference);

                var direction = shouldIncrease ? 1m : -1m;
                var nextOffset = Clamp(CurrentOffset + (direction * StepSize));

                if (nextOffset == CurrentOffset)
                {
                    if (TryExpandBounds(shouldIncrease))
                    {
                        nextOffset = Clamp(CurrentOffset + (direction * StepSize));
                    }

                    if (nextOffset == CurrentOffset)
                    {
                        if (!ReduceStepTowardMinimum())
                        {
                            return false;
                        }

                        nextOffset = Clamp(CurrentOffset + (direction * StepSize));
                        if (nextOffset == CurrentOffset)
                        {
                            return false;
                        }
                    }
                }

                CurrentOffset = nextOffset;
                return true;
            }

            public bool TryPromoteCoarserPrecision()
            {
                if (DecimalPlaces == 3)
                {
                    DecimalPlaces = 2;
                    CurrentOffset = Math.Round(CurrentOffset, DecimalPlaces);
                    StepSize = ClampStep(Math.Max(coarseStep, Math.Round(StepSize, DecimalPlaces)));
                    return true;
                }

                return false;
            }

            public BarrierResolutionResult GetBestResult()
            {
                if (bestOffset.HasValue && bestReturn.HasValue)
                {
                    return BarrierResolutionResult.CreateSuccess(bestOffset.Value, bestReturn.Value);
                }

                return null;
            }

            private void AdjustForDirectionChange(bool shouldIncrease)
            {
                if (lastDirectionIncrease.HasValue && lastDirectionIncrease.Value != shouldIncrease)
                {
                    StepSize = ClampStep(StepSize / 2m);
                }

                lastDirectionIncrease = shouldIncrease;
            }

            private void AdjustStepMagnitude(bool shouldIncrease)
            {
                if (shouldIncrease)
                {
                    StepSize = ClampStep(StepSize * 1.5m);
                }
                else
                {
                    StepSize = ClampStep(StepSize / 2m);
                }
            }

            private void ApplyPrecisionCap(decimal difference)
            {
                var minStep = GetMinimumStep();

                if (difference <= tolerance)
                {
                    StepSize = minStep;
                    return;
                }

                if (difference <= tolerance * 2m)
                {
                    StepSize = ClampStep(Math.Max(minStep, StepSize / 4m));
                    return;
                }

                if (difference <= tolerance * 5m)
                {
                    StepSize = ClampStep(Math.Max(minStep, StepSize / 2m));
                    return;
                }

                if (difference <= targetRoi)
                {
                    var ratio = Math.Max(difference / targetRoi, 0.01m);
                    var scaled = Math.Max(CurrentOffset * ratio, minStep);
                    var cap = Math.Round(scaled, DecimalPlaces);
                    StepSize = ClampStep(Math.Min(StepSize, cap));
                }
            }

            private bool ReduceStepTowardMinimum()
            {
                var minimumStep = GetMinimumStep();
                if (StepSize <= minimumStep)
                {
                    return false;
                }

                StepSize = ClampStep(StepSize / 2m);
                return StepSize > 0;
            }

            private decimal Clamp(decimal value)
            {
                if (value < minOffset)
                {
                    return minOffset;
                }

                if (value > maxOffset)
                {
                    return maxOffset;
                }

                return value;
            }

            private decimal ClampStep(decimal value)
            {
                var minStep = GetMinimumStep();
                var span = Math.Max(maxOffset - minOffset, minStep);
                var maxStep = Math.Max(span / 2m, minStep);
                var clamped = Math.Min(Math.Max(value, minStep), maxStep);
                return Math.Round(clamped, DecimalPlaces);
            }

            private decimal GetMinimumStep()
            {
                return DecimalPlaces == 3 ? fineStep : coarseStep;
            }

            private bool TryExpandBounds(bool shouldIncrease)
            {
                if (!shouldIncrease)
                {
                    if (minOffset <= AbsoluteMinOffset)
                    {
                        return false;
                    }

                    var newMin = Math.Max(AbsoluteMinOffset, minOffset / 2m);
                    minOffset = newMin;
                    logger.Debug($"Expanded lower barrier search bound to {minOffset:F3}.");
                    return true;
                }

                if (maxOffset >= AbsoluteMaxOffset)
                {
                    return false;
                }

                var expanded = Math.Min(AbsoluteMaxOffset, Math.Max(maxOffset * 2m, maxOffset + coarseStep));
                maxOffset = expanded;
                logger.Debug($"Expanded upper barrier search bound to {maxOffset:F3}.");
                return true;
            }
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
