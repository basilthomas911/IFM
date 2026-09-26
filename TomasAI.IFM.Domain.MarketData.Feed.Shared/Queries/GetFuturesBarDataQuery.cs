using System;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.QueryParameters;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.Queries;

/// <summary>
/// MessagePack-serializable query to retrieve futures bar data for a contract/symbol and date/time range.
/// </summary>
/// <remarks>
/// Follows the project's MessagePack pattern used by other view models/queries:
/// - Annotated with <see cref="MessagePackObjectAttribute"/>.
/// - Explicit properties annotated with sequential <see cref="KeyAttribute"/> indices.
/// - A parameterless constructor for serializers and a full constructor annotated with <see cref="SerializationConstructorAttribute"/>.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public class GetFuturesBarDataQuery : IQuery<FuturesBarDataReadModel[]>
{
    [IgnoreMember] public const string Actor = "FuturesBarDataQuery";
    [IgnoreMember] public const string Verb = "GetFuturesBarData";
    [IgnoreMember] public const int ErrorId = 1012;

    [Key(0)] public ActorSubject Subject { get; set; }
    [Key(1)] public IActorEntityId EntityId { get; set; }
    [IgnoreMember] public int ErrorCode { get; set; }
    [IgnoreMember] public string? QueryParams { get; set; }

    [Key(2)]
    public string ContractId { get; set; }

    [Key(3)]
    public string Symbol { get; set; }

    [Key(4)]
    public DateOnly ValueDate { get; set; }

    [Key(5)]
    public DateTime StartDate { get; set; }

    [Key(6)]
    public DateTime EndDate { get; set; }

    public GetFuturesBarDataQuery() { }

    public GetFuturesBarDataQuery(string contractId, string symbol, DateOnly valueDate, DateTime startDate, DateTime endDate)
    {
        ContractId = contractId ?? string.Empty;
        Symbol = symbol ?? string.Empty;
        ValueDate = valueDate;
        StartDate = startDate;
        EndDate = endDate;
        EntityId = new GetFuturesBarDataParameter(contractId, symbol, valueDate, startDate, endDate);
        ErrorCode = ErrorId;
    }

    /// <summary>
    /// MessagePack serialization constructor.
    /// </summary>
    [SerializationConstructor]
    public GetFuturesBarDataQuery(
        ActorSubject subject,     // Key(0)
        IActorEntityId entityId,  // Key(1)
        string contractId,        // Key(2)
        string symbol,            // Key(3)
        DateOnly valueDate,       // Key(4)
        DateTime startDate,       // Key(5)
        DateTime endDate)         // Key(6)
    {
        Subject = subject;
        EntityId = new GetFuturesBarDataParameter(contractId, symbol, valueDate, startDate, endDate);
        ContractId = contractId ?? string.Empty;
        Symbol = symbol ?? string.Empty;
        ValueDate = valueDate;
        StartDate = startDate;
        EndDate = endDate;
        ErrorCode = ErrorId;
    }
}
