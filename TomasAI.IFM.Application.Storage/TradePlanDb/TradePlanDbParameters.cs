using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;

namespace TomasAI.IFM.Application.Storage.TradePlanDb;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetTradePlan(
    int PortfolioId,
    int FundId,
    int OrderId,
    int TradeId,
    Guid PositionId,
    DateOnly ValueDate) : IBindValue
{
    public object Bind() =>
        new object?[] { PortfolioId, FundId, OrderId, TradeId, PositionId, ValueDate };
}

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct IronCondorScalarParameters(StrategyTradePlanSnapshot Plan, IronCondorTradePlanSnapshot Snapshot) : IBindValue
    {
        /// <inheritdoc />
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051", Justification = "Keep the transaction, resource ownership and failure ordering together during this provider migration; splitting it would obscure the atomic persistence boundary.")]
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
