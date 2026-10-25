using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Reference.Shared.ParameterSets;

/// <summary>Concrete binary parameter request; contains no encoded parameter document.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record PreviewTypedParameterDraftQuery : IQuery<ParameterSetValue>
{
    public PreviewTypedParameterDraftQuery() { }
    [SerializationConstructor]
    public PreviewTypedParameterDraftQuery(ActorSubject subject, IActorEntityId entityId, Guid setId, string componentCode, ParameterSetValue? source, int targetHorizon, bool rebuildIntervals)
    {
        Subject=subject; EntityId=entityId;
        SetId=setId;
        ComponentCode=componentCode;
        Source=source;
        TargetHorizon=targetHorizon;
        RebuildIntervals=rebuildIntervals;
    }
    public const string Actor="ParameterSetQuery";
    public const string Verb="PreviewTypedParameterDraft";
    [Key(0)] public ActorSubject Subject {get;init;}
    [Key(1)] public IActorEntityId EntityId {get;init;}=ActorEntityId.Default;
    [Key(2)] public Guid SetId {get;init;}=Guid.Empty;
    [Key(3)] public string ComponentCode {get;init;}=ParameterSchemaRegistry.RegimeComponent;
    [Key(4)] public ParameterSetValue? Source {get;init;}=null;
    [Key(5)] public int TargetHorizon {get;init;}=(int)TomasAI.IFM.Domain.MarketData.Analytics.Shared.TimeFrameType.Daily;
    [Key(6)] public bool RebuildIntervals {get;init;}=false;
    [IgnoreMember] public int ErrorCode {get;init;}=33101;
    [IgnoreMember] public string? QueryParams {get;init;}
}
