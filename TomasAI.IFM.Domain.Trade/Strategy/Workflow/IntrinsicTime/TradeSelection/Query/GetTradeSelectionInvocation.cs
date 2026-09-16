using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection.Query.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection.Query.Model;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection.Query;
/// <summary>Handles <see cref="GetTradeSelectionInvocationQuery"/>.</summary>
public static class GetTradeSelectionInvocation
{
    /// <summary>Reads and returns the exact Trade Selection invocation.</summary>
    public static async ValueTask ExecuteAsync(this GetTradeSelectionInvocationQuery query, ITradeSelectionQueryContext services, IQueryActorContext<TradeSelectionQueryActor> context, CancellationToken cancellationToken)
    {
        var value = await TradeSelectionQueryModel.ExactAsync(services, query.Access, query.WorkflowId, query.InvocationId, cancellationToken);
        await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, new ServiceOk<TradeSelectionProjection>(value));
    }
}