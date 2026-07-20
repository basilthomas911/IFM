using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Fund.Shared.Commands;

/// <summary>
/// Represents a command to complete a specific fund order.
/// </summary>
/// <remarks>
/// - Routed to the Fund bounded context.
/// - MessagePack-serializable: base keys 0–5 from base command, payload keys start at 6.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record CompleteFundOrderCommand : ICommand<FundId>
{
    public const string Actor = "FundCommand";
    public const string Verb = "CompleteOrder";

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
    /// Gets or sets the fund identifier.
    /// </summary>
    [Key(6)]
    public int FundId { get; init; }

    /// <summary>
    /// Gets or sets the order identifier within the fund.
    /// </summary>
    [Key(7)]
    public int OrderId { get; init; }

    /// <summary>
    /// Parameterless constructor used by MessagePack for property-based deserialization.
    /// </summary>
    public CompleteFundOrderCommand() { }

    /// <summary>
    /// Convenience constructor for creating the command in application code.
    /// </summary>
    /// <param name="fundId">The fund identifier.</param>
    /// <param name="orderId">The order identifier within the fund.</param>
    public CompleteFundOrderCommand(int fundId, int orderId)
    {
        FundId = fundId;
        OrderId = orderId;
        EntityId = new(fundId);
        ErrorCode = 2006;
        RouteTo = BoundedContextName.FundBoundedContext;
    }

    /// <summary>
    /// Full deserializing constructor used by MessagePack. Parameters align with key order 0–7.
    /// </summary>
    /// <param name="commandId">Command identifier (key 0).</param>
    /// <param name="subject">Actor subject for routing (key 1).</param>
    /// <param name="postEvents">Indicates whether to publish resulting events (key 2).</param>
    /// <param name="entityId">Fund entity identifier (key 3).</param>
    /// <param name="errorCode">Associated error code (key 4).</param>
    /// <param name="routeTo">Target bounded context (key 5).</param>
    /// <param name="fundId">The fund identifier (key 6).</param>
    /// <param name="orderId">The order identifier within the fund (key 7).</param>
    [SerializationConstructor]
    public CompleteFundOrderCommand(
        Guid commandId,
        ActorSubject subject,
        bool postEvents,
        FundId entityId,
        int errorCode,
        BoundedContextName routeTo,
        int fundId,
        int orderId)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
        FundId = fundId;
        OrderId = orderId;
    }
}