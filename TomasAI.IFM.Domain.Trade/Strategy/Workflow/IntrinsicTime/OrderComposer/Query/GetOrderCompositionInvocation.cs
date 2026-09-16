using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Query.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Query.Model;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Query;

/// <summary>Handles <see cref="GetOrderCompositionInvocationQuery"/>.</summary>
public static class GetOrderCompositionInvocation
{
    /// <summary>Reads and returns the exact order-composition invocation.</summary>
    public static async ValueTask ExecuteAsync(this GetOrderCompositionInvocationQuery query, IOrderCompositionQueryContext services, IQueryActorContext<OrderCompositionQueryActor> context, CancellationToken cancellationToken)
    {
        var value = await OrderCompositionQueryModel.ExactAsync(services, query.Access, query.WorkflowId, query.InvocationId, cancellationToken);
        await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, new ServiceOk<OrderCompositionProjection>(value));
    }
}