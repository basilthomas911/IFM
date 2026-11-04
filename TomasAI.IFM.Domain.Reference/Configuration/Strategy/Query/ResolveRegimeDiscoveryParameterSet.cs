using TomasAI.IFM.Domain.Reference.Configuration.Strategy.Query.Actor;
using TomasAI.IFM.Domain.Reference.Shared.Configuration.Strategy;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Reference.Configuration.Strategy.Query;
/// <summary>Handles <see cref="ResolveRegimeDiscoveryParameterSetQuery"/>.</summary>
public static class ResolveRegimeDiscoveryParameterSet
{

    extension(ResolveRegimeDiscoveryParameterSetQuery query)
    {
        /// <summary>Resolves and returns the effective Regime Discovery parameter set.</summary>
        public async ValueTask<RegimeDiscoveryParameterSet> ExecuteAsync(IRegimeDiscoveryConfigurationQueryContext services, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);
            ArgumentNullException.ThrowIfNull(services);
            var result = (await services.ConfigurationDb.GetEffectiveRegimeDiscoveryAsync(query.EffectiveAtUtc, query.TargetHorizon, cancellationToken).ConfigureAwait(false))?.ParameterSet
                ?? throw new KeyNotFoundException("The requested Regime Discovery parameter set was not found.");
            return result;
        }
    }
}
