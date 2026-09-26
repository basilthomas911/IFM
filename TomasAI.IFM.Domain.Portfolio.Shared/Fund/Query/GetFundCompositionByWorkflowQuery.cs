using TomasAI.IFM.Domain.Portfolio.Shared.Common;
using MessagePack;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Queries;

/// <summary>Represents the GetFundCompositionByWorkflowQuery actor message.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record GetFundCompositionByWorkflowQuery : IQuery<FundCompositionWorkflowProjectionReadModel[]>
{
    public const string Actor = PortfolioQueryRoutes.Fund;
    public const string Verb = "GetFundCompositionByWorkflow";
    public const int ErrorId = 34100;

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public ActorEntityId EntityId { get; init; } = ActorEntityId.Default;
    [Key(2)] public Guid WorkflowId { get; init; } = default!;
    [Key(3)] public Guid CorrelationId { get; init; }
    [Key(4)] public DateTime RequestedOnUtc { get; init; }
    [Key(5)] public PortfolioAccessContext Access { get; init; } = new();

    [IgnoreMember] public int ErrorCode => ErrorId;
    [IgnoreMember] public string? QueryParams => EntityId.Format();
    [IgnoreMember] IActorEntityId IQuery.EntityId => EntityId;

    /// <summary>Initializes an empty message for serialization.</summary>
    public GetFundCompositionByWorkflowQuery() { }

    /// <summary>Initializes the query from its application values.</summary>
    /// <param name="workflowId">The WorkflowId query value.</param>
    public GetFundCompositionByWorkflowQuery(Guid workflowId)
    {
        WorkflowId = workflowId;
    }

    /// <summary>Rehydrates every serialized field in permanent numeric-key order.</summary>
    /// <param name="subject">The Subject wire value.</param>
    /// <param name="entityId">The EntityId wire value.</param>
    /// <param name="workflowId">The WorkflowId wire value.</param>
    /// <param name="correlationId">The CorrelationId wire value.</param>
    /// <param name="requestedOnUtc">The RequestedOnUtc wire value.</param>
    /// <param name="access">The Access wire value.</param>
    [SerializationConstructor]
    public GetFundCompositionByWorkflowQuery(ActorSubject subject, ActorEntityId entityId, Guid workflowId, Guid correlationId, DateTime requestedOnUtc, PortfolioAccessContext access)
    {
        Subject = subject;
        EntityId = entityId;
        WorkflowId = workflowId;
        CorrelationId = correlationId;
        RequestedOnUtc = requestedOnUtc;
        Access = access;
    }
}
