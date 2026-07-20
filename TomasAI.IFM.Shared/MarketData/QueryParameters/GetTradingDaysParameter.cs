using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.MarketData.QueryParameters;

/// <summary>
/// Represents the parameters required to retrieve the number of trading days within a specified date range.
/// </summary>
[MessagePackObject(false)]
public record GetTradingDaysParameter : IActorEntityId, IQueryParameter
{
    [Key(0)] public DateOnly StartDate { get; init; }

    [Key(1)] public DateOnly EndDate { get; init; }

    [Key(2)] public MarketType MarketType { get; init; }

    [Key(3)] public CurrencyType CurrencyType { get; init; }

    [IgnoreMember]
    public string? QueryParams { get; private set; }

    public GetTradingDaysParameter() { }

    [SerializationConstructor]
    public GetTradingDaysParameter(DateOnly startDate, DateOnly endDate, MarketType marketType, CurrencyType currencyType)
    {
        StartDate = startDate;
        EndDate = endDate;
        MarketType = marketType;
        CurrencyType = currencyType;
        QueryParams = $"startDate={StartDate:yyyy-MM-dd}&endDate={EndDate:yyyy-MM-dd}&marketType={MarketType}&currencyType={CurrencyType}";
    }

    public string Format()
        => $"{StartDate:yyyy-MM-dd}.{EndDate:yyyy-MM-dd}.{MarketType}.{CurrencyType}";
}
