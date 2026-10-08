using MessagePack;
namespace TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
/// <summary>Contains a bounded page of persisted occurrences with an opaque Scylla continuation.</summary>
[MessagePackObject]
public sealed record ScheduledTaskRunPage
{
    [Key(0)] public ScheduledTaskRun[] Runs { get; init; } = [];
    [Key(1)] public byte[]? PagingState { get; init; }
}
/// <summary>Contains a page of retained stdout and its byte continuation; missing artifacts are explicit.</summary>
[MessagePackObject]
public sealed record ScheduledTaskOutputPage
{
    [Key(0)] public string Text { get; init; } = "";
    [Key(1)] public long NextOffset { get; init; }
    [Key(2)] public bool EndOfOutput { get; init; }
    [Key(3)] public bool Available { get; init; }
}
/// <summary>Reads only retained output under a configured host artifact root.</summary>
public interface IScheduledTaskOutputReader
{
    /// <summary>Reads bounded UTF-8 output using the persisted run's relative artifact identity.</summary>
    ValueTask<ScheduledTaskOutputPage> ReadAsync(string relativeDirectory, long offset, CancellationToken cancellationToken);
}
