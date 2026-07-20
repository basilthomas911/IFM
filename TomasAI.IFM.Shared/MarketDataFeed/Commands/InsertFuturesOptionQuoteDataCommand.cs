using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.MarketDataFeed.Commands;

/// <summary>
/// Command to insert a futures option quote snapshot (price/size levels) for a specific contract.
/// </summary>
/// <remarks>
/// Follows the MessagePack serialization pattern used by other commands. Routes to
/// <see cref="BoundedContextName.FuturesOptionQuoteDataBoundedContext"/>. Custom properties begin at key index 6
/// because base command members occupy keys 0–5 in <see cref="BaseCommand{TEntityId}"/>.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record InsertFuturesOptionQuoteDataCommand : ICommand<QuoteId>
{
    public const string Actor = "FuturesOptionQuoteDataCommand";
    public const string Verb = "Insert";
    public const int ErrorId = 5026;

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

    /// <summary>
    /// Unique integer quote identifier.
    /// </summary>
    [Key(6)]
    public int QuoteId { get; init; }

    /// <summary>
    /// Futures option contract identifier associated with this quote.
    /// </summary>
    [Key(7)]
    public string ContractId { get; init; }

    /// <summary>
    /// Quote data payload (levels, sides, price/size info).
    /// </summary>
    [Key(8)]
    public QuoteData QuoteData { get; init; }

    /// <summary>
    /// Parameterless constructor required for MessagePack deserialization.
    /// </summary>
    public InsertFuturesOptionQuoteDataCommand() { }

    /// <summary>
    /// Creates a new command to insert a futures option quote snapshot.
    /// </summary>
    /// <param name="quoteId">Unique quote identifier.</param>
    /// <param name="contractId">Associated contract identifier.</param>
    /// <param name="quoteData">Quote data payload.</param>
    public InsertFuturesOptionQuoteDataCommand(int quoteId, string contractId, QuoteData quoteData)
    {
        QuoteId = quoteId;
        ContractId = contractId ?? string.Empty;
        QuoteData = quoteData ?? throw new ArgumentNullException(nameof(quoteData));

        EntityId = new QuoteId(QuoteId);
        RouteTo = BoundedContextName.FuturesOptionQuoteDataBoundedContext;
        ErrorCode = 5026;
    }

    // Optional explicit serialization constructor (keys must match indices)
    [SerializationConstructor]
    public InsertFuturesOptionQuoteDataCommand(
        Guid commandId,                // Key(0)
        ActorSubject subject,          // Key(1)
        bool postEvents,               // Key(2)
        QuoteId entityId,              // Key(3)
        int errorCode,                 // Key(4)
        BoundedContextName routeTo,    // Key(5)
        int quoteId,                   // Key(6)
        string contractId,             // Key(7)
        QuoteData quoteData)           // Key(8)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
        QuoteId = quoteId;
        ContractId = contractId;
        QuoteData = quoteData;
    }
}