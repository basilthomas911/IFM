using MessagePack;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Shared.MarketDataAnalytics.Commands;

/// <summary>
/// Command to start (initialize) an LLM-based futures trade signal workflow for the specified trade signal entity.
/// </summary>
/// <remarks>
/// Follows the MessagePack serialization pattern used by other analytics commands. Routes to
/// <see cref="BoundedContextName.FuturesTradeSignalLLMBoundedContext"/>. Custom properties start at key index 6
/// because base command members occupy keys 0–5.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record StartFuturesTradeSignalLLMCommand
    : ICommand<FuturesTradeSignalId>
{
    public const string Actor = "FuturesTradeSignalLLMCommand";
    public const string Verb = "Start";
    public const int ErrorId = 20003;

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
    /// The target trade signal entity identifier (contract + value date).
    /// </summary>
    [Key(6)]
    public FuturesTradeSignalId Id { get; init; }

    /// <summary>
    /// Parameterless constructor required for MessagePack deserialization.
    /// </summary>
    public StartFuturesTradeSignalLLMCommand() { }

    /// <summary>
    /// Creates a new command to start processing for the specified LLM trade signal entity.
    /// </summary>
    /// <param name="id">Trade signal entity identifier (cannot be null).</param>
    public StartFuturesTradeSignalLLMCommand(FuturesTradeSignalId id)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        EntityId = Id;
        ErrorCode = ErrorId;
        RouteTo = BoundedContextName.FuturesTradeSignalLLMBoundedContext;
    }

    // Optional explicit serialization constructor (keys must match indices)
    [SerializationConstructor]
    public StartFuturesTradeSignalLLMCommand(
        Guid commandId,                        // Key(0)
        ActorSubject subject,                  // Key(1)
        bool postEvents,                       // Key(2)
        FuturesTradeSignalId entityId,         // Key(3)
        int errorCode,                         // Key(4)
        BoundedContextName routeTo,            // Key(5)
        FuturesTradeSignalId id)               // Key(6)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
        Id = id;
    }
}