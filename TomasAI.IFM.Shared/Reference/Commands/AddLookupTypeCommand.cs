using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Reference.ViewModels;

namespace TomasAI.IFM.Shared.Reference.Commands;

/// <summary>
/// Command to add a new lookup type definition.
/// </summary>
/// <remarks>
/// MessagePack serialization pattern: base command keys 0�5; custom properties start at key 6.
/// Routes to <see cref="BoundedContextName.LookupTypeBoundedContext"/> with error code 8001.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record AddLookupTypeCommand : ICommand<LookupTypeId>
{
    // Actor/verb constants
    [IgnoreMember] public const string Actor = "LookupTypeCommand";
    [IgnoreMember] public const string Verb = "Add";

    /// <summary>Error code for this command (excluded from serialization).</summary>
    [IgnoreMember] public const int ErrorId = 8001;

    // Base command members (keys 0..5)
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; }
    [Key(3)] public LookupTypeId EntityId { get; init; }
    [Key(4)] public int ErrorCode { get; init; }
    [Key(5)] public BoundedContextName RouteTo { get; init; }

    // Ignored / derived members
    [IgnoreMember] public string CommandName => GetType().Name;
    [IgnoreMember] public string StreamId => $"{Subject.StreamId}";
    [IgnoreMember] public string EventSource => $"{Actor}Actor";
    [IgnoreMember] public DateTime OriginatedOn => DateTime.UtcNow;
    [IgnoreMember] public string OriginatedBy => $"{Environment.UserDomainName}\\{Environment.UserName}";

    /// <summary>Lookup type view model payload to add.</summary>
    [Key(6)]
    public LookupTypeReadModel LookupType { get; init; }

    /// <summary>Parameterless constructor required for MessagePack deserialization.</summary>
    public AddLookupTypeCommand() { }

    /// <summary>
    /// Creates a new command to add a lookup type.
    /// </summary>
    /// <param name="lookupType">Lookup type view model (cannot be null).</param>
    public AddLookupTypeCommand(LookupTypeReadModel lookupType)
    {
        LookupType = lookupType ?? throw new ArgumentNullException(nameof(lookupType));
        EntityId = LookupType.Id;
        RouteTo = BoundedContextName.LookupTypeBoundedContext;
        ErrorCode = 8001;
    }

    /// <summary>
    /// MessagePack serialization constructor (indices must match <see cref="KeyAttribute"/> values).
    /// </summary>
    [SerializationConstructor]
    public AddLookupTypeCommand(
        Guid commandId,             // Key(0)
        ActorSubject subject,       // Key(1)
        bool postEvents,            // Key(2)
        LookupTypeId entityId,      // Key(3)
        int errorCode,              // Key(4)
        BoundedContextName routeTo, // Key(5)
        LookupTypeReadModel lookupType) // Key(6)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
        LookupType = lookupType;
    }
}