using System.Collections.Concurrent;
using TomasAI.IFM.Framework.MarketData.Contracts.LastPrice;
using TomasAI.IFM.Framework.MarketData.Contracts.Pricing;
using TomasAI.IFM.Framework.MarketData.DataBento;
using TomasAI.IFM.Framework.MarketData.DataBento.OptionChain;

namespace TomasAI.IFM.Application.MarketData.Pricing;

/// <summary>Production Black-76 implementation of the existing chain callback boundary; no I/O or actor work.</summary>
public sealed class Black76OptionChainGreeksEnricher(OptionChainPricingInputStore inputs, Guid generationId, TimeProvider? time = null,
    Func<string, OptionPricingQuote?>? readUnderlying = null)
    : IOptionChainGreeksEnricher
{
    readonly TimeProvider clock = time ?? TimeProvider.System;

    public OptionGreeksSnapshot EnrichQuote(DatabentoOptionChainRoute route, LastQuoteTickSnapshot tick)
    {
        ArgumentNullException.ThrowIfNull(route);
        if (!inputs.TryGet(route.FuturesOptionContractId, out var current) || current is null)
            return Failed(route, "PricingContextUnavailable", clock.GetUtcNow());
        var result = Calculate(route, current, tick);
        inputs.ObserveQuote(route.FuturesOptionContractId, current, tick);
        return result;
    }

    public OptionGreeksSnapshot EnrichTrade(DatabentoOptionChainRoute route, LastTradeTickSnapshot tick)
    {
        ArgumentNullException.ThrowIfNull(route);
        // A trade never becomes an IV mark. Re-evaluate the latest two-sided quote at the current time.
        if (!inputs.TryGet(route.FuturesOptionContractId, out var current) || current?.OptionQuote is not { } quote)
            return Failed(route, "QuoteUnavailable", clock.GetUtcNow());
        return Calculate(route, current, quote);
    }

    OptionGreeksSnapshot Calculate(DatabentoOptionChainRoute route, OptionChainPricingInputs input, LastQuoteTickSnapshot tick)
    {
        var at = clock.GetUtcNow();
        if (readUnderlying is not null)
        {
            var underlying = readUnderlying(input.Context.Contract.UnderlyingContractId);
            if (underlying is null) return Failed(route, "UnderlyingQuoteUnavailable", at);
            input = input with { Underlying = underlying };
        }
        var c = input.Context;
        // The callback instance belongs to one worker generation. Never stamp an old callback with a new epoch.
        if (generationId == Guid.Empty || c.GenerationId != generationId)
            return Failed(route, "Recovering", at);
        if (route.Definition.Instrument.InstrumentId != c.Contract.InstrumentId
            || !string.Equals(route.Definition.Dataset,c.Contract.Dataset,StringComparison.Ordinal) || !string.Equals(route.Definition.RawSymbol,c.Contract.RawSymbol,StringComparison.Ordinal)
            || !string.Equals(route.FuturesOptionContractId,c.Contract.ContractId,StringComparison.Ordinal) || !string.Equals(tick.ContractId,c.Contract.ContractId,StringComparison.Ordinal)
            || route.Definition.Right is not (OptionRightSelection.Call or OptionRightSelection.Put)
            || tick.BidPrice is null || tick.AskPrice is null)
            return Failed(route, "ContractOrQuoteInvalid", at);
        var quote = new OptionPricingQuote(tick.ContractId, tick.BidPrice.Value, tick.AskPrice.Value,
            tick.BidSize, tick.AskSize, tick.EventTimestamp,
            tick.LocalReceivedAtUtc == default ? tick.ReceiveTimestamp : tick.LocalReceivedAtUtc, tick.SourceSequence, c.GenerationId);
        var result = Black76PricingModel.Calculate(c, input.Underlying, quote, route.Definition.StrikePrice,
            route.Definition.Right == OptionRightSelection.Call, at);
        if (result.Failure is { } failure) return Failed(route, failure.Code, at) with { PricingFailure = failure };
        var g = result.Value!;
        return new OptionGreeksSnapshot
        {
            IsValid = true,
            PriceSource = OptionGreeksPriceSource.QuoteMidpoint,
            FuturesContractId = c.Contract.UnderlyingContractId,
            FuturesPrice = input.Underlying.Bid / 2m + input.Underlying.Ask / 2m,
            OptionMarkPrice = quote.Bid / 2m + quote.Ask / 2m,
            RiskFreeRate = c.Rate.AnnualContinuousRate,
            TimeToExpiryYears = g.TimeToExpiry,
            ImpliedVolatility = g.ImpliedVolatility,
            TheoreticalPrice = g.TheoreticalPrice,
            Delta = g.Delta,
            Gamma = g.Gamma,
            Vega = g.Vega,
            Theta = g.Theta,
            Rho = g.Rho,
            FuturesPriceSourceSequence = input.Underlying.Sequence,
            OptionPriceSourceSequence = quote.Sequence,
            FuturesPriceTimestamp = input.Underlying.EventAtUtc,
            OptionPriceTimestamp = quote.EventAtUtc,
            CalculatedAtUtc = at,
            PricingContextDigest = g.ContextDigest
        };
    }

    static OptionGreeksSnapshot Failed(DatabentoOptionChainRoute route, string code, DateTimeOffset at) => new()
    {
        IsValid = false,
        IsStale = code is "StaleData" or "TreasuryStale",
        FailureReason = OptionGreeksFailureReason.PricingContextUnavailable,
        FuturesContractId = route.Definition.Underlying,
        CalculatedAtUtc = at,
        PricingFailure = new(code, "PricingContext/Quote", route.FuturesOptionContractId, "A qualified quote and pricing context are required.")
    };
}
