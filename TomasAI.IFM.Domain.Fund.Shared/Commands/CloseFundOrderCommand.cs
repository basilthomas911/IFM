using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Fund.Shared.Commands;

/// <summary>
/// Represents a command to close a specific fund order.
/// </summary>
/// <remarks>
/// This command targets the Fund bounded context and is associated with the fund identified by the provided
/// <see cref="FundOrderId"/>. It follows the MessagePack pattern used by other commands:
/// - Base command members are serialized with keys 0–5.
/// - Payload members in this class start at key 6.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record CloseFundOrderCommand : ICommand<FundId>
{
    public const string Actor = "FundCommand";
    public const string Verb = "CloseOrder";
    public const int ErrorId = 2013;

    // Base command members (keys 0..5)
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; }
    [Key(3)] public FundId EntityId { get; init; }
    [Key(4)] public int ErrorCode { get; init; }
    [Key(5)] public BoundedContextName RouteTo { get; init; }

    // Ignored / derived members
    [IgnoreMember] public string CommandName => GetType().Name;
    [IgnoreMember] public string StreamId => $"{Subject.StreamId}";
    [IgnoreMember] public string EventSource => $"{Actor}Actor";
    [IgnoreMember] public DateTime OriginatedOn => DateTime.UtcNow;
    [IgnoreMember] public string OriginatedBy => $"{Environment.UserDomainName}\\{Environment.UserName}";

    /// <summary>
    /// Gets or sets the identifier of the fund order to close.
    /// </summary>
    /// <remarks>Serialized with key 6.</remarks>
    [Key(6)]
    public FundOrderId FundOrderId { get; init; }

    /// <summary>
    /// Parameterless constructor used by MessagePack for property-based deserialization.
    /// </summary>
    public CloseFundOrderCommand() { }

    /// <summary>
    /// Convenience constructor for creating the command in application code.
    /// </summary>
    /// <param name="fundOrderId">The fund order identifier to close.</param>
    public CloseFundOrderCommand(FundOrderId fundOrderId)
    {
        FundOrderId = fundOrderId;
        EntityId = new(fundOrderId.FundId);
        ErrorCode = ErrorId;
        RouteTo = BoundedContextName.FundBoundedContext;
    }

    /// <summary>
    /// Full deserializing constructor used by MessagePack. Parameters must align with key order 0–6.
    /// </summary>
    /// <param name="commandId">Command identifier (key 0).</param>
    /// <param name="subject">Actor subject for routing (key 1).</param>
    /// <param name="postEvents">Indicates whether to publish resulting events (key 2).</param>
    /// <param name="entityId">Fund entity identifier (key 3).</param>
    /// <param name="errorCode">Associated error code (key 4).</param>
    /// <param name="routeTo">Target bounded context (key 5).</param>
    /// <param name="fundOrderId">The fund order identifier to close (key 6).</param>
    [SerializationConstructor]
    public CloseFundOrderCommand(
        Guid commandId,
        ActorSubject subject,
        bool postEvents,
        FundId entityId,
        int errorCode,
        BoundedContextName routeTo,
        FundOrderId fundOrderId)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
        FundOrderId = fundOrderId;
    }
}