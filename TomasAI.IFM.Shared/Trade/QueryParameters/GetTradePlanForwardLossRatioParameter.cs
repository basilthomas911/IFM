using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.Trade.QueryParameters;

/// <summary>
/// Represents the parameters required to retrieve the trade plan forward loss ratio.
/// </summary>
[MessagePackObject(false)]
public record GetTradePlanForwardLossRatioParameter : IActorEntityId, IQueryParameter
{
    [Key(0)] public DateOnly ValueDate { get; init; }

    [IgnoreMember]
    public string? QueryParams { get; private set; }

    public GetTradePlanForwardLossRatioParameter() { }

    [SerializationConstructor]
    public GetTradePlanForwardLossRatioParameter(DateOnly valueDate)
    {
        ValueDate = valueDate;
        QueryParams = $"valueDate={ValueDate:yyyy-MM-dd}";
    }

    public string Format()
        => $"{ValueDate:yyyy-MM-dd}";
}
