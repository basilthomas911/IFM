using MessagePack;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Shared.MarketDataAnalytics.Commands;

/// <summary>
/// Command to generate an RSI (Relative Strength Index) signal for a futures contract over a fixed window.
/// </summary>
/// <remarks>
/// Follows the MessagePack serialization pattern used by other analytics commands. The command is routed to
/// <see cref="BoundedContextName.FuturesRsiSignalBoundedContext"/>. Window size is fixed (60) and not serialized.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record GenerateFuturesRsiSignalCommand : ICommand<FuturesRsiSignalEntityId>
{
    public const string Actor = "FuturesRsiSignalCommand";
    public const string Verb = "Generate";
    public const int ErrorId = 20001;

    // Base command members (keys 0..5)
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; } 
    [Key(2)] public bool PostEvents { get; init; }
    [Key(3)] public FuturesRsiSignalEntityId EntityId { get; init; } 
    [Key(4)] public int ErrorCode { get; init; }
    [Key(5)] public BoundedContextName RouteTo { get; init; }

    // Ignored / derived members
    [IgnoreMember] public string CommandName => GetType().Name;
    [IgnoreMember] public string StreamId => $"{Subject.StreamId}";
    [IgnoreMember] public string EventSource => $"{Actor}Actor";
    [IgnoreMember] public DateTime OriginatedOn => DateTime.UtcNow;
    [IgnoreMember] public string OriginatedBy => $"{Environment.UserDomainName}\\{Environment.UserName}";

    [Key(6)]
    public FuturesRsiSignalId FuturesRsiSignalId { get; init; }
    [Key(7)]
    public decimal FuturesPrice { get; init; }

    
    /// <summary>
    /// Parameterless constructor required for MessagePack deserialization.
    /// </summary>
    public GenerateFuturesRsiSignalCommand() { }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="futuresRsiSignalId"></param>
    /// <param name="futuresPrice"></param>
    /// <exception cref="ArgumentNullException"></exception>
    public GenerateFuturesRsiSignalCommand(
        FuturesRsiSignalId futuresRsiSignalId,
        decimal futuresPrice)
    {
        FuturesRsiSignalId = futuresRsiSignalId;
        FuturesPrice = futuresPrice;

        EntityId = futuresRsiSignalId.ToEntityId();
        ErrorCode = 20001;
        RouteTo = BoundedContextName.FuturesRsiSignalBoundedContext;
    }

    // Optional explicit serialization constructor (keys must match indices)
    [SerializationConstructor]
    public GenerateFuturesRsiSignalCommand(
        Guid commandId,                    // Key(0)
        ActorSubject subject,              // Key(1)
        bool postEvents,                   // Key(2)
        FuturesRsiSignalEntityId entityId, // Key(3)
        int errorCode,                     // Key(4)
        BoundedContextName routeTo,        // Key(5)
        FuturesRsiSignalId futuresRsiSignalId,
        decimal futuresPrice)  // Key(6)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
        FuturesRsiSignalId = futuresRsiSignalId;
        FuturesPrice = futuresPrice;
    }
}