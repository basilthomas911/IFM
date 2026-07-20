using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.MarketData.QueryParameters;

/// <summary>
/// Represents the parameters required to retrieve Iron Condor market data.
/// </summary>
/// <remarks>Use this type to specify the contract identifiers, date range, and market context when requesting
/// Iron Condor market data. This record is typically used as a data transfer object in service or actor-based APIs.</remarks>
[MessagePackObject(false)]
public record GetIronCondorMarketDataParameter : IActorEntityId, IQueryParameter
{
    [Key(0)] public string UnderlyingContractId { get; init; } = string.Empty;
    [Key(1)] public string ShortPutOptionContractId { get; init; } = string.Empty;
    [Key(2)] public string LongPutOptionContractId { get; init; } = string.Empty;
    [Key(3)] public string ShortCallOptionContractId { get; init; } = string.Empty;
    [Key(4)] public string LongCallOptionContractId { get; init; } = string.Empty;
    [Key(5)] public DateOnly StartDate { get; init; }
    [Key(6)] public DateOnly EndDate { get; init; }
    [Key(7)] public MarketType MarketType { get; init; }
    [Key(8)] public CurrencyType CurrencyType { get; init; }

    [IgnoreMember]
    public string? QueryParams { get; private set; }

    public GetIronCondorMarketDataParameter() { }

    [SerializationConstructor]
    public GetIronCondorMarketDataParameter(
        string underlyingContractId,
        string shortPutOptionContractId,
        string longPutOptionContractId,
        string shortCallOptionContractId,
        string longCallOptionContractId,
        DateOnly startDate,
        DateOnly endDate,
        MarketType marketType,
        CurrencyType currencyType)
    {
        UnderlyingContractId = underlyingContractId ?? string.Empty;
        ShortPutOptionContractId = shortPutOptionContractId ?? string.Empty;
        LongPutOptionContractId = longPutOptionContractId ?? string.Empty;
        ShortCallOptionContractId = shortCallOptionContractId ?? string.Empty;
        LongCallOptionContractId = longCallOptionContractId ?? string.Empty;
        StartDate = startDate;
        EndDate = endDate;
        MarketType = marketType;
        CurrencyType = currencyType;

        QueryParams =
            $"underlyingContractId={UnderlyingContractId}" +
            $"&shortPutOptionContractId={ShortPutOptionContractId}" +
            $"&longPutOptionContractId={LongPutOptionContractId}" +
            $"&shortCallOptionContractId={ShortCallOptionContractId}" +
            $"&longCallOptionContractId={LongCallOptionContractId}" +
            $"&startDate={StartDate:yyyy-MM-dd}&endDate={EndDate:yyyy-MM-dd}" +
            $"&marketType={MarketType}&currencyType={CurrencyType}";
    }

    public string Format()
        => $"{UnderlyingContractId}.{ShortPutOptionContractId}.{LongPutOptionContractId}.{ShortCallOptionContractId}.{LongCallOptionContractId}.{StartDate:yyyy-MM-dd}.{EndDate:yyyy-MM-dd}.{MarketType}.{CurrencyType}";
}
