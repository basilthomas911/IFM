using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;

/// <summary>Contains actor-owned deployment facts for a bounded host catalog.</summary>
[MessagePackObject]
public sealed record ScheduledTaskCatalog
{
    [Key(0)] public ScheduledTaskId Id { get; init; }
    [Key(1)] public long Revision { get; init; }
    [Key(2)] public string HostId { get; init; } = "";
    [Key(3)] public string Environment { get; init; } = "";
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Retains the existing array-shaped serialization and application contract.")]
    [Key(4)] public ScheduledTaskProject[] Projects { get; init; } = [];
    [Key(5)] public ScheduledTaskHostCapability? HostCapability { get; init; }
}
