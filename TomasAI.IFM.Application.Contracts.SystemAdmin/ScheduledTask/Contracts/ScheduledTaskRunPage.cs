using MessagePack;
namespace TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
/// <summary>Contains a bounded page of persisted occurrences with an opaque Scylla continuation.</summary>
[MessagePackObject]
public sealed record ScheduledTaskRunPage
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Retains the existing array-shaped serialization and application contract.")]
    [Key(0)] public ScheduledTaskRun[] Runs { get; init; } = [];
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Retains the existing array-shaped serialization and application contract.")]
    [Key(1)] public byte[]? PagingState { get; init; }
}
