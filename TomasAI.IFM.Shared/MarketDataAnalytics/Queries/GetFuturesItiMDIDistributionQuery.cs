using MessagePack;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;
using TomasAI.IFM.Shared.MarketDataAnalytics.QueryParameters;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.MarketDataAnalytics.Queries;

/// <summary>
/// MessagePack-serializable query to retrieve the ITI MDI distribution for a specific futures contract on a given value date.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record GetFuturesItiMDIDistributionQuery : IQuery<FuturesItiMDIDistributionReadModel>
{
    [IgnoreMember] public const string Actor = "FuturesItiMDIDistributionQuery";
    [IgnoreMember] public const string Verb = "GetFuturesItiMDIDistribution";
    [IgnoreMember] public const int ErrorId = 1030;

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; }
    [IgnoreMember] public int ErrorCode { get; init; }
    [IgnoreMember] public string? QueryParams { get; init; }

    [Key(2)]
    public string ContractId { get; init; } = string.Empty;

    [Key(3)]
    public DateOnly ValueDate { get; init; }

    /// <summary>Parameterless constructor for serializers.</summary>
    public GetFuturesItiMDIDistributionQuery() { }

    /// <summary>Primary constructor to create the query in code and initialize defaults.</summary>
    public GetFuturesItiMDIDistributionQuery(string contractId, DateOnly valueDate)
    {
        ContractId = contractId ?? string.Empty;
        ValueDate = valueDate;
        EntityId = new GetFuturesItiMDIDistributionParameter(contractId, valueDate);
        ErrorCode = ErrorId;
    }

    /// <summary>
    /// MessagePack serialization constructor.
    /// </summary>
    [SerializationConstructor]
    public GetFuturesItiMDIDistributionQuery(
        ActorSubject subject,       // Key(0)
        IActorEntityId entityId,    // Key(1)
        string contractId,          // Key(2)
        DateOnly valueDate)         // Key(3)
    {
        Subject = subject;
        EntityId = new GetFuturesItiMDIDistributionParameter(contractId, valueDate);
        ContractId = contractId ?? string.Empty;
        ValueDate = valueDate;
        ErrorCode = ErrorId;
    }
}

/// <summary>
/// MessagePack-serializable query to retrieve the ITI MDI distribution for futures contracts by trend for a specific value date.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record GetFuturesItiMDIDistributionByTrendQuery : IQuery<FuturesItiMDIDistributionReadModel>
{
    [IgnoreMember] public const string Actor = "FuturesItiMDIDistributionByTrendQuery";
    [IgnoreMember] public const string Verb = "GetFuturesItiMDIDistributionByTrend";
    [IgnoreMember] public const int ErrorId = 1030;

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; }
    [IgnoreMember] public int ErrorCode { get; init; }
    [IgnoreMember] public string? QueryParams { get; init; }

    [Key(2)]
    public string ContractId { get; init; } = string.Empty;

    [Key(3)]
    public DateOnly ValueDate { get; init; }

    /// <summary>Parameterless constructor for serializers.</summary>
    public GetFuturesItiMDIDistributionByTrendQuery() { }

    /// <summary>Primary constructor to create the query in code and initialize defaults.</summary>
    public GetFuturesItiMDIDistributionByTrendQuery(string contractId, DateOnly valueDate)
    {
        ContractId = contractId ?? string.Empty;
        ValueDate = valueDate;
        EntityId = new GetFuturesItiMDIDistributionByTrendParameter(contractId, valueDate);
        ErrorCode = ErrorId;
    }

    /// <summary>
    /// MessagePack serialization constructor.
    /// </summary>
    [SerializationConstructor]
    public GetFuturesItiMDIDistributionByTrendQuery(
        ActorSubject subject,       // Key(0)
        IActorEntityId entityId,    // Key(1)
        string contractId,          // Key(2)
        DateOnly valueDate)         // Key(3)
    {
        Subject = subject;
        EntityId = new GetFuturesItiMDIDistributionByTrendParameter(contractId, valueDate);
        ContractId = contractId ?? string.Empty;
        ValueDate = valueDate;
        ErrorCode = ErrorId;
    }
}