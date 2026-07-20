using MessagePack;
using TomasAI.IFM.Shared.MarketDataAnalytics.QueryParameters;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.MarketDataAnalytics.Queries;

/// <summary>
/// MessagePack-serializable query to retrieve the RSI (Relative Strength Index) signal for a specific
/// futures contract on a given value date and signal type.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record GetFuturesRsiDailySignalQuery : IQuery<FuturesRsiSignalReadModel>
{
    [IgnoreMember] public const string Actor = "FuturesRsiSignalQuery";
    [IgnoreMember] public const string Verb = "GetFuturesRsiDailySignal";
    [IgnoreMember] public const int ErrorId = 1010;

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

    public GetFuturesRsiDailySignalQuery() { }

    public GetFuturesRsiDailySignalQuery(string contractId, TradeTimePeriodType timePeriod, int periodLength)
    {
        ContractId = contractId;
        TimePeriod = timePeriod;
        PeriodLength = periodLength;
        EntityId = new GetFuturesRsiDailySignalParameter(contractId, timePeriod, periodLength);
        ErrorCode = ErrorId;
    }

    /// <summary>
    /// MessagePack serialization constructor.
    /// </summary>
    [SerializationConstructor]
    public GetFuturesRsiDailySignalQuery(
        ActorSubject subject,             // Key(0)
        IActorEntityId entityId,          // Key(1)
        string contractId,               // Key(2)
        TradeTimePeriodType timePeriod,  // Key(4)
        int periodLength) // Key(5)
    {
        Subject = subject;
        EntityId = new GetFuturesRsiDailySignalParameter(contractId, timePeriod, periodLength);
        ContractId = contractId ?? string.Empty;
        TimePeriod = timePeriod;
        PeriodLength = periodLength;
        ErrorCode = ErrorId;
    }
}
