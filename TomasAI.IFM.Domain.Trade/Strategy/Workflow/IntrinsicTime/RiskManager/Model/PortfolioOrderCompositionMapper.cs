using System.Security.Cryptography;
using System.Text;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Portfolio;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Model;

/// <summary>Maps a portfolio-neutral composed opportunity into the Portfolio's atomic decision contract.</summary>
public static class PortfolioOrderCompositionMapper
{
    public static EvaluatePortfolioOrderCompositionCommand CreateRequest(
        IntrinsicTimeStrategyWorkflowView view,
        int portfolioId,
        DateTime requestedAtUtc)
    {
        if (portfolioId <= 0) throw new ArgumentOutOfRangeException(nameof(portfolioId));
        var result = view.OrderComposition.Result?.ReadCompositionResult()
            ?? throw new InvalidOperationException("RiskManager requires a committed Order Composition result.");
        var candidate = result.Candidate
            ?? throw new InvalidOperationException("RiskManager cannot evaluate an absent composition candidate.");
        if (view.SelectionBinding?.SchemaVersion != 2 || candidate.PortfolioId != 0 || candidate.FundId != 0
            || candidate.OrderId != 0 || candidate.PrimaryTradeId != 0)
            throw new InvalidOperationException("RM.PORTFOLIO.PREMATURE_OWNERSHIP");

        var strategy = StrategyKind(view.CompositionExecution?.CompositionBinding.BuilderCode);
        var componentId = StableId(candidate.CandidateId, "component");
        var legs = candidate.Legs.Select((leg, ordinal) => new TradeLegDefinition
        {
            TradeLegId = StableId(candidate.CandidateId, $"leg/{ordinal}"),
            ContractId = leg.InstrumentId,
            ContractKey = leg.InstrumentId,
            AssetFamily = string.Equals(leg.InstrumentClass, "Futures", StringComparison.Ordinal)
                ? TradeAssetFamily.Futures : TradeAssetFamily.FuturesOption,
            SignedQuantity = checked((string.Equals(leg.Side, "Buy", StringComparison.Ordinal) ? 1 : -1)
                * leg.Ratio * candidate.UnitQuantity),
            Expiry = leg.ExpirationUtc == default ? null : DateOnly.FromDateTime(leg.ExpirationUtc),
            Strike = leg.Strike,
            PutCall = leg.Right is true ? (byte)1 : leg.Right is false ? (byte)2 : null
        }).ToArray();
        var requiredCapital = new decimal?[]
        {
            candidate.RiskEvidence.MaximumLoss,
            candidate.RiskEvidence.StressLoss,
            candidate.RiskEvidence.PlannedLoss
        }.Where(value => value is >= 0).Select(value => value!.Value).DefaultIfEmpty(0).Max();
        var body = new PortfolioOrderCandidate
        {
            CompositionId = result.ResultId,
            WorkflowId = view.WorkflowId.Value,
            DecisionHorizon = candidate.TargetHorizon.ToString(),
            StrategyKind = (PortfolioExecutionStrategyKind)strategy,
            ValueDate = result.DecisionContext.ValueDate,
            ValidUntilUtc = candidate.ValidUntilUtc,
            Origin = "IntrinsicTimeStrategyWorkflow",
            Components =
            [
                new TradeOrderComponentDefinition
                {
                    ComponentId = componentId,
                    StrategyKind = strategy,
                    Legs = legs,
                    PermitBalancedPartialAcceptance = false
                }.ToPortfolioComponent()
            ],
            RequiredCapital = requiredCapital,
            EvidenceHash = candidate.CandidateHash,
            DeploymentKey = candidate.DeploymentKey,
            MaximumLoss = candidate.RiskEvidence.MaximumLoss ?? 0,
            StressLoss = candidate.RiskEvidence.StressLoss ?? candidate.RiskEvidence.PlannedLoss ?? 0,
            Notional = candidate.RiskEvidence.Notional ?? 0,
            ProductSymbol = candidate.Product.Symbol,
            ProductExchange = candidate.Product.Exchange,
            ProductCurrency = candidate.Product.Currency,
            Delta = candidate.Greeks.Delta,
            Gamma = candidate.Greeks.Gamma,
            Vega = candidate.Greeks.Vega,
            PositionType = PortfolioExecutionPositionType.Opening,
            VolatilityEvidence = result.DecisionContext.VolatilityEvidence
        };
        var operationId = StableId(view.WorkflowId.Value, $"portfolio-order-composition/{view.WorkflowRevision}");
        var entityId = new FinancialExecutionId(portfolioId, operationId);
        var request = new EvaluatePortfolioOrderCompositionCommand
        {
            CommandId = operationId,
            Subject = new ActorSubject(ActorType.Function, EvaluatePortfolioOrderCompositionCommand.Actor,
                EvaluatePortfolioOrderCompositionCommand.Verb, entityId.Format()),
            EntityId = entityId,
            OperationId = operationId,
            PortfolioId = portfolioId,
            CorrelationId = view.CorrelationId,
            CausationId = view.OrderComposition.SourceEventId,
            RequestedAtUtc = requestedAtUtc,
            ExpiresAtUtc = new[] { view.ExpiresAtUtc, candidate.ValidUntilUtc }.Min(),
            ExpectedFinancialRevision = 0,
            Body = body,
            Access = new FinancialAccess("IntrinsicTimeStrategyWorkflow", ["OrderCompositionEvaluate"], [portfolioId])
        };
        return request with { InputSha256 = FinancialCanonicalHash.Request(request) };
    }

    public static void ValidateReceipt(IntrinsicTimeStrategyWorkflowView view, PortfolioOrderCompositionReceipt receipt,
        int expectedPortfolioId)
    {
        var composition = view.OrderComposition.Result?.ReadCompositionResult()
            ?? throw new InvalidOperationException("Committed composition is missing.");
        if (receipt.CompositionId != composition.ResultId || receipt.WorkflowId != view.WorkflowId.Value
            || receipt.PortfolioId != expectedPortfolioId || expectedPortfolioId <= 0 || receipt.FinancialRevision <= 0)
            throw new InvalidOperationException("RM.PORTFOLIO.RECEIPT_IDENTITY");
        if (receipt.VolatilityEvidence != composition.DecisionContext.VolatilityEvidence)
            throw new InvalidOperationException("RM.PORTFOLIO.VOLATILITY_EVIDENCE");
        if (receipt.Status == PortfolioOrderCompositionStatus.ExecuteTradeOrders && receipt.TradeOrders.Length == 0
            || receipt.Status == PortfolioOrderCompositionStatus.NoTradeOrders && receipt.TradeOrders.Length != 0)
            throw new InvalidOperationException("RM.PORTFOLIO.RECEIPT_STATUS");
        var candidate = composition.Candidate!;
        var expectedLegs = candidate.Legs.Select((leg, ordinal) => new
        {
            LegId = StableId(candidate.CandidateId, $"leg/{ordinal}"),
            ContractId = leg.InstrumentId
        }).ToArray();
        if (receipt.FundDecisions.Select(value => value.FundId).Distinct().Count() != receipt.FundDecisions.Length
            || receipt.TradeOrders.Select(order => order.Id.FundId).Distinct().Count() != receipt.TradeOrders.Length
            || receipt.CapacityEffects.Select(effect => effect.FundId).Distinct().Count() != receipt.CapacityEffects.Length
            || receipt.CapacityEffects.Length != receipt.TradeOrders.Length
            || receipt.TradeOrders.Any(order => !order.Id.IsValid || order.Id.PortfolioId != expectedPortfolioId
                || order.Revision != 1 || order.Status != PortfolioExecutionOrderStatus.Approved
                || order.PositionType != PortfolioExecutionPositionType.Opening
                || order.VolatilityEvidence != composition.DecisionContext.VolatilityEvidence
                || order.DefinitionHash != candidate.CandidateHash || order.Components.Length != 1
                || order.Components[0].ReservedTradeId <= 0
                || !order.Components[0].Legs.Select(leg => new { LegId = leg.TradeLegId, leg.ContractId })
                    .SequenceEqual(expectedLegs))
            || receipt.FundDecisions.Count(value => value.Accepted) != receipt.TradeOrders.Length
            || receipt.FundDecisions.Where(value => value.Accepted).Any(value => value.OrderId is null
                || !receipt.TradeOrders.Any(order => order.Id.FundId == value.FundId && order.Id.OrderId == value.OrderId)))
            throw new InvalidOperationException("RM.PORTFOLIO.RECEIPT_ORDER");
        if (receipt.CapacityEffects.Any(effect => effect.PortfolioId != expectedPortfolioId
            || effect.FundId <= 0 || effect.OrderId <= 0 || string.IsNullOrWhiteSpace(effect.UnderlyingScopeKey)
            || effect.RequiredCash < 0 || effect.Exposures.Length == 0
            || !receipt.TradeOrders.Any(order => order.Id.FundId == effect.FundId && order.Id.OrderId == effect.OrderId)))
            throw new InvalidOperationException("RM.PORTFOLIO.RECEIPT_CAPACITY");
    }

    public static void ValidateCompletion(
        IntrinsicTimeStrategyWorkflowView view,
        EvaluatePortfolioOrderCompositionCommand request,
        PortfolioOrderCompositionCompletedEvent completed)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(completed);
        if (completed.Id == Guid.Empty || completed.CommandId != request.CommandId
            || completed.OperationId != request.OperationId || completed.PortfolioId != request.PortfolioId
            || completed.EntityId != request.EntityId || completed.InputHash != request.InputSha256
            || completed.CorrelationId != request.CorrelationId || completed.CausationId != request.CausationId
            || completed.AggregateId != request.EntityId.Format()
            || completed.CommittedAtUtc.Kind != DateTimeKind.Utc)
            throw new InvalidOperationException("RM.PORTFOLIO.COMPLETION_IDENTITY");
        ValidateReceipt(view, completed.Receipt, request.PortfolioId);
    }

    public static PortfolioRiskDecision ToDecision(PortfolioOrderCompositionReceipt receipt) => new()
    {
        CompositionId = receipt.CompositionId,
        WorkflowId = receipt.WorkflowId,
        Status = receipt.Status == PortfolioOrderCompositionStatus.ExecuteTradeOrders
            ? PortfolioRiskDecisionStatus.ExecuteTradeOrders : PortfolioRiskDecisionStatus.NoTradeOrders,
        TradeOrders = [.. receipt.TradeOrders.Select(PortfolioExecutionContractMapper.ToTradeOrder)],
        FinancialRevision = receipt.FinancialRevision,
        AcceptedFundCount = receipt.FundDecisions.Count(value => value.Accepted),
        RejectedFundCount = receipt.FundDecisions.Count(value => !value.Accepted),
        PortfolioId = receipt.PortfolioId
        ,VolatilityEvidence = receipt.VolatilityEvidence
    };

    public static void ValidateDecision(IntrinsicTimeStrategyWorkflowView view, PortfolioRiskDecision decision)
    {
        var composition = view.OrderComposition.Result?.ReadCompositionResult()
            ?? throw new InvalidOperationException("Committed composition is missing.");
        if (decision.CompositionId != composition.ResultId || decision.WorkflowId != view.WorkflowId.Value
            || decision.PortfolioId <= 0
            || decision.FinancialRevision <= 0 || decision.AcceptedFundCount < 0 || decision.RejectedFundCount < 0)
            throw new InvalidOperationException("RM.PORTFOLIO.RECEIPT_IDENTITY");
        if (decision.VolatilityEvidence != composition.DecisionContext.VolatilityEvidence)
            throw new InvalidOperationException("RM.PORTFOLIO.VOLATILITY_EVIDENCE");
        if (decision.Status == PortfolioRiskDecisionStatus.ExecuteTradeOrders && decision.TradeOrders.Length == 0
            || decision.Status == PortfolioRiskDecisionStatus.NoTradeOrders && decision.TradeOrders.Length != 0)
            throw new InvalidOperationException("RM.PORTFOLIO.RECEIPT_STATUS");
        if (decision.TradeOrders.Any(order => !order.Id.IsValid || order.Id.PortfolioId != decision.PortfolioId || order.Components.Length == 0
            || order.Components.SelectMany(component => component.Legs).Any(leg => string.IsNullOrWhiteSpace(leg.ContractId))))
            throw new InvalidOperationException("RM.PORTFOLIO.RECEIPT_ORDER");
    }

    static TradeStrategyKind StrategyKind(string? builderCode) => builderCode switch
    {
        "Future" => TradeStrategyKind.FuturesOutright,
        "CallVertical" or "PutVertical" => TradeStrategyKind.VerticalSpread,
        "IronCondor" => TradeStrategyKind.IronCondor,
        _ => throw new InvalidOperationException("RM.PORTFOLIO.STRATEGY_UNSUPPORTED")
    };

    public static Guid StableId(Guid seed, string purpose)
        => new(SHA256.HashData(Encoding.UTF8.GetBytes($"{seed:N}|{purpose}")).AsSpan(0, 16));
}
