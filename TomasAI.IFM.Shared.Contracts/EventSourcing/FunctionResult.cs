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
            throw new ArgumentException("A Function result must contain exactly one completed or failed value.", nameof(completed));
        Completed = completed;
        Failed = failed;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1000", Justification = "The typed terminal-result factory must preserve the completed/failed generic reply contract.")]
    public static FunctionResult<TCompletedEvent, TFailedEvent> Complete(TCompletedEvent value)
        => new(value ?? throw new ArgumentNullException(nameof(value)), null);

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1000", Justification = "The typed terminal-result factory must preserve the completed/failed generic reply contract.")]
    public static FunctionResult<TCompletedEvent, TFailedEvent> Fail(TFailedEvent value)
        => new(null, value ?? throw new ArgumentNullException(nameof(value)));
}
