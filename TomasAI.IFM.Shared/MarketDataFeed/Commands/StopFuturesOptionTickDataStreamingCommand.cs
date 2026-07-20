using MessagePack;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Shared.MarketDataFeed.Commands;

/// <summary>
/// Command to stop streaming futures option tick data for a specific feed and contract.
/// </summary>
/// <remarks>
/// Follows the MessagePack serialization pattern (base command keys 0–5). Custom properties start at key 6.
/// Routes to <see cref="BoundedContextName.FuturesOptionTickDataBoundedContext"/>.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record StopFuturesOptionTickDataStreamingCommand
    : ICommand<FuturesOptionTickEntityId>
{
    public const string Actor = "FuturesOptionTickDataCommand";
    public const string Verb = "StopStreaming";
    public const int ErrorId = 5005;

    // Base command members (keys 0..5)
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; }
    [Key(3)] public FuturesOptionTickEntityId EntityId { get; init; }
    [Key(4)] public int ErrorCode { get; init; }
    [Key(5)] public BoundedContextName RouteTo { get; init; }

    // Ignored / derived members
    [IgnoreMember] public string CommandName => GetType().Name;
    [IgnoreMember] public string StreamId => $"{Subject.StreamId}";
    [IgnoreMember] public string EventSource => $"{Actor}Actor";
    [IgnoreMember] public DateTime OriginatedOn => DateTime.UtcNow;
    [IgnoreMember] public string OriginatedBy => $"{Environment.UserDomainName}\\{Environment.UserName}";

    /// <summary>The futures option contract identifier associated with the streaming session.</summary>
    [Key(6)]
    public string ContractId { get; init; }

    /// <summary>
    /// Parameterless constructor required for MessagePack deserialization.
    /// </summary>
    public StopFuturesOptionTickDataStreamingCommand() { }

    /// <summary>
    /// Creates a new command to stop streaming futures option tick data.
    /// </summary>
    /// <param name="entityId">Futures option tick entity identifier.</param>
    /// <param name="contractId">Option contract identifier.</param>
    public StopFuturesOptionTickDataStreamingCommand(FuturesOptionTickEntityId entityId, string contractId)
    {
        EntityId = entityId;
        ContractId = contractId ?? string.Empty;
        RouteTo = BoundedContextName.FuturesOptionTickDataBoundedContext;
        ErrorCode = ErrorId;
    }

    // Optional explicit serialization constructor (keys must match indices)
    [SerializationConstructor]
    public StopFuturesOptionTickDataStreamingCommand(
        Guid commandId,                // Key(0)
        ActorSubject subject,          // Key(1)
        bool postEvents,               // Key(2)
        FuturesOptionTickEntityId entityId,               // Key(3)
        int errorCode,                 // Key(4)
        BoundedContextName routeTo,    // Key(5)
        string contractId)             // Key(6)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
        ContractId = contractId;
    }
}