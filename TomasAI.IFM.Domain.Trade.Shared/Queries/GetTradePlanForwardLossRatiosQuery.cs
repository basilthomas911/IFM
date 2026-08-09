using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared.QueryParameters;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Queries;

[MessagePackObject(AllowPrivate = true)]
public record GetTradePlanForwardLossRatiosQuery : IQuery<TradePlanForwardLossRatioReadModel[]>
{
    [IgnoreMember] public const string Actor = "TradePlanQuery";
    [IgnoreMember] public const string Verb = "GetTradePlanForwardLossRatios";
    [IgnoreMember] public const int ErrorId = 1030;

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; }
    [IgnoreMember] public int ErrorCode { get; init; }
    [IgnoreMember] public string? QueryParams { get; init; }

    [Key(2)]
    public DateOnly StartDate { get; init; }

    [Key(3)]
    public DateOnly EndDate { get; init; }

    public GetTradePlanForwardLossRatiosQuery() { }

    public GetTradePlanForwardLossRatiosQuery(DateOnly startDate, DateOnly endDate)
    {
        StartDate = startDate;
        EndDate = endDate;
        EntityId = new GetTradePlanForwardLossRatiosParameter(startDate, endDate);
        ErrorCode = ErrorId;
    }

    [SerializationConstructor]
    public GetTradePlanForwardLossRatiosQuery(
        ActorSubject subject,
        IActorEntityId entityId,
        DateOnly startDate,
        DateOnly endDate)
    {
        Subject = subject;
        EntityId = new GetTradePlanForwardLossRatiosParameter(startDate, endDate);
        StartDate = startDate;
        EndDate = endDate;
        ErrorCode = ErrorId;
    }
}
