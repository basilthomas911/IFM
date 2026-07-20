using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.MarketDataAnalytics.Commands;

/// <summary>
/// Represents a command to clear the MDI watermark for a specific futures trade signal.
/// </summary>
/// <remarks>
/// MessagePack-serializable using numeric keys:
/// - Base command members occupy keys 0–5 (<see cref="BaseCommand{TEntityId}"/>).
/// - This command’s payload member (<see cref="FuturesTradeSignalId"/>) uses key 6.
/// The command routes to <see cref="BoundedContextName.FuturesTradeSignalBoundedContext"/> and targets the
/// futures trade signal entity identified by the supplied <see cref="FuturesTradeSignalId"/>.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record ClearFuturesTradeSignalMDIWatermarkCommand : ICommand<FuturesTradeSignalId>
{
    public const string Actor = "FuturesTradeSignalCommand";
    public const string Verb = "ClearMDIWatermark";

    // Base command members (keys 0..5)
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; }
    [Key(3)] public FuturesTradeSignalId EntityId { get; init; }
    [Key(4)] public int ErrorCode { get; init; }
    [Key(5)] public BoundedContextName RouteTo { get; init; }

    // Ignored / derived members
    [IgnoreMember] public string CommandName => GetType().Name;
    [IgnoreMember] public string StreamId => $"{Subject.StreamId}";
    [IgnoreMember] public string EventSource => $"{Actor}Actor";
    [IgnoreMember] public DateTime OriginatedOn => DateTime.UtcNow;
    [IgnoreMember] public string OriginatedBy => $"{Environment.UserDomainName}\\{Environment.UserName}";

    /// <summary>
    /// Gets or sets the identifier of the futures trade signal whose MDI watermark will be cleared.
    /// </summary>
    /// <remarks>Serialized with key 6.</remarks>
    [Key(6)]
    public FuturesTradeSignalId FuturesTradeSignalId { get; init; }

    /// <summary>
    /// Parameterless constructor used by MessagePack for property-based deserialization.
    /// </summary>
    public ClearFuturesTradeSignalMDIWatermarkCommand() { }

    /// <summary>
    /// Convenience constructor for creating the command in application code.
    /// </summary>
    /// <param name="futuresTradeSignalId">The futures trade signal identifier.</param>
    public ClearFuturesTradeSignalMDIWatermarkCommand(FuturesTradeSignalId futuresTradeSignalId)
    {
        FuturesTradeSignalId = futuresTradeSignalId ?? throw new ArgumentNullException(nameof(futuresTradeSignalId));
        EntityId = futuresTradeSignalId;
        ErrorCode = 20010;
        RouteTo = BoundedContextName.FuturesTradeSignalBoundedContext;
    }

    /// <summary>
    /// Full deserializing constructor used by MessagePack. Parameters must align with key order 0–6.
    /// </summary>
    /// <param name="commandId">Command identifier (key 0).</param>
    /// <param name="subject">Actor subject for routing (key 1).</param>
    /// <param name="postEvents">Indicates whether resulting events should be posted (key 2).</param>
    /// <param name="entityId">Futures trade signal entity identifier (key 3).</param>
    /// <param name="errorCode">Associated error code (key 4).</param>
    /// <param name="routeTo">Target bounded context (key 5).</param>
    /// <param name="futuresTradeSignalId">Payload futures trade signal identifier (key 6).</param>
    [SerializationConstructor]
    public ClearFuturesTradeSignalMDIWatermarkCommand(
        Guid commandId,
        ActorSubject subject,
        bool postEvents,
        FuturesTradeSignalId entityId,
        int errorCode,
        BoundedContextName routeTo,
        FuturesTradeSignalId futuresTradeSignalId)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
        FuturesTradeSignalId = futuresTradeSignalId;
    }
}