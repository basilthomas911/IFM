using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;

/// <summary>Contains projected schedules, catalog and a bounded recent-run page for the UI.</summary>
[MessagePackObject]
public sealed record ScheduledTasksDashboard
{
    [Key(0)] public ScheduledTaskCatalog? Catalog { get; init; }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Retains the existing array-shaped serialization and application contract.")]
    [Key(1)] public ScheduledTaskDefinition[] Schedules { get; init; } = [];
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Retains the existing array-shaped serialization and application contract.")]
    [Key(2)] public ScheduledTaskRun[] RecentRuns { get; init; } = [];
    [Key(3)] public DateTimeOffset ReadAtUtc { get; init; }
}
