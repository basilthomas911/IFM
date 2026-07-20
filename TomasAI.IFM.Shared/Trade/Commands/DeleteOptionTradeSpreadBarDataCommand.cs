using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.Trade.Commands;

/// <summary>
/// Command to delete spread bar data for a specific option trade, trade type, and value date.
/// </summary>
/// <remarks>
/// Follows the MessagePack serialization pattern: base command keys 0–5; custom members start at key 6.
/// Routes to <see cref="BoundedContextName.OptionTradeBoundedContext"/> with error code 4033.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record DeleteOptionTradeSpreadBarDataCommand
    : ICommand<OptionTradeEntityId>
{
    public const string Actor = "OptionTradeCommand";
    public const string Verb = "DeleteSpreadBarData";
    public const int ErrorId = 4033;

    // Base command members (keys 0..5)
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; }
    [Key(3)] public OptionTradeEntityId EntityId { get; init; }
    [Key(4)] public int ErrorCode { get; init; }
    [Key(5)] public BoundedContextName RouteTo { get; init; }

    // Ignored / derived members
    [IgnoreMember] public string CommandName => GetType().Name;
    [IgnoreMember] public string StreamId => $"{Subject.StreamId}";
    [IgnoreMember] public string EventSource => $"{Actor}Actor";
    [IgnoreMember] public DateTime OriginatedOn => DateTime.UtcNow;
    [IgnoreMember] public string OriginatedBy => $"{Environment.UserDomainName}\\{Environment.UserName}";

    /// <summary>The option trade identifier whose spread bar data should be deleted.</summary>
    [Key(6)]
    public OptionTradeEntityId OptionTradeId { get; init; }

    /// <summary>The option trade strategy/type associated with the data.</summary>
    [Key(7)]
    public TradeType TradeType { get; init; }

    /// <summary>The value (trading) date of the data to delete.</summary>
    [Key(8)]
    public DateOnly ValueDate { get; init; }

    /// <summary>Parameterless constructor required for MessagePack deserialization.</summary>
    public DeleteOptionTradeSpreadBarDataCommand() { }

    /// <summary>
    /// Creates a new command to delete spread bar data for an option trade.
    /// </summary>
    /// <param name="optionTradeId">The option trade identifier.</param>
    /// <param name="tradeType">The trade type/strategy.</param>
    /// <param name="valueDate">The value (trading) date.</param>
    public DeleteOptionTradeSpreadBarDataCommand(OptionTradeEntityId optionTradeId, TradeType tradeType, DateOnly valueDate)
    {
        OptionTradeId = optionTradeId;
        TradeType = tradeType;
        ValueDate = valueDate;

        EntityId = OptionTradeId;
        RouteTo = BoundedContextName.OptionTradeBoundedContext;
        ErrorCode = ErrorId;
    }

    /// <summary>
    /// MessagePack serialization constructor (indices must match <see cref="KeyAttribute"/> values).
    /// </summary>
    [SerializationConstructor]
    public DeleteOptionTradeSpreadBarDataCommand(
        Guid commandId,                 // Key(0)
        ActorSubject subject,           // Key(1)
        bool postEvents,                // Key(2)
        OptionTradeEntityId entityId,   // Key(3)
        int errorCode,                  // Key(4)
        BoundedContextName routeTo,     // Key(5)
        OptionTradeEntityId optionTradeId, // Key(6)
        TradeType tradeType,            // Key(7)
        DateOnly valueDate)             // Key(8)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;

        OptionTradeId = optionTradeId;
        TradeType = tradeType;
        ValueDate = valueDate;
    }
}