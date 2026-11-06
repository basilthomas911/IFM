using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection.Query.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection.Query.Model;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection.Query;
/// <summary>Handles <see cref = "GetTradeSelectionInvocationQuery"/>.</summary>
public static class GetTradeSelectionInvocation
{

    extension(GetTradeSelectionInvocationQuery query)
    {
        /// <summary>Reads and returns the exact Trade Selection invocation.</summary>
        public async ValueTask<ServiceResult<TradeSelectionProjection>> ExecuteAsync(ITradeSelectionQueryContext services, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);
            ArgumentNullException.ThrowIfNull(services);
            var value = await (TradeSelectionQueryModel.ExactAsync(services, query.Access, query.WorkflowId, query.InvocationId, cancellationToken)).ConfigureAwait(false);
            return new ServiceOk<TradeSelectionProjection>(value);
        }
    }
}
