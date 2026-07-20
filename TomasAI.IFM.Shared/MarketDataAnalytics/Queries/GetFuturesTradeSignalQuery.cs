using MessagePack;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;
using TomasAI.IFM.Shared.MarketDataAnalytics.QueryParameters;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.MarketDataAnalytics.Queries;

/// <summary>
/// MessagePack-serializable query to retrieve a futures trade signal for a specific contract and value date.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record GetFuturesTradeSignalQuery : IQuery<FuturesTradeSignalV2ReadModel>
{
    [IgnoreMember] public const string Actor = "FuturesTradeSignalQuery";
    [IgnoreMember] public const string Verb = "GetFuturesTradeSignal";
    [IgnoreMember] public const int ErrorId = 1008;

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; }
    [IgnoreMember] public int ErrorCode { get; init; }
    [IgnoreMember] public string? QueryParams { get; init; }

    [Key(2)]
    public string ContractId { get; init; }

    [Key(3)]
    public DateOnly ValueDate { get; init; }

    /// <summary>Parameterless constructor for serializers.</summary>
    public GetFuturesTradeSignalQuery() { }

    /// <summary>Primary constructor to create the query in code and initialize defaults.</summary>
    public GetFuturesTradeSignalQuery(string contractId, DateOnly valueDate)
    {
        ContractId = contractId;
        ValueDate = valueDate;
        EntityId = new GetFuturesTradeSignalParameter(contractId, valueDate);
        ErrorCode = ErrorId;
    }

    /// <summary>
    /// MessagePack serialization constructor.
    /// </summary>
    [SerializationConstructor]
    public GetFuturesTradeSignalQuery(
        ActorSubject subject,       // Key(0)
        IActorEntityId entityId,    // Key(1)
        string contractId,          // Key(2)
        DateOnly valueDate)         // Key(3)
    {
        Subject = subject;
        EntityId = new GetFuturesTradeSignalParameter(contractId, valueDate);
        ContractId = contractId ?? string.Empty;
        ValueDate = valueDate;
        ErrorCode = ErrorId;
    }
}

/// <summary>
/// MessagePack-serializable query to retrieve the most recent futures trade signal.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record GetLastFuturesTradeSignalQuery : IQuery<FuturesTradeSignalV2ReadModel>
{
    [IgnoreMember] public const string Actor = "FuturesTradeSignalQuery";
    [IgnoreMember] public const string Verb = "GetLastFuturesTradeSignal";
    [IgnoreMember] public const int ErrorId = 1009;

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; }
    [IgnoreMember] public int ErrorCode { get; init; }
    [IgnoreMember] public string? QueryParams { get; init; }

    /// <summary>Parameterless constructor for serializers.</summary>
    public GetLastFuturesTradeSignalQuery()
    {
        EntityId = new GetLastFuturesTradeSignalParameter();
        ErrorCode = ErrorId;
    }

    /// <summary>
    /// MessagePack serialization constructor.
    /// </summary>
    [SerializationConstructor]
    public GetLastFuturesTradeSignalQuery(
        ActorSubject subject,       // Key(0)
        IActorEntityId entityId)    // Key(1)
    {
        Subject = subject;
        EntityId = new GetLastFuturesTradeSignalParameter();
        ErrorCode = ErrorId;
    }
}