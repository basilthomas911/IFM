using TomasAI.IFM.Domain.Trade.Shared;
using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition;
[MessagePackObject(AllowPrivate = true)]
public sealed record GetOrderCompositionResultQuery:IQuery<OrderCompositionResult>
{

    /// <summary>Creates an empty query for serialization.</summary>
    public GetOrderCompositionResultQuery() { }

    /// <summary>Rehydrates every published query field in permanent numeric-key order.</summary>
    /// <param name="subject">The Subject field.</param>
    /// <param name="entityId">The EntityId field.</param>
    /// <param name="access">The Access field.</param>
    /// <param name="workflowId">The WorkflowId field.</param>
    /// <param name="invocationId">The InvocationId field.</param>
    /// <param name="resultId">The ResultId field.</param>
    [SerializationConstructor]
    public GetOrderCompositionResultQuery(ActorSubject subject, IActorEntityId entityId, CompositionQueryAccess access, StrategyWorkflowId workflowId, Guid invocationId, Guid resultId)
    {
        Subject = subject;
        EntityId = entityId;
        Access = access;
        WorkflowId = workflowId;
        InvocationId = invocationId;
        ResultId = resultId;
    }
    [IgnoreMember] public const string Actor="OrderCompositionPipelineQuery";
    [IgnoreMember] public const string Verb="GetOrderCompositionResult";
    [IgnoreMember] public const int ErrorId=23214;
    [Key(0)] public ActorSubject Subject {get;init;}
    [Key(1)] public IActorEntityId EntityId {get;init;}=ActorEntityId.Default;
    [Key(2)] public CompositionQueryAccess Access {get;init;}=new(string.Empty,[]);
    [Key(3)] public StrategyWorkflowId WorkflowId {get;init;}
    [Key(4)] public Guid InvocationId {get;init;}
    [Key(5)] public Guid ResultId {get;init;}
    [IgnoreMember] public int ErrorCode {get;init;}=ErrorId;
    [IgnoreMember] public string? QueryParams {get;init;}
}
