using MessagePack;
using TomasAI.IFM.Domain.MarketData.Shared.QueryParameters;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Shared.Queries;

[MessagePackObject(AllowPrivate = true)]
public sealed class GetEconomicCalendarPageQuery : IQuery<EconomicCalendarPageReadModel>
{
    [IgnoreMember] public const string Actor = "EconomicCalendarQuery";
    [IgnoreMember] public const string Verb = "GetEconomicCalendarPage";
    [IgnoreMember] public const int ErrorId = 1042;

    [Key(0)] public ActorSubject Subject { get; set; }
    [Key(1)] public IActorEntityId EntityId { get; set; }
    [Key(2)] public EconomicCalendarPageRequest Request { get; init; } = new();
    [IgnoreMember] public int ErrorCode { get; set; } = ErrorId;
    [IgnoreMember] public string? QueryParams => null;

    public GetEconomicCalendarPageQuery()
        => EntityId = new GetEconomicCalendarPageParameter(Request);

    public GetEconomicCalendarPageQuery(EconomicCalendarPageRequest request)
    {
        Request = request ?? throw new ArgumentNullException(nameof(request));
        EntityId = new GetEconomicCalendarPageParameter(request);
    }

    [SerializationConstructor]
    public GetEconomicCalendarPageQuery(ActorSubject subject, IActorEntityId entityId, EconomicCalendarPageRequest request)
    {
        Subject = subject;
        Request = request ?? throw new ArgumentNullException(nameof(request));
        EntityId = entityId ?? new GetEconomicCalendarPageParameter(request);
    }
}
