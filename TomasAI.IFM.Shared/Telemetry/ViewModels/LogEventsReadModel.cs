using Newtonsoft.Json;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.Telemetry.ViewModels;

/// <summary>
/// MessagePack-serializable telemetry log event entry (timestamp, level, message, and key/value properties).
/// </summary>
/// <remarks>
/// Implements <see cref="IActorEntityId"/>; formatted key uses dot notation: "Timestamp.Level" where
/// Timestamp = yyyyMMddHHmmss. Properties are serialized as a dictionary.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record LogEvent : IActorEntityId
{
    /// <summary>UTC/local timestamp when the log event occurred.</summary>
    [Key(0)]
    [JsonProperty("Timestamp")]
    public DateTime Timestamp { get; init; }

    /// <summary>Log severity level (e.g., Information, Warning, Error).</summary>
    [Key(1)]
    [JsonProperty("Level")]
    public string Level { get; init; } = "Information";

    /// <summary>Fully rendered log message text.</summary>
    [Key(2)]
    [JsonProperty("RenderedMessage")]
    public string RenderedMessage { get; init; } = string.Empty;

    /// <summary>Additional structured log properties (flattened key/value pairs).</summary>
    [Key(3)]
    [JsonProperty("Properties")]
    public Dictionary<string, string> Properties { get; init; } = new();

    /// <summary>
    /// Formats the log event identifier as "Timestamp.Level".
    /// </summary>
    public string Format() => string.Create(null, stackalloc char[64], $"{Timestamp:yyyyMMddHHmmss}.{Level}");
}

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

/// <summary>
/// MessagePack-serializable lightweight view model for a single log event entry.
/// </summary>
/// <remarks>
/// Pattern mirrors FundOrderReadModel: explicit properties with sequential MessagePack keys.
/// A derived identifier is excluded from serialization. Use for simplified log representations.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record LogEventReadModel
{
    /// <summary>Timestamp when the event occurred.</summary>
    [Key(0)]
    public DateTime Timestamp { get; init; }

    /// <summary>Log severity level.</summary>
    [Key(1)]
    public string LogLevel { get; init; } = string.Empty;

    /// <summary>Log message text.</summary>
    [Key(2)]
    public string Message { get; init; } = string.Empty;

    /// <summary>Service or component identifier producing the log.</summary>
    [Key(3)]
    public string ServiceId { get; init; } = string.Empty;

    /// <summary>
    /// Parameterless constructor for serializers.
    /// </summary>
    public LogEventReadModel() { }

    /// <summary>
    /// Creates a new log event view model.
    /// </summary>
    /// <param name="timestamp">Event timestamp.</param>
    /// <param name="logLevel">Severity level.</param>
    /// <param name="message">Log message.</param>
    /// <param name="serviceId">Originating service identifier.</param>
    public LogEventReadModel(DateTime timestamp, string logLevel, string message, string serviceId)
    {
        Timestamp = timestamp;
        LogLevel = logLevel ?? string.Empty;
        Message = message ?? string.Empty;
        ServiceId = serviceId ?? string.Empty;
    }

    /// <summary>Derived identifier (excluded from MessagePack) combining timestamp and service.</summary>
    [JsonIgnore]
    [IgnoreMember]
    public string Id => $"{Timestamp:yyyyMMddHHmmss}.{ServiceId}";

    /// <summary>
    /// Returns a compact JSON representation of the log event view model.
    /// </summary>
    public override string ToString() => JsonConvert.SerializeObject(this);
}
