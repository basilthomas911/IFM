using Newtonsoft.Json;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.Telemetry.ViewModels;

/// <summary>
/// MessagePack-serializable container for a batch of log events.
/// </summary>
/// <remarks>
/// Implements <see cref="IActorEntityId"/> with a dot-separated key "BatchDate.EventsCount".
/// Mirrors the FundOrderReadModel pattern with explicit properties and MessagePack keys.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record LogEventsReadModel : IActorEntityId
{
    /// <summary>Date associated with this batch of log events.</summary>
    [Key(0)]
    public DateTime BatchDate { get; init; }

    /// <summary>Log events included in this batch.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized/configuration array contract and consumer bindings; changing collection types is a separate API migration.")]
    [Key(1)]
    public LogEvent[] Events { get; init; }

    /// <summary>Parameterless constructor for serializers; defaults the batch date to today (UTC).</summary>
    public LogEventsReadModel()
    {
        BatchDate = DateTime.UtcNow.Date;
    }

    /// <summary>Create a new batch of log events.</summary>
    /// <param name="batchDate">Batch date.</param>
    /// <param name="events">Events array.</param>
    public LogEventsReadModel(DateTime batchDate, LogEvent[] events)
    {
        BatchDate = batchDate;
        Events = events ?? [];
    }

    /// <summary>Derived identifier for this batch (excluded from MessagePack).</summary>
    [JsonIgnore]
    [IgnoreMember]
    public LogEventsId Id => new(BatchDate.Date);

    /// <summary>
    /// Formats the identifier as a dot-separated key: "yyyyMMdd.EventsCount".
    /// </summary>
    public string Format() => string.Create(null, stackalloc char[80], $"{BatchDate:yyyyMMdd}.{(Events?.Length ?? 0)}");
}
