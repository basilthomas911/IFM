using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Queries;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Reference;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.ViewModels;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Query.Actor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Query;
/// <summary>Handles <see cref = "GetRegimeDiscoveryDecisionReferenceQuery"/>.</summary>
public static class GetRegimeDiscoveryDecisionReference
{

    extension(GetRegimeDiscoveryDecisionReferenceQuery query)
    {
        /// <summary>Generates and returns the immutable Regime Discovery decision reference.</summary>
        public ValueTask<ServiceResult<RegimeDiscoveryDecisionReferenceDto[]>> ExecuteAsync(IRegimeDiscoveryQueryContext services, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);
            cancellationToken.ThrowIfCancellationRequested();
            var result = new RegimeDiscoveryDecisionReferenceGenerator().Generate();
            return ValueTask.FromResult<ServiceResult<RegimeDiscoveryDecisionReferenceDto[]>>(new ServiceResult<RegimeDiscoveryDecisionReferenceDto[]>(result));
        }
    }
}
