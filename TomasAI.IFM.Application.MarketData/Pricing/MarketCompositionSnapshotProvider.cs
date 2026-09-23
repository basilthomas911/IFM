using System.Collections.Immutable;
using MessagePack;
using TomasAI.IFM.Framework.MarketData.Contracts.Pricing;
using TomasAI.IFM.Framework.Serialization;

namespace TomasAI.IFM.Application.MarketData.Pricing;

[MessagePackObject]
public sealed record CompositionSnapshotRequest([property: Key(0)] Guid SnapshotId, [property: Key(1)] string ScopeId, [property: Key(2)] string Horizon,
    [property: Key(3)] Guid GenerationId, [property: Key(4)] DateTimeOffset EvaluatedAtUtc, [property: Key(5)] DateTimeOffset DeadlineUtc,
    [property: Key(6)] bool IncludeOptions, [property: Key(7)] int MaximumContracts = 512, [property: Key(8)] int MaximumQuoteAgeMilliseconds = 1000,
    [property: Key(9)] int MaximumQuoteSkewMilliseconds = 250,
    ImmutableArray<CompositionFutureDefinition> Futures = default)
{
    [Key(10)] public ImmutableArray<CompositionFutureDefinition> Futures { get; init; } = Futures.IsDefault ? [] : Futures;
    [Key(11)] public bool SelectionOnly { get; init; }
    [Key(12)] public bool AllowMissingOptionQuotes { get; init; }
}

/// <summary>One atomic source view; scope token and generation must remain stable across all pages.</summary>
public sealed record CompositionMarketPage(string ScopeToken, Guid GenerationId, int TotalContracts,
    ImmutableArray<CompositionMarketInstrument> Instruments, string? Continuation);

[MessagePackObject]
public sealed record CompositionFutureDefinition(
    [property: Key(0)] string ContractId, [property: Key(1)] string Root,
    [property: Key(2)] string Dataset, [property: Key(3)] string Exchange,
    [property: Key(4)] string Currency, [property: Key(5)] DateTimeOffset LastTradingUtc,
    [property: Key(6)] decimal Multiplier, [property: Key(7)] decimal TickSize,
    [property: Key(8)] string DefinitionDigest);

[MessagePackObject]
public sealed record CompositionMarketInstrument(
    [property: Key(0)] string ContractId,
    [property: Key(1)] OptionPricingQuote? Quote,
    [property: Key(2)] OptionPricingContext? Pricing,
    [property: Key(3)] decimal? Strike,
    [property: Key(4)] bool? IsCall,
    [property: Key(5)] OptionPricingQuote? Underlying,
    [property: Key(6)] CompositionFutureDefinition? FutureDefinition = null)
{
    [Key(7)]
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public OptionSelectionValue? Selection { get; init; }
    [Key(8)] public long? SessionVolume { get; init; }
    [Key(9)] public long? OpenInterest { get; init; }
    [Key(10)] public DateTimeOffset? StatisticsAtUtc { get; init; }
}

/// <summary>Source owns complete-scope enumeration and current-generation price observations.</summary>
public interface ICompositionMarketSource
{
    Task<CompositionMarketPage> ReadAsync(CompositionSnapshotRequest request, string? continuation, CancellationToken cancellationToken);
}

[MessagePackObject]
public sealed record CompositionInstrumentSnapshot(
    [property: Key(0)] CompositionMarketInstrument Instrument,
    [property: Key(1)] OptionPricingValue? Valuation);

[MessagePackObject]
public sealed record MarketCompositionSnapshot(
    [property: Key(0)] int SchemaVersion,
    [property: Key(1)] Guid SnapshotId,
    [property: Key(2)] string ScopeId,
    [property: Key(3)] string ScopeToken,
    [property: Key(4)] string Horizon,
    [property: Key(5)] Guid GenerationId,
    [property: Key(6)] DateTimeOffset EvaluatedAtUtc,
    [property: Key(7)] DateTimeOffset ValidUntilUtc,
    [property: Key(8)] ImmutableArray<CompositionInstrumentSnapshot> Instruments,
    [property: Key(9)] string Digest);

[MessagePackObject]
public sealed record CompositionSnapshotResult([property: Key(0)] MarketCompositionSnapshot? Snapshot, [property: Key(1)] OptionPricingFailure? Failure);

public interface IMarketCompositionSnapshotProvider
{
    Task<CompositionSnapshotResult> CaptureAsync(CompositionSnapshotRequest request, CancellationToken cancellationToken);
}

/// <summary>Captures all pages and values the bounded scope at one frozen instant; never selects a strategy.</summary>
public sealed class MarketCompositionSnapshotProvider(ICompositionMarketSource source, TimeProvider? time = null) : IMarketCompositionSnapshotProvider
{
    readonly TimeProvider clock = time ?? TimeProvider.System;

    public async Task<CompositionSnapshotResult> CaptureAsync(CompositionSnapshotRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        CompositionSnapshotResult Fail(string code, string input = "Snapshot") => new(null, new(code, input, "", "Complete coherent composition evidence is unavailable."));
        if (request.SnapshotId == Guid.Empty || string.IsNullOrWhiteSpace(request.ScopeId) || request.ScopeId.Length > 128
            || request.GenerationId == Guid.Empty || request.Horizon is not ("Daily" or "Weekly" or "Monthly")
            || request.MaximumContracts is < 1 or > 512 || request.MaximumQuoteAgeMilliseconds is < 1 or > 5000
            || request.MaximumQuoteSkewMilliseconds is < 0 or > 2000
            || request.EvaluatedAtUtc.Offset != TimeSpan.Zero || request.DeadlineUtc.Offset != TimeSpan.Zero
            || request.EvaluatedAtUtc > clock.GetUtcNow() || request.DeadlineUtc <= clock.GetUtcNow()) return Fail("InvalidSnapshotRequest");
        var wait = request.DeadlineUtc - clock.GetUtcNow();
        if (wait > TimeSpan.FromSeconds(10)) wait = TimeSpan.FromSeconds(10);
        using var timeout = new CancellationTokenSource(wait, clock);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var continuations = new HashSet<string>(StringComparer.Ordinal);
        var values = ImmutableArray.CreateBuilder<CompositionInstrumentSnapshot>();
        string? cursor = null, scopeToken = null;
        int? total = null;
        DateTimeOffset validUntil = request.DeadlineUtc;
        int maximumSkew = request.MaximumQuoteSkewMilliseconds;
        var quoteTimes = new List<DateTimeOffset>();
        try
        {
            do
            {
                linked.Token.ThrowIfCancellationRequested();
                var page = await source.ReadAsync(request, cursor, linked.Token).ConfigureAwait(false);
                if (page.GenerationId != request.GenerationId) return Fail("Recovering");
                if (string.IsNullOrWhiteSpace(page.ScopeToken) || page.ScopeToken.Length > 128
                    || scopeToken is not null && scopeToken != page.ScopeToken
                    || total is not null && total != page.TotalContracts) return Fail("IncompleteChain");
                scopeToken = page.ScopeToken; total = page.TotalContracts;
                if (total < 0 || total > request.MaximumContracts || page.Instruments.IsDefault
                    || page.Instruments.Length > request.MaximumContracts || values.Count + page.Instruments.Length > total)
                    return Fail("SnapshotLimit");
                foreach (var instrument in page.Instruments)
                {
                    if (!seen.Add(instrument.ContractId) || instrument.Quote is { } observedQuote && observedQuote.ContractId != instrument.ContractId) return Fail("ConflictingDefinition");
                    if (instrument.Quote is null)
                    {
                        if (!request.AllowMissingOptionQuotes || !request.IncludeOptions || request.SelectionOnly
                            || instrument.Pricing is null || instrument.Strike is null || instrument.IsCall is null)
                            return Fail("QuoteUnavailable", instrument.ContractId);
                        values.Add(new(instrument with { Selection = null }, null));
                        continue;
                    }
                    if (request.AllowMissingOptionQuotes && request.IncludeOptions && !request.SelectionOnly
                        && instrument.Pricing is { } optionContext && instrument.Strike is { } optionStrike
                        && instrument.IsCall is { } optionIsCall)
                    {
                        var liveQuote = instrument.Quote;
                        var quoteUsable = liveQuote.Bid > 0 && liveQuote.Ask >= liveQuote.Bid
                            && liveQuote.BidSize >= 0 && liveQuote.AskSize >= 0
                            && liveQuote.GenerationId == request.GenerationId
                            && liveQuote.EventAtUtc <= request.EvaluatedAtUtc
                            && liveQuote.ReceivedAtUtc <= request.EvaluatedAtUtc
                            && (request.EvaluatedAtUtc - liveQuote.EventAtUtc).TotalMilliseconds
                                <= Math.Min(optionContext.MaximumQuoteAgeMilliseconds, request.MaximumQuoteAgeMilliseconds);
                        if (!quoteUsable)
                        {
                            values.Add(new(instrument with { Quote = null, Selection = null }, null));
                            continue;
                        }
                        var priced = instrument.Underlying is null ? null : Black76PricingModel.Calculate(
                            optionContext, instrument.Underlying, liveQuote, optionStrike, optionIsCall, request.EvaluatedAtUtc).Value;
                        values.Add(new(instrument with { Selection = null }, priced));
                        continue;
                    }
                    OptionPricingValue? value = null;
                    var effectiveInstrument = instrument;
                    int maximumAge = request.MaximumQuoteAgeMilliseconds;
                    if (instrument.Pricing is { } context)
                    {
                        if (!request.IncludeOptions || instrument.Strike is null || instrument.IsCall is null || instrument.Underlying is null)
                            return Fail("ContractMetadataUnavailable");
                        context = context with
                        {
                            MaximumQuoteAgeMilliseconds = Math.Min(context.MaximumQuoteAgeMilliseconds, request.MaximumQuoteAgeMilliseconds),
                            MaximumQuoteSkewMilliseconds = Math.Min(context.MaximumQuoteSkewMilliseconds, request.MaximumQuoteSkewMilliseconds)
                        };
                        maximumAge = context.MaximumQuoteAgeMilliseconds;
                        maximumSkew = Math.Min(maximumSkew, context.MaximumQuoteSkewMilliseconds);
                        effectiveInstrument = instrument with { Pricing = context };
                        if (request.SelectionOnly)
                        {
                            var invalid = Black76PricingModel.ValidateInputs(context, instrument.Underlying, instrument.Quote,
                                instrument.Strike.Value, instrument.IsCall.Value, request.EvaluatedAtUtc);
                            if (invalid is not null) return new(null, invalid);
                            var selection = instrument.Selection;
                            if (selection is null || selection.ContextDigest != PricingSemanticHash.Compute(instrument.Pricing)
                                || !double.IsFinite(selection.Price) || !double.IsFinite(selection.Delta) || !double.IsFinite(selection.ImpliedVolatility)
                                || selection.CalculatedAtUtc > request.EvaluatedAtUtc || selection.ValidUntilUtc <= request.EvaluatedAtUtc
                                || selection.IvOption.GenerationId != request.GenerationId || selection.IvUnderlying.GenerationId != request.GenerationId)
                                return Fail("SelectionCalculationUnavailable");
                            validUntil = Min(validUntil, selection.ValidUntilUtc);
                            // Preserve the exact context that qualifies the cached IV; request limits are checked separately.
                            effectiveInstrument = instrument;
                        }
                        else
                        {
                            var priced = Black76PricingModel.Calculate(context, instrument.Underlying, instrument.Quote,
                                instrument.Strike.Value, instrument.IsCall.Value, request.EvaluatedAtUtc);
                            if (priced.Failure is not null) return new(null, priced.Failure);
                            value = priced.Value;
                            effectiveInstrument = effectiveInstrument with { Selection = null };
                        }
                        validUntil = Min(validUntil, context.ValidUntilUtc);
                    }
                    else
                    {
                        if (request.IncludeOptions || instrument.Strike is not null || instrument.IsCall is not null)
                            return Fail("PricingContextUnavailable");
                        var future = instrument.FutureDefinition;
                        if (future is null || future.ContractId != instrument.ContractId || future.Root != "ES"
                            || future.Dataset != "GLBX.MDP3" || future.Currency != "USD" || string.IsNullOrWhiteSpace(future.Exchange)
                            || future.LastTradingUtc.Offset != TimeSpan.Zero || future.LastTradingUtc <= request.EvaluatedAtUtc
                            || future.Multiplier <= 0 || future.TickSize <= 0 || future.DefinitionDigest is not { Length: 64 })
                            return Fail("ContractMetadataUnavailable");
                        validUntil = Min(validUntil, future.LastTradingUtc);
                    }
                    // The outright futures branch never requests Treasury, option definitions, IV or Greeks.
                    foreach (var quote in instrument.Underlying is null ? new[] { instrument.Quote } : new[] { instrument.Quote, instrument.Underlying })
                    {
                        if (quote.GenerationId != request.GenerationId) return Fail("Recovering");
                        if (quote.Bid <= 0 || quote.Ask < quote.Bid || quote.BidSize < 0 || quote.AskSize < 0
                            || quote.EventAtUtc.Offset != TimeSpan.Zero || quote.ReceivedAtUtc.Offset != TimeSpan.Zero
                            || quote.EventAtUtc > request.EvaluatedAtUtc || quote.ReceivedAtUtc > request.EvaluatedAtUtc)
                            return Fail("InvalidQuote", quote.ContractId);
                        var expiry = quote.EventAtUtc.AddMilliseconds(maximumAge);
                        if (expiry < request.EvaluatedAtUtc) return Fail("StaleData", quote.ContractId);
                        validUntil = Min(validUntil, expiry);
                        quoteTimes.Add(quote.EventAtUtc);
                    }
                    values.Add(new(effectiveInstrument, value));
                }
                cursor = page.Continuation;
                if (cursor is not null && (cursor.Length is < 1 or > 2048 || page.Instruments.IsEmpty || !continuations.Add(cursor)))
                    return Fail("IncompleteChain");
            } while (cursor is not null);
            if (values.Count != total) return Fail("IncompleteChain");
            if (quoteTimes.Count > 0 && (quoteTimes.Max() - quoteTimes.Min()).TotalMilliseconds > maximumSkew)
                return Fail("IncoherentQuotes");
            var snapshot = new MarketCompositionSnapshot(request.SelectionOnly ? 2 : 1, request.SnapshotId, request.ScopeId, scopeToken!, request.Horizon,
                request.GenerationId, request.EvaluatedAtUtc, validUntil, values.OrderBy(x => x.Instrument.ContractId, StringComparer.Ordinal).ToImmutableArray(), "");
            // Canonical order is independent of page/enumeration order. Only semantic inputs enter this hash.
            snapshot = snapshot with { Digest = PricingSemanticHash.Compute(snapshot) };
            if (MessagePackBinarySerializer.MeasureContent(snapshot) > 524288) return Fail("SnapshotLimit");
            linked.Token.ThrowIfCancellationRequested();
            if (clock.GetUtcNow() >= validUntil) return Fail("StaleData");
            return new(snapshot, null);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return Fail("SnapshotTimeout"); }
        catch (CompositionMarketSourceException exception) { return Fail(exception.Code); }
    }

    static DateTimeOffset Min(DateTimeOffset x, DateTimeOffset y) => x < y ? x : y;
}
