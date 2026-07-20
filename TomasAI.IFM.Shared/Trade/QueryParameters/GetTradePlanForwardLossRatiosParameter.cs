using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.Trade.QueryParameters;

/// <summary>
/// Represents the parameters required to retrieve the trade plan forward loss ratios over a date range.
/// </summary>
[MessagePackObject(false)]
public record GetTradePlanForwardLossRatiosParameter : IActorEntityId, IQueryParameter
{
    [Key(0)] public DateOnly StartDate { get; init; }
    [Key(1)] public DateOnly EndDate { get; init; }

    [IgnoreMember]
    public string? QueryParams { get; private set; }

    public GetTradePlanForwardLossRatiosParameter() { }

    [SerializationConstructor]
    public GetTradePlanForwardLossRatiosParameter(DateOnly startDate, DateOnly endDate)
    {
        StartDate = startDate;
        EndDate = endDate;
        QueryParams = $"startDate={StartDate:yyyy-MM-dd}&endDate={EndDate:yyyy-MM-dd}";
    }

    public string Format()
        => $"{StartDate:yyyy-MM-dd}-{EndDate:yyyy-MM-dd}";
}
