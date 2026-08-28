using MessagePack;

namespace TomasAI.IFM.Shared.EventSourcing;

/// <summary>Typed Function reply containing exactly one completed or failed terminal value.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed class FunctionResult<TCompletedEvent, TFailedEvent>
    where TCompletedEvent : class
    where TFailedEvent : class
{
    [Key(0)] public TCompletedEvent? Completed { get; init; }
    [Key(1)] public TFailedEvent? Failed { get; init; }
    [IgnoreMember] public bool IsCompleted => Completed is not null && Failed is null;
    [IgnoreMember] public bool IsFailed => Failed is not null && Completed is null;
    [IgnoreMember] public bool IsTerminal => IsCompleted || IsFailed;

    public FunctionResult() { }

    [SerializationConstructor]
    public FunctionResult(TCompletedEvent? completed, TFailedEvent? failed)
    {
        if ((completed is null) == (failed is null))
            throw new ArgumentException("A Function result must contain exactly one completed or failed value.");
        Completed = completed;
        Failed = failed;
    }

    public static FunctionResult<TCompletedEvent, TFailedEvent> Complete(TCompletedEvent value)
        => new(value ?? throw new ArgumentNullException(nameof(value)), null);

    public static FunctionResult<TCompletedEvent, TFailedEvent> Fail(TFailedEvent value)
        => new(null, value ?? throw new ArgumentNullException(nameof(value)));
}

/// <summary>Identifies the Function lifecycle stage that produced a failed response.</summary>
public enum FunctionFailureStage : byte
{
    Unknown = 0,
    Parsing = 1,
    Validation = 2,
    Loading = 3,
    Execution = 4,
    Projection = 5,
    Persistence = 6
}
