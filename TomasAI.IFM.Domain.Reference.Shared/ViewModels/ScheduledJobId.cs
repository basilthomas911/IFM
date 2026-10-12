using System;
using MessagePack;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.JobScheduler;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Reference.Shared.ViewModels;

/// <summary>
/// MessagePack-serializable identifier for a scheduled job, composed of JobId and JobName.
/// </summary>
/// <remarks>
/// Implements IActorEntityId. The formatted key uses dot notation: "JobId.JobName".
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record ScheduledJobId(
    [property: Key(0)] int JobId,
    [property: Key(1)] string JobName) : IActorEntityId
{
    /// <summary>
    /// Parameterless constructor for serializers; initializes to defaults.
    /// </summary>
    public ScheduledJobId() : this(0, string.Empty) { }

    /// <summary>
    /// Formats the identifier as a dot-separated string: "JobId.JobName".
    /// </summary>
    public string Format() => string.Create(null, stackalloc char[64], $"{JobId}.{JobName}");

    /// <summary>
    /// Returns a human-readable string representation of the identifier.
    /// </summary>
    public override string ToString() => $"{JobId} : {JobName}";
}
