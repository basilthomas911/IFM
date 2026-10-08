using MessagePack;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Queries;
/// <summary>Requests GetScheduledTaskRunHistory through the persisted scheduled-task query route.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record GetScheduledTaskRunHistoryQuery : IQuery<ScheduledTaskRunPage>
{
    public const string Actor = "ScheduledTaskQuery";
    public const string Verb = "GetScheduledTaskRunHistory";
    [Key(0)] public ActorSubject Subject { get; init; } = default!;
    [Key(1)] public ScheduledTaskId EntityId { get; init; }
    [Key(2)] public string Environment { get; init; } = "Development";
    [Key(3)] public string HostId { get; init; } = "development";
    [Key(4)] public int PageSize { get; init; } = 50;
    [Key(5)] public ScheduledTaskId ScheduleId { get; init; } = default;
    [Key(6)] public byte[]? PagingState { get; init; }
    [IgnoreMember] IActorEntityId IQuery.EntityId => EntityId;
    [IgnoreMember] public int ErrorCode => 9300;
    [IgnoreMember] public string? QueryParams => null;
}
