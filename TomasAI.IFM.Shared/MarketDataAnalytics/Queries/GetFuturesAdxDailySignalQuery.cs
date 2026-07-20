using MessagePack;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.MarketDataAnalytics.Queries;

/// <summary>
/// MessagePack-serializable query to retrieve the ADX (Average Directional Index) signal
/// for a specific futures contract on a given value date.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record GetFuturesAdxDailySignalQuery : IQuery<FuturesAdxSignalReadModel>
{
    [IgnoreMember] public const string Actor = "FuturesAdxSignalQuery";
    [IgnoreMember] public const string Verb = "GetFuturesAdxDailySignal";
    [IgnoreMember] public const int ErrorId = 1024;

    [Key(0)] public ActorSubject Subject { get; init; } 
    [Key(1)] public IActorEntityId EntityId { get; init; } 
    [IgnoreMember] public int ErrorCode { get; init; }
    [IgnoreMember] public string? QueryParams { get; init; }

    [Key(2)]
    public string ContractId { get; init; } 

    [Key(3)]
    public TradeTimePeriodType TimePeriod { get; init; }

    [Key(4)]
    public int PeriodLength { get; init; }
    
    public GetFuturesAdxDailySignalQuery()
    {
        ErrorCode = ErrorId;
    }

    public GetFuturesAdxDailySignalQuery(string contractId, TradeTimePeriodType timePeriod, int periodLength)
    {
        ContractId = contractId ?? string.Empty;
        TimePeriod = timePeriod;
        PeriodLength = periodLength;
        EntityId = new FuturesAdxDailySignalEntityId(contractId!, timePeriod, periodLength);
        ErrorCode = ErrorId;
    }

    /// <summary>
    /// MessagePack serialization constructor.
    /// </summary>
    [SerializationConstructor]
    public GetFuturesAdxDailySignalQuery(
        ActorSubject subject,
        IActorEntityId entityId,
        string contractId,
        TradeTimePeriodType timePeriod,
        int periodLength)
    {
        Subject = subject;
        ContractId = contractId ?? string.Empty;
        TimePeriod = timePeriod;
        PeriodLength = periodLength;
        EntityId = new FuturesAdxDailySignalEntityId(contractId!,  timePeriod, periodLength);
        ErrorCode = ErrorId;
    }
}
