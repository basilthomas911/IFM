using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.Extensions;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.State;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Model;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.Model;
namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command;

/// <summary>Queries authoritative services and commits a complete fifth-stage invocation before its notification can dispatch.</summary>
public static class PrepareRiskManagement
{
    /// <summary>An audit reservation alone is not a prepared invocation. The handler's committed revision/identity guards make retries safe.</summary>
    /// <param name="command">The concrete command intent.</param>
    /// <param name="token">Cancels preflight processing.</param>
    /// <returns>The prepared business result or command acceptance.</returns>
    public static ValueTask<bool> ResumeAfterAuditAsync(this PrepareRiskManagementCommand command, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        return ValueTask.FromResult(true);
    }
    /// <summary>Prepares the workflow decision, validates its immutable changes, and applies the source events.</summary>
    /// <param name="command">The concrete workflow command.</param>
    /// <param name="context">The authorized workflow services, clock, and logger.</param>
    /// <param name="state">The authoritative workflow state.</param>
    /// <returns>The command acceptance or rejection.</returns>
    public static async ValueTask<ServiceResult<GuidResult>> ExecuteAsync(this PrepareRiskManagementCommand command,
        ICommandActorContext<IntrinsicTimeStrategyWorkflowCommandActor> context, IntrinsicTimeStrategyWorkflowCommandState state)
    {
        var preparation = new WorkflowSnapshotPreparation(state.WorkflowDefinition);
        var preparationResult = await PrepareWorkflowAsync(command, context, preparation).ConfigureAwait(false);
        if (!preparationResult.Success) return preparationResult;
        var errorMsg = "IntrinsicTimeStrategyWorkflow.STATE.APPLY_FAILED: unable to apply PrepareRiskManagement event";
        var updated = command.Compute(preparation.Freeze(), out var workflowTransition) switch
        {
            _ when workflowTransition.RejectionReason is not null => command.UpdateFailed(ref errorMsg, workflowTransition.RejectionReason),
            _ => state.Update(command.CreateWorkflowLifecycleEvents(workflowTransition), command)
        };
        return updated ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId)) : command.UpdateFailed(errorMsg);
    }
    /// <summary>Prepares immutable workflow changes while preserving deadline, stale-result, and financial-read ordering.</summary>
    /// <param name="command">The concrete command intent.</param>
    /// <param name="context">The authorized actor services and logging context.</param>
    /// <param name="state">The authoritative state or local preparation snapshot, as declared by the method.</param>
    /// <returns>The prepared business result or command acceptance.</returns>
    internal static async ValueTask<ServiceResult<GuidResult>> PrepareWorkflowAsync(this PrepareRiskManagementCommand command,
        ICommandActorContext<IntrinsicTimeStrategyWorkflowCommandActor> context, WorkflowSnapshotPreparation state)
    {
        using var trace = WorkflowTrace.Source.StartActivity("risk.prepare");
        trace?.SetTag("ifm.workflow.id", command.WorkflowId.ToString());
        trace?.SetTag("ifm.workflow.entity", command.EntityId.Format());
        try { return await PrepareAsync(command, context, state).ConfigureAwait(false); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            var now = context.TimeProvider.GetUtcNow().UtcDateTime;
            return new FailRiskManagementCommand
            {
                CommandId = command.CommandId,
                EntityId = command.EntityId,
                WorkflowId = command.WorkflowId,
                Subject = new(ActorType.Command, FailRiskManagementCommand.Actor, FailRiskManagementCommand.Verb, command.EntityId.Format()),
                InputWorkflowRevision = command.InputWorkflowRevision,
                SourceEventId = command.CommandId,
                CorrelationId = state.CurrentView?.CorrelationId ?? command.CommandId,
                CausationId = command.CommandId,
                FailedAtUtc = now,
                Failure = new()
                {
                    ErrorCode = PrepareRiskManagementCommand.ErrorId,
                    ErrorType = "RiskPreparationFailed",
                    ErrorMessage = ex is RiskCalculationException risk ? risk.ReasonCode :
                        $"RM.PREPARATION.INVALID:{ex.Message}",
                    ErrorData = ex.GetType().Name,
                    FailedAtUtc = now
                }
            }.PrepareWorkflow(context, state);
        }
    }

    /// <summary>Prepares immutable workflow changes while preserving deadline, stale-result, and financial-read ordering.</summary>
    /// <param name="command">The concrete command intent.</param>
    /// <param name="context">The authorized actor services and logging context.</param>
    /// <param name="state">The authoritative state or local preparation snapshot, as declared by the method.</param>
    /// <returns>The prepared business result or command acceptance.</returns>
    static async ValueTask<ServiceResult<GuidResult>> PrepareAsync(PrepareRiskManagementCommand command,
        ICommandActorContext<IntrinsicTimeStrategyWorkflowCommandActor> context, WorkflowSnapshotPreparation state)
    {
        using var timing_risk_prepare_current_view = WorkflowTrace.Start("risk.prepare.current_view", null);
        var current = state.CurrentView;
        timing_risk_prepare_current_view?.Stop();
        if (current is not { Status: WorkflowStrategyMachineStatus.Started, CurrentStage: StrategyWorkflowStage.RiskManagement }
            || current.WorkflowId != command.WorkflowId || current.WorkflowRevision != command.InputWorkflowRevision
            || current.RiskExecution is not null) return new ServiceOk<GuidResult>(new(command.CommandId));
        var now = context.TimeProvider.GetUtcNow().UtcDateTime;
        using var timing_risk_prepare_candidate_decode = WorkflowTrace.Start("risk.prepare.candidate_decode", current);
        var candidate = current.OrderComposition.Result!.ReadCompositionResult().Candidate!;
        timing_risk_prepare_candidate_decode?.Stop();
        var deployment = current.SelectionBinding!.CatalogDefinitions.Single(x => x.Key == candidate.DeploymentKey);
        var reference = deployment.PipelineParameters.SingleOrDefault(x => x.Kind == CatalogPipelineParameterKind.RiskManagement)
            ?? throw new RiskCalculationException("RM.CONFIG.MISSING");
        using var timing_risk_prepare_policy_read = WorkflowTrace.Start("risk.prepare.policy_read", current);
        var row = await context.DbFactory.ConfigurationDb.GetSelectionPipelinePolicyAsync(reference.Kind, reference.Id, reference.Version).ConfigureAwait(false)
            ?? throw new RiskCalculationException("RM.CONFIG.MISSING");
        timing_risk_prepare_policy_read?.Stop();
        TradeSelectionContracts.ValidatePipelinePolicy(row);
        RiskUnitModel.Require(row.PayloadSha256 == reference.Hash && row.Status == CatalogLifecycleStatus.Published
            && row.EffectiveFromUtc is not null && row.EffectiveFromUtc <= now && !(row.RetiredAtUtc <= now), "RM.CONFIG.NOT_EFFECTIVE");
        var policy = RiskParameterSet.Read(row.PayloadJson);
        var owner = (IIntrinsicTimeStrategyWorkflowCommandContext)context;
        using var timing_risk_prepare_admission_read = WorkflowTrace.Start("risk.prepare.admission_read", current);
        var financial = await owner.FinancialApi.GetFinancialAdmissionSnapshotAsync(new()
        {
            PortfolioId = candidate.PortfolioId,
            FundId = candidate.FundId,
            Access = new("IntrinsicTimeStrategyWorkflow", ["LedgerRead"], [candidate.PortfolioId])
        }, new(candidate.DeploymentKey, FinancialScopeKeys.Underlying(candidate.Product.Symbol, candidate.Product.Exchange, candidate.Product.Currency))).ConfigureAwait(false);
        timing_risk_prepare_admission_read?.Stop();
        RiskUnitModel.Require(financial.Success && financial.Value is not null, "RM.AUTHORITY.UNAVAILABLE");
        now = context.TimeProvider.GetUtcNow().UtcDateTime;
        using var timing_risk_prepare_build_request = WorkflowTrace.Start("risk.prepare.build_request", current);
        var request = RiskPreparation.Create(current, policy, financial.Value!, command.CommandId, now);
        timing_risk_prepare_build_request?.Stop();
        var next = current with
        {
            WorkflowRevision = request.InputWorkflowRevision,
            UpdatedAtUtc = now,
            CausationId = command.CommandId,
            RiskExecution = request,
            RiskManagement = current.RiskManagement with { InputWorkflowRevision = request.InputWorkflowRevision, ExpiresAtUtc = request.ExpiresAtUtc }
        };
        using var updateTrace = WorkflowTrace.Start("risk.prepare.state_update", current);
        state.Record(new WorkflowSnapshotChange
        {
            SnapshotEventId = Guid.CreateVersion7(new DateTimeOffset(now)),
            EntityId = command.EntityId,
            WorkflowId = next.WorkflowId,
            WorkflowRevision = next.WorkflowRevision,
            CorrelationId = next.CorrelationId,
            CausationId = next.CausationId,
            PreviousStatus = current.Status,
            WorkflowDefinition = next,
            UpdatedAtUtc = now
        }, command);
        updateTrace?.Stop();
        return new ServiceOk<GuidResult>(new(command.CommandId));
    }
    /// <summary>Checks ownership and required data of prepared workflow changes without mutation.</summary>
    /// <param name="command">The concrete workflow intent.</param>
    /// <param name="workflowChanges">The immutable proposals prepared from the current workflow and observed inputs.</param>
    /// <param name="workflowTransition">The accepted proposals or business rejection.</param>
    /// <returns>True when every proposed workflow snapshot belongs to the command.</returns>
    internal static bool Compute(this PrepareRiskManagementCommand command, IReadOnlyList<WorkflowSnapshotChange> workflowChanges, out WorkflowSnapshotTransition workflowTransition)
    {
        workflowTransition = workflowChanges.Any(change => change.WorkflowDefinition is null || !Equals(change.EntityId, command.EntityId))
            ? new([], "IntrinsicTimeStrategyWorkflow: the computed workflow snapshot is missing or belongs to another entity.")
            : new(workflowChanges);
        return workflowTransition.RejectionReason is null;
    }
    /// <summary>Creates each ordered source event with the originating command identity.</summary>
    /// <param name="command">The originating workflow command.</param>
    /// <param name="workflowTransition">The accepted workflow snapshots.</param>
    /// <returns>The source events ready for state application.</returns>
    internal static IReadOnlyList<WorkflowStrategyStateUpdatedEvent> CreateWorkflowLifecycleEvents(this PrepareRiskManagementCommand command, WorkflowSnapshotTransition workflowTransition)
        => workflowTransition.WorkflowChanges.Select(change => new WorkflowStrategyStateUpdatedEvent
        {
            CommandId = command.CommandId,
            Subject = new(ActorType.Event, WorkflowStrategyStateUpdatedEvent.Actor, WorkflowStrategyStateUpdatedEvent.Verb, command.EntityId.Format()),
            Id = change.SnapshotEventId,
            EntityId = command.EntityId,
            AggregateId = command.EntityId.Format(),
            EventSource = command.EventSource,
            ReceivedOn = change.UpdatedAtUtc,
            WorkflowId = change.WorkflowId,
            WorkflowRevision = change.WorkflowRevision,
            CorrelationId = change.CorrelationId,
            CausationId = change.CausationId,
            PreviousStatus = change.PreviousStatus,
            WorkflowDefinition = change.WorkflowDefinition,
            UpdatedAtUtc = change.UpdatedAtUtc
        }).ToArray();
}
