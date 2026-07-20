using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.Reference;

/// <summary>
/// MessagePack-serializable identifier for an economic calendar event, composed of event date, country code, and event name.
/// </summary>
/// <remarks>
/// Implements <see cref="IActorEntityId"/>. The formatted key uses dot notation:
/// "EventDate.CountryCode.EventName" where EventDate is yyyyMMddHHmmss.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record EconomicCalendarId(
    /// <summary>The UTC date and time of the economic calendar event.</summary>
    [property: Key(0)] DateTime EventDate,
    /// <summary>ISO country code associated with the event.</summary>
    [property: Key(1)] string CountryCode,
    /// <summary>Descriptive name of the economic calendar event.</summary>
    [property: Key(2)] string EventName) : IActorEntityId
{
    /// <summary>
    /// Parameterless constructor for serializers; initializes to defaults.
    /// </summary>
    public EconomicCalendarId() : this(default, string.Empty, string.Empty) { }

    /// <summary>
    /// Formats the identifier as a dot-separated string: "EventDate.CountryCode.EventName".
    /// </summary>
    /// <returns>Formatted string with EventDate in yyyyMMddHHmmss format.</returns>
    public string Format()
        => $"{EventDate:yyyyMMddHHmmss}.{CountryCode}.{EventName?.Replace(" ", "-")}";

    /// <summary>
    /// Returns a human-readable string representation of the identifier.
    /// </summary>
    /// <returns>Formatted string "yyyy-MM-dd hh:mm:ss tt CC = Name".</returns>
    public override string ToString() => $"{EventDate:yyyy-MM-dd hh:mm:ss tt} {CountryCode} = {EventName}";
}