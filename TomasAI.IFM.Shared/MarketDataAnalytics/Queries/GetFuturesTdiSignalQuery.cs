using MessagePack;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.MarketDataAnalytics.Queries;

/// <summary>
/// MessagePack-serializable query to retrieve the TDI (Trend Direction / Divergence Index) signal
/// for a specific futures contract on a given value date.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record GetFuturesTdiSignalQuery : IQuery<FuturesTdiSignalReadModel>
{
    [IgnoreMember] public const string Actor = "FuturesTdiSignalQuery";
    [IgnoreMember] public const string Verb = "GetFuturesTdiSignal";
    [IgnoreMember] public const int ErrorId = 1021;

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

    public GetFuturesTdiSignalQuery() { }

    public GetFuturesTdiSignalQuery(string contractId, DateOnly valueDate, TradeTimePeriodType timePeriod = default)
    {
        ContractId = contractId ?? string.Empty;
        ValueDate = valueDate;
        TimePeriod = timePeriod;
        EntityId = new FuturesTdiSignalEntityId(contractId, valueDate, timePeriod);
        ErrorCode = ErrorId;
    }

    /// <summary>
    /// MessagePack serialization constructor.
    /// </summary>
    [SerializationConstructor]
    public GetFuturesTdiSignalQuery(
        ActorSubject subject,              // Key(0)
        IActorEntityId entityId,           // Key(1)
        string contractId,                 // Key(2)
        DateOnly valueDate,                // Key(3)
        TradeTimePeriodType timePeriod)  // Key(4)
    {
        Subject = subject;
        ContractId = contractId ?? string.Empty;
        ValueDate = valueDate;
        TimePeriod = timePeriod;
        EntityId = new FuturesTdiSignalEntityId(contractId, valueDate, timePeriod);
        ErrorCode = ErrorId;
    }
}
