using System.Globalization;
using TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Portfolio;
using TomasAI.IFM.Domain.Trade.Shared.Order;

namespace TomasAI.IFM.Domain.Trade.Order.Query.Model;

/// <summary>Read-only financial preparation. Portfolio admission still owns permission to trade.</summary>
public static class TradeOrderPreparationCompute
{
    public static TradeOrderPreparation Calculate(TradeOrderPreparationRequest request, FuturesContractV3ReadModel underlying,
        DateOnly valueDate, DateTime nowUtc, decimal tick, decimal perLegCommission,
        TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog.CatalogKey deployment, string horizon, string feeBasis)
    {
        ArgumentNullException.ThrowIfNull(request); ArgumentNullException.ThrowIfNull(underlying);
        var draft = request.Draft;
        if (request.PortfolioId <= 0 || request.Quantity is < 1 or > 100000 || valueDate == default || nowUtc.Kind != DateTimeKind.Utc
            || draft is null || draft.CompositionId == Guid.Empty || draft.SetupTrade is not { FundId: > 0, OrderId: > 0, TradeId: > 0 }
            || draft.Components.Length != 1 || tick <= 0 || perLegCommission < 0)
            throw new ArgumentException("Order preparation requires a scoped, valid draft, server context and fee/tick policy.");
        var component = draft.Components[0].ToTradeComponent();
        var legs = component.Legs;
        var expected = component.StrategyKind == TradeStrategyKind.FuturesOutright ? 1 : component.StrategyKind == TradeStrategyKind.IronCondor ? 4 : 2;
        if (component.StrategyKind is not (TradeStrategyKind.FuturesOutright or TradeStrategyKind.VerticalSpread or TradeStrategyKind.IronCondor)
            || legs.Length != expected || legs.Select(l => l.ContractId).Distinct(StringComparer.Ordinal).Count() != legs.Length
            || legs.Any(l => l.TradeLegId == Guid.Empty || Math.Abs(l.SignedQuantity) != request.Quantity || l.CashMultiplier <= 0)
            || legs.Select(l => l.CashMultiplier).Distinct().Count() != 1 || component.SignedNetDebitLimit is not { } limit)
            throw new ArgumentException("Distinct balanced strategy legs and an explicit price are required.");
        if (component.StrategyKind == TradeStrategyKind.VerticalSpread && (legs.Select(l => l.PutCall).Distinct().Count() != 1 || legs.Sum(l => l.SignedQuantity) != 0)
            || component.StrategyKind == TradeStrategyKind.IronCondor && (legs.Count(l => l.PutCall == 1) != 2 || legs.Count(l => l.PutCall == 2) != 2
                || legs.GroupBy(l => l.PutCall).Any(group => group.Sum(l => l.SignedQuantity) != 0)))
            throw new ArgumentException("Strategy legs do not form balanced vertical spreads.");
        if (legs.Any(l => l.AssetFamily == TradeAssetFamily.FuturesOption) && legs.Select(l => l.Expiry).Distinct().Count() != 1)
            throw new ArgumentException("The selected strategy requires a single option expiry.");
        if (decimal.Remainder(limit, tick) != 0) throw new ArgumentException("The selected order price is off tick.");
        var cash = limit * request.Quantity * legs[0].CashMultiplier;
        var notional = Math.Abs(cash);
        var maximumLoss = notional;
        if (component.StrategyKind != TradeStrategyKind.FuturesOutright)
        {
            if (legs.Any(l => l.AssetFamily != TradeAssetFamily.FuturesOption || l.Strike is null || l.PutCall is not (1 or 2))
                || legs.Where(l => l.PutCall == 1).Sum(l => l.SignedQuantity) != 0)
                throw new ArgumentException("Supported option payoff requires resolved strikes and bounded call exposure.");
            var payoffLegs = legs.Select(l => new TradeOrderPreviewLeg(l.ContractId, Math.Sign(l.SignedQuantity), Math.Abs(l.SignedQuantity),
                l.Strike, l.PutCall == 1, false, null, null, null, null, l.CashMultiplier)).ToArray();
            var payoff = TradeOrderPayoffCompute.Calculate(payoffLegs, cash);
            // Preserve the existing desktop candidate capital/stress proxy, not a new margin model.
            maximumLoss = Math.Max(notional, payoff.MaximumLoss ?? throw new ArgumentException("Unbounded option loss is unsupported by this editor."));
        }
        var fees = legs.Select(l => perLegCommission * Math.Abs(l.SignedQuantity)).ToArray();
        var totalFees = fees.Sum();
        var candidate = draft with { StrategyKind = (PortfolioExecutionStrategyKind)component.StrategyKind, ValueDate = valueDate, ValidUntilUtc = nowUtc.AddMinutes(5), DecisionHorizon = horizon,
            DeploymentKey = deployment, RequiredCapital = maximumLoss, MaximumLoss = maximumLoss, StressLoss = maximumLoss,
            Notional = notional, ProductSymbol = underlying.Symbol, ProductExchange = underlying.Exchange, ProductCurrency = underlying.Currency,
            Components = [(component with { MinimumSignedNetDebitLimit = limit - 10m * tick, MaximumSignedNetDebitLimit = limit,
                TickIncrement = tick }).ToPortfolioComponent()] };
        var evidence = TomasAI.IFM.Domain.Portfolio.Shared.Financial.FinancialCanonicalHash.Compute(new {
            request.PortfolioId, candidate.CompositionId, candidate.Components, candidate.BrokerAccountAlias,
            candidate.BrokerEnvironment, candidate.ValueDate, candidate.DeploymentKey, maximumLoss, notional,
            candidate.BrokerOrderType, candidate.BrokerAlgorithm, candidate.TimeInForce, candidate.AlgorithmPace });
        candidate = candidate with { EvidenceHash = evidence, MicroExecutionProfileId = "ManualPriceImprovement", MicroExecutionProfileVersion = 1,
            MicroExecutionProfileHash = evidence, DecisionEvidence = TomasAI.IFM.Domain.MarketData.Analytics.Shared.MarketDecisionEvidence.CaptureJson(
                "BackendManualOrderPreparation/v1", new { request, maximumLoss, notional, fees, feeBasis, tick, deployment }, nowUtc) };
        return new(candidate, cash, totalFees, cash + totalFees, fees, feeBasis, nowUtc);
    }
}
