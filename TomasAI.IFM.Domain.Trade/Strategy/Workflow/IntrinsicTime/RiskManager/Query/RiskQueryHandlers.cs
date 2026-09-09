using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Query.Actor;
namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Query;
public static class RiskQueryHandlers
{
    public static async ValueTask ExecuteAsync(this GetRiskInvocationQuery q,IRiskQueryContext s,IQueryActorContext<RiskQueryActor> c,CancellationToken t)
        => await c.ReplyAsync(q.Subject.ThreadId,q.Subject.Verb,new ServiceOk<RiskObservation>(await Exact(s,q.Access,q.WorkflowId,q.InvocationId,t)));
    public static async ValueTask ExecuteAsync(this GetRiskResultQuery q,IRiskQueryContext s,IQueryActorContext<RiskQueryActor> c,CancellationToken t)
    {
        var observation=await Exact(s,q.Access,q.WorkflowId,q.InvocationId,t);
        if(observation.Calculation is not {} result || result.ResultId!=q.ResultId) throw new KeyNotFoundException("Exact Risk result not found.");
        await c.ReplyAsync(q.Subject.ThreadId,q.Subject.Verb,new ServiceOk<RiskAssessmentResult>(result));
    }
    public static async ValueTask ExecuteAsync(this GetRiskHistoryPageQuery q,IRiskQueryContext s,IQueryActorContext<RiskQueryActor> c,CancellationToken t)
    {
        await Authorize(s,q.Access,q.PortfolioId,q.FundId,t);
        var page=await s.DbFactory.TradeDb.GetRiskHistoryAsync(q.PortfolioId,q.FundId,q.ValueDate,q.PageSize,RiskPaging.Decode(q.PortfolioId,q.FundId,q.ValueDate,q.PageSize,q.PagingState),t);
        await c.ReplyAsync(q.Subject.ThreadId,q.Subject.Verb,new ServiceOk<RiskHistoryPage>(new(page.Items,RiskPaging.Encode(q.PortfolioId,q.FundId,q.ValueDate,q.PageSize,page.PagingState))));
    }
    static async Task Authorize(IRiskQueryContext s,CompositionQueryAccess access,int portfolio,int fund,CancellationToken t)
    {
        if(access is null || string.IsNullOrWhiteSpace(access.Principal) || access.Roles.Length==0) throw new UnauthorizedAccessException("Portfolio read authority required.");
        using var scope=PortfolioAccessScope.Push(new(){Principal=access.Principal,Roles=access.Roles});
        var value=await s.PortfolioQueries.GetFundAsync(portfolio,fund,cancellationToken:t);
        if(!value.Success || value.Value?.PortfolioId!=portfolio || value.Value.FundId!=fund) throw new UnauthorizedAccessException("Portfolio/Fund access denied.");
    }
    public static async Task<RiskObservation> Exact(IRiskQueryContext s,CompositionQueryAccess access,StrategyWorkflowId workflow,Guid invocation,CancellationToken t)
    {
        if(workflow.Value==Guid.Empty || invocation==Guid.Empty) throw new ArgumentException("Exact Risk identities required.");
        TomasAI.IFM.Shared.EventSourcing.IEvent? source=null;
        var authoritativeUnavailable=false;
        WorkflowStrategyStateUpdatedEvent? projected=null;
        try { projected=await s.DbFactory.TradeDb.GetRiskInvocationAsync(workflow.Value,invocation,t); }
        catch(OperationCanceledException) when(t.IsCancellationRequested){throw;}
        catch(Exception e) when(e is InvalidDataException || e.InnerException is InvalidDataException){throw;}
        catch(Exception e){s.Logger.LogWarning(e,"Risk history unavailable; reading committed invocation {Invocation}",invocation);}
        try {source=await s.Journal.ByCommandAsync(invocation,t);}
        catch(OperationCanceledException) when(t.IsCancellationRequested){throw;}
        catch(Exception e) when(projected is not null){authoritativeUnavailable=true;s.Logger.LogWarning(e,"Risk committed source unavailable for {Invocation}",invocation);}
        var snapshot=projected ?? source as WorkflowStrategyStateUpdatedEvent;
        var completion=source as RiskManagementFunctionCompletedEvent;
        var entity=snapshot?.EntityId ?? completion?.EntityId ?? throw new KeyNotFoundException("Risk invocation not found.");
        var read=new RedispatchCurrentStrategyPipelineCommand { EntityId=entity,Subject=new(ActorType.Command,RedispatchCurrentStrategyPipelineCommand.Actor,RedispatchCurrentStrategyPipelineCommand.Verb,entity.Format()) };
        TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model.IntrinsicTimeStrategyWorkflowView? current=null;
        try {current=(await s.WorkflowRepository.LoadStateAsync(read,t)).CurrentView;}
        catch(OperationCanceledException) when(t.IsCancellationRequested){throw;}
        catch(Exception e) when(snapshot is not null){authoritativeUnavailable=true;s.Logger.LogWarning(e,"Risk current workflow unavailable for {Invocation}",invocation);}
        var behind=false;
        if(current?.WorkflowId==workflow && current.RiskExecution?.CommandId==invocation && (snapshot is null || current.WorkflowRevision>snapshot.WorkflowRevision))
        {
            behind=true;
            snapshot=await s.Journal.ByCommandAsync(current.CausationId,t) as WorkflowStrategyStateUpdatedEvent
                ?? throw new InvalidDataException("Current workflow has no committed snapshot evidence.");
        }
        if(snapshot is null || RiskHistoryIdentity.Row(snapshot) is not {} row || row.WorkflowId!=workflow.Value || row.InvocationId!=invocation)
            throw new KeyNotFoundException("Exact Risk invocation awaiting history projection.");
        await Authorize(s,access,row.PortfolioId,row.FundId,t);
        var calculation=snapshot.State.RiskManagement.Result?.RiskResult ?? (completion?.WorkflowId==workflow ? completion.Result : null);
        var accepted=calculation is not null && snapshot.State.RiskManagement.Result?.RiskResult?.ResultId==calculation.ResultId;
        if(authoritativeUnavailable)return new(snapshot,calculation,"Unavailable","Unavailable",DateTime.UtcNow,null,behind,accepted);
        TomasAI.IFM.Domain.Portfolio.Shared.ViewModels.FundOrderProjectionReadModel order;
        try {order=(await s.Funds.LoadFundAsync(new PortfolioFundId(row.PortfolioId,row.FundId),t)).Composition(checked((int)row.OrderId)).Order;}
        catch(OperationCanceledException) when(t.IsCancellationRequested){throw;}
        catch(Exception e){s.Logger.LogWarning(e,"Current Fund state unavailable for {Invocation}",invocation);return new(snapshot,calculation,"Unavailable","Unavailable",DateTime.UtcNow,null,behind,accepted);}
        try
        {
        var sync=snapshot.TerminalRisk is {} terminal ? order.TerminalRisk==terminal ? "Synchronized" : order.RiskAuthorization is null ? "Pending" : "Reconciliation required" : order.Status;
        if(sync=="Pending" && snapshot.State.FinancialHandoff is {} pending)
        {
            var scope=new FinancialReadScope {PortfolioId=row.PortfolioId,FundId=row.FundId,Access=new(access.Principal,["LedgerRead"],[row.PortfolioId])};
            var receipt=await s.Financial.ReadAsync(scope,new GetPostingReceiptRequest(pending.ReservationRequest.OperationId),t);
            if(receipt.Status==FinancialReadStatus.Unavailable)sync="Pending: financial state unavailable";
            else if(receipt.Value?.Reservation is {} grant)
            {
                var hold=await s.Financial.ReadAsync(scope,new GetCapacityReservationRequest(grant.Receipt.ReservationId),t);
                sync=hold.Status==FinancialReadStatus.Unavailable ? "Pending: financial state unavailable" :
                    hold.Value?.Current.Status is ReservationStatus.Released or ReservationStatus.Expired ? "Pending" : "Pending: capacity reconciliation required";
            }
        }
        var authority="Not authorized";
        var now=DateTime.UtcNow;
        if(snapshot.State.FinancialHandoff is { Phase:RiskFinancialHandoffPhase.Authorized, Authorization: {} authorization })
        {
            authority="Superseded";
            if(current?.WorkflowId==workflow && current.RiskExecution?.CommandId==invocation)
            {
                var scope=new FinancialReadScope { PortfolioId=row.PortfolioId,FundId=row.FundId,Access=new(access.Principal,["LedgerRead"],[row.PortfolioId]) };
                var reservation=await s.Financial.ReadAsync(scope,new GetCapacityReservationRequest(authorization.ReservationId),t);
                var admission=await s.Financial.ReadAsync(scope,new GetFinancialAdmissionSnapshotRequest(snapshot.State.RiskExecution!.Authority.DeploymentKey,snapshot.State.RiskExecution.SizingAuthority.UnderlyingId),t);
                now=DateTime.UtcNow;
                authority=reservation.Status==FinancialReadStatus.Found && admission.Status==FinancialReadStatus.Found && reservation.FinancialRevision!=admission.FinancialRevision ? "Changed during read; refresh" : admission.Status==FinancialReadStatus.Unavailable ? "Unavailable" : admission.Value is null || !admission.Value.CanPrepareAdmission || admission.Value.Authority!=snapshot.State.RiskExecution.Authority ? "Authority revoked or changed" : reservation.Status==FinancialReadStatus.Unavailable ? "Unavailable" : reservation.Value is not {} value ? "Reservation not found" :
                    now>=value.Current.ValidUntilUtc ? "Expired" : value.Current.Status!=ReservationStatus.Reserved ? value.Current.Status.ToString() :
                    order.RiskAuthorization!=authorization ? "Fund authorization differs" : "Authorized intent";
            }
        }
        return new(snapshot,calculation,authority,sync,now,order,behind,accepted);
        }
        catch(OperationCanceledException) when(t.IsCancellationRequested){throw;}
        catch(Exception e){s.Logger.LogWarning(e,"Current financial authority unavailable for {Invocation}",invocation);return new(snapshot,calculation,"Unavailable","Pending: financial state unavailable",DateTime.UtcNow,order,behind,accepted);}
    }
}
