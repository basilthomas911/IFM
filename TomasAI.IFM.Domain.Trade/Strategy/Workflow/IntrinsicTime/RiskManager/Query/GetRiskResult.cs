using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Query.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Query.Model;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Query;
/// <summary>Handles <see cref = "GetRiskResultQuery"/>.</summary>
public static class GetRiskResult
{

    extension(GetRiskResultQuery query)
    {
        /// <summary>Reads and returns the exact completed risk result.</summary>
        public async ValueTask<ServiceResult<RiskAssessmentResult>> ExecuteAsync(IRiskQueryContext services, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);
            ArgumentNullException.ThrowIfNull(services);
            var observation = await (RiskQueryModel.ExactAsync(services, query.Access, query.WorkflowId, query.InvocationId, cancellationToken)).ConfigureAwait(false);
            if (observation.Calculation is not { } result || result.ResultId != query.ResultId)
                throw new KeyNotFoundException("Exact Risk result not found.");
            return new ServiceOk<RiskAssessmentResult>(result);
        }
    }
}
