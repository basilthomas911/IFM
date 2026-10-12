using MessagePack;

namespace TomasAI.IFM.Domain.Application.Shared;

/// <summary>Queryable process-local snapshot of the latest application startup.</summary>
[MessagePackObject]
public sealed record ApplicationStartupStatus
{
    [Key(0)] public ApplicationLifecycleState State { get; init; }
    [Key(1)] public DateOnly ValueDate { get; init; }
    [Key(2)] public Guid ProcessBootId { get; init; }
    [Key(3)] public Guid CommandId { get; init; }
    [Key(4)] public Guid CorrelationId { get; init; }
    [Key(5)] public DateTime StartedAtUtc { get; init; }
    [Key(6)] public DateTime? CompletedAtUtc { get; init; }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized message/configuration contract, including enum widths, numeric values, and collection shape.")]
    [Key(7)] public ApplicationStartupActivityResult[] Activities { get; init; } = [];
    [Key(8)] public string Summary { get; init; } = string.Empty;
}
