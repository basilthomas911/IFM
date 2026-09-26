using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
[MessagePackObject(AllowPrivate = true)]
public sealed record ApplySignalStartupPlanCommand:IParameterStartupMutation
{

    /// <summary>Creates an empty command for serialization.</summary>
    public ApplySignalStartupPlanCommand() { }

    /// <summary>Rehydrates the published command fields in permanent numeric-key order.</summary>
    /// <param name="commandId">The CommandId field.</param>
    /// <param name="subject">The Subject field.</param>
    /// <param name="postEvents">The PostEvents field.</param>
    /// <param name="entityId">The EntityId field.</param>
    /// <param name="errorCode">The ErrorCode field.</param>
    /// <param name="routeTo">The RouteTo field.</param>
    /// <param name="runId">The RunId field.</param>
    /// <param name="expectedFingerprint">The ExpectedFingerprint field.</param>
    [SerializationConstructor]
    public ApplySignalStartupPlanCommand(Guid commandId, ActorSubject subject, bool postEvents, ParameterStartupEntityId entityId, int errorCode, BoundedContextName routeTo, Guid runId, string expectedFingerprint)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
        RunId = runId;
        ExpectedFingerprint = expectedFingerprint;
    }
 public const string Actor="ParameterStartupCommand";public const string Verb="ApplySignalStartupPlan";
 [Key(0)] public Guid CommandId{get;init;}
 [Key(1)] public ActorSubject Subject{get;init;}
 [Key(2)] public bool PostEvents{get;init;}=true;
 [Key(3)] public ParameterStartupEntityId EntityId{get;init;}
 [Key(4)] public int ErrorCode{get;init;}=33010;
 [Key(5)] public BoundedContextName RouteTo{get;init;}=BoundedContextName.StrategyConfigurationBoundedContext;
 [Key(6)] public Guid RunId{get;init;}
 [Key(7)] public string ExpectedFingerprint{get;init;}=string.Empty;
 [IgnoreMember] public string CommandName=>nameof(ApplySignalStartupPlanCommand);
 [IgnoreMember] public string StreamId=>Subject.StreamId;
 [IgnoreMember] public string EventSource=>Actor;
 [IgnoreMember] public DateTime OriginatedOn=>DateTime.UtcNow;
 [IgnoreMember] public string OriginatedBy=>$"{Environment.UserDomainName}\\{Environment.UserName}";
}
