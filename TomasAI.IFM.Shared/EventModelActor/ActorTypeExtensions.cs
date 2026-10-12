namespace TomasAI.IFM.Shared.EventModelActor;

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
