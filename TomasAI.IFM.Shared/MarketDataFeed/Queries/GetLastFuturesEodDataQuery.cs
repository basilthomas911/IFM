using MessagePack;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed.QueryParameters;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.MarketDataFeed.Queries;

/// <summary>
/// MessagePack-serializable query to retrieve the most recent end-of-day (EOD) futures data.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public class GetLastFuturesEodDataQuery : IQuery<FuturesEodDataV2ReadModel>
{
    [IgnoreMember] public const string Actor = "FuturesEodDataQuery";
    [IgnoreMember] public const string Verb = "GetLastFuturesEodData";
    [IgnoreMember] public const int ErrorId = 1014;

    [Key(0)] public ActorSubject Subject { get; set; }
    [Key(1)] public IActorEntityId EntityId { get; set; }
    [IgnoreMember] public int ErrorCode { get; set; }
    [IgnoreMember] public string? QueryParams { get; set; }

    [Key(2)]
    public string ContractId { get; set; }

    [Key(3)]
    public DateOnly ValueDate { get; set; }

    public GetLastFuturesEodDataQuery() { }

    public GetLastFuturesEodDataQuery(string contractId, DateOnly valueDate)
    {
        ContractId = contractId ?? string.Empty;
        ValueDate = valueDate;
        EntityId = new GetLastFuturesEodDataParameter(contractId, valueDate);
        ErrorCode = ErrorId;
    }

    /// <summary>
    /// MessagePack serialization constructor.
    /// </summary>
    [SerializationConstructor]
    public GetLastFuturesEodDataQuery(
        ActorSubject subject,     // Key(0)
        IActorEntityId entityId,  // Key(1)
        string contractId,        // Key(2)
        DateOnly valueDate)       // Key(3)
    {
        Subject = subject;
        EntityId = new GetLastFuturesEodDataParameter(contractId, valueDate);
        ContractId = contractId ?? string.Empty;
        ValueDate = valueDate;
        ErrorCode = ErrorId;
    }
}