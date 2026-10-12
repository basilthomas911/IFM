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
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0016", Justification = "Preserve the concrete collection type in the existing configuration/message/actor listener contract.")]
    [Key(3)]
    [JsonProperty("Properties")]
    public Dictionary<string, string> Properties { get; init; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Formats the log event identifier as "Timestamp.Level".
    /// </summary>
    public string Format() => string.Create(null, stackalloc char[64], $"{Timestamp:yyyyMMddHHmmss}.{Level}");
}
