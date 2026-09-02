using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Application.MarketData.Databento;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.MarketCondition;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Model;

/// <summary>Captures the ES quote/trade hot value and its exact one-minute ATR lineage.</summary>
public sealed class MarketConditionFuturesQuoteAdapter(IMarketDataApi marketData, IDbContextFactory storage)
    : IMarketConditionFuturesQuoteAdapter
{
    public async ValueTask<MarketConditionRawFuturesQuote> ReadOnceAsync(string instrumentRoot,
        DateTime evaluationTimestampUtc, CancellationToken cancellationToken)
    {
        if (!marketData.TryGetOnTheRunFuturesContract(instrumentRoot, out var contract) ||
            !marketData.TryGetLastTickPrice(contract.ContractId, out var price) ||
            price.Quote is not { } quote || price.Trade is not { } trade ||
            quote.BidPrice is not { } bid || quote.AskPrice is not { } ask)
            throw Invalid("The current futures contract, quote, or trade is unavailable.");

        var atr = await storage.MarketDataDb.GetLastFuturesAtrSignalAsync(contract.ContractId,
            price.ValueDate, TimeFrameType.OneMinute, 14, cancellationToken).ConfigureAwait(false);
        if (atr?.Metadata is not { IsValid: true } metadata || atr.AtrValue <= 0d ||
            !double.IsFinite(atr.AtrValue) || !double.IsFinite(atr.TrueRange))
            throw Invalid("The authoritative one-minute ATR observation is unavailable or invalid.");

        return new()
        {
            BidPrice = (double)bid, AskPrice = (double)ask,
            BidSize = quote.BidSize, AskSize = quote.AskSize,
            LastPrice = (double)trade.LastPrice,
            OneMinuteMoveAtr = Math.Abs(atr.TrueRange / atr.AtrValue),
            QuoteObservation = Observation("PrimaryFuturesFeed", quote.EventTimestamp, quote.ReceiveTimestamp,
                quote.SourceSequence),
            TradeObservation = Observation("PrimaryFuturesTrade", trade.EventTimestamp, trade.ReceiveTimestamp,
                Math.Max(trade.SourceSequence, metadata.SourceSequence))
        };
    }

    static MarketConditionCalculationException Invalid(string message) => new(
        MarketConditionFailureCategory.RequiredInputInvalid, MarketConditionReasonCodes.RequiredInput, message);

    internal static MarketSourceObservation Observation(string source, DateTimeOffset timestamp,
        DateTimeOffset received, long sequence) => new()
        {
            SourceId = source,
            SourceTimestampUtc = timestamp.UtcDateTime,
            ReceivedAtUtc = received.UtcDateTime,
            SequenceId = sequence,
            Availability = MarketSourceAvailability.Available,
            Validity = MarketSourceValidity.Valid
        };
}

/// <summary>Joins the Securities option universe to one read of each eligible hot quote.</summary>
public sealed class MarketConditionOptionUniverseAdapter(IMarketDataApi marketData, IDbContextFactory storage)
    : IMarketConditionOptionUniverseAdapter
{
    public async ValueTask<IReadOnlyCollection<MarketConditionRawOptionContract>> ReadOnceAsync(
        string instrumentRoot, decimal futuresUnderlyingPrice,
        MarketConditionOptionLiquidityConfiguration configuration, DateTime evaluationTimestampUtc,
        CancellationToken cancellationToken)
    {
        var date = DateOnly.FromDateTime(evaluationTimestampUtc);
        var definitions = await storage.SecuritiesDb.GetFuturesOptionContractsAsync(instrumentRoot,
            cancellationToken).ConfigureAwait(false);
        var eligible = definitions.Where(x =>
                x.ContractMonth.DayNumber - date.DayNumber >= configuration.MinimumDte &&
                x.ContractMonth.DayNumber - date.DayNumber <= configuration.MaximumDte &&
                Math.Abs((decimal)x.StrikePrice - futuresUnderlyingPrice) / futuresUnderlyingPrice <=
                configuration.MaximumAbsoluteMoneyness)
            .OrderBy(x => x.ContractMonth).ThenBy(x => x.StrikePrice)
            .ThenBy(x => x.OptionType, StringComparer.Ordinal).ThenBy(x => x.ContractId, StringComparer.Ordinal)
            .ToArray();
        if (eligible.Length == 0)
            throw new MarketConditionCalculationException(MarketConditionFailureCategory.RequiredInputInvalid,
                MarketConditionReasonCodes.RequiredInput,
                "The Securities store contains no eligible ES futures-option contracts.");

        return eligible.Select(definition =>
        {
            if (!marketData.TryGetLastOptionTickPrice(definition.ContractId, out var price) ||
                price.Price.Quote is not { } quote || quote.BidPrice is not { } bid ||
                quote.AskPrice is not { } ask)
                return Missing(definition.ContractId, definition.Symbol, definition.ContractMonth,
                    definition.OptionType, definition.StrikePrice);
            return new MarketConditionRawOptionContract
            {
                ContractId = definition.ContractId, InstrumentRoot = definition.Symbol,
                ExpirationDate = definition.ContractMonth, OptionType = definition.OptionType,
                StrikePrice = definition.StrikePrice, BidPrice = (double)bid, AskPrice = (double)ask,
                BidSize = quote.BidSize, AskSize = quote.AskSize,
                UnderlyingPrice = (double)(price.Greeks?.FuturesPrice ?? 0m),
                Observation = MarketConditionFuturesQuoteAdapter.Observation("FuturesOptionFeed",
                    quote.EventTimestamp, quote.ReceiveTimestamp, quote.SourceSequence)
            };
        }).ToArray();
    }

    static MarketConditionRawOptionContract Missing(string id, string root, DateOnly expiration,
        string optionType, double strike) => new()
        {
            ContractId = id, InstrumentRoot = root, ExpirationDate = expiration,
            OptionType = optionType, StrikePrice = strike,
            Observation = new MarketSourceObservation
            {
                SourceId = "FuturesOptionFeed", Availability = MarketSourceAvailability.Unavailable,
                Validity = MarketSourceValidity.Valid
            }
        };
}

/// <summary>Uses the configured CME calendar for holidays, DST, and early closes.</summary>
public sealed class MarketConditionSessionAdapter(IMarketSessionCalendar calendar) : IMarketConditionSessionAdapter
{
    public ValueTask<MarketConditionSessionState> ReadOnceAsync(string instrumentRoot,
        MarketConditionSessionConfiguration configuration, DateTime evaluationTimestampUtc,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var at = new DateTimeOffset(DateTime.SpecifyKind(evaluationTimestampUtc, DateTimeKind.Utc));
        var valueDate = calendar.GetValueDate(at);
        var zone = ResolveTimeZone(configuration.ExchangeTimeZoneId);
        var local = TimeZoneInfo.ConvertTime(at, zone);
        var tradingDate = calendar.IsTradingDate(valueDate);
        var open = false;
        var effectiveEntryEnd = configuration.EntryWindowEnd;
        if (tradingDate)
        {
            var bounds = calendar.GetSession(valueDate);
            open = at >= bounds.StartUtc && at < bounds.EndUtc;
            var localClose = TimeZoneInfo.ConvertTime(bounds.EndUtc, zone).TimeOfDay;
            if (localClose < effectiveEntryEnd) effectiveEntryEnd = localClose;
        }
        var entry = open && configuration.EligibleWeekdays.Contains(local.DayOfWeek) &&
                    local.TimeOfDay >= configuration.EntryWindowStart && local.TimeOfDay <= effectiveEntryEnd;
        return ValueTask.FromResult(new MarketConditionSessionState
        {
            Status = open ? MarketSessionStatus.Open : MarketSessionStatus.Closed,
            IsEntryWindow = entry, ExchangeLocalTime = local.TimeOfDay,
            ExchangeLocalWeekday = local.DayOfWeek, Observation = CheckedNow("SessionCalendar", evaluationTimestampUtc)
        });
    }

    static TimeZoneInfo ResolveTimeZone(string id)
    {
        foreach (var candidate in new[] { id, "America/New_York", "Eastern Standard Time" })
            try { return TimeZoneInfo.FindSystemTimeZoneById(candidate); }
            catch (TimeZoneNotFoundException) { }
        throw new MarketConditionCalculationException(MarketConditionFailureCategory.ConfigurationUnavailable,
            MarketConditionReasonCodes.Configuration, "The configured exchange timezone is unavailable.");
    }

    internal static MarketSourceObservation CheckedNow(string source, DateTime at) => new()
    {
        SourceId = source, SourceTimestampUtc = at, ReceivedAtUtc = at, SequenceId = at.Ticks,
        Availability = MarketSourceAvailability.Available, Validity = MarketSourceValidity.Valid
    };
}

/// <summary>Classifies authoritative US economic-calendar rows into the V1 typed risk categories.</summary>
public sealed class MarketConditionEventRiskAdapter(IDbContextFactory storage) : IMarketConditionEventRiskAdapter
{
    public async ValueTask<MarketConditionEventRiskState> ReadOnceAsync(
        MarketConditionEventRiskConfiguration configuration, DateTime evaluationTimestampUtc,
        CancellationToken cancellationToken)
    {
        if (configuration.RequiredEventCategories.Any(x => x is not ("HighImpact" or "RateDecision")))
            throw new MarketConditionCalculationException(MarketConditionFailureCategory.ConfigurationUnavailable,
                MarketConditionReasonCodes.Configuration, "An unsupported required event-risk category was configured.");
        var before = Math.Max(configuration.HighImpactBeforeMinutes, configuration.RateDecisionBeforeMinutes);
        var after = Math.Max(configuration.HighImpactAfterMinutes, configuration.RateDecisionAfterMinutes);
        var rows = await storage.MarketDataDb.GetEconomicCalendarsAsync(
            evaluationTimestampUtc.AddMinutes(-after), evaluationTimestampUtc.AddMinutes(before), "US",
            cancellationToken).ConfigureAwait(false);
        if (rows.Any(x => !x.IsValid || string.IsNullOrWhiteSpace(x.EventName)))
            throw new MarketConditionCalculationException(MarketConditionFailureCategory.RequiredInputInvalid,
                MarketConditionReasonCodes.RequiredInput, "Economic-calendar event identity is invalid.");

        var active = rows.Select(x => new
            {
                Value = x,
                Category = IsRateDecision(x.EventName) ? "RateDecision" :
                    string.Equals(x.Impact, "High", StringComparison.OrdinalIgnoreCase) ? "HighImpact" : string.Empty
            })
            .Where(x => configuration.RequiredEventCategories.Contains(x.Category, StringComparer.Ordinal))
            .Where(x => InWindow(x.Value.EventDate, x.Category, configuration, evaluationTimestampUtc))
            .OrderBy(x => Math.Abs((x.Value.EventDate - evaluationTimestampUtc).Ticks))
            .ThenBy(x => x.Value.EventDate).ThenBy(x => x.Value.EventName, StringComparer.Ordinal)
            .FirstOrDefault();
        return new MarketConditionEventRiskState
        {
            Status = active is null ? MarketEventRiskStatus.Clear : MarketEventRiskStatus.Blocked,
            EventId = active is null ? string.Empty : $"US|{active.Value.EventDate:O}|{active.Value.EventName}",
            Category = active?.Category ?? string.Empty,
            Observation = MarketConditionSessionAdapter.CheckedNow("EventRiskCalendar", evaluationTimestampUtc)
        };
    }

    static bool IsRateDecision(string name) =>
        name.Contains("rate decision", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("FOMC", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("Federal Reserve", StringComparison.OrdinalIgnoreCase);

    static bool InWindow(DateTime eventAt, string category, MarketConditionEventRiskConfiguration c, DateTime at)
    {
        var before = category == "RateDecision" ? c.RateDecisionBeforeMinutes : c.HighImpactBeforeMinutes;
        var after = category == "RateDecision" ? c.RateDecisionAfterMinutes : c.HighImpactAfterMinutes;
        return at >= eventAt.AddMinutes(-before) && at <= eventAt.AddMinutes(after);
    }
}

/// <summary>Computes the exact five-minute VX relative increase from live price and persisted minute history.</summary>
public sealed class MarketConditionVolatilityAdapter(IMarketDataApi marketData, IDbContextFactory storage)
    : IMarketConditionVolatilityAdapter
{
    public async ValueTask<MarketConditionVolatilityShockState> ReadOnceAsync(string instrumentRoot,
        DateTime evaluationTimestampUtc, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!marketData.TryGetOnTheRunFuturesContract("VX", out var contract) ||
            !marketData.TryGetLastTickPrice(contract.ContractId, out var current) || current.Trade is not { } trade)
            throw Invalid("The current VX futures trade is unavailable.");
        var target = evaluationTimestampUtc.AddMinutes(-5);
        var bars = await storage.MarketDataDb.GetFuturesBarDataAsync(contract.ContractId, "VX", current.ValueDate,
            target.AddMinutes(-5), target).ConfigureAwait(false);
        var baseline = bars.Where(x => x.BarDate <= target && x.BarValue > 0m)
            .OrderByDescending(x => x.BarDate).FirstOrDefault();
        if (baseline is null)
            throw Invalid("The five-minute VX baseline is unavailable.");
        return new MarketConditionVolatilityShockState
        {
            FiveMinuteRelativeIncrease = (trade.LastPrice - baseline.BarValue) / baseline.BarValue,
            Observation = MarketConditionFuturesQuoteAdapter.Observation("VolatilityVX",
                trade.EventTimestamp, trade.ReceiveTimestamp, trade.SourceSequence)
        };
    }

    static MarketConditionCalculationException Invalid(string message) => new(
        MarketConditionFailureCategory.RequiredInputInvalid, MarketConditionReasonCodes.RequiredInput, message);
}

/// <summary>Typed replacement point for the future IBKR connection/session authority.</summary>
public interface IMarketConditionBrokerReadiness
{
    MarketConditionBrokerReadinessSnapshot Read(DateTime evaluationTimestampUtc);
}

public readonly record struct MarketConditionBrokerReadinessSnapshot(
    MarketOperationalStatus Status, DateTime ObservedAtUtc, long SequenceId);

/// <summary>Fail-closed default until the IBKR service publishes a concrete readiness source.</summary>
public sealed class UnavailableMarketConditionBrokerReadiness : IMarketConditionBrokerReadiness
{
    public MarketConditionBrokerReadinessSnapshot Read(DateTime evaluationTimestampUtc) =>
        new(MarketOperationalStatus.Unavailable, evaluationTimestampUtc, evaluationTimestampUtc.Ticks);
}

/// <summary>Maps exact feed/cache runtime state and broker readiness to required V1 health IDs.</summary>
public sealed class MarketConditionOperationalHealthAdapter(
    DatabentoMarketDataApi marketData, IMarketConditionBrokerReadiness broker)
    : IMarketConditionOperationalHealthAdapter
{
    public ValueTask<IReadOnlyCollection<MarketConditionOperationalHealthItem>> ReadOnceAsync(
        IReadOnlyCollection<string> requiredSources, DateTime evaluationTimestampUtc,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var health = marketData.GetHealth();
        var brokerHealth = broker.Read(evaluationTimestampUtc);
        IReadOnlyCollection<MarketConditionOperationalHealthItem> result = requiredSources
            .Order(StringComparer.Ordinal).Select(source => source switch
            {
                "PrimaryFuturesFeed" => Item(source,
                    health.Running && health.Epoch is { Running: true, ProcessingFailures: 0 }
                        ? MarketOperationalStatus.Healthy : MarketOperationalStatus.Unavailable,
                    evaluationTimestampUtc, Sequence(health)),
                "FuturesOptionFeed" => Item(source,
                    health.Running && health.Epoch is { Running: true, ProcessingFailures: 0 }
                        ? MarketOperationalStatus.Healthy : MarketOperationalStatus.Unavailable,
                    evaluationTimestampUtc, Sequence(health)),
                "LatestValueCache" => Item(source,
                    health.Epoch is { LastPriceStoreActive: true, LastPriceSlots: > 0 }
                        ? MarketOperationalStatus.Healthy : MarketOperationalStatus.Unavailable,
                    evaluationTimestampUtc, Sequence(health)),
                "IbkrSession" => Item(source, brokerHealth.Status, brokerHealth.ObservedAtUtc,
                    brokerHealth.SequenceId),
                _ => throw new MarketConditionCalculationException(
                    MarketConditionFailureCategory.ConfigurationUnavailable, MarketConditionReasonCodes.Configuration,
                    $"Required operational health source '{source}' has no registered authority.")
            }).ToArray();
        return ValueTask.FromResult(result);
    }

    static long Sequence(DatabentoMarketDataApiHealth value) => value.Epoch is { } epoch
        ? Math.Max(epoch.SourceQuoteRecords, epoch.SourceTradeRecords) : 0;

    static MarketConditionOperationalHealthItem Item(string source, MarketOperationalStatus status,
        DateTime at, long sequence) => new()
        {
            SourceId = source, Status = status,
            Observation = new MarketSourceObservation
            {
                SourceId = source, SourceTimestampUtc = at, ReceivedAtUtc = at, SequenceId = sequence,
                Availability = MarketSourceAvailability.Available, Validity = MarketSourceValidity.Valid
            }
        };
}
