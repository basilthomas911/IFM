namespace TomasAI.IFM.Shared.EventModelActor;

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
