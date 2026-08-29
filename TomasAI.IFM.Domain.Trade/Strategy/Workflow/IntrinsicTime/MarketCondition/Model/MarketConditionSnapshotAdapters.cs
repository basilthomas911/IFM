using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.MarketCondition;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Model;

/// <summary>
/// Authoritative, bounded source values consumed by the Market Condition adapter boundary.
/// Provider-native floating point values are normalized exactly once before a snapshot is sealed.
/// </summary>
public sealed record MarketConditionRawFuturesQuote
{
    public double BidPrice { get; init; }
    public double AskPrice { get; init; }
    public double BidSize { get; init; }
    public double AskSize { get; init; }
    public double LastPrice { get; init; }
    public double OneMinuteMoveAtr { get; init; }
    public MarketSourceObservation QuoteObservation { get; init; } = new();
    public MarketSourceObservation TradeObservation { get; init; } = new();
}

/// <summary>One option quote joined to authoritative Securities metadata.</summary>
public sealed record MarketConditionRawOptionContract
{
    public string ContractId { get; init; } = string.Empty;
    public string InstrumentRoot { get; init; } = string.Empty;
    public DateOnly ExpirationDate { get; init; }
    public string OptionType { get; init; } = string.Empty;
    public double StrikePrice { get; init; }
    public double BidPrice { get; init; }
    public double AskPrice { get; init; }
    public double BidSize { get; init; }
    public double AskSize { get; init; }
    public double UnderlyingPrice { get; init; }
    public MarketSourceObservation Observation { get; init; } = new();
}

/// <summary>Typed adapter contracts used by live feed, Securities, calendar, event, health, and broker integrations.</summary>
public interface IMarketConditionFuturesQuoteAdapter
{
    ValueTask<MarketConditionRawFuturesQuote> ReadOnceAsync(string instrumentRoot, DateTime evaluationTimestampUtc,
        CancellationToken cancellationToken);
}

public interface IMarketConditionOptionUniverseAdapter
{
    ValueTask<IReadOnlyCollection<MarketConditionRawOptionContract>> ReadOnceAsync(string instrumentRoot,
        decimal futuresUnderlyingPrice, MarketConditionOptionLiquidityConfiguration configuration,
        DateTime evaluationTimestampUtc, CancellationToken cancellationToken);
}

public interface IMarketConditionSessionAdapter
{
    ValueTask<MarketConditionSessionState> ReadOnceAsync(string instrumentRoot,
        MarketConditionSessionConfiguration configuration, DateTime evaluationTimestampUtc,
        CancellationToken cancellationToken);
}

public interface IMarketConditionEventRiskAdapter
{
    ValueTask<MarketConditionEventRiskState> ReadOnceAsync(MarketConditionEventRiskConfiguration configuration,
        DateTime evaluationTimestampUtc, CancellationToken cancellationToken);
}

public interface IMarketConditionVolatilityAdapter
{
    ValueTask<MarketConditionVolatilityShockState> ReadOnceAsync(string instrumentRoot,
        DateTime evaluationTimestampUtc, CancellationToken cancellationToken);
}

public interface IMarketConditionOperationalHealthAdapter
{
    ValueTask<IReadOnlyCollection<MarketConditionOperationalHealthItem>> ReadOnceAsync(
        IReadOnlyCollection<string> requiredSources, DateTime evaluationTimestampUtc,
        CancellationToken cancellationToken);
}

public interface IMarketConditionSnapshotAdapterCoordinator
{
    ValueTask<MarketConditionSnapshot> PublishAsync(ExecuteMarketConditionPipelineCommand command,
        MarketConditionWorkflowEligibilityState workflowEligibility, DateTime evaluationTimestampUtc,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Reads every production adapter exactly once and assembles one immutable bounded value.
/// </summary>
public sealed class MarketConditionSnapshotAdapterCoordinator(
    IMarketConditionFuturesQuoteAdapter futures,
    IMarketConditionOptionUniverseAdapter options,
    IMarketConditionSessionAdapter session,
    IMarketConditionEventRiskAdapter events,
    IMarketConditionVolatilityAdapter volatility,
    IMarketConditionOperationalHealthAdapter health) : IMarketConditionSnapshotAdapterCoordinator
{
    public async ValueTask<MarketConditionSnapshot> PublishAsync(ExecuteMarketConditionPipelineCommand command,
        MarketConditionWorkflowEligibilityState workflowEligibility, DateTime evaluationTimestampUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(workflowEligibility);
        var p = command.ParameterSet;
        var futuresValue = await futures.ReadOnceAsync(command.InstrumentRoot, evaluationTimestampUtc,
            cancellationToken).ConfigureAwait(false);
        var normalizedFutures = MarketConditionSnapshotAssembler.Normalize(futuresValue);
        var optionsTask = options.ReadOnceAsync(command.InstrumentRoot, normalizedFutures.LastPrice,
            p.OptionLiquidity, evaluationTimestampUtc, cancellationToken).AsTask();
        var sessionTask = session.ReadOnceAsync(command.InstrumentRoot, p.Session, evaluationTimestampUtc,
            cancellationToken).AsTask();
        var eventsTask = events.ReadOnceAsync(p.EventRisk, evaluationTimestampUtc, cancellationToken).AsTask();
        var volatilityTask = volatility.ReadOnceAsync(command.InstrumentRoot, evaluationTimestampUtc,
            cancellationToken).AsTask();
        var healthTask = health.ReadOnceAsync(p.OperationalReadiness.RequiredHealthSources,
            evaluationTimestampUtc, cancellationToken).AsTask();
        await Task.WhenAll(optionsTask, sessionTask, eventsTask, volatilityTask, healthTask)
            .ConfigureAwait(false);

        var optionQuality = MarketConditionSnapshotAssembler.AggregateOptions(await optionsTask.ConfigureAwait(false),
            normalizedFutures.LastPrice, DateOnly.FromDateTime(evaluationTimestampUtc), p.OptionLiquidity);
        var sessionState = await sessionTask.ConfigureAwait(false);
        var eventRisk = await eventsTask.ConfigureAwait(false);
        var shock = await volatilityTask.ConfigureAwait(false);
        var operationalHealth = (await healthTask.ConfigureAwait(false))
            .OrderBy(x => x.SourceId, StringComparer.Ordinal).ToArray();
        var observations = new[]
        {
            normalizedFutures.QuoteObservation, normalizedFutures.TradeObservation,
            optionQuality.Observation, sessionState.Observation, eventRisk.Observation, shock.Observation
        }.Concat(operationalHealth.Select(x => x.Observation)).ToArray();
        var marketObservations = new[]
        {
            normalizedFutures.QuoteObservation, normalizedFutures.TradeObservation,
            optionQuality.Observation, shock.Observation
        };
        var snapshot = MarketConditionSnapshotHash.Seal(new MarketConditionSnapshot
        {
            SnapshotId = Guid.CreateVersion7(new DateTimeOffset(evaluationTimestampUtc, TimeSpan.Zero)),
            WorkflowId = command.WorkflowId,
            EntityId = command.WorkflowEntityId,
            FundId = command.FundId,
            InstrumentRoot = command.InstrumentRoot,
            TargetHorizon = command.TargetHorizon,
            EvaluationTimestampUtc = evaluationTimestampUtc,
            MarketDataAsOfUtc = marketObservations.Min(x => x.SourceTimestampUtc),
            SourceSequenceWatermark = observations.Max(x => x.SequenceId),
            FuturesQuote = normalizedFutures,
            OptionChainQuality = optionQuality,
            SessionState = sessionState,
            EventRiskState = eventRisk,
            VolatilityShockState = shock,
            OperationalHealth = operationalHealth,
            WorkflowEligibility = workflowEligibility,
            DataQualityItems = observations.OrderBy(x => x.SourceId, StringComparer.Ordinal).ToArray()
        });
        return snapshot;
    }
}

/// <summary>Deterministically converts bounded adapter values into immutable snapshot components.</summary>
public static class MarketConditionSnapshotAssembler
{
    public static MarketConditionFuturesQuote Normalize(MarketConditionRawFuturesQuote quote)
    {
        ArgumentNullException.ThrowIfNull(quote);
        return new()
        {
            BidPrice = Decimal(quote.BidPrice, nameof(quote.BidPrice)),
            AskPrice = Decimal(quote.AskPrice, nameof(quote.AskPrice)),
            BidSize = Decimal(quote.BidSize, nameof(quote.BidSize)),
            AskSize = Decimal(quote.AskSize, nameof(quote.AskSize)),
            LastPrice = Decimal(quote.LastPrice, nameof(quote.LastPrice)),
            OneMinuteMoveAtr = Decimal(quote.OneMinuteMoveAtr, nameof(quote.OneMinuteMoveAtr)),
            QuoteObservation = quote.QuoteObservation,
            TradeObservation = quote.TradeObservation
        };
    }

    public static MarketConditionOptionChainQuality AggregateOptions(
        IEnumerable<MarketConditionRawOptionContract> contracts,
        decimal futuresUnderlyingPrice,
        DateOnly evaluationDate,
        MarketConditionOptionLiquidityConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(contracts);
        ArgumentNullException.ThrowIfNull(configuration);
        if (futuresUnderlyingPrice <= 0m)
            throw new ArgumentOutOfRangeException(nameof(futuresUnderlyingPrice));

        var eligible = contracts
            .Select(Normalize)
            .Where(x => string.Equals(x.InstrumentRoot, "ES", StringComparison.Ordinal) &&
                        x.Dte >= configuration.MinimumDte && x.Dte <= configuration.MaximumDte &&
                        Math.Abs(x.StrikePrice - futuresUnderlyingPrice) / futuresUnderlyingPrice <=
                        configuration.MaximumAbsoluteMoneyness)
            .OrderBy(x => x.ExpirationDate)
            .ThenBy(x => x.StrikePrice)
            .ThenBy(x => x.OptionType, StringComparer.Ordinal)
            .ThenBy(x => x.ContractId, StringComparer.Ordinal)
            .ToArray();

        if (eligible.Length == 0)
            throw new MarketConditionCalculationException(MarketConditionFailureCategory.RequiredInputInvalid,
                MarketConditionReasonCodes.RequiredInput, "The authoritative option universe contains no eligible contracts.");

        var valid = eligible.Where(static x => x.Observation.Validity == MarketSourceValidity.Valid &&
                                               x.Observation.Availability == MarketSourceAvailability.Available &&
                                               x.BidPrice > 0m && x.AskPrice >= x.BidPrice &&
                                               x.UnderlyingPrice > 0m)
            .Select(x => x with
            {
                RelativeSpread = (x.AskPrice - x.BidPrice) / ((x.BidPrice + x.AskPrice) / 2m),
                UnderlyingMismatch = Math.Abs(x.UnderlyingPrice - futuresUnderlyingPrice) / futuresUnderlyingPrice
            }).ToArray();

        var observations = eligible.Select(x => x.Observation).ToArray();
        return new()
        {
            CandidateContractCount = eligible.Length,
            ValidQuoteCount = valid.Length,
            EligibleExpirationCount = eligible.Select(x => x.ExpirationDate).Distinct().Count(),
            HasCalls = eligible.Any(x => IsCall(x.OptionType)),
            HasPuts = eligible.Any(x => IsPut(x.OptionType)),
            ValidQuoteCoverage = Round((decimal)valid.Length / eligible.Length),
            MedianRelativeSpread = Median(valid.Select(x => x.RelativeSpread)),
            P90RelativeSpread = Percentile90(valid.Select(x => x.RelativeSpread)),
            MedianBidSize = Median(valid.Select(x => x.BidSize)),
            MedianAskSize = Median(valid.Select(x => x.AskSize)),
            UnderlyingMismatch = valid.Length == 0 ? 0m : Round(valid.Max(x => x.UnderlyingMismatch)),
            Observation = AggregateObservation(observations)
        };

        NormalizedOption Normalize(MarketConditionRawOptionContract value)
        {
            if (string.IsNullOrWhiteSpace(value.ContractId) || string.IsNullOrWhiteSpace(value.InstrumentRoot) ||
                value.ExpirationDate == default || (!IsCall(value.OptionType) && !IsPut(value.OptionType)))
                throw new MarketConditionCalculationException(MarketConditionFailureCategory.RequiredInputInvalid,
                    MarketConditionReasonCodes.RequiredInput, "Authoritative option contract metadata is invalid.");
            return new(value.ContractId, value.InstrumentRoot, value.ExpirationDate,
                value.OptionType, value.ExpirationDate.DayNumber - evaluationDate.DayNumber,
                Decimal(value.StrikePrice, nameof(value.StrikePrice)),
                Decimal(value.BidPrice, nameof(value.BidPrice)), Decimal(value.AskPrice, nameof(value.AskPrice)),
                Decimal(value.BidSize, nameof(value.BidSize)), Decimal(value.AskSize, nameof(value.AskSize)),
                Decimal(value.UnderlyingPrice, nameof(value.UnderlyingPrice)), value.Observation, 0m, 0m);
        }
    }

    static MarketSourceObservation AggregateObservation(IReadOnlyCollection<MarketSourceObservation> observations)
    {
        if (observations.Count == 0)
            throw new MarketConditionCalculationException(MarketConditionFailureCategory.RequiredInputInvalid,
                MarketConditionReasonCodes.RequiredInput, "Option quote observations are missing.");
        if (observations.Any(x => string.IsNullOrWhiteSpace(x.SourceId) ||
                                  (x.Availability != MarketSourceAvailability.Unavailable && x.SourceTimestampUtc == default) ||
                                  x.Validity == MarketSourceValidity.Unknown ||
                                  x.Availability == MarketSourceAvailability.Unknown))
            throw new MarketConditionCalculationException(MarketConditionFailureCategory.RequiredInputInvalid,
                MarketConditionReasonCodes.RequiredInput, "Option quote observation metadata is invalid.");
        var available = observations.Count(x => x.Availability == MarketSourceAvailability.Available);
        var availability = available == 0
            ? MarketSourceAvailability.Unavailable
            : available < observations.Count || observations.Any(x => x.Availability == MarketSourceAvailability.Degraded)
                ? MarketSourceAvailability.Degraded : MarketSourceAvailability.Available;
        var timestamped = observations.Where(x => x.SourceTimestampUtc != default).ToArray();
        return new()
        {
            SourceId = "OptionChain",
            SourceTimestampUtc = timestamped.Length == 0 ? default : timestamped.Min(x => x.SourceTimestampUtc),
            ReceivedAtUtc = observations.Where(x => x.ReceivedAtUtc != default).Select(x => x.ReceivedAtUtc)
                .DefaultIfEmpty().Min(),
            SequenceId = observations.Max(x => x.SequenceId),
            Availability = availability,
            Validity = observations.Any(x => x.Validity == MarketSourceValidity.Invalid)
                ? MarketSourceValidity.Invalid : MarketSourceValidity.Valid
        };
    }

    static decimal Decimal(double value, string name)
    {
        if (!double.IsFinite(value))
            throw new MarketConditionCalculationException(MarketConditionFailureCategory.RequiredInputInvalid,
                MarketConditionReasonCodes.RequiredInput, $"Provider value {name} is not finite.");
        try { return checked((decimal)value); }
        catch (OverflowException)
        {
            throw new MarketConditionCalculationException(MarketConditionFailureCategory.RequiredInputInvalid,
                MarketConditionReasonCodes.RequiredInput, $"Provider value {name} is outside the decimal range.");
        }
    }

    static bool IsCall(string value) => value.Equals("Call", StringComparison.OrdinalIgnoreCase) || value == "C";
    static bool IsPut(string value) => value.Equals("Put", StringComparison.OrdinalIgnoreCase) || value == "P";
    static decimal Median(IEnumerable<decimal> values)
    {
        var ordered = values.Order().ToArray();
        if (ordered.Length == 0) return 0m;
        var middle = ordered.Length / 2;
        return Round(ordered.Length % 2 == 0 ? (ordered[middle - 1] + ordered[middle]) / 2m : ordered[middle]);
    }
    static decimal Percentile90(IEnumerable<decimal> values)
    {
        var ordered = values.Order().ToArray();
        if (ordered.Length == 0) return 0m;
        return Round(ordered[(int)Math.Ceiling(ordered.Length * 0.90m) - 1]);
    }
    static decimal Round(decimal value) => Math.Round(value, 6, MidpointRounding.AwayFromZero);

    sealed record NormalizedOption(string ContractId, string InstrumentRoot, DateOnly ExpirationDate,
        string OptionType, int Dte, decimal StrikePrice, decimal BidPrice, decimal AskPrice,
        decimal BidSize, decimal AskSize, decimal UnderlyingPrice, MarketSourceObservation Observation,
        decimal RelativeSpread, decimal UnderlyingMismatch);
}
