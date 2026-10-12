namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>Allows the supervisor to retire a disposable actor without invoking draining shutdown hooks.</summary>
public interface IReplaceableRealtimeActor
{
    /// <summary>Gets the cancellation and fencing authority for this instance.</summary>
    RealtimeActorGeneration RealtimeGeneration { get; }
    /// <summary>Retires this instance; its context is never reused by its replacement.</summary>
    void RetireRealtimeGeneration();
}
