using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Query.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Query.Model;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Query;
/// <summary>Handles <see cref = "GetOrderCompositionInvocationQuery"/>.</summary>
public static class GetOrderCompositionInvocation
{

    extension(GetOrderCompositionInvocationQuery query)
    {
        /// <summary>Reads and returns the exact order-composition invocation.</summary>
        public async ValueTask<ServiceResult<OrderCompositionProjection>> ExecuteAsync(IOrderCompositionQueryContext services, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);
            ArgumentNullException.ThrowIfNull(services);
            var value = await (OrderCompositionQueryModel.ExactAsync(services, query.Access, query.WorkflowId, query.InvocationId, cancellationToken)).ConfigureAwait(false);
            return new ServiceOk<OrderCompositionProjection>(value);
        }
    }
}
