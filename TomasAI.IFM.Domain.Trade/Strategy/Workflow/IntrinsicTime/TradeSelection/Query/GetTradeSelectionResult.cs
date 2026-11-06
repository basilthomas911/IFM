using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection.Query.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection.Query.Model;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection.Query;
/// <summary>Handles <see cref = "GetTradeSelectionResultQuery"/>.</summary>
public static class GetTradeSelectionResult
{

    extension(GetTradeSelectionResultQuery query)
    {
        /// <summary>Reads and returns the exact Trade Selection result.</summary>
        public async ValueTask<ServiceResult<TradeSelectionResult>> ExecuteAsync(ITradeSelectionQueryContext services, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);
            ArgumentNullException.ThrowIfNull(services);
            var value = await (TradeSelectionQueryModel.ExactAsync(services, query.Access, query.WorkflowId, query.InvocationId, cancellationToken)).ConfigureAwait(false);
            if (value.Completion.Result.ResultId != query.ResultId)
                throw new KeyNotFoundException("Exact selector result not found.");
            return new ServiceOk<TradeSelectionResult>(TradeSelectionContracts.ReadResult(value.Completion.Result));
        }
    }
}
