using MessagePack;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.MarketDataAnalytics.Queries;

/// <summary>
/// MessagePack-serializable query to retrieve the ATR (Average True Range) signal
/// for a specific futures contract on a given value date.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record GetFuturesAtrDailySignalQuery : IQuery<FuturesAtrSignalReadModel>
{
    [IgnoreMember] public const string Actor = "FuturesAtrSignalQuery";
    [IgnoreMember] public const string Verb = "GetFuturesAtrDailySignal";
    [IgnoreMember] public const int ErrorId = 1023;

    [Key(0)] public ActorSubject Subject { get; init; } 
    [Key(1)] public IActorEntityId EntityId { get; init; } 
    [IgnoreMember] public int ErrorCode { get; init; }
    [IgnoreMember] public string? QueryParams { get; init; }

    [Key(2)]
    public string ContractId { get; init; } 

    [Key(3)]
    public TradeTimePeriodType TimePeriod { get; init; }
    
    [Key(4)]
    public int  PeriodLength { get; init; }

    public GetFuturesAtrDailySignalQuery()
    {
        ErrorCode = ErrorId;
    }

    /// <summary>
    /// Convenience constructor for API endpoints.
    /// </summary>
    public GetFuturesAtrDailySignalQuery(string contractId, TradeTimePeriodType timePeriod, int periodLength)
    {
        ContractId = contractId;
        TimePeriod = timePeriod;
        PeriodLength = periodLength;
        EntityId = new FuturesAtrDailySignalEntityId(contractId, timePeriod, periodLength);
        ErrorCode = ErrorId;
    }

    /// <summary>
    /// MessagePack serialization constructor.
    /// </summary>
    [SerializationConstructor]
    public GetFuturesAtrDailySignalQuery(
        ActorSubject subject,
        IActorEntityId entityId,
        string contractId,
        TradeTimePeriodType timePeriod,
        int periodLength)
    {
        Subject = subject;
        ContractId = contractId;
        TimePeriod = timePeriod;
        PeriodLength = periodLength;
        EntityId = new FuturesAtrDailySignalEntityId(contractId, timePeriod, periodLength);
        ErrorCode = ErrorId;
    }
}
