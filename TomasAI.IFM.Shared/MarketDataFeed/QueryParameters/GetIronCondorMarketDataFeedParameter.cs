using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.MarketDataFeed.QueryParameters;

/// <summary>
/// Represents the parameters required to retrieve market data for an Iron Condor options strategy.
/// </summary>
[MessagePackObject(false)]
public record GetIronCondorMarketDataFeedParameter : IActorEntityId, IQueryParameter
{
    [Key(0)] public string UnderlyingContractId { get; init; } = string.Empty;
    [Key(1)] public string ShortPutOptionContractId { get; init; } = string.Empty;
    [Key(2)] public string LongPutOptionContractId { get; init; } = string.Empty;
    [Key(3)] public string ShortCallOptionContractId { get; init; } = string.Empty;
    [Key(4)] public string LongCallOptionContractId { get; init; } = string.Empty;
    [Key(5)] public DateOnly ValueDate { get; init; }

    [IgnoreMember]
    public string? QueryParams { get; private set; }

    public GetIronCondorMarketDataFeedParameter() { }

    [SerializationConstructor]
    public GetIronCondorMarketDataFeedParameter(
        string underlyingContractId,
        string shortPutOptionContractId,
        string longPutOptionContractId,
        string shortCallOptionContractId,
        string longCallOptionContractId,
        DateOnly valueDate)
    {
        UnderlyingContractId = underlyingContractId ?? string.Empty;
        ShortPutOptionContractId = shortPutOptionContractId ?? string.Empty;
        LongPutOptionContractId = longPutOptionContractId ?? string.Empty;
        ShortCallOptionContractId = shortCallOptionContractId ?? string.Empty;
        LongCallOptionContractId = longCallOptionContractId ?? string.Empty;
        ValueDate = valueDate;
        QueryParams = $"underlyingContractId={UnderlyingContractId}&shortPutOptionContractId={ShortPutOptionContractId}&longPutOptionContractId={LongPutOptionContractId}&shortCallOptionContractId={ShortCallOptionContractId}&longCallOptionContractId={LongCallOptionContractId}&valueDate={ValueDate:yyyy-MM-dd}";
    }

    public string Format()
        => $"{UnderlyingContractId}.{ShortPutOptionContractId}.{LongPutOptionContractId}.{ShortCallOptionContractId}.{LongCallOptionContractId}.{ValueDate:yyyy-MM-dd}";
}
