using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Strategy.Contracts.Shared.Configuration;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Risk;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Model;
/// <summary>Resolves one exact published monitoring policy off the option tick path.</summary>
public sealed class StrategyRiskParameterSetResolver(IDbContextFactory databases, string environment, Guid? parameterSetId = null, int version = 1) : IDisposable
{
    int disposed;
    /// <summary>Releases the owned serialization gate after the host has stopped its workers; safe for DI aliases.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) == 0)
            selection.Dispose();
    }

    readonly SemaphoreSlim selection = new(1, 1);
    StrategyRiskParameterSet? selected;
    /// <summary>Loads the configured immutable version; only Development may provision the explicit development default.</summary>
    public async Task<StrategyRiskParameterSet> ResolveAsync(CancellationToken token)
    {
        if (Volatile.Read(ref selected) is { } cached)
            return cached;
        await selection.WaitAsync(token).ConfigureAwait(false);
        try
        {
            if (Volatile.Read(ref selected) is { } resolved)
                return resolved;
            var development = string.Equals(environment, "Development", StringComparison.Ordinal);
            var defaults = StrategyRiskParameterSet.CreateIronCondorDevelopmentDefault();
            var id = parameterSetId ?? (development ? defaults.ParameterSetId : throw new InvalidOperationException("IronCondorRisk.PARAMETER_SET_REQUIRED"));
            var stored = await databases.ConfigurationDb.GetStrategyPositionRiskVersionAsync(id, version, token).ConfigureAwait(false);
            if (stored is null && development && id == defaults.ParameterSetId && version == defaults.Version)
            {
                try
                {
                    defaults.Validate();
                    await databases.ConfigurationDb.InsertStrategyPositionRiskDraftAsync(defaults, "Provisional ES daily-loss monitoring defaults", "DevelopmentDefaultProvisioner", token).ConfigureAwait(false);
                }
                catch when (!token.IsCancellationRequested)
                {
                    if (await databases.ConfigurationDb.GetStrategyPositionRiskVersionAsync(id, version, token).ConfigureAwait(false)is null)
                        throw;
                }

                stored = await databases.ConfigurationDb.GetStrategyPositionRiskVersionAsync(id, version, token).ConfigureAwait(false);
            }

            if (development && stored?.Status == ConfigurationParameterSetStatus.Draft && string.Equals(stored.ParameterSet.Hash(), defaults.Hash(), StringComparison.Ordinal))
            {
                await databases.ConfigurationDb.PublishAsync(StrategyParameterSetKind.StrategyPositionRisk, id, version, DateTime.UtcNow.AddSeconds(-1), token).ConfigureAwait(false);
                stored = await databases.ConfigurationDb.GetStrategyPositionRiskVersionAsync(id, version, token).ConfigureAwait(false);
            }

            var now = DateTime.UtcNow;
            if (stored is null || stored.Status != ConfigurationParameterSetStatus.Published || stored.EffectiveFromUtc is null || stored.EffectiveFromUtc > now || stored.RetiredAtUtc <= now || !string.Equals(stored.ParameterSet.Environment, environment, StringComparison.Ordinal))
                throw new InvalidOperationException("IronCondorRisk.PUBLISHED_ENVIRONMENT_POLICY_REQUIRED");
            Volatile.Write(ref selected, stored.ParameterSet);
            return stored.ParameterSet;
        }
        finally
        {
            selection.Release();
        }
    }
}
