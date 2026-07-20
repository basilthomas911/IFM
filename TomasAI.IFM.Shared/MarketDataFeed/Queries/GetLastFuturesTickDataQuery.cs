using MessagePack;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed.QueryParameters;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.MarketDataFeed.Queries;

/// <summary>
/// MessagePack-serializable query to retrieve the most recent futures tick data for a specific contract and value date.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public class GetLastFuturesTickDataQuery : IQuery<FuturesTickDataV2ReadModel>
{
    [IgnoreMember] public const string Actor = "FuturesTickDataQuery";
    [IgnoreMember] public const string Verb = "GetLastFuturesTickData";
    [IgnoreMember] public const int ErrorId = 1015;

    [Key(0)] public ActorSubject Subject { get; set; }
    [Key(1)] public IActorEntityId EntityId { get; set; }
    [IgnoreMember] public int ErrorCode { get; set; }
    [IgnoreMember] public string? QueryParams { get; set; }

    [Key(2)]
    public string ContractId { get; set; }

    [Key(3)]
    public DateOnly ValueDate { get; set; }

    public GetLastFuturesTickDataQuery() { }

    public GetLastFuturesTickDataQuery(string contractId, DateOnly valueDate)
    {
        ContractId = contractId ?? string.Empty;
        ValueDate = valueDate;
        EntityId = new GetLastFuturesTickDataParameter(contractId, valueDate);
        ErrorCode = ErrorId;
    }

    /// <summary>
    /// MessagePack serialization constructor.
    /// </summary>
    [SerializationConstructor]
    public GetLastFuturesTickDataQuery(
        ActorSubject subject,     // Key(0)
        IActorEntityId entityId,  // Key(1)
        string contractId,        // Key(2)
        DateOnly valueDate)       // Key(3)
    {
        Subject = subject;
        EntityId = new GetLastFuturesTickDataParameter(contractId, valueDate);
        ContractId = contractId ?? string.Empty;
        ValueDate = valueDate;
        ErrorCode = ErrorId;
    }
}

/// <summary>
/// MessagePack-serializable query to retrieve the most recent futures tick data for a specific contract and tick date.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public class GetLastFuturesTickDataByTickDateQuery : IQuery<FuturesTickDataV2ReadModel>
{
    [IgnoreMember] public const string Actor = "FuturesTickDataQuery";
    [IgnoreMember] public const string Verb = "GetLastFuturesTickDataByTickDate";
    [IgnoreMember] public const int ErrorId = 1015;

    [Key(0)] public ActorSubject Subject { get; set; }
    [Key(1)] public IActorEntityId EntityId { get; set; }
    [IgnoreMember] public int ErrorCode { get; set; }
    [IgnoreMember] public string? QueryParams { get; set; }

    [Key(2)]
    public string ContractId { get; set; }

    [Key(3)]
    public DateTime TickDate { get; set; }

    public GetLastFuturesTickDataByTickDateQuery() { }

    public GetLastFuturesTickDataByTickDateQuery(string contractId, DateTime tickDate)
    {
        ContractId = contractId ?? string.Empty;
        TickDate = tickDate;
        EntityId = new GetLastFuturesTickDataByTickDateParameter(contractId, tickDate);
        ErrorCode = ErrorId;
    }

    /// <summary>
    /// MessagePack serialization constructor.
    /// </summary>
    [SerializationConstructor]
    public GetLastFuturesTickDataByTickDateQuery(
        ActorSubject subject,     // Key(0)
        IActorEntityId entityId,  // Key(1)
        string contractId,        // Key(2)
        DateTime tickDate)        // Key(3)
    {
        Subject = subject;
        EntityId = new GetLastFuturesTickDataByTickDateParameter(contractId, tickDate);
        ContractId = contractId ?? string.Empty;
        TickDate = tickDate;
        ErrorCode = ErrorId;
    }
}


