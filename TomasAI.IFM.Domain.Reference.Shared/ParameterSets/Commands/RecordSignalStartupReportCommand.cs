using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Reference.Shared.ParameterSets;


[MessagePackObject(AllowPrivate = true)]
public sealed record RecordSignalStartupReportCommand:IParameterStartupMutation
{

    /// <summary>Creates an empty command for serialization.</summary>
    public RecordSignalStartupReportCommand() { }

    /// <summary>Rehydrates the published command fields in permanent numeric-key order.</summary>
    /// <param name="commandId">The CommandId field.</param>
    /// <param name="subject">The Subject field.</param>
    /// <param name="postEvents">The PostEvents field.</param>
    /// <param name="entityId">The EntityId field.</param>
    /// <param name="errorCode">The ErrorCode field.</param>
    /// <param name="routeTo">The RouteTo field.</param>
    /// <param name="runId">The RunId field.</param>
    /// <param name="expectedFingerprint">The ExpectedFingerprint field.</param>
    /// <param name="report">The Report field.</param>
    [SerializationConstructor]
    public RecordSignalStartupReportCommand(Guid commandId, ActorSubject subject, bool postEvents, ParameterStartupEntityId entityId, int errorCode, BoundedContextName routeTo, Guid runId, string expectedFingerprint, ParameterSignalStartupReport report)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
        RunId = runId;
        ExpectedFingerprint = expectedFingerprint;
        Report = report;
    }
 public const string Actor="ParameterStartupCommand";public const string Verb="RecordSignalStartupReport";
 [Key(0)] public Guid CommandId{get;init;}
 [Key(1)] public ActorSubject Subject{get;init;}
 [Key(2)] public bool PostEvents{get;init;}=true;
 [Key(3)] public ParameterStartupEntityId EntityId{get;init;}
 [Key(4)] public int ErrorCode{get;init;}=33010;
 [Key(5)] public BoundedContextName RouteTo{get;init;}=BoundedContextName.StrategyConfigurationBoundedContext;
 [Key(6)] public Guid RunId{get;init;}
 [Key(7)] public string ExpectedFingerprint{get;init;}=string.Empty;
 [Key(8)] public ParameterSignalStartupReport Report{get;init;}=default!;
 [IgnoreMember] public string CommandName=>nameof(RecordSignalStartupReportCommand);
 [IgnoreMember] public string StreamId=>Subject.StreamId;
 [IgnoreMember] public string EventSource=>Actor;
 [IgnoreMember] public DateTime OriginatedOn=>DateTime.UtcNow;
 [IgnoreMember] public string OriginatedBy=>$"{Environment.UserDomainName}\\{Environment.UserName}";
}
