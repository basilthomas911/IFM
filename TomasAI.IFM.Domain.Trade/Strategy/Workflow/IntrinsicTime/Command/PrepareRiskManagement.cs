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

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command;

/// <summary>Queries authoritative services and commits a complete fifth-stage invocation before its notification can dispatch.</summary>
public static class PrepareRiskManagement
{
    /// <summary>An audit reservation alone is not a prepared invocation. The handler's committed revision/identity guards make retries safe.</summary>
    public static ValueTask<bool> ResumeAfterAuditAsync(this PrepareRiskManagementCommand command,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        return ValueTask.FromResult(true);
    }
    public static async ValueTask<ServiceResult<GuidResult>> ExecuteAsync(this PrepareRiskManagementCommand command,
        ICommandActorContext<IntrinsicTimeStrategyWorkflowCommandActor> context,IntrinsicTimeStrategyWorkflowCommandState state)
    {
        using var trace = WorkflowTrace.Source.StartActivity("risk.prepare");
        trace?.SetTag("ifm.workflow.id", command.WorkflowId.ToString());
        trace?.SetTag("ifm.workflow.entity", command.EntityId.Format());
        try { return await PrepareAsync(command,context,state).ConfigureAwait(false); }
        catch(Exception ex) when(ex is ArgumentException or InvalidOperationException)
        {
            var now=context.TimeProvider.GetUtcNow().UtcDateTime;
            return new FailRiskManagementCommand
            {
                CommandId=command.CommandId,EntityId=command.EntityId,WorkflowId=command.WorkflowId,
                Subject=new(ActorType.Command,FailRiskManagementCommand.Actor,FailRiskManagementCommand.Verb,command.EntityId.Format()),
                InputWorkflowRevision=command.InputWorkflowRevision,SourceEventId=command.CommandId,
                CorrelationId=state.CurrentView?.CorrelationId ?? command.CommandId,CausationId=command.CommandId,FailedAtUtc=now,
                Failure=new() { ErrorCode=PrepareRiskManagementCommand.ErrorId,ErrorType="RiskPreparationFailed",
                    ErrorMessage=ex is RiskCalculationException risk ? risk.ReasonCode :
                        $"RM.PREPARATION.INVALID:{ex.Message}",
                    ErrorData=ex.GetType().Name,
                    FailedAtUtc=now }
            }.Execute(context,state);
        }
    }

    static async ValueTask<ServiceResult<GuidResult>> PrepareAsync(PrepareRiskManagementCommand command,
        ICommandActorContext<IntrinsicTimeStrategyWorkflowCommandActor> context,IntrinsicTimeStrategyWorkflowCommandState state)
    {
        using var timing_risk_prepare_current_view = WorkflowTrace.Start("risk.prepare.current_view", null);
        var current=state.CurrentView;
        timing_risk_prepare_current_view?.Stop();
        if (current is not { Status:WorkflowStrategyMachineStatus.Started,CurrentStage:StrategyWorkflowStage.RiskManagement }
            || current.WorkflowId!=command.WorkflowId || current.WorkflowRevision!=command.InputWorkflowRevision
            || current.RiskExecution is not null) return new ServiceOk<GuidResult>(new(command.CommandId));
        var now=context.TimeProvider.GetUtcNow().UtcDateTime;
        using var timing_risk_prepare_candidate_decode = WorkflowTrace.Start("risk.prepare.candidate_decode", current);
        var candidate=current.OrderComposition.Result!.ReadCompositionResult().Candidate!;
        timing_risk_prepare_candidate_decode?.Stop();
        var deployment=current.SelectionBinding!.CatalogDefinitions.Single(x=>x.Key==candidate.DeploymentKey);
        var reference=deployment.PipelineParameters.SingleOrDefault(x=>x.Kind==CatalogPipelineParameterKind.RiskManagement)
            ?? throw new RiskCalculationException("RM.CONFIG.MISSING");
        using var timing_risk_prepare_policy_read = WorkflowTrace.Start("risk.prepare.policy_read", current);
        var row=await context.DbFactory.ConfigurationDb.GetSelectionPipelinePolicyAsync(reference.Kind,reference.Id,reference.Version).ConfigureAwait(false)
            ?? throw new RiskCalculationException("RM.CONFIG.MISSING");
        timing_risk_prepare_policy_read?.Stop();
        TradeSelectionContracts.ValidatePipelinePolicy(row);
        RiskUnitModel.Require(row.PayloadSha256==reference.Hash && row.Status==CatalogLifecycleStatus.Published
            && row.EffectiveFromUtc is not null && row.EffectiveFromUtc<=now && !(row.RetiredAtUtc<=now),"RM.CONFIG.NOT_EFFECTIVE");
        var policy=RiskParameterSet.Read(row.PayloadJson);
        var owner=(IIntrinsicTimeStrategyWorkflowCommandContext)context;
        using var timing_risk_prepare_admission_read = WorkflowTrace.Start("risk.prepare.admission_read", current);
        var financial=await owner.FinancialApi.GetFinancialAdmissionSnapshotAsync(new()
        {
            PortfolioId=candidate.PortfolioId,FundId=candidate.FundId,
            Access=new("IntrinsicTimeStrategyWorkflow",["LedgerRead"],[candidate.PortfolioId])
        },new(candidate.DeploymentKey,FinancialScopeKeys.Underlying(candidate.Product.Symbol,candidate.Product.Exchange,candidate.Product.Currency))).ConfigureAwait(false);
        timing_risk_prepare_admission_read?.Stop();
        RiskUnitModel.Require(financial.Success && financial.Value is not null,"RM.AUTHORITY.UNAVAILABLE");
        now=context.TimeProvider.GetUtcNow().UtcDateTime;
        using var timing_risk_prepare_build_request = WorkflowTrace.Start("risk.prepare.build_request", current);
        var request=RiskPreparation.Create(current,policy,financial.Value!,command.CommandId,now);
        timing_risk_prepare_build_request?.Stop();
        var next=current with
        {
            WorkflowRevision=request.InputWorkflowRevision,UpdatedAtUtc=now,CausationId=command.CommandId,RiskExecution=request,
            RiskManagement=current.RiskManagement with { InputWorkflowRevision=request.InputWorkflowRevision,ExpiresAtUtc=request.ExpiresAtUtc }
        };
        using var updateTrace = WorkflowTrace.Start("risk.prepare.state_update", current);
        state.Update(new WorkflowStrategyStateUpdatedEvent
        {
            Subject=new(ActorType.Event,WorkflowStrategyStateUpdatedEvent.Actor,WorkflowStrategyStateUpdatedEvent.Verb,command.EntityId.Format()),
            Id=Guid.CreateVersion7(new DateTimeOffset(now)),EntityId=command.EntityId,CommandId=command.CommandId,
            AggregateId=command.EntityId.Format(),EventSource=command.EventSource,ReceivedOn=now,WorkflowId=next.WorkflowId,
            WorkflowRevision=next.WorkflowRevision,CorrelationId=next.CorrelationId,CausationId=next.CausationId,
            PreviousStatus=current.Status,State=next,UpdatedAtUtc=now
        },command);
        updateTrace?.Stop();
        return new ServiceOk<GuidResult>(new(command.CommandId));
    }
}
