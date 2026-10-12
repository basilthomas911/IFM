using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;

/// <summary>Contains operator timing and target settings, independent of Quartz runtime objects.</summary>
[MessagePackObject]
public sealed record ScheduledTaskSchedule
{
    [Key(0)] public string TaskKey { get; init; } = "";
    [Key(1)] public string Name { get; init; } = "";
    [Key(2)] public string Environment { get; init; } = "Development";
    [Key(3)] public string HostId { get; init; } = "development";
    [Key(4)] public ScheduledTaskTiming Timing { get; init; }
    [Key(5)] public string Expression { get; init; } = "";
    [Key(6)] public string TimeZoneId { get; init; } = "America/New_York";
    [Key(7)] public DateTimeOffset? StartsAtUtc { get; init; }
    [Key(8)] public DateTimeOffset? EndsAtUtc { get; init; }
    [Key(9)] public int MaximumRuntimeSeconds { get; init; } = 1800;
    [Key(10)] public int DispatchToleranceSeconds { get; init; } = 60;
    [Key(11)] public string Description { get; init; } = "";
}
