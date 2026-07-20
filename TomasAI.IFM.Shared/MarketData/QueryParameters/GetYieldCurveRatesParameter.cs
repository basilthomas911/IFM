using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.MarketData.QueryParameters;

/// <summary>
/// Represents the parameters required to retrieve yield curve rates for a specific date range.
/// </summary>
/// <remarks>Use this type to specify the start and end dates when requesting yield curve rates.
/// This record is typically used as a data transfer object in service or actor-based APIs.</remarks>
[MessagePackObject(false)]
public record GetYieldCurveRatesParameter : IActorEntityId, IQueryParameter
{
    [Key(0)] public DateOnly StartDate { get; init; }
    [Key(1)] public DateOnly EndDate { get; init; }

    [IgnoreMember]
    public string? QueryParams { get; private set; }

    public GetYieldCurveRatesParameter() { }

    [SerializationConstructor]
    public GetYieldCurveRatesParameter(DateOnly startDate, DateOnly endDate)
    {
        StartDate = startDate;
        EndDate = endDate;
        QueryParams = $"startDate={StartDate:yyyy-MM-dd}&endDate={EndDate:yyyy-MM-dd}";
    }

    public string Format()
        => $"{StartDate:yyyy-MM-dd}.{EndDate:yyyy-MM-dd}";
}
