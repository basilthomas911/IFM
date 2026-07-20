using MessagePack;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;
using TomasAI.IFM.Shared.MarketDataAnalytics.QueryParameters;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.MarketDataAnalytics.Queries;

/// <summary>
/// MessagePack-serializable query to retrieve ITI signal data for a specific futures contract on a given value date.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record GetFuturesItiSignalDataQuery : IQuery<FuturesItiSignalDataReadModel>
{
    [IgnoreMember] public const string Actor = "FuturesItiSignalQuery";
    [IgnoreMember] public const string Verb = "GetFuturesItiSignalData";
    [IgnoreMember] public const int ErrorId = 1022;

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

    public GetFuturesItiSignalDataQuery() { }

    public GetFuturesItiSignalDataQuery(string contractId, DateOnly valueDate, TradeTimePeriodType timePeriod)
    {
        ContractId = contractId ?? string.Empty;
        ValueDate = valueDate;
        TimePeriod = timePeriod;
        EntityId = new GetFuturesItiSignalDataParameter(contractId, valueDate, timePeriod);
        ErrorCode = ErrorId;
    }

    /// <summary>
    /// MessagePack serialization constructor.
    /// </summary>
    [SerializationConstructor]
    public GetFuturesItiSignalDataQuery(
        ActorSubject subject,    // Key(0)
        IActorEntityId entityId, // Key(1)
        string contractId,       // Key(2)
        DateOnly valueDate,      // Key(3)
        TradeTimePeriodType timePeriod) // Key(4)
    {
        Subject = subject;
        EntityId = new GetFuturesItiSignalDataParameter(contractId, valueDate, timePeriod);
        ContractId = contractId ?? string.Empty;
        ValueDate = valueDate;
        TimePeriod = timePeriod;
        ErrorCode = ErrorId;
    }
}
