using MessagePack;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed.QueryParameters;

namespace TomasAI.IFM.Shared.MarketDataFeed.Queries;

/// <summary>
/// Represents a query to retrieve the streaming request ID.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public class GetStreamingRequestIdQuery : IQuery<ScalarValue<int>>
{
    [IgnoreMember] public const string Actor = "MarketDataFeedQuery";
    [IgnoreMember] public const string Verb = "GetStreamingRequestId";
    [IgnoreMember] public const int ErrorId = 1016;

    [Key(0)] public ActorSubject Subject { get; set; }
    [Key(1)] public IActorEntityId EntityId { get; set; }
    [IgnoreMember] public int ErrorCode { get; set; }
    [IgnoreMember] public string? QueryParams { get; set; }

    [Key(2)]
    public string RequestKey { get; set; }

    public GetStreamingRequestIdQuery() 
    {
        EntityId = new GetStreamingRequestIdParameter();
        ErrorCode = ErrorId;
    }

    public GetStreamingRequestIdQuery(string requestKey)
    {
        RequestKey = requestKey ?? string.Empty;
        EntityId = new GetStreamingRequestIdParameter();
        ErrorCode = ErrorId;
    }

    /// <summary>
    /// MessagePack serialization constructor.
    /// </summary>
    [SerializationConstructor]
    public GetStreamingRequestIdQuery(
        ActorSubject subject,     // Key(0)
        IActorEntityId entityId,  // Key(1)
        string requestKey)        // Key(2)
    {
        Subject = subject;
        EntityId = new GetStreamingRequestIdParameter();
        RequestKey = requestKey ?? string.Empty;
        ErrorCode = ErrorId;
    }
}
