using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.TradePlanDb;

/// <summary>Projects available legacy scalar fields for the current disposable snapshot, with no replay or repair.</summary>
internal static class IronCondorTradePlanProjection
{
    const string UpdateScalars = "UPDATE iron_condor_trade_plan SET sequenceId=?,actionDate=?,tradeDate=?,maturityDate=?,tradeType=?,actionType=?,actionSubType=?,actionState=?,actionReason=?,tradePnl=?,forwardLossRatio=?,lossProbability=?,mScore=?,maxProfit=?,maxLoss=?,minProfitTarget=?,dailyProfitTarget=?,assetPrice=?,assetStdDev=?,assetMean=?,assetPriceChange=?,marketTrend=?,marketVolatility=?,marketDirection=?,vixVolatility=?,tradeRisk=?,fiftyDayMA=?,fiveDayXMA=?,putOTMProbability=?,callOTMProbability=?,shortPutGamma=?,shortCallGamma=?,gammaRisk=?,netPrice=?,forwardPrice=?,forwardDelta=?,stopLossLimit=?,trendType=?,trendStrength=?,rsi=?,rsiSlope=?,tdi=?,tdiStrength=?,createdOn=?,createdBy=?,sourceEventId=?,positionSequence=?,routeGeneration=?,calculationVersion=?,inputStatus=?,actionDateTime=?,dailyPnl=?,dailyLoss=?,dailyLossLimit=?,lossHeadroom=?,forwardDailyPnl=?,forwardLoss=?,forwardLossHeadroom=?,projectedPositionValueChange=?,estimatedExitCommission=?,estimatedExitSlippage=?,estimatedExitCosts=?,putSpreadPrice=?,callSpreadPrice=?,combinedSpreadPrice=?,putForwardPrice=?,callForwardPrice=?,combinedForwardPrice=?,currentPositionValue=?,forwardPositionValue=?,estimatedCloseValue=?,netDelta=?,netGamma=?,netVega=?,netTheta=?,underlyingPrice=?,distanceToShortPut=?,distanceToShortCall=?,timeToExpiryDays=?,scenarioHorizonSeconds=?,scenarioUnderlyingPrice=?,scenarioUnderlyingMove=?,scenarioVolatilityShift=?,scenarioKind=?,pricingModelVersion=?,oldestQuoteAtUtc=?,riskValidUntilUtc=?,calculationStatus=?,exitRecommended=?,exitReason=?,quantity=?,contractMultiplier=?,currency=?,riskParameterSetId=?,riskParameterSetVersion=?,riskParameterSetHash=?,snapshotPayload=? WHERE portfolioId=? AND fundId=? AND orderId=? AND tradeId=? AND positionId=? AND valueDate=? AND planRevision=?;";

    /// <summary>Writes available business fields and input provenance; no scalar is fabricated for an absent provider.</summary>
    /// <param name="context">The independently configured TradePlanDb writer.</param>
    /// <param name="plan">The committed immutable plan revision.</param>
    /// <param name="cancellationToken">Cancels this single projection attempt; failed snapshots are dropped.</param>
    internal static async Task ProjectAsync(TradePlanDbContext context, StrategyTradePlanSnapshot plan, CancellationToken cancellationToken)
    {
        if (plan.IronCondorTradePlanSnapshot is not { } snapshot) return;
        await context.Database.Use(nameof(IronCondorTradePlanProjection), UpdateScalars)
            .SetParameters(new ScalarParameters(plan, snapshot))
            .ExecuteCommandAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    readonly record struct ScalarParameters(StrategyTradePlanSnapshot Plan, IronCondorTradePlanSnapshot Snapshot) : IBindValue
    {
        /// <inheritdoc />
        public object Bind()
        {
            var id = Plan.Position.Id.Trade;
            return new object?[]
            {
                Snapshot.SequenceId,
                Snapshot.ActionDate,
                Snapshot.TradeDate,
                Snapshot.MaturityDate,
                Snapshot.TradeType,
                Snapshot.ActionType,
                Snapshot.ActionSubType,
                Snapshot.ActionState,
                Snapshot.ActionReason,
                Snapshot.TradePnl,
                Snapshot.ForwardLossRatio,
                Snapshot.LossProbability,
                Snapshot.MScore,
                Snapshot.MaxProfit,
                Snapshot.MaxLoss,
                Snapshot.MinProfitTarget,
                Snapshot.DailyProfitTarget,
                Snapshot.AssetPrice,
                Snapshot.AssetStdDev,
                Snapshot.AssetMean,
                Snapshot.AssetPriceChange,
                Snapshot.MarketTrend,
                Snapshot.MarketVolatility,
                Snapshot.MarketDirection,
                Snapshot.VixVolatility,
                Snapshot.TradeRisk,
                Snapshot.FiftyDayMA,
                Snapshot.FiveDayXMA,
                Snapshot.PutOTMProbability,
                Snapshot.CallOTMProbability,
                Snapshot.ShortPutGamma,
                Snapshot.ShortCallGamma,
                Snapshot.GammaRisk,
                Snapshot.NetPrice,
                Snapshot.ForwardPrice,
                Snapshot.ForwardDelta,
                Snapshot.StopLossLimit,
                Snapshot.TrendType,
                Snapshot.TrendStrength,
                Snapshot.Rsi,
                Snapshot.RsiSlope,
                Snapshot.Tdi,
                Snapshot.TdiStrength,
                Snapshot.CreatedOn,
                Snapshot.CreatedBy,
                Snapshot.SourceEventId, Plan.Position.PositionSequence, Plan.Position.RouteGeneration,
                Snapshot.CalculationVersion, Snapshot.IsComplete ? "Complete" : "Unavailable",
                Snapshot.ActionDateTime,
                Snapshot.DailyPnl,
                Snapshot.DailyLoss,
                Snapshot.DailyLossLimit,
                Snapshot.LossHeadroom,
                Snapshot.ForwardDailyPnl,
                Snapshot.ForwardLoss,
                Snapshot.ForwardLossHeadroom,
                Snapshot.ProjectedPositionValueChange,
                Snapshot.EstimatedExitCommission,
                Snapshot.EstimatedExitSlippage,
                Snapshot.EstimatedExitCosts,
                Snapshot.PutSpreadPrice,
                Snapshot.CallSpreadPrice,
                Snapshot.CombinedSpreadPrice,
                Snapshot.PutForwardPrice,
                Snapshot.CallForwardPrice,
                Snapshot.CombinedForwardPrice,
                Snapshot.CurrentPositionValue,
                Snapshot.ForwardPositionValue,
                Snapshot.EstimatedCloseValue,
                Snapshot.NetDelta,
                Snapshot.NetGamma,
                Snapshot.NetVega,
                Snapshot.NetTheta,
                Snapshot.UnderlyingPrice,
                Snapshot.DistanceToShortPut,
                Snapshot.DistanceToShortCall,
                Snapshot.TimeToExpiryDays,
                Snapshot.ScenarioHorizonSeconds,
                Snapshot.ScenarioUnderlyingPrice,
                Snapshot.ScenarioUnderlyingMove,
                Snapshot.ScenarioVolatilityShift,
                Snapshot.ScenarioKind,
                Snapshot.PricingModelVersion,
                Snapshot.OldestQuoteAtUtc,
                Snapshot.RiskValidUntilUtc,
                Snapshot.CalculationStatus,
                Snapshot.ExitRecommended,
                Snapshot.ExitReason,
                Snapshot.Quantity,
                Snapshot.ContractMultiplier,
                Snapshot.Currency,
                Snapshot.StrategyRiskParameterSet?.ParameterSetId, Snapshot.StrategyRiskParameterSet?.Version, Snapshot.RiskParameterSetHash,
                MessagePackBinarySerializer.Shared.Serialize(Snapshot),
                id.PortfolioId, id.FundId, id.OrderId, id.TradeId, Plan.Position.Id.PositionId,
                Plan.ValueDate, Plan.PlanRevision
            };
        }
    }
}
