using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.MarketDataFeed.Commands;

/// <summary>
/// Command to stop streaming futures option quote data for a specific quote identifier.
/// </summary>
/// <remarks>
/// Follows the MessagePack serialization pattern (base command keys 0–5). Custom properties start at key 6.
/// Routes to <see cref="BoundedContextName.FuturesOptionQuoteDataBoundedContext"/>.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record StopFuturesOptionQuoteDataStreamingCommand
    : ICommand<QuoteId>
{
    public const string Actor = "FuturesOptionQuoteDataCommand";
    public const string Verb = "StopStreaming";
    public const int ErrorId = 5024;

    // Base command members (keys 0..5)
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; }
    [Key(3)] public QuoteId EntityId { get; init; }
    [Key(4)] public int ErrorCode { get; init; }
    [Key(5)] public BoundedContextName RouteTo { get; init; }

    // Ignored / derived members
    [IgnoreMember] public string CommandName => GetType().Name;
    [IgnoreMember] public string StreamId => $"{Subject.StreamId}";
    [IgnoreMember] public string EventSource => $"{Actor}Actor";
    [IgnoreMember] public DateTime OriginatedOn => DateTime.UtcNow;
    [IgnoreMember] public string OriginatedBy => $"{Environment.UserDomainName}\\{Environment.UserName}";

    /// <summary>The unique identifier of the quote whose streaming should be stopped.</summary>
    [Key(6)]
    public int QuoteId { get; init; }

    /// <summary>
    /// Parameterless constructor required for MessagePack deserialization.
    /// </summary>
    public StopFuturesOptionQuoteDataStreamingCommand() { }

    /// <summary>
    /// Creates a new command to stop streaming futures option quote data for the specified quote id.
    /// </summary>
    /// <param name="quoteId">The quote identifier.</param>
    public StopFuturesOptionQuoteDataStreamingCommand(int quoteId)
    {
        QuoteId = quoteId;
        EntityId = new QuoteId(QuoteId);
        RouteTo = BoundedContextName.FuturesOptionQuoteDataBoundedContext;
        ErrorCode = 5024;
    }

    // Optional explicit serialization constructor (keys must match indices)
    [SerializationConstructor]
    public StopFuturesOptionQuoteDataStreamingCommand(
        Guid commandId,                  // Key(0)
        ActorSubject subject,            // Key(1)
        bool postEvents,                 // Key(2)
        QuoteId entityId,                // Key(3)
        int errorCode,                   // Key(4)
        BoundedContextName routeTo,      // Key(5)
        int quoteId)                     // Key(6)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
        QuoteId = quoteId;
    }
}