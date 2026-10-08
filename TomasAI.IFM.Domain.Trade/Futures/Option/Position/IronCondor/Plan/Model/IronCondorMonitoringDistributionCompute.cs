using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.OptionPricer.Shared;
using TomasAI.IFM.Domain.OptionPricer.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using LossProbability = TomasAI.IFM.Framework.OptionPricer.Black76.LossProbability;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Model;

/// <summary>Adapts current OptionCalculator leg prices to the recovered deterministic forward-spread and MAD loss calculations.</summary>
/// <remarks>This is the existing single-scenario legacy model, not a Monte Carlo distribution or a statistical probability claim.</remarks>
public static class IronCondorMonitoringDistributionCompute
{
    /// <summary>Calculates paired forward observations from qualified theoretical option prices, using exact quantity and multiplier.</summary>
    /// <param name="trade">Persisted established Iron Condor and opening execution costs.</param>
    /// <param name="position">The current position used for PnL and opening price differences.</param>
    /// <param name="prices">The four completed OptionCalculator valuations.</param>
    /// <param name="risk">Qualified Greeks, used for short delta and OTM skew direction.</param>
    /// <param name="limits">Matching initialized currency loss limit.</param><param name="valueDate">The evaluation session.</param>
    /// <returns>Put and call observations ready for the existing paired distribution command.</returns>
    /// <exception cref="ArgumentException">A required provider observation or denominator is unavailable.</exception>
    public static (SpreadDistributionReadModel Put, SpreadDistributionReadModel Call) Calculate(
        EstablishedTradeDefinition trade, StrategyPositionSnapshot position, IronCondorCalculatedSpreadPrices prices,
        MarketCompositionSnapshot[] risk, TradeLimitReadModel limits, DateOnly valueDate)
    {
        var type = IronCondorMonitoringInputReader.IdentifyTradeType(trade.Legs);
        if (trade.Id != position.Id.Trade || limits.TradeId != trade.Id.TradeId || limits.TradeType != type || limits.MaxLoss >= 0
            || !FuturesTradingValueDate.TryGet(new DateTimeOffset(trade.EstablishedAtUtc), out var openingDate))
            throw new ArgumentException("Matching trade, negative currency loss limit and opening exchange date required.");
        var expiry = trade.Legs.Select(leg => leg.Expiry).Distinct().Single() ?? throw new ArgumentException("Common expiry required.");
        var expiryDays = expiry.DayNumber - valueDate.DayNumber;
        var tradingDays = expiry.DayNumber - openingDate.DayNumber;
        if (expiryDays <= 0 || tradingDays <= 0) throw new ArgumentException("Positive remaining and opening-to-expiry days required.");
        var putShort = prices.OptionLegPrices.Single(leg => !leg.IsCall && leg.SignedRatio < 0);
        var callShort = prices.OptionLegPrices.Single(leg => leg.IsCall && leg.SignedRatio < 0);
        var putProbability = IronCondorMonitoringInputReader.Probability(FindScope(risk, putShort.ContractId));
        var callProbability = IronCondorMonitoringInputReader.Probability(FindScope(risk, callShort.ContractId));
        if (putProbability is null || callProbability is null) throw new ArgumentException("Qualified put/call OTM probabilities required.");
        var putLossFactor = putProbability > callProbability ? 0 : 1;
        var putValues = CreateSpreadValues(prices, expiryDays, false); var callValues = CreateSpreadValues(prices, expiryDays, true);
        var putMean = putValues.Values.Single(); var callMean = callValues.Values.Single();
        var putForward = CalculateForwardPrice(putMean, Math.Abs(prices.PutSpreadPrice), expiryDays, tradingDays,
            putLossFactor, Math.Abs(FindScope(risk, putShort.ContractId).Instruments[0].Valuation!.Delta));
        var callForward = CalculateForwardPrice(callMean, Math.Abs(prices.CallSpreadPrice), expiryDays, tradingDays,
            1-putLossFactor, Math.Abs(FindScope(risk, callShort.ContractId).Instruments[0].Valuation!.Delta));
        var multipliers = trade.Legs.Select(leg => leg.CashMultiplier).Distinct().ToArray();
        if (multipliers.Length != 1 || multipliers[0] <= 0) throw new ArgumentException("Common positive multiplier required.");
        var currentPnl = (position.UnrealizedPnl + position.RealizedPnl) * multipliers[0]
            - trade.OpeningCommission - position.ClosingFills.Sum(fill => fill.Commission);
        var loss = currentPnl < 0
            ? new LossProbability(putValues.Values, callValues.Values, (double)limits.MaxLoss).Calculate(
                Math.Abs(position.Legs[0].SignedQuantity), (double)multipliers[0], CalculateSpreadPriceChange(position, false), CalculateSpreadPriceChange(position, true))
            : LossProbability.Empty;
        return (CreateObservation(trade.Id.TradeId, valueDate, type, prices, expiryDays, false, putForward, putLossFactor == 1 ? loss : LossProbability.Empty),
            CreateObservation(trade.Id.TradeId, valueDate, type, prices, expiryDays, true, callForward, putLossFactor == 0 ? loss : LossProbability.Empty));
    }

    /// <summary>Matches one qualified scope by exact option contract, independent of provider ordering.</summary>
    /// <param name="risk">The four qualified option observations.</param><param name="contractId">The persisted exact contract.</param>
    /// <returns>The unique scope; inconsistent or duplicate observations are rejected.</returns>
    static MarketCompositionSnapshot FindScope(MarketCompositionSnapshot[] risk, string contractId)
        => risk.Single(scope => scope.Instruments.Single().Instrument.ContractId == contractId);

    /// <summary>Creates one deterministic short-minus-long spread observation from the two OptionCalculator leg prices.</summary>
    /// <param name="prices">The complete four-leg theoretical valuations.</param><param name="expiryDays">Remaining calendar days.</param>
    /// <param name="isCall">Selects calls when true or puts when false.</param><returns>The recovered single-scenario value collection.</returns>
    static ProbabilityValueCollection CreateSpreadValues(IronCondorCalculatedSpreadPrices prices, int expiryDays, bool isCall)
    {
        var shortLeg = prices.OptionLegPrices.Single(leg => leg.IsCall == isCall && leg.SignedRatio < 0);
        var longLeg = prices.OptionLegPrices.Single(leg => leg.IsCall == isCall && leg.SignedRatio > 0);
        var observation = new OptionSpreadResult(0, expiryDays, shortLeg.UnderlyingPrice, shortLeg.AnnualContinuousRate,
            shortLeg.AnnualContinuousRate, (double)shortLeg.Strike, shortLeg.ImpliedVolatility, (double)longLeg.Strike, longLeg.ImpliedVolatility);
        observation.ShortValues.Add([shortLeg.TheoreticalPrice]); observation.LongValues.Add([longLeg.TheoreticalPrice]);
        return new([observation]);
    }

    /// <summary>Calculates abs(sum((current - opening)*signed quantity)/strategy quantity) for one put or call spread.</summary>
    /// <param name="position">The coherent position observations.</param><param name="isCall">Selects calls when true or puts when false.</param>
    /// <returns>The recovered per-strategy spread price change in points.</returns>
    static double CalculateSpreadPriceChange(StrategyPositionSnapshot position, bool isCall)
    {
        var pair = position.Legs.Where(leg => leg.PutCall == (isCall ? 1 : 2)).ToArray();
        if (pair.Length != 2 || pair.Any(leg => leg.SignedQuantity == 0)
            || Math.Abs(pair[0].SignedQuantity) != Math.Abs(pair[1].SignedQuantity))
            throw new ArgumentException("Two opposing legs with equal nonzero quantities required.");
        return (double)Math.Abs(pair.Sum(leg => (leg.CurrentPrice-leg.OpeningPrice)*leg.SignedQuantity)/Math.Abs(pair[0].SignedQuantity));
    }

    /// <summary>Captures the forward spread and legacy MAD loss score under the existing persisted distribution schema.</summary>
    /// <param name="tradeId">The established trade identity.</param><param name="valueDate">The exchange session.</param>
    /// <param name="type">The short or long condor strategy.</param><param name="prices">The calculator input evidence and version.</param>
    /// <param name="expiryDays">Remaining calendar days.</param><param name="isCall">Selects call or put observation.</param>
    /// <param name="forward">The recovered forward spread in points.</param><param name="lossScore">Legacy MAD ratio, which can exceed one.</param>
    /// <returns>The current observation with original UTC calculation time.</returns>
    static SpreadDistributionReadModel CreateObservation(int tradeId, DateOnly valueDate, TradeType type,
        IronCondorCalculatedSpreadPrices prices, int expiryDays, bool isCall, double forward, LossProbabilityDataModel lossScore)
    {
        var shortLeg = prices.OptionLegPrices.Single(leg => leg.IsCall == isCall && leg.SignedRatio < 0);
        var longLeg = prices.OptionLegPrices.Single(leg => leg.IsCall == isCall && leg.SignedRatio > 0);
        var spreadType = (type,isCall) switch
        {
            (TradeType.ShortIronCondor,false) => TradeType.PutCreditSpread,
            (TradeType.ShortIronCondor,true) => TradeType.CallCreditSpread,
            (TradeType.LongIronCondor,false) => TradeType.PutDebitSpread,
            _ => TradeType.CallDebitSpread
        };
        return new(prices.CalculatedAtUtc.Ticks, tradeId, valueDate, spreadType, TradeStatus.IntraDay, expiryDays,
            forward, lossScore.Value, lossScore.Threshold, lossScore.ThresholdCount, shortLeg.ImpliedVolatility,
            longLeg.ImpliedVolatility, 0, prices.CalculatedAtUtc);
    }

    /// <summary>Calculates legacy forward = mean * (1 +/- 2*abs(delta)*sqrt(expiryDays/tradingDays)); a nonpositive mean uses the current spread magnitude.</summary>
    /// <param name="calculatedMean">Short-minus-long OptionCalculator spread price.</param><param name="currentSpreadMagnitude">Positive current spread fallback from those same calculated legs.</param>
    /// <param name="expiryDays">Remaining calendar days.</param><param name="tradingDays">Opening-to-expiry calendar days, per legacy definition.</param>
    /// <param name="lossFactor">One increases the forward price; zero decreases it.</param><param name="shortDelta">Absolute qualified short delta.</param>
    /// <returns>The legacy forward spread observation, in points; its sign is retained until combined by the legacy initializer.</returns>
    public static double CalculateForwardPrice(double calculatedMean, double currentSpreadMagnitude, int expiryDays,
        int tradingDays, int lossFactor, double shortDelta)
    {
        if (!double.IsFinite(calculatedMean) || !double.IsFinite(currentSpreadMagnitude) || currentSpreadMagnitude <= 0
            || expiryDays <= 0 || tradingDays <= 0 || lossFactor is not (0 or 1) || !double.IsFinite(shortDelta) || shortDelta < 0 || shortDelta > 1)
            throw new ArgumentException("Finite current pricing, positive day counts and qualified delta required.");
        var mean = calculatedMean > 0 ? calculatedMean : currentSpreadMagnitude;
        return mean * (1 + (lossFactor == 1 ? 1 : -1) * 2 * shortDelta * Math.Sqrt((double)expiryDays/tradingDays));
    }
}
