using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

namespace TomasAI.IFM.Shared.MarketDataFeed.Commands;

/// <summary>
/// Command to start streaming futures option quote data (quotes + related contracts) for a specified quote identifier.
/// </summary>
/// <remarks>
/// Follows the MessagePack serialization pattern used by other commands. Routes to
/// <see cref="BoundedContextName.FuturesOptionQuoteDataBoundedContext"/>. Custom properties begin at key index 6
/// because base command members occupy keys 0�5.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record StartFuturesOptionQuoteDataStreamingCommand
    : ICommand<QuoteId>
{
    public const string Actor = "FuturesOptionQuoteDataCommand";
    public const string Verb = "StartStreaming";
    public const int ErrorId = 5023;

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

    /// <summary>Unique quote identifier for the streaming session.</summary>
    [Key(6)]
    public int QuoteId { get; init; }

    /// <summary>Collection of option quote snapshots to initialize the stream.</summary>
    [Key(7)]
    public FuturesOptionQuoteReadModel[] FuturesOptionQuotes { get; init; }

    /// <summary>Associated option contract definitions relevant to the quotes.</summary>
    [Key(8)]
    public FuturesOptionContractReadModel[] FuturesOptionContracts { get; init; }

    /// <summary>
    /// Parameterless constructor required for MessagePack deserialization.
    /// </summary>
    public StartFuturesOptionQuoteDataStreamingCommand() { }

    /// <summary>
    /// Creates a new command to start streaming futures option quote data.
    /// </summary>
    /// <param name="quotedId">Unique quote identifier.</param>
    /// <param name="futuresOptionQuotes">Initial quote snapshots (cannot be null).</param>
    /// <param name="futuresOptionContracts">Associated futures option contracts (cannot be null).</param>
    public StartFuturesOptionQuoteDataStreamingCommand(
        int quotedId,
        FuturesOptionQuoteReadModel[] futuresOptionQuotes,
        FuturesOptionContractReadModel[] futuresOptionContracts)
    {
        QuoteId = quotedId;
        FuturesOptionQuotes = futuresOptionQuotes ?? throw new ArgumentNullException(nameof(futuresOptionQuotes));
        FuturesOptionContracts = futuresOptionContracts ?? throw new ArgumentNullException(nameof(futuresOptionContracts));

        EntityId = new QuoteId(QuoteId);
        RouteTo = BoundedContextName.FuturesOptionQuoteDataBoundedContext;
        ErrorCode = 5023;
    }

    // Optional explicit serialization constructor (keys must match indices)
    [SerializationConstructor]
    public StartFuturesOptionQuoteDataStreamingCommand(
        Guid commandId,                            // Key(0)
        ActorSubject subject,                      // Key(1)
        bool postEvents,                           // Key(2)
        QuoteId entityId,                          // Key(3)
        int errorCode,                             // Key(4)
        BoundedContextName routeTo,                // Key(5)
        int quoteId,                               // Key(6)
        FuturesOptionQuoteReadModel[] quotes,      // Key(7)
        FuturesOptionContractReadModel[] contracts // Key(8)
    )
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
        QuoteId = quoteId;
        FuturesOptionQuotes = quotes;
        FuturesOptionContracts = contracts;
    }
}