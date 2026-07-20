using System;
using MessagePack;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed.QueryParameters;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.MarketDataFeed.Queries;

/// <summary>
/// MessagePack-serializable query to retrieve end-of-day futures data for a specific contract within a date range.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public class GetFuturesEodDataByDateRangeQuery : IQuery<FuturesEodDataV2ReadModel[]>
{
    [IgnoreMember] public const string Actor = "FuturesEodDataQuery";
    [IgnoreMember] public const string Verb = "GetFuturesEodDataByDateRange";
    [IgnoreMember] public const int ErrorId = 1013;

    [Key(0)] public ActorSubject Subject { get; set; }
    [Key(1)] public IActorEntityId EntityId { get; set; }
    [IgnoreMember] public int ErrorCode { get; set; }
    [IgnoreMember] public string? QueryParams { get; set; }

    [Key(2)]
    public string ContractId { get; set; }

    [Key(3)]
    public DateOnly StartDate { get; set; }

    [Key(4)]
    public DateOnly EndDate { get; set; }

    public GetFuturesEodDataByDateRangeQuery() { }

    public GetFuturesEodDataByDateRangeQuery(string contractId, DateOnly startDate, DateOnly endDate)
    {
        ContractId = contractId ?? string.Empty;
        StartDate = startDate;
        EndDate = endDate;
        EntityId = new GetFuturesEodDataByDateRangeParameter(contractId, startDate, endDate);
        ErrorCode = ErrorId;
    }

    /// <summary>
    /// MessagePack serialization constructor.
    /// </summary>
    [SerializationConstructor]
    public GetFuturesEodDataByDateRangeQuery(
        ActorSubject subject,     // Key(0)
        IActorEntityId entityId,  // Key(1)
        string contractId,        // Key(2)
        DateOnly startDate,       // Key(3)
        DateOnly endDate)         // Key(4)
    {
        Subject = subject;
        EntityId = new GetFuturesEodDataByDateRangeParameter(contractId, startDate, endDate);
        ContractId = contractId ?? string.Empty;
        StartDate = startDate;
        EndDate = endDate;
        ErrorCode = ErrorId;
    }
}
