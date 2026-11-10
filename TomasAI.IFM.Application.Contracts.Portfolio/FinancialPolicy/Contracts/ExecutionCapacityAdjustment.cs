using MessagePack;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

/// <summary>Source-bound actual execution quantities; these facts do not authorize another broker submission.</summary>
[MessagePackObject]
public sealed record ExecutionCapacityAdjustment
{
    [Key(0)] public int OrderId { get; init; }
    [Key(1)] public int FundId { get; init; }
    [Key(2)] public Guid ExecutionId { get; init; }
    [Key(3)] public Guid SourceCommandId { get; init; }
    [Key(4)] public string ExecutionHash { get; init; } = string.Empty;
    [Key(5)] public int ApprovedUnits { get; init; }
    [Key(6)] public int FilledUnits { get; init; }
    [Key(7)] public int CancelledUnits { get; init; }
    /// <summary>A committed Trade Order expiry fenced execution before any broker submission.</summary>
    [Key(8)] public bool UnsubmittedExpiry { get; init; }
}
