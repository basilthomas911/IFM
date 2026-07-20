using MessagePack;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Shared.MarketDataFeed.Commands;

/// <summary>
/// Command to insert a futures closing price for a specific futures data identifier.
/// </summary>
/// <remarks>
/// Follows the MessagePack serialization pattern used by other commands. Routes to
/// <see cref="BoundedContextName.FuturesClosingPriceBoundedContext"/>. Custom properties begin at key index 6
/// because base command members occupy keys 0–5.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record InsertFuturesClosingPriceCommand : ICommand<FuturesDataId>
{
    public const string Actor = "FuturesClosingPriceCommand";
    public const string Verb = "Insert";
    public const int ErrorId = 5021;

    // Base command members (keys 0..5)
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; }
    [Key(3)] public FuturesDataId EntityId { get; init; }
    [Key(4)] public int ErrorCode { get; init; }
    [Key(5)] public BoundedContextName RouteTo { get; init; }

    // Ignored / derived members
    [IgnoreMember] public string CommandName => GetType().Name;
    [IgnoreMember] public string StreamId => $"{Subject.StreamId}";
    [IgnoreMember] public string EventSource => $"{Actor}Actor";
    [IgnoreMember] public DateTime OriginatedOn => DateTime.UtcNow;
    [IgnoreMember] public string OriginatedBy => $"{Environment.UserDomainName}\\{Environment.UserName}";

    /// <summary>
    /// The futures data identifier for which the closing price is being recorded.
    /// </summary>
    [Key(6)]
    public FuturesDataId FuturesClosingPriceId { get; init; }

    /// <summary>
    /// The closing price to insert.
    /// </summary>
    [Key(7)]
    public decimal ClosingPrice { get; init; }

    /// <summary>
    /// Parameterless constructor required for MessagePack deserialization.
    /// </summary>
    public InsertFuturesClosingPriceCommand() { }

    /// <summary>
    /// Creates a new command to insert a closing price for the specified futures data.
    /// </summary>
    /// <param name="futuresClosingPriceId">The futures data identifier.</param>
    /// <param name="closingPrice">The closing price value.</param>
    public InsertFuturesClosingPriceCommand(FuturesDataId futuresClosingPriceId, decimal closingPrice)
    {
        FuturesClosingPriceId = IsArgumentNull.Set(futuresClosingPriceId);
        ClosingPrice = closingPrice;

        EntityId = FuturesClosingPriceId;
        ErrorCode = 5021;
        RouteTo = BoundedContextName.FuturesClosingPriceBoundedContext;
    }

    // Optional explicit serialization constructor (keys must match indices)
    [SerializationConstructor]
    public InsertFuturesClosingPriceCommand(
        Guid commandId,                 // Key(0)
        ActorSubject subject,           // Key(1)
        bool postEvents,                // Key(2)
        FuturesDataId entityId,         // Key(3)
        int errorCode,                  // Key(4)
        BoundedContextName routeTo,     // Key(5)
        FuturesDataId closingPriceId,   // Key(6)
        decimal closingPrice)           // Key(7)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;

        FuturesClosingPriceId = closingPriceId;
        ClosingPrice = closingPrice;
    }
}