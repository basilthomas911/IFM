using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Query.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Query.Model;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Query;

/// <summary>Handles <see cref="GetOrderCompositionResultQuery"/>.</summary>
public static class GetOrderCompositionResult
{
    /// <summary>Reads and returns the exact completed order-composition result.</summary>
    public static async ValueTask ExecuteAsync(this GetOrderCompositionResultQuery query, IOrderCompositionQueryContext services, IQueryActorContext<OrderCompositionQueryActor> context, CancellationToken cancellationToken)
    {
        var value = await OrderCompositionQueryModel.ExactAsync(services, query.Access, query.WorkflowId, query.InvocationId, cancellationToken);
        if (value.Completion.Result.ResultId != query.ResultId)
            throw new KeyNotFoundException("Exact composition result not found.");
        await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, new ServiceOk<OrderCompositionResult>(OrderCompositionContracts.ReadResult(value.Completion.Result)));
    }
}