using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection.Query.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection.Query.Model;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection.Query;
/// <summary>Handles <see cref="GetTradeSelectionResultQuery"/>.</summary>
public static class GetTradeSelectionResult
{
    /// <summary>Reads and returns the exact Trade Selection result.</summary>
    public static async ValueTask ExecuteAsync(this GetTradeSelectionResultQuery query, ITradeSelectionQueryContext services, IQueryActorContext<TradeSelectionQueryActor> context, CancellationToken cancellationToken)
    {
        var value = await TradeSelectionQueryModel.ExactAsync(services, query.Access, query.WorkflowId, query.InvocationId, cancellationToken);
        if (value.Completion.Result.ResultId != query.ResultId)
            throw new KeyNotFoundException("Exact selector result not found.");
        await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, new ServiceOk<TradeSelectionResult>(TradeSelectionContracts.ReadResult(value.Completion.Result)));
    }
}