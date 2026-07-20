using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;

namespace TomasAI.IFM.Shared.Trade.Commands;

/// <summary>
/// Command to place a trade based on a computed futures trade signal.
/// </summary>
/// <remarks>
/// MessagePack serialization pattern: base command keys 0�5; custom members start at key 6.
/// Routes to <see cref="BoundedContextName.TradePlacementBoundedContext"/> with error code 4038.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record SignalTradePlacementCommand
    : ICommand<TradePlacementId>
{
    public const string Actor = "TradePlacementCommand";
    public const string Verb = "Signal";
    public const int ErrorId = 4038;

    // Base command members (keys 0..5)
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; }
    [Key(3)] public TradePlacementId EntityId { get; init; }
    [Key(4)] public int ErrorCode { get; init; }
    [Key(5)] public BoundedContextName RouteTo { get; init; }

    // Ignored / derived members
    [IgnoreMember] public string CommandName => GetType().Name;
    [IgnoreMember] public string StreamId => $"{Subject.StreamId}";
    [IgnoreMember] public string EventSource => $"{Actor}Actor";
    [IgnoreMember] public DateTime OriginatedOn => DateTime.UtcNow;
    [IgnoreMember] public string OriginatedBy => $"{Environment.UserDomainName}\\{Environment.UserName}";

    /// <summary>The futures trade signal payload used to evaluate and place a trade.</summary>
    [Key(6)]
    public FuturesTradeSignalV2ReadModel FuturesTradeSignal { get; init; }

    /// <summary>
    /// Parameterless constructor required for MessagePack deserialization.
    /// </summary>
    public SignalTradePlacementCommand() { }

    /// <summary>
    /// Creates a new command to place a trade from the specified futures trade signal.
    /// </summary>
    /// <param name="futuresTradeSignal">The computed futures trade signal (cannot be null).</param>
    public SignalTradePlacementCommand(FuturesTradeSignalV2ReadModel futuresTradeSignal)
    {
        FuturesTradeSignal = futuresTradeSignal;

        EntityId = new TradePlacementId(FuturesTradeSignal.ContractId, FuturesTradeSignal.ValueDate);
        ErrorCode = ErrorId;
        RouteTo = BoundedContextName.TradePlacementBoundedContext;
    }

    /// <summary>
    /// MessagePack serialization constructor (indices must match <see cref="KeyAttribute"/> values).
    /// </summary>
    [SerializationConstructor]
    public SignalTradePlacementCommand(
        Guid commandId,                    // Key(0)
        ActorSubject subject,              // Key(1)
        bool postEvents,                   // Key(2)
        TradePlacementId entityId,         // Key(3)
        int errorCode,                     // Key(4)
        BoundedContextName routeTo,        // Key(5)
        FuturesTradeSignalV2ReadModel futuresTradeSignal) // Key(6)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
        FuturesTradeSignal = futuresTradeSignal;
    }
}