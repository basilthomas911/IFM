using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Shared.OptionPricer.Commands;

/// <summary>
/// Represents a command to delete paired put and call spread distributions for a specific trade context.
/// </summary>
/// <remarks>This command encapsulates all information required to identify and remove spread distributions
/// associated with a particular trade, including trade identifiers, status, value date, and expiry details. It is
/// intended for use within a distributed messaging system and is designed to be serialized and deserialized using
/// MessagePack. The command supports routing and event posting within a bounded context architecture.</remarks>
[MessagePackObject(AllowPrivate = true)]
public record DeleteSpreadDistributionCommand : ICommand<SpreadDistributionEntityId>
{
    public const string Actor = "SpreadDistributionCommand";
    public const string Verb = "Delete";
    public const int ErrorId = 7005;

    // Base command members (keys 0..5)
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; }
    [Key(3)] public SpreadDistributionEntityId EntityId { get; init; }
    [Key(4)] public int ErrorCode { get; init; }
    [Key(5)] public BoundedContextName RouteTo { get; init; }

    // Ignored / derived members
    [IgnoreMember] public string CommandName => GetType().Name;
    [IgnoreMember] public string StreamId => $"{Subject.StreamId}";
    [IgnoreMember] public string EventSource => $"{Actor}Actor";
    [IgnoreMember] public DateTime OriginatedOn => DateTime.UtcNow;
    [IgnoreMember] public string OriginatedBy => $"{Environment.UserDomainName}\\{Environment.UserName}";

    [Key(6)] public int TradeId { get; init; } 
    [Key(7)] public DateOnly ValueDate { get; init; } 
    [Key(8)] public TradeStatus TradeStatus { get; init; }
    [Key(9)] public int DaysToExpiry { get; init; }

    /// <summary>Parameterless constructor required for MessagePack deserialization.</summary>
    public DeleteSpreadDistributionCommand() { }

    /// <summary>
    /// Initializes a new instance of the DeleteSpreadDistributionCommand class to represent a request for deleting a
    /// spread distribution associated with the specified trade details.
    /// </summary>
    /// <remarks>Use this command within the spread distribution management context to request deletion of a
    /// spread distribution. Ensure that the provided tradeId corresponds to an existing trade and that all parameters
    /// meet their documented constraints.</remarks>
    /// <param name="tradeId">The unique identifier of the trade for which the spread distribution is to be deleted. Must be a positive
    /// integer.</param>
    /// <param name="valueDate">The date on which the trade's value is determined. This date must not be in the past.</param>
    /// <param name="tradeStatus">The current status of the trade, specified as a value of the TradeStatus enumeration.</param>
    /// <param name="daysToExpiry">The number of days remaining until the trade expires. Must be zero or a positive integer.</param>
    public DeleteSpreadDistributionCommand(
        int tradeId,
        DateOnly valueDate,
        TradeStatus tradeStatus,
        int daysToExpiry)
    {

        EntityId = new SpreadDistributionEntityId(tradeId,valueDate);

        TradeId = tradeId;
        ValueDate = valueDate;
        TradeStatus = tradeStatus;
        DaysToExpiry = daysToExpiry;
        RouteTo = BoundedContextName.SpreadDistributionBoundedContext;
        ErrorCode = 7001;
    }

    /// <summary>
    /// MessagePack serialization constructor (indices must match <see cref="KeyAttribute"/> values).
    /// </summary>
    [SerializationConstructor]
    public DeleteSpreadDistributionCommand(
        Guid commandId,                 // Key(0)
        ActorSubject subject,           // Key(1)
        bool postEvents,                // Key(2)
        SpreadDistributionEntityId entityId, // Key(3)
        int errorCode,                  // Key(4)
        BoundedContextName routeTo,     // Key(5)
        int tradeId,  // Key(6)
        DateOnly valueDate,             // Key(7)
        TradeStatus tradeStatus,        // Key(8)
        int daysToExpiry)               // Key(9)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
        TradeId = tradeId;
        ValueDate = valueDate;
        TradeStatus = tradeStatus;
        DaysToExpiry = daysToExpiry;
    }
}