using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Model;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Function.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Function.Actor;
public sealed class RiskManagementFunctionContext : FunctionActorContext, IRiskManagementFunctionContext
{
    readonly Lazy<IEventSourceFunctionStateRepository<RiskManagementFunctionState, ExecuteRiskManagementPipelineCommand>> _repository;
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6672", Justification = "This typed context adapts services for its actor and intentionally retains the actor logger category.")]
    public RiskManagementFunctionContext(IActorSupervisor supervisor, ILogger<RiskManagementFunctionActor> logger) : base(supervisor, new ActorMailboxId(ActorType.Function, RiskManagementFunctionActor.ActorName))
    {
        Logger = IsArgumentNull.Set(logger);
        TimeProvider = TimeProvider.System;
        _repository = new(() => Container.Resolve<IEventSourceFunctionStateRepository<RiskManagementFunctionState, ExecuteRiskManagementPipelineCommand>>());
    }

    public IRiskEvaluator CalculationModel { get; } = new RiskEvaluator();
    public ILogger<RiskManagementFunctionActor> Logger { get; }
    public TimeProvider TimeProvider { get; }
    public IEventSourceFunctionStateRepository<RiskManagementFunctionState, ExecuteRiskManagementPipelineCommand> StateRepository => _repository.Value;
    public IFunctionProjector<RiskManagementFunctionCompletedEvent>? FunctionProjector => null;
}
