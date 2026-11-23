using TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Model;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Realtime;

/// <summary>Pure cached eligibility first; financial commitment starts only from its acknowledged workflow snapshot.</summary>
public static class PreparedPortfolioRisk
{
    internal static async ValueTask EvaluateAsync(IntrinsicTimeStrategyWorkflowView view, IIntrinsicTimeStrategyWorkflowRealtimeContext context)
    {
        var now = context.TimeProvider.GetUtcNow().UtcDateTime;
        try
        {
            if (view.EntityId.Assignment is not { } assignment || context.DecisionRuntime is not { } runtime
                || context.FinancialDecisionState is not { } state
                || !state.TryAcquire(assignment.PortfolioId, assignment.FundId, now, out var financial))
            {
                await ExecuteRiskManagement.FailAsync(view, context, "RM.INPUT.FINANCIAL_NOT_READY", "No authoritative ready financial generation.", now).ConfigureAwait(false);
                return;
            }
            var request = PortfolioOrderCompositionMapper.CreateRequest(view, assignment.PortfolioId, now, financial!.FinancialRevision);
            var approval = await runtime.PortfolioAsync(request, financial, view).ConfigureAwait(false);
            var command = Completion(view, approval.Request.OperationId, "risk-decision") with
            { EntryApproval = approval, CompletedAtUtc = approval.ProducedAtUtc };
            await context.SendAsync<CompleteRiskManagementCommand, IntrinsicTimeStrategyWorkflowEntityId>(command, view.EntityId).ConfigureAwait(false);
        }
        catch (Exception error)
        {
            await ExecuteRiskManagement.FailAsync(view, context, "RM.DECISION.FAILED", error.GetBaseException().Message,
                context.TimeProvider.GetUtcNow().UtcDateTime).ConfigureAwait(false);
        }
    }

    /// <summary>This is outside Risk calculation; no eligibility is recalculated after this point.</summary>
    internal static async ValueTask CommitAsync(IntrinsicTimeStrategyWorkflowView view,
        IIntrinsicTimeStrategyWorkflowRealtimeContext context, bool reconcileOnly = false)
    {
        var pending = view.EntryCommit ?? throw new InvalidOperationException("Missing durable entry approval.");
        var service = context.EntryCommits ?? throw new InvalidOperationException("Entry-commit service is unavailable.");
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        PortfolioEntryCommitResult result;
        try
        {
            result = reconcileOnly || pending.Phase == WorkflowEntryCommitPhase.OutcomeUnknown
                ? await service.ReconcileAsync(pending.Approval, cancellation.Token).ConfigureAwait(false)
                : await service.CommitOnceAsync(pending.Approval, cancellation.Token).ConfigureAwait(false);
        }
        catch (Exception) { result = new(PortfolioEntryCommitOutcome.Unknown, null, "ENTRY.COMMIT.OUTCOME_UNKNOWN"); }
        var phase = result.Outcome switch
        {
            PortfolioEntryCommitOutcome.Committed => WorkflowEntryCommitPhase.Committed,
            PortfolioEntryCommitOutcome.Rejected => WorkflowEntryCommitPhase.Rejected,
            _ => WorkflowEntryCommitPhase.OutcomeUnknown
        };
        var at = context.TimeProvider.GetUtcNow().UtcDateTime;
        var source = result.Completed?.Id ?? PortfolioOrderCompositionMapper.StableId(pending.Approval.Request.OperationId, phase.ToString());
        if (result.Completed is { } completed)
            PortfolioOrderCompositionMapper.ValidateCompletion(view, pending.Approval.Request, completed);
        var command = Completion(view, source, "entry-commit") with
        {
            CompletedAtUtc = at,
            EntryCommit = pending with { Phase = phase, UpdatedAtUtc = at, Reason = result.Reason },
            PortfolioDecision = result.Completed is { } receipt ? PortfolioOrderCompositionMapper.ToDecision(receipt.Receipt) : null
        };
        await context.SendAsync<CompleteRiskManagementCommand, IntrinsicTimeStrategyWorkflowEntityId>(command, view.EntityId).ConfigureAwait(false);
    }

    static CompleteRiskManagementCommand Completion(IntrinsicTimeStrategyWorkflowView view, Guid source, string purpose) => new()
    {
        CommandId = PortfolioOrderCompositionMapper.StableId(source, $"{view.WorkflowId}/{view.WorkflowRevision}/{purpose}"),
        Subject = new(ActorType.Command, CompleteRiskManagementCommand.Actor, CompleteRiskManagementCommand.Verb, view.EntityId.Format()),
        EntityId = view.EntityId, WorkflowId = view.WorkflowId, InputWorkflowRevision = view.WorkflowRevision,
        SourceEventId = source, CorrelationId = view.CorrelationId, CausationId = source
    };
}
