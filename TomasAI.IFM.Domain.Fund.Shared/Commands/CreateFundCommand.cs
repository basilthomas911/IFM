using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Fund.Shared.Commands;

/// <summary>
/// Represents a command to create a new fund.
/// </summary>
/// <remarks>
/// - Routed to the Fund bounded context.
/// - MessagePack-serializable: base keys 0�5 from base command, payload keys start at 6.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record CreateFundCommand : ICommand<FundId>
{
    public const string Actor = "FundCommand";
    public const string Verb = "Create";
    public const int ErrorId = 2007;

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
    /// Gets or sets the new fund to be created.
    /// </summary>
    /// <remarks>Serialized with key 6.</remarks>
    [Key(6)]
    public FundReadModel NewFund { get; init; }

    /// <summary>
    /// Parameterless constructor used by MessagePack for property-based deserialization.
    /// </summary>
    public CreateFundCommand() { }

    /// <summary>
    /// Convenience constructor for creating the command in application code.
    /// </summary>
    /// <param name="newFund">The fund to be created.</param>
    public CreateFundCommand(FundReadModel newFund)
    {
        NewFund = newFund ?? throw new ArgumentNullException(nameof(newFund));
        EntityId = new(newFund.FundId);
        ErrorCode = ErrorId;
        RouteTo = BoundedContextName.FundBoundedContext;
    }

    /// <summary>
    /// Full deserializing constructor used by MessagePack. Parameters align with key order 0�6.
    /// </summary>
    /// <param name="commandId">Command identifier (key 0).</param>
    /// <param name="subject">Actor subject for routing (key 1).</param>
    /// <param name="postEvents">Indicates whether to publish resulting events (key 2).</param>
    /// <param name="entityId">Fund entity identifier (key 3).</param>
    /// <param name="errorCode">Associated error code (key 4).</param>
    /// <param name="routeTo">Target bounded context (key 5).</param>
    /// <param name="newFund">The fund payload (key 6).</param>
    [SerializationConstructor]
    public CreateFundCommand(
        Guid commandId,
        ActorSubject subject,
        bool postEvents,
        FundId entityId,
        int errorCode,
        BoundedContextName routeTo,
        FundReadModel newFund)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
        NewFund = newFund;
    }
}