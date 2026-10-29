using System.Diagnostics;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Application.MarketData.Rust.Delivery;
using TomasAI.IFM.Shared.MarketData.Contracts.LastPrice;
using TomasAI.IFM.Shared.MarketData.Contracts.Pricing;
using TomasAI.IFM.Shared.MarketData.Pricing;
namespace TomasAI.IFM.Application.MarketData.Rust.Api;

/// <summary>Independent actual-trade IV/Greek attempts using the native solver.
/// Quote-cache IV is never substituted. Application evidence persistence and
/// semantic identities retain their existing contracts.</summary>
public sealed class RustOptionTradePricing(NativeMarketDataRuntime runtime)
{
    readonly RustOptionPricing pricing = new(runtime);
    /// <summary>Matches the current trade-price result and failure precedence.
    /// One original caller budget covers native qualification and calculation.</summary>
    public async Task<OptionGreeksSnapshot> CalculateAsync(OptionPricingContext context, OptionPricingQuote? underlying,
        LastTradeTickSnapshot trade, decimal strike, bool call, DateTimeOffset calculatedAt, TimeSpan remainingBudget,
        CancellationToken cancellationToken = default, DateTimeOffset? qualifiedUntilUtc = null)
    {
        var started = Stopwatch.GetTimestamp(); ArgumentNullException.ThrowIfNull(context); cancellationToken.ThrowIfCancellationRequested();
        if (remainingBudget < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(remainingBudget));
        OptionGreeksSnapshot Failed(OptionPricingFailure failure) => new()
        {
            IsValid = false, PriceSource = OptionGreeksPriceSource.Trade, FailureReason = OptionGreeksFailureReason.PricingContextUnavailable,
            PricingFailure = failure, FuturesContractId = context.Contract.UnderlyingContractId, OptionMarkPrice = trade.Price,
            OptionPriceSourceSequence = trade.SourceSequence, OptionPriceTimestamp = trade.EventTimestamp, CalculatedAtUtc = calculatedAt
        };
        if (underlying is null) return Failed(new("UnderlyingQuoteUnavailable", "Underlying", trade.ContractId, "Trade retained without a qualified underlying quote."));
        if (trade.Size == 0 || trade.ValueDate == default) return Failed(new("InvalidTrade", "Trade", trade.ContractId, "Trade size and value date are required."));
        var observation = new OptionPricingQuote(trade.ContractId, trade.Price, trade.Price, trade.Size, trade.Size,
            trade.EventTimestamp, trade.ReceiveTimestamp, trade.SourceSequence, context.GenerationId);
        // A prepared reference may end at an earlier calendar/tenor boundary.
        // Preserve the existing validation precedence, then fence that extra
        // immutable window without changing the context or its semantic hash.
        if (qualifiedUntilUtc is { } end && calculatedAt >= end)
            return Failed(runtime.ValidateOptionInputs(context, underlying, observation, strike, call, calculatedAt)
                ?? new("StaleData", "OptionPricing", trade.ContractId, "Source-time reference qualification has expired."));
        var result = await pricing.CalculateAsync(context, underlying, observation, strike, call, calculatedAt,
            Remaining(remainingBudget, started), cancellationToken).ConfigureAwait(false);
        if (result.Failure is { } failure) return Failed(failure);
        var g = result.Value!;
        return new()
        {
            IsValid = true, PriceSource = OptionGreeksPriceSource.Trade, FuturesContractId = context.Contract.UnderlyingContractId,
            FuturesPrice = underlying.Bid / 2m + underlying.Ask / 2m, OptionMarkPrice = trade.Price, RiskFreeRate = context.Rate.AnnualContinuousRate,
            TimeToExpiryYears = g.TimeToExpiry, ImpliedVolatility = g.ImpliedVolatility, TheoreticalPrice = g.TheoreticalPrice,
            Delta = g.Delta, Gamma = g.Gamma, Vega = g.Vega, Theta = g.Theta, Rho = g.Rho,
            FuturesPriceSourceSequence = underlying.Sequence, OptionPriceSourceSequence = trade.SourceSequence,
            FuturesPriceTimestamp = underlying.EventAtUtc, OptionPriceTimestamp = trade.EventTimestamp, CalculatedAtUtc = calculatedAt,
            PricingContextDigest = PricingSemanticHash.Compute(new { Version = 1, Basis = "Trade", trade, context, underlying, calculatedAt, g.ContextDigest })
        };
    }
    /// <summary>Retains the exact source nanoseconds and first complete pricing
    /// attempt before returning. Writer timeout/failure escapes to recovery;
    /// neither a transient enqueue nor an unacknowledged write is success.</summary>
    public async Task<OptionGreeksSnapshot> CalculateAndRetainAsync(OptionTradeSource source, string underlyingContractId,
        OptionPricingContext? context, OptionPricingQuote? underlying, decimal strike, bool call, DateTimeOffset calculatedAt,
        IOptionTradeEvidenceWriter writer, TimeSpan remainingBudget, CancellationToken cancellationToken = default,
        DateTimeOffset? qualifiedUntilUtc = null)
    {
        var started = Stopwatch.GetTimestamp(); ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(writer);
        cancellationToken.ThrowIfCancellationRequested(); if (remainingBudget < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(remainingBudget));
        var trade = new LastTradeTickSnapshot(source.ContractId, source.ValueDate, source.Price, source.Size, source.Sequence,
            DateTimeOffset.UnixEpoch.AddTicks(source.EventNanoseconds / 100), DateTimeOffset.UnixEpoch.AddTicks(source.ReceiveNanoseconds / 100));
        if (context is not null && (context.GenerationId != source.GenerationId || context.Contract.Dataset != source.Dataset
            || context.Contract.PublisherId != source.PublisherId || context.Contract.InstrumentId != source.InstrumentId
            || context.Contract.RawSymbol != source.RawSymbol || context.Contract.ContractId != source.ContractId
            || context.Contract.UnderlyingContractId != underlyingContractId)) context = null;
        if (context is null) underlying = null;
        OptionGreeksSnapshot Pending(string code) => new()
        {
            IsValid = false, PriceSource = OptionGreeksPriceSource.Trade, FuturesContractId = underlyingContractId, CalculatedAtUtc = calculatedAt,
            FailureReason = OptionGreeksFailureReason.PricingContextUnavailable,
            PricingFailure = new(code, "OptionPricing", source.ContractId, "Qualified calculation is unavailable.")
        };
        Exception? unexpected = null; OptionGreeksSnapshot result;
        try { result = context is null ? Pending("PricingContextUnavailable") : await CalculateAsync(context, underlying, trade, strike, call,
            calculatedAt, Remaining(remainingBudget, started), cancellationToken, qualifiedUntilUtc).ConfigureAwait(false); }
        catch (Exception error) when (error is ArgumentException or ArithmeticException) { unexpected = error; result = Pending("TradePricingFailed"); }
        var evidence = new OptionTradeEvidence(source, context, underlying, calculatedAt, result.IsValid ? new(result.ImpliedVolatility!.Value,
            result.Delta!.Value, result.Gamma!.Value, result.Theta!.Value, result.Vega!.Value, result.Rho!.Value, result.TheoreticalPrice!.Value,
            result.TimeToExpiryYears!.Value, result.PricingContextDigest!) : null,
            result.IsValid ? null : result.PricingFailure ?? Pending("TradePricingFailed").PricingFailure);
        evidence.Validate();
        using var stopping = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        stopping.CancelAfter(Remaining(remainingBudget, started));
        await writer.WriteAsync(evidence, stopping.Token).AsTask().WaitAsync(stopping.Token).ConfigureAwait(false);
        if (unexpected is not null) throw new InvalidOperationException("Option trade pricing failed; failure evidence was acknowledged and recovery is required.", unexpected);
        return result;
    }
    /// <summary>Prices and acknowledges the original captured source inputs.
    /// A later reference update or hot-book quote cannot rewrite this evidence.</summary>
    public Task<OptionGreeksSnapshot> CalculateAndRetainAsync(NativeCapturedOptionTrade captured,
        IOptionTradeEvidenceWriter writer, TimeSpan remainingBudget, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(captured);
        var definition = captured.Definition;
        return CalculateAndRetainAsync(captured.Source, definition.Pricing.Contract.UnderlyingContractId, definition.Pricing,
            captured.Underlying, definition.Strike, definition.IsCall, captured.CalculatedAtUtc, writer, remainingBudget,
            cancellationToken, captured.QualifiedUntilUtc);
    }
    static TimeSpan Remaining(TimeSpan budget, long started) => TimeSpan.FromTicks(Math.Max(0,
        Math.Min(budget.Ticks, TimeSpan.FromMinutes(2).Ticks) - Stopwatch.GetElapsedTime(started).Ticks));
}
