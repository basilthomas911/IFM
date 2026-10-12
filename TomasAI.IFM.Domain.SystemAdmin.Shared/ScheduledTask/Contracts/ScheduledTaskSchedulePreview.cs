using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;

/// <summary>Contains Quartz-validated next occurrences and useful validation failures.</summary>
[MessagePackObject]
public sealed record ScheduledTaskSchedulePreview
{
    [Key(0)] public bool Valid { get; init; }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Retains the existing array-shaped serialization and application contract.")]
    [Key(1)] public string[] Errors { get; init; } = [];
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Retains the existing array-shaped serialization and application contract.")]
    [Key(2)] public DateTimeOffset[] NextFireTimesUtc { get; init; } = [];
    [Key(3)] public string TimeZoneId { get; init; } = "";
}
