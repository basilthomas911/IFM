using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Model;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Realtime;

/// <summary>Dispatches saved financial requests. Lost replies leave the checkpoint recoverable under its original identity.</summary>
public static class ExecuteRiskFinancialHandoff
{
    public static async ValueTask ExecuteFinancialHandoffAsync(this IntrinsicTimeStrategyWorkflowView view,IIntrinsicTimeStrategyWorkflowRealtimeContext context)
    {
        using var trace = WorkflowTrace.Start("risk.financial_handoff", view);
        var handoff=view.FinancialHandoff;
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var api=context.FinancialApi;
        switch(handoff?.Phase ?? RiskFinancialHandoffPhase.None)
        {
            case RiskFinancialHandoffPhase.ReservePending:
            {
                // Transport may represent a Function refusal as ServiceFailed with no typed value.
                // Any received reply prompts reconciliation, never permission to replace the request.
                // The Command requires an authoritative receipt or absent-receipt/newer-revision proof.
                using var timing_authorization_reserve_call = WorkflowTrace.Start("authorization.reserve_call", view);
                _ = await api.ReserveAsync(handoff!.ReservationRequest,timeout.Token).ConfigureAwait(false);
                timing_authorization_reserve_call?.Stop();
                break;
            }
            case RiskFinancialHandoffPhase.FundPending:
            {
                var grant=handoff!.Authorization!;
                var scope=new FinancialReadScope { PortfolioId=grant.PortfolioId,FundId=grant.FundId,
                    Access=new("IntrinsicTimeStrategyWorkflow",["LedgerRead"],[grant.PortfolioId]) };
                using var timing_authorization_prior_receipt_read = WorkflowTrace.Start("authorization.prior_receipt_read", view);
                var prior=await api.GetFundRiskAuthorizationAsync(scope,new(handoff.FundCommandId),timeout.Token).ConfigureAwait(false);
                timing_authorization_prior_receipt_read?.Stop();
                if(prior.Value?.Value?.Authorization!=grant)
                {
                    using var timing_authorization_fund_authorize_call = WorkflowTrace.Start("authorization.fund_authorize_call", view);
                    var result=await context.PortfolioCommands.AuthorizeRiskAsync(handoff.FundCommandId,
                        new(grant.PortfolioId,grant.FundId,grant.OrderId),handoff.FundOrderVersion,grant,timeout.Token).ConfigureAwait(false);
                    timing_authorization_fund_authorize_call?.Stop();
                    RiskUnitModel.Require(result.Success,"RM.HANDOFF.FUND_PENDING");
                }
                break;
            }
            // Execution is a future delivery. Never consume or submit from this workflow,
            // including when replaying preliminary checkpoints written by an older build.
            case RiskFinancialHandoffPhase.ConsumePending:
            case RiskFinancialHandoffPhase.Consumed:
            case RiskFinancialHandoffPhase.Submitted:
            case RiskFinancialHandoffPhase.Authorized:
                return;
        }
        using var timing_authorization_build_advance = WorkflowTrace.Start("authorization.build_advance", view);
        var advance=RiskFinancialHandoff.Advance(view);
        timing_authorization_build_advance?.Stop();
        using var timing_authorization_advance_send = WorkflowTrace.Start("authorization.advance_send", view);
        await context.SendAsync<AdvanceRiskFinancialHandoffCommand,IntrinsicTimeStrategyWorkflowEntityId>(advance,advance.EntityId).ConfigureAwait(false);
        timing_authorization_advance_send?.Stop();
    }
}
