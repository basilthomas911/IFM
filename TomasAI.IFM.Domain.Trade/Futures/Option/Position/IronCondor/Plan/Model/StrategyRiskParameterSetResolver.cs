using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Strategy.Contracts.Shared.Configuration;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Risk;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Model;

/// <summary>Resolves one exact published monitoring policy off the option tick path.</summary>
public sealed class StrategyRiskParameterSetResolver(IDbContextFactory databases, string environment,
    Guid? parameterSetId = null, int version = 1)
{
    readonly SemaphoreSlim selection = new(1, 1);
    StrategyRiskParameterSet? selected;

    /// <summary>Loads the configured immutable version; only Development may provision the explicit development default.</summary>
    public async Task<StrategyRiskParameterSet> ResolveAsync(CancellationToken token)
    {
        if (selected is not null) return selected;
        await selection.WaitAsync(token).ConfigureAwait(false);
        try
        {
            if (selected is not null) return selected;
            var development = environment == "Development";
            var defaults = StrategyRiskParameterSet.CreateIronCondorDevelopmentDefault();
            var id = parameterSetId ?? (development ? defaults.ParameterSetId
                : throw new InvalidOperationException("IronCondorRisk.PARAMETER_SET_REQUIRED"));
            var stored = await databases.ConfigurationDb.GetStrategyPositionRiskVersionAsync(id, version, token).ConfigureAwait(false);
            if (stored is null && development && id == defaults.ParameterSetId && version == defaults.Version)
            {
                try { await databases.ConfigurationDb.InsertStrategyPositionRiskDraftAsync(defaults,
                    "Provisional ES daily-loss monitoring defaults", "DevelopmentDefaultProvisioner", token).ConfigureAwait(false); }
                catch when (!token.IsCancellationRequested)
                {
                    if (await databases.ConfigurationDb.GetStrategyPositionRiskVersionAsync(id, version, token).ConfigureAwait(false) is null) throw;
                }
                stored = await databases.ConfigurationDb.GetStrategyPositionRiskVersionAsync(id, version, token).ConfigureAwait(false);
            }
            if (development && stored?.Status == ConfigurationParameterSetStatus.Draft
                && stored.ParameterSet.Hash() == defaults.Hash())
            {
                await databases.ConfigurationDb.PublishAsync(StrategyParameterSetKind.StrategyPositionRisk,
                    id, version, DateTime.UtcNow.AddSeconds(-1), token).ConfigureAwait(false);
                stored = await databases.ConfigurationDb.GetStrategyPositionRiskVersionAsync(id, version, token).ConfigureAwait(false);
            }
            var now = DateTime.UtcNow;
            if (stored is null || stored.Status != ConfigurationParameterSetStatus.Published
                || stored.EffectiveFromUtc is null || stored.EffectiveFromUtc > now || stored.RetiredAtUtc <= now
                || stored.ParameterSet.Environment != environment)
                throw new InvalidOperationException("IronCondorRisk.PUBLISHED_ENVIRONMENT_POLICY_REQUIRED");
            return selected = stored.ParameterSet;
        }
        finally { selection.Release(); }
    }
}
