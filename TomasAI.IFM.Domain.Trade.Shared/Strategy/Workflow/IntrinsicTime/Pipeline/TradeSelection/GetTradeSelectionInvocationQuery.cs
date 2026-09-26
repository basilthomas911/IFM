using TomasAI.IFM.Domain.Trade.Shared;
using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
[MessagePackObject(AllowPrivate = true)]
public sealed record GetTradeSelectionInvocationQuery:IQuery<TradeSelectionProjection>
{

    /// <summary>Creates an empty query for serialization.</summary>
    public GetTradeSelectionInvocationQuery() { }

    /// <summary>Rehydrates every published query field in permanent numeric-key order.</summary>
    /// <param name="subject">The Subject field.</param>
    /// <param name="entityId">The EntityId field.</param>
    /// <param name="access">The Access field.</param>
    /// <param name="workflowId">The WorkflowId field.</param>
    /// <param name="invocationId">The InvocationId field.</param>
    [SerializationConstructor]
    public GetTradeSelectionInvocationQuery(ActorSubject subject, IActorEntityId entityId, SelectionQueryAccess access, StrategyWorkflowId workflowId, Guid invocationId)
    {
        Subject = subject;
        EntityId = entityId;
        Access = access;
        WorkflowId = workflowId;
        InvocationId = invocationId;
    }
    [IgnoreMember] public const string Actor="TradeSelectionPipelineQuery";
    [IgnoreMember] public const string Verb="GetTradeSelectionInvocation";
    [IgnoreMember] public const int ErrorId=23210;
    [Key(0)] public ActorSubject Subject {get;init;}
    [Key(1)] public IActorEntityId EntityId {get;init;}=ActorEntityId.Default;
    [Key(2)] public SelectionQueryAccess Access {get;init;}=new(string.Empty,[]);
    [Key(3)] public StrategyWorkflowId WorkflowId {get;init;}
    [Key(4)] public Guid InvocationId {get;init;}
    [IgnoreMember] public int ErrorCode {get;init;}=ErrorId;
    [IgnoreMember] public string? QueryParams {get;init;}
}
