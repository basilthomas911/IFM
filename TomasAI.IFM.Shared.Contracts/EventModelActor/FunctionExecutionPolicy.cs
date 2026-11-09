namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>Local, typed execution policy returned by a mapped domain extension for one lifecycle stage.</summary>
/// <remarks>This is not an actor message. A null logical deadline leaves only an explicitly configured physical budget, if any.</remarks>
public sealed record FunctionExecutionPolicy
{
    /// <summary>Creates a policy with one clock and an optional absolute UTC deadline.</summary>
    public FunctionExecutionPolicy(TimeProvider clock, DateTime? deadlineUtc,
        FunctionCompletionMode completionMode = FunctionCompletionMode.ProjectThenPersist,
        TimeProvider? physicalClock = null, TimeSpan? physicalTimeout = null)
    {
        ArgumentNullException.ThrowIfNull(clock);
        if (deadlineUtc is { } at && (at == default || at.Kind != DateTimeKind.Utc))
            throw new ArgumentException("A Function deadline must be a non-default UTC instant.", nameof(deadlineUtc));
        Clock = clock;
        PhysicalClock = physicalClock ?? TimeProvider.System;
        if (physicalTimeout is { } duration && duration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(physicalTimeout));
        PhysicalTimeout = physicalTimeout;
        DeadlineUtc = deadlineUtc;
        if (!Enum.IsDefined(completionMode)) throw new ArgumentOutOfRangeException(nameof(completionMode));
        CompletionMode = completionMode;
    }

    /// <summary>Business clock used for logical timers and deadline comparisons.</summary>
    public TimeProvider Clock { get; }
    /// <summary>Independent monotonic clock guarding a stalled logical stage.</summary>
    public TimeProvider PhysicalClock { get; }
    /// <summary>Optional physical execution budget; otherwise the initial remaining logical budget is used.</summary>
    public TimeSpan? PhysicalTimeout { get; }
    /// <summary>Absolute deadline, or null when this stage intentionally has no time limit.</summary>
    public DateTime? DeadlineUtc { get; }
    /// <summary>Persistence policy; atomic completion requires an enlisted repository and no projector.</summary>
    public FunctionCompletionMode CompletionMode { get; }
}
