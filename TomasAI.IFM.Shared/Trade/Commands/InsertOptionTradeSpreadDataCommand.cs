using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Trade.ViewModels;

namespace TomasAI.IFM.Shared.Trade.Commands;

/// <summary>
/// Command to insert spread data for a specific option trade.
/// </summary>
/// <remarks>
/// MessagePack serialization pattern: base command keys 0–5; custom properties start at key 6.
/// Routes to <see cref="BoundedContextName.OptionTradeBoundedContext"/> with error code 5032.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record InsertOptionTradeSpreadDataCommand
    : ICommand<OptionTradeEntityId>
{
    public const string Actor = "OptionTradeCommand";
    public const string Verb = "InsertSpreadData";
    public const int ErrorId = 5032;

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

    /// <summary>The spread data payload for the target option trade.</summary>
    [Key(6)]
    public OptionTradeSpreadsDataModel OptionTradeSpreadData { get; init; }

    /// <summary>
    /// Parameterless constructor required for MessagePack deserialization.
    /// </summary>
    public InsertOptionTradeSpreadDataCommand() { }

    /// <summary>
    /// Creates a new command to insert option trade spread data.
    /// </summary>
    /// <param name="optionTradeSpreadData">The spread data model (cannot be null).</param>
    public InsertOptionTradeSpreadDataCommand(OptionTradeSpreadsDataModel optionTradeSpreadData)
    {
        OptionTradeSpreadData = optionTradeSpreadData ?? throw new ArgumentNullException(nameof(optionTradeSpreadData));

        EntityId = new OptionTradeEntityId(OptionTradeSpreadData.OrderId, OptionTradeSpreadData.TradeId);
        RouteTo = BoundedContextName.OptionTradeBoundedContext;
        ErrorCode = ErrorId;
    }

    /// <summary>
    /// MessagePack serialization constructor (indices must match <see cref="KeyAttribute"/> values).
    /// </summary>
    [SerializationConstructor]
    public InsertOptionTradeSpreadDataCommand(
        Guid commandId,                   // Key(0)
        ActorSubject subject,             // Key(1)
        bool postEvents,                  // Key(2)
        OptionTradeEntityId entityId,     // Key(3)
        int errorCode,                    // Key(4)
        BoundedContextName routeTo,       // Key(5)
        OptionTradeSpreadsDataModel optionTradeSpreadData) // Key(6)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
        OptionTradeSpreadData = optionTradeSpreadData;
    }
}