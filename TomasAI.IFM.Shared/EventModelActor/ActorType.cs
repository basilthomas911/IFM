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
