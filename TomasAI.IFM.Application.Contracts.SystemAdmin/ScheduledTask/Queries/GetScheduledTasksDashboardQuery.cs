using MessagePack;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Queries;
/// <summary>Requests GetScheduledTasksDashboard through the persisted scheduled-task query route.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record GetScheduledTasksDashboardQuery : IQuery<ScheduledTasksDashboard>
{
    public const string Actor = "ScheduledTaskQuery";
    public const string Verb = "GetScheduledTasksDashboard";
    [Key(0)] public ActorSubject Subject { get; init; } = default!;
    [Key(1)] public ScheduledTaskId EntityId { get; init; }
    [Key(2)] public string Environment { get; init; } = "Development";
    [Key(3)] public string HostId { get; init; } = "development";
    [Key(4)] public int PageSize { get; init; } = 50;
    [Key(5)] public ScheduledTaskId SelectedScheduleId { get; init; } = default;
    [IgnoreMember] IActorEntityId IQuery.EntityId => EntityId;
    [IgnoreMember] public int ErrorCode => 9300;
    [IgnoreMember] public string? QueryParams => null;
}
