using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.Actor.Client;

/// <summary>
/// MessagePack-serializable test command used in application actor tests and samples.
/// </summary>
/// <remarks>
/// Follows the StopFuturesOptionTickDataStreamingCommand pattern:
/// - Base keys 0�5 are reserved by BaseCommand{TEntityId}.
/// - Custom properties start at key 6.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record TestCommand
    : ICommand<TestId>
{
    public const string Actor = "TestCommand";
    public const string Verb = "Test";
    public const int ErrorId = 1001;

    // Base command members (keys 0..5)
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; } = default!;
    [Key(2)] public bool PostEvents { get; init; }
    [Key(3)] public TestId EntityId { get; init; } = default!;
    [Key(4)] public int ErrorCode { get; init; }
    [Key(5)] public BoundedContextName RouteTo { get; init; }

    // Ignored / derived members
    [IgnoreMember] public string CommandName => GetType().Name;
    [IgnoreMember] public string StreamId => $"{Subject.StreamId}";
    [IgnoreMember] public string EventSource => $"{Actor}Actor";
    [IgnoreMember] public DateTime OriginatedOn => DateTime.UtcNow;
    [IgnoreMember] public string OriginatedBy => $"{Environment.UserDomainName}\\{Environment.UserName}";

    /// <summary>Sample message payload.</summary>
    [Key(6)]
    public string MsgString { get; init; } = string.Empty;

    /// <summary>
    /// Parameterless constructor for MessagePack deserialization.
    /// </summary>
    public TestCommand() { }

    /// <summary>
    /// Creates a new test command with the provided message string.
    /// </summary>
    /// <param name="msgString">Message payload.</param>
    public TestCommand(string msgString)
    {
        MsgString = msgString ?? string.Empty;

        EntityId = new TestId(DateOnly.FromDateTime(DateTime.UtcNow));
        RouteTo = BoundedContextName.ApplicationBoundedContext;
        ErrorCode = ErrorId;
    }

    /// <summary>
    /// MessagePack serialization constructor (indices must match Key attributes).
    /// </summary>
    [SerializationConstructor]
    public TestCommand(
        Guid commandId,              // Key(0)
        ActorSubject subject,        // Key(1)
        bool postEvents,             // Key(2)
        TestId entityId,             // Key(3)
        int errorCode,               // Key(4)
        BoundedContextName routeTo,  // Key(5)
        string msgString)            // Key(6)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
        MsgString = msgString ?? string.Empty;
    }
}

/// <summary>
/// Simple test entity identifier for unit tests (formats as yyyy-MM-dd).
/// </summary>
[MessagePackObject]
public readonly record struct TestId : IActorEntityId
{
    /// <summary>As-of (value) date.</summary>
    [Key(0)]
    public DateOnly ValueDate { get; init; }

    /// <summary>
    /// MessagePack serialization constructor (indices must match <see cref="KeyAttribute"/> values).
    /// </summary>
    [SerializationConstructor]
    public TestId(DateOnly valueDate) => ValueDate = valueDate;

    /// <summary>
    /// Formats the identifier using a dotted representation. For this single-field id it returns yyyy-MM-dd.
    /// </summary>
    public string Format() => string.Create(null, stackalloc char[48], $"{ValueDate:yyyy-MM-dd}");
}
