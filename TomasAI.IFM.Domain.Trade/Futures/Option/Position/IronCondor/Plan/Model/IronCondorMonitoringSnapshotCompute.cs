using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Model;

/// <summary>Builds legacy monitoring values from captured observations without changing actor state.</summary>
public static class IronCondorMonitoringSnapshotCompute
{
    /// <summary>Maps committed valuation and computes forwardPrice, forwardLossRatio and MScore using recovered formulas.</summary>
    /// <param name="tradePlan">The coherent calculated position valuation.</param>
    /// <param name="sourceEventId">The initiating position observation event.</param>
    /// <param name="inputs">Captured distribution and historical baseline; null explicitly degrades legacy monitoring.</param>
    /// <returns>A snapshot with available business values and explicit missing-input reasons.</returns>
    public static IronCondorTradePlanSnapshot Create(StrategyTradePlanSnapshot tradePlan,
        Guid sourceEventId, IronCondorTradePlanInputs? inputs)
    {
        var id = tradePlan.Position.Id.Trade;
        var reasons = new List<string>();
        var snapshot = new IronCondorTradePlanSnapshot
        {
            OrderId = id.OrderId, TradeId = id.TradeId, ValueDate = tradePlan.ValueDate,
            SequenceId = tradePlan.PlanRevision, ActionDate = tradePlan.CalculatedAtUtc,
            TradePnl = tradePlan.TotalPnl, ActionReason = tradePlan.Explanation,
            CreatedOn = tradePlan.CalculatedAtUtc, CreatedBy = "IronCondorTradePlanFunction",
            Position = tradePlan.Position, SourceEventId = sourceEventId,
            IronCondorTradePlanInputs = inputs is null ? null : inputs with
            { HistoricalForwardLossRatios = [.. inputs.HistoricalForwardLossRatios] }
        };

        snapshot = CaptureBusinessObservations(snapshot, tradePlan, inputs, reasons);
        if (tradePlan.Position.AsOfUtc.Kind != DateTimeKind.Utc || tradePlan.Position.AsOfUtc == default
            || tradePlan.Position.AsOfUtc > tradePlan.CalculatedAtUtc
            || tradePlan.CalculatedAtUtc-tradePlan.Position.AsOfUtc > TimeSpan.FromSeconds(tradePlan.Parameters.MaximumDataAgeSeconds))
            reasons.Add("IronCondorTradePlan.INPUTS.POSITION: position prices are unavailable, future-dated or stale.");
        if (inputs?.FundCashAsOfUtc is { } cashAt && (cashAt.Kind != DateTimeKind.Utc || cashAt > tradePlan.CalculatedAtUtc
            || tradePlan.CalculatedAtUtc-cashAt > TimeSpan.FromSeconds(30)))
            reasons.Add("IronCondorTradePlan.INPUTS.CASH: captured Fund cash is stale.");
        if (inputs is null)
            reasons.Add("IronCondorTradePlan.INPUTS.DISTRIBUTION: forward spread prices and MScore baseline are unavailable.");
        else if (inputs.TradeType is not (TradeType.ShortIronCondor or TradeType.LongIronCondor)
            || inputs.ObservedAtUtc.Kind != DateTimeKind.Utc || inputs.ObservedAtUtc == default
            || inputs.ObservedAtUtc > tradePlan.CalculatedAtUtc
            || tradePlan.CalculatedAtUtc - inputs.ObservedAtUtc > TimeSpan.FromSeconds(tradePlan.Parameters.MaximumDataAgeSeconds)
            || string.IsNullOrWhiteSpace(inputs.BaselineIdentity) || string.IsNullOrWhiteSpace(inputs.DistributionIdentity))
            reasons.Add("IronCondorTradePlan.INPUTS.DISTRIBUTION: distribution/baseline identity or observation timestamp is invalid or stale.");
        else
        {
            try
            {
                var forwardPrice = IronCondorLegacyValueInitializers.CalculateForwardPrice(inputs.PutForwardPrice, inputs.CallForwardPrice);
                var ratio = IronCondorLegacyValueInitializers.CalculateForwardLossRatio(forwardPrice, inputs.ForwardLossPriceLimit);
                snapshot = snapshot with { TradeType = inputs.TradeType.ToString(), ForwardPrice = forwardPrice, ForwardLossRatio = ratio };
                var mScore = IronCondorLegacyValueInitializers.CalculateMScore(ratio, inputs.HistoricalForwardLossRatios);
                snapshot = snapshot with { MScore = mScore,
                    TradeRisk = IronCondorLegacyValueInitializers.CalculateTradeRisk(inputs.TradeType, mScore,
                        inputs.TradeType == TradeType.ShortIronCondor ? forwardPrice > snapshot.NetPrice : forwardPrice < snapshot.NetPrice).ToString() };
            }
            catch (ArgumentException error)
            {
                reasons.Add($"IronCondorTradePlan.INPUTS.DISTRIBUTION: {error.Message}");
            }
        }
        if (snapshot.TradeDate is null || snapshot.MaturityDate is null || snapshot.FiftyDayMA is null
            || snapshot.FiveDayXMA is null || snapshot.PutOTMProbability is null || snapshot.CallOTMProbability is null
            || snapshot.LossProbability is null || snapshot.ForwardDelta is null || snapshot.StopLossLimit is null)
            reasons.Add("IronCondorTradePlan.INPUTS.BASELINE: expiry, daily averages or distribution/risk observations are incomplete.");
        if (snapshot.NetPrice is null)
            reasons.Add("IronCondorTradePlan.INPUTS.PRICING: current qualified four-leg OptionCalculator spread prices are unavailable.");
        if (reasons.Count == 0 && inputs is not null && snapshot.ForwardLossRatio is not null)
            snapshot = IronCondorLegacyRuleCompute.Evaluate(snapshot, inputs);
        if (snapshot.ActionType is null || snapshot.ActionSubType is null)
            reasons.Add("IronCondorTradePlan.RULES.UNAVAILABLE: complete legacy rule evaluation has not been captured.");
        return snapshot with { UnavailableReasons = [.. reasons.Distinct()] };
    }

    /// <summary>Copies versioned domain observations and calculates gamma risk without querying providers or changing state.</summary>
    /// <param name="snapshot">The proposed monitoring output.</param>
    /// <param name="tradePlan">The coherent position valuation and session identity.</param>
    /// <param name="inputs">The captured business observations.</param>
    /// <param name="reasons">Receives explicit unavailable-input reasons.</param>
    /// <returns>The output populated only with observed business values.</returns>
    static IronCondorTradePlanSnapshot CaptureBusinessObservations(IronCondorTradePlanSnapshot snapshot,
        StrategyTradePlanSnapshot tradePlan, IronCondorTradePlanInputs? inputs, List<string> reasons)
    {
        if (inputs?.TradeLimits is { } limits && limits.TradeId == snapshot.TradeId && limits.TradeType == inputs.TradeType
            && limits.MaxProfit > 0 && limits.MaxLoss < 0 && limits.DailyProfitTarget >= 0)
            snapshot = snapshot with { MaxProfit = limits.MaxProfit, MaxLoss = limits.MaxLoss,
                MinProfitTarget = limits.MinProfitTarget, DailyProfitTarget = limits.DailyProfitTarget };
        else reasons.Add("IronCondorTradePlan.INPUTS.LIMITS: matching configured trade limits are unavailable.");
        if (inputs?.UnderlyingStatistics is { } underlying && underlying.ValueDate == tradePlan.ValueDate
            && underlying.ClosePrice > 0 && double.IsFinite(underlying.DailyStdDev) && underlying.DailyStdDev >= 0
            && double.IsFinite(underlying.Mean) && double.IsFinite(underlying.DailyPercentChange))
            snapshot = snapshot with { AssetPrice = Convert.ToDecimal(underlying.ClosePrice), AssetStdDev = underlying.DailyStdDev,
                AssetMean = underlying.Mean, AssetPriceChange = underlying.DailyPercentChange,
                MarketTrend = underlying.MarketDirection.ToString(), MarketVolatility = underlying.MarketVolatility.ToString(),
                MarketDirection = underlying.PriceDirection.ToString(), VixVolatility = underlying.PriceVolatility.ToString() };
        else reasons.Add("IronCondorTradePlan.INPUTS.UNDERLYING: current-session underlying statistics are unavailable.");
        if (inputs?.TradeSignal is { } signal && signal.ValueDate == tradePlan.ValueDate
            && double.IsFinite(signal.RSI) && double.IsFinite(signal.RSISlope))
            snapshot = snapshot with { TrendType = signal.TrendType.ToString(), TrendStrength = signal.TrendStrength.ToString(),
                Rsi = signal.RSI, RsiSlope = signal.RSISlope, Tdi = signal.TDI.ToString(), TdiStrength = signal.TDIStrength.ToString() };
        else reasons.Add("IronCondorTradePlan.INPUTS.ANALYTICS: current-session seeded analytics are unavailable.");
        if (inputs?.ShortPutGamma is { } put && inputs.ShortCallGamma is { } call
            && double.IsFinite(put) && double.IsFinite(call) && put >= 0 && call >= 0)
            snapshot = snapshot with { ShortPutGamma = put, ShortCallGamma = call,
                GammaRisk = IronCondorLegacyValueInitializers.CalculateGammaRisk(call, put).ToString() };
        else reasons.Add("IronCondorTradePlan.INPUTS.GREEKS: qualified short-leg Greeks are unavailable.");
        return snapshot with { TradeDate = inputs?.TradeDate, MaturityDate = inputs?.MaturityDate,
            PutOTMProbability = inputs?.PutOTMProbability, CallOTMProbability = inputs?.CallOTMProbability,
            LossProbability = inputs?.LossProbability, ForwardDelta = inputs?.ForwardDelta, StopLossLimit = inputs?.StopLossLimit,
            FiftyDayMA = inputs?.FiftyDayMA, FiveDayXMA = inputs?.FiveDayXMA,
            NetPrice = inputs?.CalculatedSpreadPrices is { } calculated
                && calculated.CalculatedAtUtc <= tradePlan.CalculatedAtUtc && calculated.ValidUntilUtc > tradePlan.CalculatedAtUtc
                ? Convert.ToDecimal(calculated.CombinedSpreadPrice) : null };
    }

    /// <summary>Calculates legacy netPrice = abs(put signed current prices) + abs(call signed current prices), per strategy unit.</summary>
    /// <param name="position">The coherent four-leg position with equal whole-contract quantities.</param>
    /// <returns>The legacy combined spread price in points, or null for incomplete or unequal leg quantities.</returns>
    internal static decimal? CalculateCombinedSpreadPrice(StrategyPositionSnapshot position)
    {
        if (position.Legs.Length != 4 || position.Legs.Any(leg => leg.SignedQuantity == 0)
            || position.Legs.Select(leg => Math.Abs(leg.SignedQuantity)).Distinct().Count() != 1
            || position.Legs.Count(leg => leg.PutCall == 1) != 2 || position.Legs.Count(leg => leg.PutCall == 2) != 2)
            return null;
        var quantity = Math.Abs(position.Legs[0].SignedQuantity);
        return position.Legs.GroupBy(leg => leg.PutCall).Sum(spread => Math.Abs(spread.Sum(leg => leg.CurrentPrice * leg.SignedQuantity))) / quantity;
    }
}
