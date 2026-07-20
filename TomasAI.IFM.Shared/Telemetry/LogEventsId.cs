using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.Telemetry;

/// <summary>
/// MessagePack-serializable identifier for a batch of telemetry log events, composed of the log event timestamp.
/// </summary>
/// <remarks>
/// Implements <see cref="IActorEntityId"/>. The formatted key uses dot-separated components; with a single
/// component it resolves to "yyyyMMddHHmmss".
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record LogEventsId(
    /// <summary>The timestamp representing the log event batch.</summary>
    [property: Key(0)] DateTime LogEventDate) : IActorEntityId
{
    /// <summary>
    /// Parameterless constructor for serializers; initializes to current UTC timestamp.
    /// </summary>
    public LogEventsId() : this(DateTime.UtcNow) { }

    /// <summary>
    /// Formats the identifier as a dot-separated key.
    /// </summary>
    /// <returns>Formatted string with the timestamp in yyyyMMddHHmmss.</returns>
    public string Format() => string.Create(null, stackalloc char[48], $"{LogEventDate:yyyyMMddHHmmss}");
}
