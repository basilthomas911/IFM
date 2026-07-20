namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>
/// Represents the type of actor in a system, categorized by its role or purpose.
/// </summary>
/// <remarks>This enumeration is commonly used to distinguish between different types of actors in a system, such
/// as those responsible for supervision, issuing commands, handling events, processing queries, or managing
/// notifications and feeds.</remarks>
public enum ActorType
{
    Default,
    Supervisor,
    Command,
    Event,
    Query,
    Notify, 
    UI
}

public static class ActorTypeExtensions
{
    public static string ToStringFast(this ActorType value) => value switch
    {
        ActorType.Default => nameof(ActorType.Default),
        ActorType.Supervisor => nameof(ActorType.Supervisor),
        ActorType.Command => nameof(ActorType.Command),
        ActorType.Event => nameof(ActorType.Event),
        ActorType.Query => nameof(ActorType.Query),
        ActorType.Notify => nameof(ActorType.Notify),
        ActorType.UI => nameof(ActorType.UI),
        _ => value.ToString()
    };

    /// <summary>
    /// Parses a <see cref="ReadOnlySpan{T}"/> into an <see cref="ActorType"/> without the
    /// dictionary lookup and allocation overhead of <see cref="Enum.Parse{TEnum}(ReadOnlySpan{char})"/>.
    /// </summary>
    public static ActorType ParseActorTypeFast(ReadOnlySpan<char> value) => value switch
    {
        "Default" => ActorType.Default,
        "Supervisor" => ActorType.Supervisor,
        "Command" => ActorType.Command,
        "Event" => ActorType.Event,
        "Query" => ActorType.Query,
        "Notify" => ActorType.Notify,
        "UI" => ActorType.UI,
        _ => Enum.Parse<ActorType>(value)
    };
}
