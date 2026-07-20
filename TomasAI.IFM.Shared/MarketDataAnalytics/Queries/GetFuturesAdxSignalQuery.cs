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
public record GetFuturesAdxSignalQuery : IQuery<FuturesAdxSignalReadModel>
{
    [IgnoreMember] public const string Actor = "FuturesAdxSignalQuery";
    [IgnoreMember] public const string Verb = "GetFuturesAdxSignal";
    [IgnoreMember] public const int ErrorId = 1024;

    [Key(0)] public ActorSubject Subject { get; init; } 
    [Key(1)] public IActorEntityId EntityId { get; init; } 
    [IgnoreMember] public int ErrorCode { get; init; }
    [IgnoreMember] public string? QueryParams { get; init; }

    [Key(2)]
    public string ContractId { get; init; } 

    [Key(3)]
    public DateOnly ValueDate { get; init; }

    [Key(4)]
    public TradeTimePeriodType TimePeriod { get; init; }

    [Key(5)]
    public int PeriodLength { get; init; }
    
    public GetFuturesAdxSignalQuery()
    {
        ErrorCode = ErrorId;
    }

    public GetFuturesAdxSignalQuery(string contractId, DateOnly valueDate, TradeTimePeriodType timePeriod, int periodLength)
    {
        ContractId = contractId ?? string.Empty;
        ValueDate = valueDate;
        TimePeriod = timePeriod;
        PeriodLength = periodLength;
        EntityId = new FuturesAdxSignalEntityId(contractId!, valueDate, timePeriod, periodLength);
        ErrorCode = ErrorId;
    }

    /// <summary>
    /// MessagePack serialization constructor.
    /// </summary>
    [SerializationConstructor]
    public GetFuturesAdxSignalQuery(
        ActorSubject subject,
        IActorEntityId entityId,
        string contractId,
        DateOnly valueDate,
        TradeTimePeriodType timePeriod,
        int periodLength)
    {
        Subject = subject;
        ContractId = contractId ?? string.Empty;
        ValueDate = valueDate;
        TimePeriod = timePeriod;
        PeriodLength = periodLength;
        EntityId = new FuturesAdxSignalEntityId(contractId!, valueDate, timePeriod, periodLength);
        ErrorCode = ErrorId;
    }
}
