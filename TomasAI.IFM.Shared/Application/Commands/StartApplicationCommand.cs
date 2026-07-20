using MessagePack;
using TomasAI.IFM.Shared.Application.CommandParameters;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.Application.Commands;

/// <summary>
/// Represents a command to initiate the start of an application within the system.
/// </summary>
/// <remarks>This command is used to signal the application bounded context to begin the startup process for the
/// application. The <see cref="EntityId"/> is automatically initialized based on the command type, and the <see
/// cref="RouteTo"/>  property is set to the application bounded context.</remarks>
[MessagePackObject()]
public record StartApplicationCommand 
    : ICommand<ApplicationEntityId>
{
    public const string Actor = "ApplicationCommand";
    public const string Verb = "Start";
    public const int ErrorId = 10001;

    // Base command members (keys 0..5)
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

    public StartApplicationCommand() : this(DateOnly.FromDateTime(DateTime.UtcNow)) { }

    public StartApplicationCommand(DateOnly valueDate)
    {
        EntityId = new(valueDate);
        RouteTo = BoundedContextName.ApplicationBoundedContext;
        ErrorCode = ErrorId;
    }

    /// <summary>
    /// MessagePack serialization constructor (indices must match Key attributes).
    /// </summary>
    [SerializationConstructor]
    public StartApplicationCommand(
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
