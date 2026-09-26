using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Reference.Shared.ParameterSets;

[MessagePackObject(AllowPrivate = true)]
public sealed record GetParameterAssignmentQuery:IQuery<ParameterAssignmentSnapshot>
{

    /// <summary>Creates an empty query for serialization.</summary>
    public GetParameterAssignmentQuery() { }

    /// <summary>Rehydrates the published query fields in permanent numeric-key order.</summary>
    /// <param name="subject">The Subject field.</param>
    /// <param name="entityId">The EntityId field.</param>
    /// <param name="workflowDefinitionId">The WorkflowDefinitionId field.</param>
    /// <param name="targetHorizon">The TargetHorizon field.</param>
    [SerializationConstructor]
    public GetParameterAssignmentQuery(ActorSubject subject, IActorEntityId entityId, string workflowDefinitionId, int targetHorizon)
    {
        Subject = subject;
        EntityId = entityId;
        WorkflowDefinitionId = workflowDefinitionId;
        TargetHorizon = targetHorizon;
    }
 public const string Actor="ParameterSetQuery"; public const string Verb="GetParameterAssignment";
 [Key(0)] public ActorSubject Subject{get;init;}
 [Key(1)] public IActorEntityId EntityId{get;init;}=ActorEntityId.Default;
 [Key(2)] public string WorkflowDefinitionId{get;init;}=string.Empty;
 [Key(3)] public int TargetHorizon{get;init;}
 [IgnoreMember] public int ErrorCode{get;init;}=33101;
 [IgnoreMember] public string? QueryParams{get;init;}
}
