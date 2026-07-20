using MessagePack;
using TomasAI.IFM.Shared.Reference.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.Reference.Queries;

/// <summary>
/// MessagePack-serializable query to retrieve all economic calendar entries.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public class GetEconomicCalendarAllQuery : IQuery<EconomicCalendarReadModel[]>
{
    [IgnoreMember] public const string Actor = "EconomicCalendarQuery";
    [IgnoreMember] public const string Verb = "GetEconomicCalendarAll";
    [IgnoreMember] public const int ErrorId = 1036;

    [Key(0)] public ActorSubject Subject { get; set; }
    [Key(1)] public IActorEntityId EntityId { get; set; }
    [IgnoreMember] public int ErrorCode { get; set; }
    [IgnoreMember] public string QueryParams { get; set; }

    public GetEconomicCalendarAllQuery()
    {
        ErrorCode = ErrorId;
        EntityId = ActorEntityId.Default;
    }

    [SerializationConstructor]
    public GetEconomicCalendarAllQuery(ActorSubject subject, IActorEntityId entityId)
    {
        Subject = subject;
        EntityId = entityId;
        ErrorCode = ErrorId;
    }
}
