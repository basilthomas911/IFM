namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>Explicit opt-in preserves existing calculation Function behavior.</summary>
public enum FunctionCompletionMode
{
    ProjectThenPersist = 0,
    AtomicBusinessAndEvent = 1,
    /// <summary>
    /// Commits the authoritative event first and treats projection as an independently recoverable
    /// observation. A projection failure is logged without replacing the committed Function result.
    /// </summary>
    EventThenProjection = 2
}
