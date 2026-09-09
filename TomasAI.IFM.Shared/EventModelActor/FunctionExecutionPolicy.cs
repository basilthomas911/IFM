namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>Local, typed execution policy returned by a mapped domain extension for one lifecycle stage.</summary>
/// <remarks>This is not an actor message. A null deadline explicitly leaves that stage unbounded.</remarks>
public sealed record FunctionExecutionPolicy
{
    /// <summary>Creates a policy with one clock and an optional absolute UTC deadline.</summary>
    public FunctionExecutionPolicy(TimeProvider clock, DateTime? deadlineUtc,
        FunctionCompletionMode completionMode = FunctionCompletionMode.ProjectThenPersist)
    {
        ArgumentNullException.ThrowIfNull(clock);
        if (deadlineUtc is { } at && (at == default || at.Kind != DateTimeKind.Utc))
            throw new ArgumentException("A Function deadline must be a non-default UTC instant.", nameof(deadlineUtc));
        Clock = clock;
        DeadlineUtc = deadlineUtc;
        if (!Enum.IsDefined(completionMode)) throw new ArgumentOutOfRangeException(nameof(completionMode));
        CompletionMode = completionMode;
    }

    /// <summary>Clock used for both timers and deadline comparisons.</summary>
    public TimeProvider Clock { get; }
    /// <summary>Absolute deadline, or null when this stage intentionally has no time limit.</summary>
    public DateTime? DeadlineUtc { get; }
    /// <summary>Persistence policy; atomic completion requires an enlisted repository and no projector.</summary>
    public FunctionCompletionMode CompletionMode { get; }
}

/// <summary>Explicit opt-in preserves existing calculation Function behavior.</summary>
public enum FunctionCompletionMode
{
    ProjectThenPersist = 0,
    AtomicBusinessAndEvent = 1
}
