using TomasAI.IFM.Framework.MarketData.Contracts.LastPrice;
using TomasAI.IFM.Framework.MarketData.Contracts.Pricing;
using TomasAI.IFM.Framework.MarketData.Pricing;

namespace TomasAI.IFM.Application.MarketData.Pricing;

/// <summary>Full-risk pricing from the actual trade price. A quote IV is never substituted.</summary>
public static class OptionTradePricing
{
    public static OptionGreeksSnapshot Calculate(OptionPricingContext context, OptionPricingQuote? underlying,
        LastTradeTickSnapshot trade, decimal strike, bool call, DateTimeOffset calculatedAt)
    {
        OptionGreeksSnapshot Failed(OptionPricingFailure failure) => new()
        {
            IsValid = false, PriceSource = OptionGreeksPriceSource.Trade,
            FailureReason = OptionGreeksFailureReason.PricingContextUnavailable, PricingFailure = failure,
            FuturesContractId = context.Contract.UnderlyingContractId, OptionMarkPrice = trade.Price,
            OptionPriceSourceSequence = trade.SourceSequence, OptionPriceTimestamp = trade.EventTimestamp,
            CalculatedAtUtc = calculatedAt
        };
        if (underlying is null)
            return Failed(new("UnderlyingQuoteUnavailable", "Underlying", trade.ContractId, "Trade retained without a qualified underlying quote."));
        if (trade.Size == 0 || trade.ValueDate == default)
            return Failed(new("InvalidTrade", "Trade", trade.ContractId, "Trade size and value date are required."));
        var observation = new OptionPricingQuote(trade.ContractId, trade.Price, trade.Price, trade.Size, trade.Size,
            trade.EventTimestamp, trade.ReceiveTimestamp, trade.SourceSequence, context.GenerationId);
        var result = Black76PricingModel.Calculate(context, underlying, observation, strike, call, calculatedAt);
        if (result.Failure is { } failure) return Failed(failure);
        var g = result.Value!;
        return new()
        {
            IsValid = true, PriceSource = OptionGreeksPriceSource.Trade,
            FuturesContractId = context.Contract.UnderlyingContractId,
            FuturesPrice = underlying.Bid / 2m + underlying.Ask / 2m, OptionMarkPrice = trade.Price,
            RiskFreeRate = context.Rate.AnnualContinuousRate, TimeToExpiryYears = g.TimeToExpiry,
            ImpliedVolatility = g.ImpliedVolatility, TheoreticalPrice = g.TheoreticalPrice,
            Delta = g.Delta, Gamma = g.Gamma, Vega = g.Vega, Theta = g.Theta, Rho = g.Rho,
            FuturesPriceSourceSequence = underlying.Sequence, OptionPriceSourceSequence = trade.SourceSequence,
            FuturesPriceTimestamp = underlying.EventAtUtc, OptionPriceTimestamp = trade.EventTimestamp,
            CalculatedAtUtc = calculatedAt,
            PricingContextDigest = PricingSemanticHash.Compute(new { Version = 1, Basis = "Trade", trade,
                context, underlying, calculatedAt, g.ContextDigest })
        };
    }
}
