using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Query.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Query.Model;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Query;
/// <summary>Handles <see cref = "GetOrderCompositionResultQuery"/>.</summary>
public static class GetOrderCompositionResult
{

    extension(GetOrderCompositionResultQuery query)
    {
        /// <summary>Reads and returns the exact completed order-composition result.</summary>
        public async ValueTask<ServiceResult<OrderCompositionResult>> ExecuteAsync(IOrderCompositionQueryContext services, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);
            ArgumentNullException.ThrowIfNull(services);
            var value = await (OrderCompositionQueryModel.ExactAsync(services, query.Access, query.WorkflowId, query.InvocationId, cancellationToken)).ConfigureAwait(false);
            if (value.Completion.Result.ResultId != query.ResultId)
                throw new KeyNotFoundException("Exact composition result not found.");
            return new ServiceOk<OrderCompositionResult>(OrderCompositionContracts.ReadResult(value.Completion.Result));
        }
    }
}
