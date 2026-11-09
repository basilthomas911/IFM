using TomasAI.IFM.Application.EventSourcing;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.EventModelActor;

/// <summary>Owns immutable event bytes and restores independent request-local Function state.</summary>
public sealed class ResidentFunctionCheckpoint<TRequest, TState>
{
    readonly Func<TRequest, TState> restore;

    ResidentFunctionCheckpoint(Func<TRequest, TState> restore, int retainedBytes)
        => (this.restore, RetainedBytes) = (restore, retainedBytes);

    public int RetainedBytes { get; }

    public TState Restore(TRequest request) => restore(request);

    /// <summary>Captures a typed full-state fact without retaining mutable request state or caller-owned event objects.</summary>
    public static ResidentFunctionCheckpoint<TRequest, TState> Capture<TEvent>(TEvent source, long streamVersion,
        Func<TRequest, TEvent, long, TState> restoreState) where TEvent : class, IEvent
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(restoreState);
        var bytes = EventLogMessagePackCodec.Shared.Serialize(source);
        var eventId = source.EventId;
        return new(request => restoreState(request,
            EventLogMessagePackCodec.Shared.Deserialize<TEvent>(eventId, bytes), streamVersion), bytes.Length);
    }
}
