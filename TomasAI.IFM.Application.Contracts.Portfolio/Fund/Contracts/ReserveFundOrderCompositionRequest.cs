using System.Globalization;
using MessagePack;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Contracts;

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
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public financial model contract.")]
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
