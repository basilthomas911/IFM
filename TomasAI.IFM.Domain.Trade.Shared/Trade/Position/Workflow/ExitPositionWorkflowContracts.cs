using System.Globalization;
using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;

public enum ExitPositionWorkflowState : byte
{
    Started = 1,
    OrderComposed = 2,
    RiskAccepted = 3,
    NoTradeOrders = 4,
    Failed = 5,
    Completed = 6
}

/// <summary>Stable actor names for each strategy-specific exit pipeline.</summary>
public static class ExitPositionWorkflowActorNames
{
    public const string IronCondorOrderComposer = "IronCondorExitOrderCompositionFunction";
    public const string IronCondorRiskManager = "IronCondorPositionExitRiskFunction";
    public const string VerticalSpreadOrderComposer = "VerticalSpreadExitOrderCompositionFunction";
    public const string VerticalSpreadRiskManager = "VerticalSpreadPositionExitRiskFunction";
    public const string FuturesOrderComposer = "FuturesExitOrderCompositionFunction";
    public const string FuturesRiskManager = "FuturesPositionExitRiskFunction";
}

[MessagePackObject]
public readonly record struct ExitPositionWorkflowId(
    [property: Key(0)] StrategyPositionId Position,
    [property: Key(1)] DateOnly ValueDate,
    [property: Key(2)] Guid ExitDecisionId) : IActorEntityId
{
    public string Format() => string.Create(CultureInfo.InvariantCulture,
        $"{Position.Format()}.{ValueDate:yyyyMMdd}.{ExitDecisionId:N}");
    [IgnoreMember] public bool IsValid => Position.IsValid && ValueDate != default && ExitDecisionId != Guid.Empty;
}

[MessagePackObject]
public sealed record ExitOrderComposition
{
    [Key(0)] public ExitPositionWorkflowId WorkflowId { get; init; }
    [Key(1)] public TradeStrategyKind StrategyKind { get; init; }
    [Key(2)] public TradeOrderComponentDefinition Component { get; init; } = new();
    [Key(3)] public TradePlanAction ExitAction { get; init; }
    [Key(4)] public string CompositionHash { get; init; } = string.Empty;
    [Key(5)] public DateTime ComposedAtUtc { get; init; }
    [Key(6)] public StrategyPositionSnapshot Position { get; init; } = new();
    [Key(7)] public TradeOrderPositionType PositionType { get; init; } = TradeOrderPositionType.Closing;
}

[MessagePackObject]
public sealed record PortfolioCloseRiskDecision
{
    [Key(0)] public Guid PortfolioCompletedEventId { get; init; }
    [Key(1)] public TradeOrderDefinition? TradeOrder { get; init; }
    [Key(2)] public bool ExecuteTradeOrder { get; init; }
    [Key(3)] public string ReasonCode { get; init; } = string.Empty;
    [Key(4)] public long FinancialRevision { get; init; }
}
