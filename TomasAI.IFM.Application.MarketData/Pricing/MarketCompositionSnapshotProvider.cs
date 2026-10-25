using System.Collections.Immutable;
using MessagePack;
using TomasAI.IFM.Shared.MarketData.Contracts.Pricing;
using TomasAI.IFM.Framework.Serialization;

namespace TomasAI.IFM.Application.MarketData.Pricing;

/// <summary>Captures all pages and values the bounded scope at one frozen instant; never selects a strategy.</summary>
public sealed class MarketCompositionSnapshotProvider(ICompositionMarketSource source, TimeProvider? time = null) : IMarketCompositionSnapshotProvider
{
    readonly TimeProvider clock = time ?? TimeProvider.System;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051", Justification = "Keep native resource ownership and recovery failure ordering together during this storage compatibility refactor.")]
    public async Task<CompositionSnapshotResult> CaptureAsync(CompositionSnapshotRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        CompositionSnapshotResult Fail(string code, string input = "Snapshot") => new(null, new(code, input, "", "Complete coherent composition evidence is unavailable."));
        if (request.SnapshotId == Guid.Empty || string.IsNullOrWhiteSpace(request.ScopeId) || request.ScopeId.Length > 128
            || request.GenerationId == Guid.Empty || request.Horizon is not ("Daily" or "Weekly" or "Monthly")
            || request.MaximumContracts is < 1 or > 2048 || request.MaximumQuoteAgeMilliseconds is < 1 or > 5000
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
                    || scopeToken is not null && !string.Equals(scopeToken,page.ScopeToken,StringComparison.Ordinal)
                    || total is not null && total != page.TotalContracts) return Fail("IncompleteChain");
                scopeToken = page.ScopeToken; total = page.TotalContracts;
                if (total < 0 || total > request.MaximumContracts || page.Instruments.IsDefault
                    || page.Instruments.Length > request.MaximumContracts || values.Count + page.Instruments.Length > total)
                    return Fail("SnapshotLimit");
                foreach (var instrument in page.Instruments)
                {
                    if (!seen.Add(instrument.ContractId) || instrument.Quote is { } observedQuote && !string.Equals(observedQuote.ContractId,instrument.ContractId,StringComparison.Ordinal)) return Fail("ConflictingDefinition");
                    if (instrument.Quote is null)
                    {
                        if (!request.AllowMissingOptionQuotes || !request.IncludeOptions
                            || instrument.Pricing is null || instrument.Strike is null || instrument.IsCall is null)
                            return Fail("QuoteUnavailable", instrument.ContractId);
                        values.Add(new(instrument with { Selection = null }, null));
                        continue;
                    }
                    if (request.AllowMissingOptionQuotes && request.IncludeOptions
                        && instrument.Pricing is { } optionContext && instrument.Strike is { } optionStrike
                        && instrument.IsCall is { } optionIsCall)
                    {
                        var liveQuote = instrument.Quote;
                        var quoteUsable = liveQuote.Bid > 0 && liveQuote.Ask >= liveQuote.Bid
                            && liveQuote.BidSize >= 0 && liveQuote.AskSize >= 0
                            && liveQuote.GenerationId == request.GenerationId
                            && liveQuote.EventAtUtc <= request.EvaluatedAtUtc.AddMilliseconds(optionContext.MaximumSourceClockLeadMilliseconds)
                            && liveQuote.ReceivedAtUtc <= request.EvaluatedAtUtc
                            && (request.EvaluatedAtUtc - liveQuote.EventAtUtc).TotalMilliseconds
                                <= Math.Min(optionContext.MaximumQuoteAgeMilliseconds, request.MaximumQuoteAgeMilliseconds);
                        if (!quoteUsable)
                        {
                            var reason = (liveQuote.GenerationId != request.GenerationId) switch { true => "QuoteGenerationMismatch", false => (liveQuote.Bid <= 0 || liveQuote.Ask < liveQuote.Bid || liveQuote.BidSize < 0 || liveQuote.AskSize < 0) switch { true => "InvalidQuote", false => (liveQuote.ReceivedAtUtc > request.EvaluatedAtUtc || liveQuote.EventAtUtc > request.EvaluatedAtUtc.AddMilliseconds(optionContext.MaximumSourceClockLeadMilliseconds)) switch { true => "QuoteClockLead", false => "QuoteAgeExceeded" } } };
                            values.Add(new(instrument with { Quote = null, Selection = null,
                                QuoteUnavailableReason = reason, LastQuoteEventAtUtc = liveQuote.EventAtUtc,
                                LastQuoteReceivedAtUtc = liveQuote.ReceivedAtUtc }, null));
                            continue;
                        }
                        if (request.SelectionOnly && !request.RiskContractIds.Contains(instrument.ContractId, StringComparer.Ordinal))
                        {
                            var limited = optionContext with
                            {
                                MaximumQuoteAgeMilliseconds = Math.Min(optionContext.MaximumQuoteAgeMilliseconds, request.MaximumQuoteAgeMilliseconds),
                                MaximumQuoteSkewMilliseconds = Math.Min(optionContext.MaximumQuoteSkewMilliseconds, request.MaximumQuoteSkewMilliseconds)
                            };
                            var selection = instrument.Selection;
                            var valid = instrument.Underlying is not null
                                && Black76PricingModel.ValidateInputs(limited, instrument.Underlying, liveQuote, optionStrike, optionIsCall, request.EvaluatedAtUtc) is null
                                && selection is not null && string.Equals(selection.ContextDigest,PricingSemanticHash.Compute(optionContext),StringComparison.Ordinal)
                                && double.IsFinite(selection.Price) && double.IsFinite(selection.Delta) && double.IsFinite(selection.ImpliedVolatility)
                                && selection.CalculatedAtUtc <= request.EvaluatedAtUtc && selection.ValidUntilUtc > request.EvaluatedAtUtc
                                && selection.IvOption.GenerationId == request.GenerationId && selection.IvUnderlying.GenerationId == request.GenerationId;
                            values.Add(new(instrument with { Selection = valid ? selection : null }, null));
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
                            if (selection is null || !string.Equals(selection.ContextDigest,PricingSemanticHash.Compute(instrument.Pricing),StringComparison.Ordinal)
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
                        if (future is null || !string.Equals(future.ContractId,instrument.ContractId,StringComparison.Ordinal) || !string.Equals(future.Root,"ES",StringComparison.Ordinal)
                            || !string.Equals(future.Dataset,"GLBX.MDP3",StringComparison.Ordinal) || !string.Equals(future.Currency,"USD",StringComparison.Ordinal) || string.IsNullOrWhiteSpace(future.Exchange)
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
                            || quote.EventAtUtc > request.EvaluatedAtUtc.AddMilliseconds(effectiveInstrument.Pricing?.MaximumSourceClockLeadMilliseconds ?? 0)
                            || quote.ReceivedAtUtc > request.EvaluatedAtUtc)
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
            if (MessagePackBinarySerializer.MeasureContent(snapshot) > 4 * 1024 * 1024) return Fail("SnapshotLimit");
            linked.Token.ThrowIfCancellationRequested();
            if (clock.GetUtcNow() >= validUntil) return Fail("StaleData");
            return new(snapshot, null);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return Fail("SnapshotTimeout"); }
        catch (CompositionMarketSourceException exception) { return Fail(exception.Code); }
    }

    static DateTimeOffset Min(DateTimeOffset x, DateTimeOffset y) => x < y ? x : y;
}
