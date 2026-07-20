using System;
using MessagePack;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed.QueryParameters;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.MarketDataFeed.Queries;

/// <summary>
/// MessagePack-serializable query to retrieve futures option spread data based on specified parameters.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public class GetFuturesOptionSpreadDataQuery : IQuery<FuturesOptionSpreadDataReadModel>
{
    [IgnoreMember] public const string Actor = "MarketDataFeedQuery";
    [IgnoreMember] public const string Verb = "GetFuturesOptionSpreadData";
    [IgnoreMember] public const int ErrorId = 1023;

    [Key(0)] public ActorSubject Subject { get; set; }
    [Key(1)] public IActorEntityId EntityId { get; set; }
    [IgnoreMember] public int ErrorCode { get; set; }
    [IgnoreMember] public string? QueryParams { get; set; }

    [Key(2)]
    public DateOnly ValueDate { get; set; }

    [Key(3)]
    public DateOnly MaturityDate { get; set; }

    [Key(4)]
    public double AssetPrice { get; set; }

    [Key(5)]
    public double RiskFreeRate { get; set; }

    [Key(6)]
    public double TimeValue { get; set; }

    [Key(7)]
    public FuturesOptionContractsReadModel OptionContracts { get; set; }

    public GetFuturesOptionSpreadDataQuery() { }

    public GetFuturesOptionSpreadDataQuery(
        DateOnly valueDate,
        DateOnly maturityDate,
        double assetPrice,
        double riskFreeRate,
        double timeValue,
        FuturesOptionContractsReadModel queryForOptionContracts)
    {
        ValueDate = valueDate;
        MaturityDate = maturityDate;
        AssetPrice = assetPrice;
        RiskFreeRate = riskFreeRate;
        TimeValue = timeValue;
        OptionContracts = queryForOptionContracts;
        EntityId = new GetFuturesOptionSpreadDataParameter(valueDate, maturityDate, assetPrice, riskFreeRate, timeValue, queryForOptionContracts);
        ErrorCode = ErrorId;
    }

    /// <summary>
    /// MessagePack serialization constructor.
    /// </summary>
    [SerializationConstructor]
    public GetFuturesOptionSpreadDataQuery(
        ActorSubject subject,                           // Key(0)
        IActorEntityId entityId,                        // Key(1)
        DateOnly valueDate,                             // Key(2)
        DateOnly maturityDate,                          // Key(3)
        double assetPrice,                              // Key(4)
        double riskFreeRate,                            // Key(5)
        double timeValue,                               // Key(6)
        FuturesOptionContractsReadModel optionContracts) // Key(7)
    {
        Subject = subject;
        EntityId = new GetFuturesOptionSpreadDataParameter(valueDate, maturityDate, assetPrice, riskFreeRate, timeValue, optionContracts);
        ValueDate = valueDate;
        MaturityDate = maturityDate;
        AssetPrice = assetPrice;
        RiskFreeRate = riskFreeRate;
        TimeValue = timeValue;
        OptionContracts = optionContracts;
        ErrorCode = ErrorId;
    }

    [IgnoreMember]
    public FuturesOptionContractReadModel QueryForShortOptionContract => OptionContracts.Contracts[0];
    [IgnoreMember]
    public FuturesOptionContractReadModel QueryForLongOptionContract => OptionContracts.Contracts[1];
}
