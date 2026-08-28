namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>
/// Represents the type of actor in a system, categorized by its role or purpose.
/// </summary>
/// <remarks>This enumeration is commonly used to distinguish between different types of actors in a system, such
/// as those responsible for supervision, issuing commands, handling events, processing queries, or managing
/// notifications and feeds.</remarks>
public enum ActorType
{
    Unknown = 0,

    // Value 1 was Supervisor. It remains reserved so persisted ActorSubject
    // payloads cannot be reinterpreted as a different actor type.
    Command = 2,
    Event = 3,
    Query = 4,
    Notify = 5,

    // Value 6 was UI and is intentionally not reused.
    Realtime = 7,

    // Function actors are non-durable request/reply execution boundaries. Their
    // successful result may still be committed to an event-sourced state stream.
    Function = 8
}

/// <summary>
/// Identifies the single transport allowed for an actor subject namespace.
/// </summary>
public enum ActorDeliveryType
{
    Unknown = 0,
    NatsCore = 1,
    NatsJetStream = 2
}

public static class ActorTypeExtensions
{
    public static string ToStringFast(this ActorType value) => value switch
    {
        ActorType.Unknown => nameof(ActorType.Unknown),
        ActorType.Command => nameof(ActorType.Command),
        ActorType.Event => nameof(ActorType.Event),
        ActorType.Query => nameof(ActorType.Query),
        ActorType.Notify => nameof(ActorType.Notify),
        ActorType.Realtime => nameof(ActorType.Realtime),
        ActorType.Function => nameof(ActorType.Function),
        _ => value.ToString()
    };

    /// <summary>
    /// Returns the only transport permitted for the actor type.
    /// </summary>
    public static ActorDeliveryType GetDeliveryType(this ActorType value) => value switch
    {
        ActorType.Command or ActorType.Query or ActorType.Notify or ActorType.Realtime or ActorType.Function
            => ActorDeliveryType.NatsCore,
        ActorType.Event => ActorDeliveryType.NatsJetStream,
        ActorType.Unknown => ActorDeliveryType.Unknown,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown actor type.")
    };

    /// <summary>
    /// Rejects a subject whose actor type is not assigned to the expected transport.
    /// </summary>
    public static void EnsureDeliveryType(
        this ActorType value,
        ActorDeliveryType expected,
        string transportName)
    {
        var actual = value.GetDeliveryType();
        if (actual != expected)
        {
            throw new InvalidOperationException(
                $"Actor type '{value}' requires delivery '{actual}' and cannot use {transportName}.");
        }
    }

    /// <summary>
    /// Parses a <see cref="ReadOnlySpan{T}"/> into an <see cref="ActorType"/> without the
    /// dictionary lookup and allocation overhead of <see cref="Enum.Parse{TEnum}(ReadOnlySpan{char})"/>.
    /// </summary>
    public static ActorType ParseActorTypeFast(ReadOnlySpan<char> value) => value switch
    {
        "Unknown" => ActorType.Unknown,
        "Command" => ActorType.Command,
        "Event" => ActorType.Event,
        "Query" => ActorType.Query,
        "Notify" => ActorType.Notify,
        "Realtime" => ActorType.Realtime,
        "Function" => ActorType.Function,
        _ => Enum.Parse<ActorType>(value)
    };
}
