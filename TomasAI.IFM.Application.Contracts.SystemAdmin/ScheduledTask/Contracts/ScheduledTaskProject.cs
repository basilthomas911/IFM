using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;

/// <summary>Describes reviewed deployed code and bounded task execution capabilities.</summary>
[MessagePackObject]
public sealed record ScheduledTaskProject
{
    [Key(0)] public string TaskKey { get; init; } = "";
    [Key(1)] public string DisplayName { get; init; } = "";
    [Key(2)] public string ProjectName { get; init; } = "";
    [Key(3)] public string ManifestVersion { get; init; } = "";
    [Key(4)] public string Platform { get; init; } = "";
    [Key(5)] public string ArtifactDigest { get; init; } = "";
    [Key(6)] public bool Available { get; init; }
    [Key(7)] public int MaximumRuntimeSeconds { get; init; } = 1800;
    [Key(8)] public bool MarketSensitive { get; init; }
    [Key(9)] public string CompletionProtocol { get; init; } = "BusinessCompletion";
}
