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
        var handoff=view.FinancialHandoff;
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var api=context.FinancialApi;
        switch(handoff?.Phase ?? RiskFinancialHandoffPhase.None)
        {
            case RiskFinancialHandoffPhase.ReservePending:
            {
                var result=await api.ReserveAsync(handoff!.ReservationRequest,timeout.Token).ConfigureAwait(false);
                RiskUnitModel.Require(result.Success && result.Value?.Completed is not null,"RM.HANDOFF.RESERVATION_PENDING");
                break;
            }
            case RiskFinancialHandoffPhase.FundPending:
            {
                var grant=handoff!.Authorization!;
                var scope=new FinancialReadScope { PortfolioId=grant.PortfolioId,FundId=grant.FundId,
                    Access=new("IntrinsicTimeStrategyWorkflow",["LedgerRead"],[grant.PortfolioId]) };
                var prior=await api.GetFundRiskAuthorizationAsync(scope,new(handoff.FundCommandId),timeout.Token).ConfigureAwait(false);
                if(prior.Value?.Value?.Authorization!=grant)
                {
                    var result=await context.PortfolioCommands.AuthorizeRiskAsync(handoff.FundCommandId,
                        new(grant.PortfolioId,grant.FundId,grant.OrderId),handoff.FundOrderVersion,grant,timeout.Token).ConfigureAwait(false);
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
        var advance=RiskFinancialHandoff.Advance(view);
        await context.SendAsync<AdvanceRiskFinancialHandoffCommand,IntrinsicTimeStrategyWorkflowEntityId>(advance,advance.EntityId).ConfigureAwait(false);
    }
}
