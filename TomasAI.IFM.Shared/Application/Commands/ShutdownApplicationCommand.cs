using MessagePack;
using TomasAI.IFM.Shared.Application.CommandParameters;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.Application.Commands;

/// <summary>
/// Represents a command to initiate the shutdown of an application.
/// </summary>
/// <remarks>This command is routed to the Application bounded context and is associated with a specific actor
/// entity. Follows MessagePack pattern: metadata keys 0..5.</remarks>
[MessagePackObject(AllowPrivate = true)]
public record ShutdownApplicationCommand : ICommand<ApplicationEntityId>
{
    public const string Actor = "ApplicationCommand";
    public const string Verb = "Shutdown";
    public const int ErrorId = 10002;

    // Serialized members (keys 0..5)
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; }
    [Key(3)] public ApplicationEntityId EntityId { get; init; }
    [Key(4)] public int ErrorCode { get; init; }
    [Key(5)] public BoundedContextName RouteTo { get; init; }

    // Ignored / derived members
    [IgnoreMember] public string CommandName => GetType().Name;
    [IgnoreMember] public string StreamId => $"{Subject.StreamId}";
    [IgnoreMember] public string EventSource => $"{Actor}Actor";
    [IgnoreMember] public DateTime OriginatedOn => DateTime.UtcNow;
    [IgnoreMember] public string OriginatedBy => $"{Environment.UserDomainName}\\{Environment.UserName}";

    public ShutdownApplicationCommand() : this(DateOnly.FromDateTime(DateTime.UtcNow)) { }

    /// <summary>
    /// Parameterless constructor required by serializers and for normal usage.
    /// </summary>
    public ShutdownApplicationCommand(DateOnly valueDate)
    {
        EntityId = new(valueDate);
        RouteTo = BoundedContextName.ApplicationBoundedContext;
        ErrorCode = ErrorId;
    }

    /// <summary>
    /// Creates a new command from a <see cref="ShutdownApplicationParameter"/>.
    /// </summary>
    /// <param name="parameter">The parameter containing the value date and error code.</param>
    public ShutdownApplicationCommand(ShutdownApplicationParameter parameter)
        : this(parameter.ValueDate) { }

    /// <summary>
    /// MessagePack serialization constructor. Keys 0..5 correspond to metadata.
    /// </summary>
    [SerializationConstructor]
    public ShutdownApplicationCommand(
        Guid commandId,               // Key(0)
        ActorSubject subject,         // Key(1)
        bool postEvents,              // Key(2)
        ApplicationEntityId entityId,       // Key(3)
        int errorCode,                // Key(4)
        BoundedContextName routeTo)   // Key(5)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
    }
}
