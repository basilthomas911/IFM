using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Queries;

/// <summary>Gets the terminal Regime Discovery projection for one workflow execution.</summary>
[MessagePackObject]
public sealed record GetRegimeDiscoveryQuery : IQuery<RegimeDiscoveryReadModel>
{
    /// <summary>Gets the Regime Discovery Query actor name.</summary>
    [IgnoreMember] public const string Actor = "RegimeDiscoveryPipelineQuery";
    /// <summary>Gets the query verb.</summary>
    [IgnoreMember] public const string Verb = "GetByWorkflowId";
    /// <summary>Gets the stable query error code.</summary>
    [IgnoreMember] public const int ErrorId = 23201;
    /// <inheritdoc />
    [Key(0)] public ActorSubject Subject { get; init; }
    /// <inheritdoc />
    [Key(1)] public IActorEntityId EntityId { get; init; } = ActorEntityId.Default;
    /// <summary>Gets the workflow execution identity.</summary>
    [Key(2)] public StrategyWorkflowId WorkflowId { get; init; }
    /// <inheritdoc />
    [IgnoreMember] public int ErrorCode { get; init; } = ErrorId;
    /// <inheritdoc />
    [IgnoreMember] public string? QueryParams { get; init; }
}
