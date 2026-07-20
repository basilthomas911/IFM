using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Reference.ViewModels;

namespace TomasAI.IFM.Shared.Reference.Commands;

/// <summary>
/// Command to change (update) an existing lookup type definition.
/// </summary>
/// <remarks>
/// MessagePack serialization pattern: base command keys 0�5; custom properties start at key 6.
/// Routes to <see cref="BoundedContextName.LookupTypeBoundedContext"/> with error code 8002.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record ChangeLookupTypeCommand
    : ICommand<LookupTypeId>
{
    [IgnoreMember] public const string Actor = "LookupTypeCommand";
    [IgnoreMember] public const string Verb = "Change";

    /// <summary>Error code for this command (excluded from serialization).</summary>
    [IgnoreMember] public const int ErrorId = 8002;

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

    /// <summary>The identifier of the lookup type to update.</summary>
    [Key(6)]
    public LookupTypeId LookupTypeId { get; init; }

    /// <summary>The updated lookup type payload.</summary>
    [Key(7)]
    public LookupTypeReadModel LookupType { get; init; }

    /// <summary>True to overwrite existing data; otherwise false.</summary>
    [Key(8)]
    public bool Overwrite { get; init; }

    /// <summary>
    /// Parameterless constructor required for MessagePack deserialization.
    /// </summary>
    public ChangeLookupTypeCommand() { }

    /// <summary>
    /// Creates a new command to update a lookup type definition.
    /// </summary>
    /// <param name="lookupTypeId">Target lookup type identifier.</param>
    /// <param name="lookupType">Updated lookup type view model (cannot be null).</param>
    /// <param name="overwrite">Set true to overwrite existing data.</param>
    public ChangeLookupTypeCommand(
        LookupTypeId lookupTypeId,
        LookupTypeReadModel lookupType,
        bool overwrite = false)
    {
        LookupTypeId = lookupTypeId;
        LookupType = lookupType ?? throw new ArgumentNullException(nameof(lookupType));
        Overwrite = overwrite;

        // Use the payload-derived id when available.
        EntityId = lookupTypeId;
        RouteTo = BoundedContextName.LookupTypeBoundedContext;
        ErrorCode = 8002;
    }

    /// <summary>
    /// MessagePack serialization constructor (indices must match <see cref="KeyAttribute"/> values).
    /// </summary>
    [SerializationConstructor]
    public ChangeLookupTypeCommand(
        Guid commandId,             // Key(0)
        ActorSubject subject,       // Key(1)
        bool postEvents,            // Key(2)
        LookupTypeId entityId,      // Key(3)
        int errorCode,              // Key(4)
        BoundedContextName routeTo, // Key(5)
        LookupTypeId lookupTypeId,  // Key(6)
        LookupTypeReadModel lookupType, // Key(7)
        bool overwrite)             // Key(8)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
        LookupTypeId = lookupTypeId;
        LookupType = lookupType;
        Overwrite = overwrite;
    }
}