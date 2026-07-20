using MessagePack;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed.QueryParameters;

namespace TomasAI.IFM.Shared.MarketDataFeed.Queries;

/// <summary>
/// Represents a query to retrieve the unique identifier for an option quote.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public class GetOptionQuoteIdQuery : IQuery<ScalarValue<int>>
{
    [IgnoreMember] public const string Actor = "MarketDataFeedQuery";
    [IgnoreMember] public const string Verb = "GetOptionQuoteId";
    [IgnoreMember] public const int ErrorId = 1016;

    [Key(0)] public ActorSubject Subject { get; set; }
    [Key(1)] public IActorEntityId EntityId { get; set; }
    [IgnoreMember] public int ErrorCode { get; set; }
    [IgnoreMember] public string? QueryParams { get; set; }

    [Key(2)]
    public string? QuoteKey { get; set; }

    public GetOptionQuoteIdQuery() 
    {
        EntityId = new GetOptionQuoteIdParameter();
        ErrorCode = ErrorId;
    }

    public GetOptionQuoteIdQuery(string quoteKey)
    {
        QuoteKey = quoteKey ?? string.Empty;
        EntityId = new GetOptionQuoteIdParameter();
        ErrorCode = ErrorId;
    }

    /// <summary>
    /// MessagePack serialization constructor.
    /// </summary>
    [SerializationConstructor]
    public GetOptionQuoteIdQuery(
        ActorSubject subject,     // Key(0)
        IActorEntityId entityId,  // Key(1)
        string quoteKey)          // Key(2)
    {
        Subject = subject;
        EntityId = new GetOptionQuoteIdParameter();
        QuoteKey = quoteKey ?? string.Empty;
        ErrorCode = ErrorId;
    }
}
