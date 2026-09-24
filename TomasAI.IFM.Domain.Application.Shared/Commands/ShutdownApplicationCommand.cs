using MessagePack;
using TomasAI.IFM.Domain.Application.Shared.CommandParameters;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Application.Shared.Commands;

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

    /// <summary>Creates a shutdown command for the current value date.</summary>
    public ShutdownApplicationCommand() : this(DateOnly.FromDateTime(DateTime.UtcNow)) { }

    /// <summary>
    /// Creates a shutdown command for the specified value date.
    /// </summary>
    /// <param name="valueDate">The application value date.</param>
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
    /// <param name="commandId">The permanent command identifier.</param>
    /// <param name="subject">The routed actor subject.</param>
    /// <param name="postEvents">Whether generated events are posted.</param>
    /// <param name="entityId">The application value-date identity.</param>
    /// <param name="errorCode">The command error code.</param>
    /// <param name="routeTo">The destination bounded context.</param>
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
