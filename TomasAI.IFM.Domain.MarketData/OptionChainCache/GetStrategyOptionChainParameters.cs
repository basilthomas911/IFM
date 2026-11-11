using TomasAI.IFM.Domain.MarketData.Shared.OptionChainCache;
using TomasAI.IFM.Domain.MarketData.Query.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.OptionChainCache;

/// <summary>Queries persisted policy projections only; command actor state is never used as a read store.</summary>
public static class GetStrategyOptionChainParameters
{

    extension(GetStrategyOptionChainParametersQuery query)
    {
        /// <summary>Reads the requested immutable version.</summary>
        public async ValueTask<ServiceResult<StrategyOptionChainParameterSet>> ExecuteAsync(IMarketDataQueryContext context, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(query);
            if (query.ParameterSetId == Guid.Empty || query.Version < 1)
                throw new ArgumentException("Exact parameter identity is required", nameof(query));
            var parameters = await context.DbFactory.MarketDataDb.ReadVersionAsync(query.ParameterSetId, query.Version, token).ConfigureAwait(false);
            ServiceResult<StrategyOptionChainParameterSet> result = parameters is null
                ? new ServiceFailed<StrategyOptionChainParameterSet>(404, "Strategy option-chain parameter version not found.")
                : new ServiceOk<StrategyOptionChainParameterSet>(parameters);
            return result;
        }
    }
}
