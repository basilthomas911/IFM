using TomasAI.IFM.Domain.Reference.Configuration.Strategy.Query.Actor;
using TomasAI.IFM.Domain.Reference.Shared.Configuration.Strategy;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Reference.Configuration.Strategy.Query;
/// <summary>Handles <see cref="ResolveRegimeDiscoveryParameterSetQuery"/>.</summary>
public static class ResolveRegimeDiscoveryParameterSet
{
    /// <summary>Resolves and returns the effective Regime Discovery parameter set.</summary>
    public static async ValueTask ExecuteAsync(this ResolveRegimeDiscoveryParameterSetQuery query, IRegimeDiscoveryConfigurationQueryContext services, IQueryActorContext<RegimeDiscoveryConfigurationQueryActor> context, CancellationToken cancellationToken)
    {
        var result = (await services.ConfigurationDb.ResolveEffectiveRegimeDiscoveryAsync(query.EffectiveAtUtc, query.TargetHorizon, cancellationToken).ConfigureAwait(false))?.ParameterSet
            ?? throw new KeyNotFoundException("The requested Regime Discovery parameter set was not found.");
        await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, new ServiceResult<RegimeDiscoveryParameterSet>(result)).ConfigureAwait(false);
    }
}