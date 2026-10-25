using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;

/// <summary>Contains persisted host identity/capability observations without live runtime reads.</summary>
[MessagePackObject]
public sealed record ScheduledTaskHostCapability
{
    [Key(0)] public string HostId { get; init; } = "";
    [Key(1)] public string Environment { get; init; } = "";
    [Key(2)] public string Platform { get; init; } = "";
    [Key(3)] public bool Ready { get; init; }
    [Key(4)] public DateTimeOffset ObservedAtUtc { get; init; }
    [Key(5)] public string Detail { get; init; } = "";
    [Key(6)] public long Generation { get; init; }
}
