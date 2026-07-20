using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketData.ViewModels;

namespace TomasAI.IFM.Shared.MarketDataFeed.QueryParameters;

/// <summary>
/// Represents the parameters required to retrieve futures option spread data based on specified parameters.
/// </summary>
[MessagePackObject(false)]
public record GetFuturesOptionSpreadDataParameter : IActorEntityId, IQueryParameter
{
    [Key(0)] public DateOnly ValueDate { get; init; }
    [Key(1)] public DateOnly MaturityDate { get; init; }
    [Key(2)] public double AssetPrice { get; init; }
    [Key(3)] public double RiskFreeRate { get; init; }
    [Key(4)] public double TimeValue { get; init; }
    [Key(5)] public FuturesOptionContractsReadModel? QueryForOptionContracts { get; init; }

    [IgnoreMember]
    public string? QueryParams { get; private set; }

    public GetFuturesOptionSpreadDataParameter() { }

    [SerializationConstructor]
    public GetFuturesOptionSpreadDataParameter(
        DateOnly valueDate,
        DateOnly maturityDate,
        double assetPrice,
        double riskFreeRate,
        double timeValue,
        FuturesOptionContractsReadModel? queryForOptionContracts)
    {
        ValueDate = valueDate;
        MaturityDate = maturityDate;
        AssetPrice = assetPrice;
        RiskFreeRate = riskFreeRate;
        TimeValue = timeValue;
        QueryForOptionContracts = queryForOptionContracts;
        QueryParams = $"valueDate={ValueDate:yyyy-MM-dd}&maturityDate={MaturityDate:yyyy-MM-dd}&assetPrice={AssetPrice}&riskFreeRate={RiskFreeRate}&timeValue={TimeValue}";
    }

    public string Format()
        => $"{ValueDate:yyyy-MM-dd}.{MaturityDate:yyyy-MM-dd}.{AssetPrice}.{RiskFreeRate}.{TimeValue}";
}
