using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;

/// <summary>Contains the desired definition and independently confirmed runtime installation.</summary>
[MessagePackObject]
public sealed record ScheduledTaskDefinition
{
    [Key(0)] public ScheduledTaskId Id { get; init; }
    [Key(1)] public long Revision { get; init; }
    [Key(2)] public long DesiredRevision { get; init; }
    [Key(3)] public ScheduledTaskSchedule Schedule { get; init; } = new();
    [Key(4)] public bool Enabled { get; init; }
    [Key(5)] public bool Removed { get; init; }
    [Key(6)] public ScheduledTaskInstallationStatus InstallationStatus { get; init; }
    [Key(7)] public long AppliedRevision { get; init; }
    [Key(8)] public bool AppliedEnabled { get; init; }
    [Key(9)] public string InstallationDetail { get; init; } = "";
    [Key(10)] public string ManifestVersion { get; init; } = "";
    [Key(11)] public string UpdatedBy { get; init; } = "";
    [Key(12)] public DateTimeOffset UpdatedAtUtc { get; init; }
    [Key(13)] public Guid OperationCommandId { get; init; }
    [Key(14)] public Guid? ActiveRunId { get; init; }
    [Key(15)] public DateTimeOffset? LastAdmittedFireUtc { get; init; }
    [Key(16)] public string InstallationFingerprint { get; init; } = "";
    [Key(17)] public string ReviewedArtifactDigest { get; init; } = "";
}
