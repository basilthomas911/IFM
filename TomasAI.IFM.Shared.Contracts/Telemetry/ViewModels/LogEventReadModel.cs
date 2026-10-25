using Newtonsoft.Json;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.Telemetry.ViewModels;

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
