namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>
/// Represents the runtime state of an actor thread.
/// </summary>
public enum ActorThreadState
{
    /// <summary>
    /// The state is unknown or has not been initialized.
    /// </summary>
    Unknown,

    /// <summary>
    /// The thread is ready to be started.
    /// </summary>
    Ready,

    /// <summary>
    /// The thread has been started.
    /// </summary>
    Started,

    /// <summary>
    /// The thread is actively processing a message.
    /// </summary>
    ProcessingMessage,

    /// <summary>
    /// The thread is waiting for a message to arrive.
    /// </summary>
    WaitingForMessage,

    /// <summary>
    /// The thread has been stopped.
    /// </summary>
    Stopped,

    /// <summary>
    /// The thread has encountered an error and is faulted.
    /// </summary>
    Faulted,

    /// <summary>
    /// The thread has timed out while waiting or processing messages.
    /// </summary>
    TimedOut
}

public static class ActorThreadStateExtensions
{
    public static string ToStringFast(this ActorThreadState value) => value switch
    {
        ActorThreadState.Unknown => nameof(ActorThreadState.Unknown),
        ActorThreadState.Ready => nameof(ActorThreadState.Ready),
        ActorThreadState.Started => nameof(ActorThreadState.Started),
        ActorThreadState.ProcessingMessage => nameof(ActorThreadState.ProcessingMessage),
        ActorThreadState.WaitingForMessage => nameof(ActorThreadState.WaitingForMessage),
        ActorThreadState.Stopped => nameof(ActorThreadState.Stopped),
        ActorThreadState.Faulted => nameof(ActorThreadState.Faulted),
        ActorThreadState.TimedOut => nameof(ActorThreadState.TimedOut),
        _ => value.ToString()
    };
}
