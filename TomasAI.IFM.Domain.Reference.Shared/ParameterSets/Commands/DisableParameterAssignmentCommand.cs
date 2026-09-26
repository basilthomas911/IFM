using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Reference.Shared.ParameterSets;

[MessagePackObject(AllowPrivate = true)]
public sealed record DisableParameterAssignmentCommand:ICommand<ParameterAssignmentEntityId>
{

    /// <summary>Creates an empty command for serialization.</summary>
    public DisableParameterAssignmentCommand() { }

    /// <summary>Rehydrates the published command fields in permanent numeric-key order.</summary>
    /// <param name="commandId">The CommandId field.</param>
    /// <param name="subject">The Subject field.</param>
    /// <param name="postEvents">The PostEvents field.</param>
    /// <param name="entityId">The EntityId field.</param>
    /// <param name="errorCode">The ErrorCode field.</param>
    /// <param name="routeTo">The RouteTo field.</param>
    /// <param name="expectedRevision">The ExpectedRevision field.</param>
    /// <param name="scope">The Scope field.</param>
    /// <param name="reference">The Reference field.</param>
    [SerializationConstructor]
    public DisableParameterAssignmentCommand(Guid commandId, ActorSubject subject, bool postEvents, ParameterAssignmentEntityId entityId, int errorCode, BoundedContextName routeTo, long expectedRevision, ParameterAssignmentScope scope, ParameterVersionRef reference)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
        ExpectedRevision = expectedRevision;
        Scope = scope;
        Reference = reference;
    }
 public const string Actor="ParameterAssignmentCommand";public const string Verb="DisableParameterAssignment";
 [Key(0)] public Guid CommandId{get;init;}
 [Key(1)] public ActorSubject Subject{get;init;}
 [Key(2)] public bool PostEvents{get;init;}=true;
 [Key(3)] public ParameterAssignmentEntityId EntityId{get;init;}
 [Key(4)] public int ErrorCode{get;init;}=33010;
 [Key(5)] public BoundedContextName RouteTo{get;init;}=BoundedContextName.StrategyConfigurationBoundedContext;
 [Key(6)] public long ExpectedRevision{get;init;}
 [Key(7)] public ParameterAssignmentScope Scope{get;init;}=null!;
 [Key(8)] public ParameterVersionRef Reference{get;init;}=null!;
 [IgnoreMember] public string CommandName=>nameof(DisableParameterAssignmentCommand);
 [IgnoreMember] public string StreamId=>Subject.StreamId;
 [IgnoreMember] public string EventSource=>Actor;
 [IgnoreMember] public DateTime OriginatedOn=>DateTime.UtcNow;
 [IgnoreMember] public string OriginatedBy=>$"{Environment.UserDomainName}\\{Environment.UserName}";
}
