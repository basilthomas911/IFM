using MessagePack;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;

/// <summary>Complete broker-neutral request to reduce one existing strategy position.</summary>
[MessagePackObject]
public sealed record PortfolioCloseOrderCandidate
{
    [Key(0)] public Guid CompositionId { get; init; }
    [Key(1)] public PortfolioExitWorkflowId WorkflowId { get; init; }
    [Key(2)] public PortfolioPositionSnapshot Position { get; init; } = new();
    [Key(3)] public PortfolioExecutionStrategyKind StrategyKind { get; init; }
    [Key(4)] public DateOnly ValueDate { get; init; }
    [Key(5)] public DateTime ValidUntilUtc { get; init; }
    [Key(6)] public string Origin { get; init; } = string.Empty;
    [Key(7)] public PortfolioExecutionComponent Component { get; init; } = new();
    [Key(8)] public string EvidenceHash { get; init; } = string.Empty;
    [Key(9)] public PortfolioExecutionPositionType PositionType { get; init; }
    /// <summary>Optional explicit closing setup; otherwise inherit the opening setup.</summary>
    [Key(10)] [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public TomasAI.IFM.Domain.Portfolio.Shared.Contracts.FundTradeSetupReference? SetupTrade { get; init; }
}
