using TomasAI.IFM.Domain.Portfolio.Shared.Common;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Query.Actor;
namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Query.Model;

/// <summary>Provides shared authorization and exact-read calculations for order-composition queries.</summary>
internal static class OrderCompositionQueryModel
{
    internal static async Task AuthorizeAsync(IOrderCompositionQueryContext services,CompositionQueryAccess access,int portfolioId,int fundId,CancellationToken token)
    {
        if(access is null || string.IsNullOrWhiteSpace(access.Principal) || access.Roles.Length==0)throw new UnauthorizedAccessException("Portfolio read authority is required.");
        using var scope=PortfolioAccessScope.Push(new(){Principal=access.Principal,Roles=access.Roles});
        var result=await services.PortfolioQueries.GetFundAsync(portfolioId,fundId,cancellationToken:token);
        if(!result.Success || result.Value is null || result.Value.PortfolioId!=portfolioId || result.Value.FundId!=fundId)throw new UnauthorizedAccessException("Portfolio/Fund access was denied.");
    }
    internal static async Task<OrderCompositionProjection> ExactAsync(IOrderCompositionQueryContext services,CompositionQueryAccess access,StrategyWorkflowId workflowId,Guid invocationId,CancellationToken token)
    {
        var completed=await services.DbFactory.TradeDb.GetOrderCompositionInvocationAsync(workflowId,invocationId,token)??throw new KeyNotFoundException("Exact composition invocation not found.");
        var result=OrderCompositionContracts.ReadResult(completed.Result);await AuthorizeAsync(services,access,result.DecisionContext.PortfolioId,result.DecisionContext.FundId,token);
        var read = new RedispatchCurrentStrategyPipelineCommand { EntityId = completed.EntityId,
            Subject = new(ActorType.Command, RedispatchCurrentStrategyPipelineCommand.Actor,
                RedispatchCurrentStrategyPipelineCommand.Verb, completed.EntityId.Format()) };
        var state = await services.WorkflowRepository.LoadStateAsync(read, token);
        var view = state.CurrentView?.WorkflowId == workflowId ? state.CurrentView : null;
        var accepted=view?.OrderComposition.Result?.PayloadSha256==completed.Result.PayloadSha256 && view.OrderComposition.SourceEventId==completed.Id;
        return new(completed,accepted,view is null,!accepted && view is {Status:not WorkflowStrategyMachineStatus.Started});
    }
}
