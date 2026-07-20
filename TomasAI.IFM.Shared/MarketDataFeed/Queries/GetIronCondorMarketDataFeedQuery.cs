using MessagePack;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed.QueryParameters;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.MarketDataFeed.Queries;

/// <summary>
/// MessagePack-serializable query to retrieve market data for an Iron Condor options strategy.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public class GetIronCondorMarketDataFeedQuery : IQuery<IronCondorMarketDataFeedReadModel>
{
    [IgnoreMember] public const string Actor = "MarketDataFeedQuery";
    [IgnoreMember] public const string Verb = "GetIronCondorMarketDataFeed";
    [IgnoreMember] public const int ErrorId = 1236;

    [Key(0)] public ActorSubject Subject { get; set; }
    [Key(1)] public IActorEntityId EntityId { get; set; }
    [IgnoreMember] public int ErrorCode { get; set; }
    [IgnoreMember] public string? QueryParams { get; set; }

    [Key(2)]
    public string UnderlyingContractId { get; set; }

    [Key(3)]
    public string ShortPutOptionContractId { get; set; }

    [Key(4)]
    public string LongPutOptionContractId { get; set; }

    [Key(5)]
    public string ShortCallOptionContractId { get; set; }

    [Key(6)]
    public string LongCallOptionContractId { get; set; }

    [Key(7)]
    public DateOnly ValueDate { get; set; }

    public GetIronCondorMarketDataFeedQuery() { }

    public GetIronCondorMarketDataFeedQuery(
        string underlyingContractId,
        string shortPutOptionContractId,
        string longPutOptionContractId,
        string shortCallOptionContractId,
        string longCallOptionContractId,
        DateOnly valueDate)
    {
        UnderlyingContractId = underlyingContractId ?? string.Empty;
        ShortPutOptionContractId = shortPutOptionContractId ?? string.Empty;
        LongPutOptionContractId = longPutOptionContractId ?? string.Empty;
        ShortCallOptionContractId = shortCallOptionContractId ?? string.Empty;
        LongCallOptionContractId = longCallOptionContractId ?? string.Empty;
        ValueDate = valueDate;
        EntityId = new GetIronCondorMarketDataFeedParameter(underlyingContractId, shortPutOptionContractId, longPutOptionContractId, shortCallOptionContractId, longCallOptionContractId, valueDate);
        ErrorCode = ErrorId;
    }

    /// <summary>
    /// MessagePack serialization constructor.
    /// </summary>
    [SerializationConstructor]
    public GetIronCondorMarketDataFeedQuery(
        ActorSubject subject,                // Key(0)
        IActorEntityId entityId,             // Key(1)
        string underlyingContractId,         // Key(2)
        string shortPutOptionContractId,     // Key(3)
        string longPutOptionContractId,      // Key(4)
        string shortCallOptionContractId,    // Key(5)
        string longCallOptionContractId,     // Key(6)
        DateOnly valueDate)                  // Key(7)
    {
        Subject = subject;
        EntityId = new GetIronCondorMarketDataFeedParameter(underlyingContractId, shortPutOptionContractId, longPutOptionContractId, shortCallOptionContractId, longCallOptionContractId, valueDate);
        UnderlyingContractId = underlyingContractId ?? string.Empty;
        ShortPutOptionContractId = shortPutOptionContractId ?? string.Empty;
        LongPutOptionContractId = longPutOptionContractId ?? string.Empty;
        ShortCallOptionContractId = shortCallOptionContractId ?? string.Empty;
        LongCallOptionContractId = longCallOptionContractId ?? string.Empty;
        ValueDate = valueDate;
        ErrorCode = ErrorId;
    }
}
