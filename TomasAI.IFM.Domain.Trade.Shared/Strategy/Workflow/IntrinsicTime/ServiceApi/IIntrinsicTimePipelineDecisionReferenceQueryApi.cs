using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Reference;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Reference;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.ServiceApi;

/// <summary>Queries generated, non-authoritative pipeline decision references over the actor transport.</summary>
public interface IIntrinsicTimePipelineDecisionReferenceQueryApi
{
    ValueTask<ServiceResult<RegimeDiscoveryDecisionReferenceDto[]>> GetRegimeDiscoveryAsync(
        CancellationToken cancellationToken = default);

    ValueTask<ServiceResult<MarketConditionDecisionReferenceDto[]>> GetMarketConditionAsync(
        CancellationToken cancellationToken = default);
}
