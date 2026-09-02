using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Application.Shared.Queries;

[MessagePackObject(AllowPrivate = true)]
public sealed record GetApplicationStartupStatusQuery : IQuery<ApplicationStartupStatus>
{
    [IgnoreMember] public const string Actor = "ApplicationQuery";
    [IgnoreMember] public const string Verb = "GetStartupStatus";
    [IgnoreMember] public const int ErrorId = 10021;
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; } = new ActorEntityId("current");
    [IgnoreMember] public int ErrorCode { get; init; } = ErrorId;
    [IgnoreMember] public string QueryParams { get; init; } = string.Empty;

    public GetApplicationStartupStatusQuery() { }

    [SerializationConstructor]
    public GetApplicationStartupStatusQuery(ActorSubject subject, IActorEntityId entityId)
    {
        Subject = subject;
        EntityId = entityId;
    }
}
