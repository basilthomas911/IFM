using MessagePack;
namespace TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
/// <summary>Contains a page of retained stdout and its byte continuation; missing artifacts are explicit.</summary>
[MessagePackObject]
public sealed record ScheduledTaskOutputPage
{
    [Key(0)] public string Text { get; init; } = "";
    [Key(1)] public long NextOffset { get; init; }
    [Key(2)] public bool EndOfOutput { get; init; }
    [Key(3)] public bool Available { get; init; }
}
