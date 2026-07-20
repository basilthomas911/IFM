using MessagePack;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Shared.MarketDataFeed.Commands;

/// <summary>
/// Command to delete a previously stored streaming request identifier for a specific feed.
/// </summary>
/// <remarks>
/// Follows the MessagePack serialization pattern used by other commands. Routes to
/// <see cref="BoundedContextName.MarketDataFeedBoundedContext"/>. Custom properties begin at key index 6
/// because base command members occupy keys 0–5.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record DeleteStreamingRequestIdCommand : ICommand<FeedId>
{
    public const string Actor = "MarketDataFeedCommand";
    public const string Verb = "DeleteStreamingRequestId";
    public const int ErrorId = 5022;

    // Base command members (keys 0..5)
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; }
    [Key(3)] public FeedId EntityId { get; init; }
    [Key(4)] public int ErrorCode { get; init; }
    [Key(5)] public BoundedContextName RouteTo { get; init; }

    // Ignored / derived members
    [IgnoreMember] public string CommandName => GetType().Name;
    [IgnoreMember] public string StreamId => $"{Subject.StreamId}";
    [IgnoreMember] public string EventSource => $"{Actor}Actor";
    [IgnoreMember] public DateTime OriginatedOn => DateTime.UtcNow;
    [IgnoreMember] public string OriginatedBy => $"{Environment.UserDomainName}\\{Environment.UserName}";

    /// <summary>
    /// The target feed identifier whose streaming request mapping should be deleted.
    /// </summary>
    [Key(6)]
    public FeedId FeedId { get; init; }

    /// <summary>
    /// Parameterless constructor required for MessagePack deserialization.
    /// </summary>
    public DeleteStreamingRequestIdCommand() { }

    /// <summary>
    /// Creates a new command to delete the streaming request identifier for the specified feed.
    /// </summary>
    /// <param name="feedId">The feed identifier.</param>
    public DeleteStreamingRequestIdCommand(FeedId feedId)
    {
        FeedId = feedId;
        EntityId = FeedId;
        ErrorCode = 5022;
        RouteTo = BoundedContextName.MarketDataFeedBoundedContext;
    }

    // Optional explicit serialization constructor (keys must match indices)
    [SerializationConstructor]
    public DeleteStreamingRequestIdCommand(
        Guid commandId,                 // Key(0)
        ActorSubject subject,           // Key(1)
        bool postEvents,                // Key(2)
        FeedId entityId,                // Key(3)
        int errorCode,                  // Key(4)
        BoundedContextName routeTo,     // Key(5)
        FeedId feedId)                  // Key(6)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
        FeedId = feedId;
    }
}