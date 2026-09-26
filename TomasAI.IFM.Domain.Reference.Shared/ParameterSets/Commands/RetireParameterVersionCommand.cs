using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
[MessagePackObject(AllowPrivate = true)]
public sealed record RetireParameterVersionCommand:IParameterSetMutation
{

    /// <summary>Creates an empty command for serialization.</summary>
    public RetireParameterVersionCommand() { }

    /// <summary>Rehydrates the published command fields in permanent numeric-key order.</summary>
    /// <param name="commandId">The CommandId field.</param>
    /// <param name="subject">The Subject field.</param>
    /// <param name="postEvents">The PostEvents field.</param>
    /// <param name="entityId">The EntityId field.</param>
    /// <param name="errorCode">The ErrorCode field.</param>
    /// <param name="routeTo">The RouteTo field.</param>
    /// <param name="expectedRevision">The ExpectedRevision field.</param>
    /// <param name="version">The Version field.</param>
    /// <param name="componentCode">The ComponentCode field.</param>
    /// <param name="name">The Name field.</param>
    /// <param name="description">The Description field.</param>
    /// <param name="schemaVersion">The SchemaVersion field.</param>
    /// <param name="payloadJson">The PayloadJson field.</param>
    [SerializationConstructor]
    public RetireParameterVersionCommand(Guid commandId, ActorSubject subject, bool postEvents, ParameterSetEntityId entityId, int errorCode, BoundedContextName routeTo, long expectedRevision, int version, string componentCode, string name, string description, int schemaVersion, string payloadJson)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
        ExpectedRevision = expectedRevision;
        Version = version;
        ComponentCode = componentCode;
        Name = name;
        Description = description;
        SchemaVersion = schemaVersion;
        PayloadJson = payloadJson;
    }
    public const string Actor="ParameterSetCommand";
    public const string Verb="RetireParameterVersion";
    [Key(0)] public Guid CommandId {get;init;}
    [Key(1)] public ActorSubject Subject {get;init;}
    [Key(2)] public bool PostEvents {get;init;}=true;
    [Key(3)] public ParameterSetEntityId EntityId {get;init;}
    [Key(4)] public int ErrorCode {get;init;}=33010;
    [Key(5)] public BoundedContextName RouteTo {get;init;}=BoundedContextName.StrategyConfigurationBoundedContext;
    [Key(6)] public long ExpectedRevision {get;init;}
    [Key(7)] public int Version {get;init;}
    [Key(8)] public string ComponentCode {get;init;}="strategy-workflow.regime-discovery";
    [Key(9)] public string Name {get;init;}=string.Empty;
    [Key(10)] public string Description {get;init;}=string.Empty;
    [Key(11)] public int SchemaVersion {get;init;}=2;
    [Key(12)] public string PayloadJson {get;init;}="{}";
    [IgnoreMember] public string CommandName=>nameof(RetireParameterVersionCommand);
    [IgnoreMember] public string StreamId=>Subject.StreamId;
    [IgnoreMember] public string EventSource=>Actor;
    [IgnoreMember] public DateTime OriginatedOn=>DateTime.UtcNow;
    [IgnoreMember] public string OriginatedBy=>$"{Environment.UserDomainName}\\{Environment.UserName}";
}
