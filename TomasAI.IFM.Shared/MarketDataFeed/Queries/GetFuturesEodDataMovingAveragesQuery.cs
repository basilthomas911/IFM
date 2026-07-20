using MessagePack;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed.QueryParameters;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.MarketDataFeed.Queries;

/// <summary>
/// MessagePack-serializable query to retrieve end-of-day moving averages for a specific futures contract.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public class GetFuturesEodDataMovingAveragesQuery : IQuery<FuturesEodDataMovingAveragesReadModel>
{
    [IgnoreMember] public const string Actor = "FuturesEodDataQuery";
    [IgnoreMember] public const string Verb = "GetFuturesEodMovingAverages";
    [IgnoreMember] public const int ErrorId = 1014;

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

    public GetFuturesEodDataMovingAveragesQuery() { }

    public GetFuturesEodDataMovingAveragesQuery(string contractId, string symbol, DateOnly valueDate)
    {
        ContractId = contractId ?? string.Empty;
        Symbol = symbol ?? string.Empty;
        ValueDate = valueDate;
        EntityId = new GetFuturesEodMovingAveragesParameter(contractId, symbol, valueDate);
        ErrorCode = ErrorId;
    }

    /// <summary>
    /// MessagePack serialization constructor.
    /// </summary>
    [SerializationConstructor]
    public GetFuturesEodDataMovingAveragesQuery(
        ActorSubject subject,     // Key(0)
        IActorEntityId entityId,  // Key(1)
        string contractId,        // Key(2)
        string symbol,            // Key(3)
        DateOnly valueDate)       // Key(4)
    {
        Subject = subject;
        EntityId = new GetFuturesEodMovingAveragesParameter(contractId, symbol, valueDate);
        ContractId = contractId ?? string.Empty;
        Symbol = symbol ?? string.Empty;
        ValueDate = valueDate;
        ErrorCode = ErrorId;
    }
}
