using MessagePack;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Contracts;

public enum ReservationDisposition
{
    Unknown = 0,
    Committed = 1,
    IdempotentReplay = 2,
}

public enum RiskDecision
{
    Unknown = 0,
    Approved = 1,
    Rejected = 2,
}

[MessagePackObject(AllowPrivate = true)]
public sealed record TradeInstruction
{
    [Key(0)] public string TradeFamily { get; init; } = string.Empty;
    [Key(1)] public string TradeRole { get; init; } = "Primary";
    [Key(2)] public string DirectionOrBias { get; init; } = string.Empty;
    [Key(3)] public string TradeAction { get; init; } = string.Empty;
    [Key(4)] public bool IsPrimaryTrade { get; init; } = true;
    [Key(5)] public string UnderlyingRoot { get; init; } = string.Empty;
    [Key(6)] public DateOnly RequestedTradeDate { get; init; }
    [Key(7)] public DateOnly? RequestedMaturityDate { get; init; }
    [Key(8)] public string Reference { get; init; } = string.Empty;
    [Key(9)] public DateTime CreatedOnUtc { get; init; }
    [Key(10)] public string CreatedBy { get; init; } = string.Empty;
}

[MessagePackObject(AllowPrivate = true)]
public sealed record PortfolioFundStrategySnapshot
{
    [Key(0)] public Guid WorkflowId { get; init; }
    [Key(1)] public long WorkflowRevision { get; init; }
    [Key(2)] public Guid CorrelationId { get; init; }
    [Key(3)] public PortfolioReadModel Portfolio { get; init; } = new();
    [Key(4)] public FundMandateReadModel Fund { get; init; } = new();
    [Key(5)] public FundAllocationReadModel Allocation { get; init; } = new();
    [Key(6)] public FundRiskEnvelopeReadModel RiskEnvelope { get; init; } = new();
    [Key(7)] public FundTradeTemplateAssignmentReadModel[] Assignments { get; init; } = [];
    [Key(8)] public DateTime ResolvedAtUtc { get; init; }
    [Key(9)] public DateTime ValidUntilUtc { get; init; }
    [Key(10)] public string PayloadSha256 { get; init; } = string.Empty;

    public PortfolioFundStrategySnapshot DefensiveCopy() => this with
    {
        Portfolio = Portfolio.DefensiveCopy(),
        Fund = Fund.DefensiveCopy(),
        Assignments = [.. Assignments.Select(x => x.DefensiveCopy())],
    };
}

[MessagePackObject(AllowPrivate = true)]
public sealed record ReserveFundOrderCompositionRequest
{
    [Key(0)] public Guid WorkflowId { get; init; }
    [Key(1)] public long WorkflowRevision { get; init; }
    [Key(2)] public Guid TradeSelectionInvocationId { get; init; }
    [Key(3)] public Guid TradeSelectionResultId { get; init; }
    [Key(4)] public string TradeSelectionResultSha256 { get; init; } = string.Empty;
    [Key(5)] public int PortfolioId { get; init; }
    [Key(6)] public long PortfolioVersion { get; init; }
    [Key(7)] public int FundId { get; init; }
    [Key(8)] public long FundMandateVersion { get; init; }
    [Key(9)] public Guid TradeTemplateId { get; init; }
    [Key(10)] public long TradeTemplateVersion { get; init; }
    [Key(11)] public Guid OrderCompositionProfileId { get; init; }
    [Key(12)] public long OrderCompositionProfileVersion { get; init; }
    [Key(13)] public string UnderlyingRoot { get; init; } = string.Empty;
    [Key(14)] public string DecisionHorizon { get; init; } = string.Empty;
    [Key(15)] public DateOnly RequestedTradeDate { get; init; }
    [Key(16)] public DateOnly? RequestedMaturityDate { get; init; }
    [Key(17)] public TradeInstruction[] TradeInstructions { get; init; } = [];
    [Key(18)] public CompositionOrigin Origin { get; init; }
    [Key(19)] public Guid IdempotencyKey { get; init; }
    [Key(20)] public DateTime RequestedAtUtc { get; init; }
    [Key(21)] public DateTime ExpiresAtUtc { get; init; }
    [Key(22)] public string PortfolioFundStrategySnapshotSha256 { get; init; } = string.Empty;

    public ReserveFundOrderCompositionRequest DefensiveCopy() => this with
    {
        TradeInstructions = [.. TradeInstructions],
    };
}

[MessagePackObject(AllowPrivate = true)]
public sealed record FundCompositionReservationResult
{
    [Key(0)] public FundOrderProjectionReadModel Order { get; init; } = new();
    [Key(1)] public FundOrderTradeProjectionReadModel[] Trades { get; init; } = [];
    [Key(2)] public long AggregateVersion { get; init; }
    [Key(3)] public DateTime CommittedOnUtc { get; init; }
    [Key(4)] public ReservationDisposition Disposition { get; init; }
    [Key(5)] public string CanonicalRequestSha256 { get; init; } = string.Empty;
}

[MessagePackObject(AllowPrivate = true)]
public sealed record OrderCompositionResultReference
{
    [Key(0)] public Guid ResultId { get; init; }
    [Key(1)] public string ResultSha256 { get; init; } = string.Empty;
    [Key(2)] public DateTime EvaluatedAtUtc { get; init; }
    [Key(3)] public DateTime ExpiresAtUtc { get; init; }
    [Key(4)] public Guid InvocationId { get; init; }
}

[MessagePackObject(AllowPrivate = true)]
public sealed record RiskManagementResultReference
{
    [Key(0)] public Guid ResultId { get; init; }
    [Key(1)] public string ResultSha256 { get; init; } = string.Empty;
    [Key(2)] public RiskDecision Decision { get; init; }
    [Key(3)] public DateTime EvaluatedAtUtc { get; init; }
    [Key(4)] public DateTime ExpiresAtUtc { get; init; }
    [Key(5)] public Guid EnvelopeId { get; init; }
    [Key(6)] public long EnvelopeVersion { get; init; }
    [Key(7)] public string CandidateSha256 { get; init; } = string.Empty;
}
