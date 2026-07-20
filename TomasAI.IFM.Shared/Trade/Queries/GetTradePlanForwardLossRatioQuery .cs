using MessagePack;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.Trade.QueryParameters;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.Trade.Queries;

/// <summary>
/// Represents a query to retrieve the forward loss ratio for a trade plan as of a specific value date.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record GetTradePlanForwardLossRatioQuery : IQuery<TradePlanForwardLossRatioReadModel>
{
    [IgnoreMember] public const string Actor = "TradePlanForwardLossRatioQuery";
    [IgnoreMember] public const string Verb = "GetTradePlanForwardLossRatio";
    [IgnoreMember] public const int ErrorId = 1030;

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; }
    [IgnoreMember] public int ErrorCode { get; init; }
    [IgnoreMember] public string? QueryParams { get; init; }

    [Key(2)]
    public DateOnly ValueDate { get; init; }

    public GetTradePlanForwardLossRatioQuery() { }

    public GetTradePlanForwardLossRatioQuery(DateOnly valueDate)
    {
        ValueDate = valueDate;
        EntityId = new GetTradePlanForwardLossRatioParameter(valueDate);
        ErrorCode = ErrorId;
    }

    [SerializationConstructor]
    public GetTradePlanForwardLossRatioQuery(
        ActorSubject subject,       // Key(0)
        IActorEntityId entityId,    // Key(1)
        DateOnly valueDate)         // Key(2)
    {
        Subject = subject;
        EntityId = new GetTradePlanForwardLossRatioParameter(valueDate);
        ValueDate = valueDate;
        ErrorCode = ErrorId;
    }
}
