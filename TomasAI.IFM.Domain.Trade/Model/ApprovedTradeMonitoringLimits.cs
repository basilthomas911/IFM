using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Model;
namespace TomasAI.IFM.Domain.Trade.Model;
/// <summary>Finalizes Portfolio-owned approved risk using exact accepted fills, never current cash or quotes.</summary>
public static class ApprovedTradeMonitoringLimits
{
    /// <summary>Builds the supported Iron Condor monitoring snapshot from immutable approval and fill evidence.</summary>
    public static EstablishedTradeMonitoringLimits Create(TradeOrderDefinition order, EstablishedTradeDefinition trade)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(trade);
        if (order.PortfolioApprovalId == Guid.Empty || order.Revision < 1 || string.IsNullOrWhiteSpace(order.DefinitionHash)
            || order.PositionType != TradeOrderPositionType.Opening || order.RequiredCapital < 0 || order.MaximumLoss < 0
            || order.Id != new TradeOrderId(trade.Id.PortfolioId, trade.Id.FundId, trade.Id.OrderId)
            || trade.StrategyKind != TradeStrategyKind.IronCondor || trade.Legs.Length != 4
            || trade.EstablishedAtUtc.Kind != DateTimeKind.Utc || trade.EstablishedAtUtc == default)
            throw new InvalidOperationException("TRADE_LIMITS.APPROVAL_MISMATCH");
        // Order-level budgets cannot be silently assigned to one of several component trades.
        if (order.Components.Length != 1) throw new InvalidOperationException("TRADE_LIMITS.AMBIGUOUS_COMPONENT_BUDGET");
        var component = order.Components[0];
        if (component.ComponentId != trade.SourceComponentId || component.ReservedTradeId != trade.Id.TradeId
            || component.StrategyKind != trade.StrategyKind || component.Legs.Length != trade.Legs.Length
            || trade.Legs.Any(leg => !component.Legs.Contains(leg)))
            throw new InvalidOperationException("TRADE_LIMITS.COMPONENT_MISMATCH");
        var multiplier = trade.Legs.Select(leg => leg.CashMultiplier).Distinct().ToArray();
        if (multiplier.Length != 1 || multiplier[0] <= 0 || trade.MaturityDate is null)
            throw new InvalidOperationException("TRADE_LIMITS.CONTRACT_EVIDENCE_MISSING");
        var fills = trade.OriginalFills;
        if (fills.Length == 0 || fills.Any(fill => fill.ExecutionAttemptId != trade.ExecutionAttemptId
            || fill.ComponentId != component.ComponentId || fill.SignedQuantity == 0 || fill.Price < 0 || fill.Commission < 0
            || !trade.Legs.Any(leg => leg.TradeLegId == fill.TradeLegId && leg.ContractId == fill.ContractId && Math.Sign(leg.SignedQuantity) == Math.Sign(fill.SignedQuantity))))
            throw new InvalidOperationException("TRADE_LIMITS.FILL_EVIDENCE_MISMATCH");
        var quantities = trade.Legs.Select(leg => Math.Abs(fills.Where(fill => fill.TradeLegId == leg.TradeLegId).Sum(fill => fill.SignedQuantity))).Distinct().ToArray();
        if (quantities.Length != 1 || quantities[0] <= 0 || trade.Legs.Any(leg => quantities[0] > Math.Abs(leg.SignedQuantity)))
            throw new InvalidOperationException("TRADE_LIMITS.UNBALANCED_FILLS");
        decimal Price(TradeLegDefinition leg)
        {
            var values = fills.Where(fill => fill.TradeLegId == leg.TradeLegId).ToArray();
            return values.Sum(fill => fill.Price * Math.Abs(fill.SignedQuantity)) / quantities[0];
        }
        decimal Spread(byte side) => Math.Abs(Price(trade.Legs.Single(leg => leg.PutCall == side && leg.SignedQuantity < 0))
            - Price(trade.Legs.Single(leg => leg.PutCall == side && leg.SignedQuantity > 0)));
        var type = IronCondorMonitoringInputReader.IdentifyTradeType(trade.Legs);
        var limits = IronCondorTradeLimitInitialization.Initialize(trade.Id.TradeId, type, Spread(2), Spread(1),
            quantities[0], multiplier[0], 0, order.RequiredCapital, fills.Sum(fill => fill.Commission),
            trade.MaturityDate.Value.DayNumber - trade.TradeDate!.Value.DayNumber, trade.EstablishedAtUtc,
            "PortfolioApproval/" + order.PortfolioApprovalId.ToString("N"), order.MaximumLoss);
        return new() { PortfolioApprovalId = order.PortfolioApprovalId, OrderRevision = order.Revision,
            DefinitionHash = order.DefinitionHash, TradeLimit = limits.TradeLimit, SpreadLimits = limits.SpreadLimits };
    }
}
