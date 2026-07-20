using MessagePack;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;
using TomasAI.IFM.Shared.MarketDataAnalytics.QueryParameters;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.MarketDataAnalytics.Queries;

/// <summary>
/// MessagePack-serializable query to retrieve ITI MDI signal data for futures contracts by trend grouping.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record GetFuturesItiSignalMDIByTrendQuery : IQuery<FuturesItiSignalMDIV2ReadModel[]>
{
    [IgnoreMember] public const string Actor = "FuturesItiSignalQuery";
    [IgnoreMember] public const string Verb = "GetFuturesItiSignalMDIByTrend";
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
    public int GroupId { get; init; }

    public GetFuturesItiSignalMDIByTrendQuery() { }

    public GetFuturesItiSignalMDIByTrendQuery(string contractId, DateOnly valueDate, int groupId)
    {
        ContractId = contractId ?? string.Empty;
        ValueDate = valueDate;
        GroupId = groupId;
        EntityId = new GetFuturesItiSignalMDIByTrendParameter(contractId, valueDate, groupId);
        ErrorCode = ErrorId;
    }

    /// <summary>
    /// MessagePack serialization constructor.
    /// </summary>
    [SerializationConstructor]
    public GetFuturesItiSignalMDIByTrendQuery(
        ActorSubject subject,    // Key(0)
        IActorEntityId entityId, // Key(1)
        string contractId,       // Key(2)
        DateOnly valueDate,      // Key(3)
        int groupId)             // Key(4)
    {
        Subject = subject;
        EntityId = new GetFuturesItiSignalMDIByTrendParameter(contractId, valueDate, groupId);
        ContractId = contractId ?? string.Empty;
        ValueDate = valueDate;
        GroupId = groupId;
        ErrorCode = ErrorId;
    }
}
