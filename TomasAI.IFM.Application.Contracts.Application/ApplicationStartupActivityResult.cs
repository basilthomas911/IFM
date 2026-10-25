using MessagePack;

namespace TomasAI.IFM.Domain.Application.Shared;

/// <summary>Typed, bounded terminal result for one startup activity.</summary>
[MessagePackObject]
public sealed record ApplicationStartupActivityResult
{
    [Key(0)] public ApplicationStartupActivity Activity { get; init; }
    [Key(1)] public ApplicationStartupActivityOutcome Outcome { get; init; }
    [Key(2)] public bool Required { get; init; }
    [Key(3)] public DateTime StartedAtUtc { get; init; }
    [Key(4)] public DateTime CompletedAtUtc { get; init; }
    [Key(5)] public int ErrorCode { get; init; }
    [Key(6)] public string Reason { get; init; } = string.Empty;

    [IgnoreMember]
    public bool IsSatisfied => Outcome is ApplicationStartupActivityOutcome.Started
        or ApplicationStartupActivityOutcome.AlreadySatisfied
        or ApplicationStartupActivityOutcome.ScheduledStopped;
}
