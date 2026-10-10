using TomasAI.IFM.Application.Api.Server.Core.Startup.Actors;
using TomasAI.IFM.Application.Api.Nats.Client;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Shared.OptionChainCache;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Application.Api.Server.Core.Development.Provisioning;

/// <summary>Creates the approved global Development profiles through command actors; never edits a published version or a Fund mandate.</summary>
public sealed class DevelopmentOptionChainParameterSeeder(IHostEnvironment environment, IConfiguration configuration,
    IDbContextFactory databases, IActorProducer producer, IActorRuntimeStartupSignal actorStartup, ILogger<DevelopmentOptionChainParameterSeeder> logger) : BackgroundService
{
    /// <summary>Creates missing initial profiles after the Development catalog is published. All waits are outside workflow execution.</summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!environment.IsDevelopment() || !configuration.GetValue("AppSettings:StrategyOptionChainCache:ProvisionDevelopmentDefaults", false)) return;
        await actorStartup.WaitAsync(stoppingToken).ConfigureAwait(false);
        string? previousFailure = null;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (await EnsureAsync(stoppingToken).ConfigureAwait(false)) return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception ex)
            {
                if (previousFailure != ex.Message)
                    logger.LogWarning(ex, "Development option cache profiles await actor/catalog readiness");
                previousFailure = ex.Message;
            }
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken).ConfigureAwait(false);
        }
    }

    /// <summary>Publishes one global set for each exact published Development option structure. Existing user versions are preserved.</summary>
    public async Task<bool> EnsureAsync(CancellationToken token)
    {
        var summaries = await databases.ConfigurationDb.GetStrategyCatalogsAsync(StrategyCatalogKind.Structure, 200, cancellationToken: token).ConfigureAwait(false);
        var api = new ParameterSetsApi(producer);
        foreach (var code in new[] { "DevelopmentIronCondor", "DevelopmentCallVertical", "DevelopmentPutVertical" })
        {
            var catalog = summaries.SingleOrDefault(x => x.Code == code && x.Status == CatalogLifecycleStatus.Published);
            if (catalog is null) return false;
            var id = StrategyCatalogExamples.StableId("StrategyOptionChainCache/" + code);
            if ((await databases.MarketDataDb.ReadPublishedAsync("Development", token).ConfigureAwait(false)).Any(row => row.ParameterSetId == id && row.Version == 1)) continue;
            var policy = (code == "DevelopmentIronCondor" ? StrategyOptionChainParameterDefaults.IronCondor(id, catalog.Key.Id, catalog.Key.Version)
                : StrategyOptionChainParameterDefaults.VerticalSpread(id, catalog.Key.Id, catalog.Key.Version)) with { Enabled = true };
            var state = await api.StateAsync(id, token).ConfigureAwait(false);
            if (state.Value is null || state.Value.Versions.Length == 0)
            {
                var created = await api.CreateAsync(new() { CommandId = StrategyCatalogExamples.StableId("StrategyOptionChainCache/Create/" + id),
                    EntityId = new(id), ComponentCode = "market-data.strategy-option-chain-cache", Name = policy.Name,
                    SchemaVersion = 1, PayloadJson = policy.Serialize() }, token).ConfigureAwait(false);
                if (!created.Success) throw new InvalidOperationException(created.ErrorMessage);
                state = await api.StateAsync(id, token).ConfigureAwait(false);
            }
            if (!state.Success || state.Value is null) throw new InvalidOperationException(state.ErrorMessage);
            var draft = state.Value.Versions.Single(x => x.Reference.Version == 1);
            // Explicit publication freezes the payload already authored in this set; no latest-version overwrite.
            var published = await api.PublishAsync(new() { CommandId = StrategyCatalogExamples.StableId("StrategyOptionChainCache/Publish/" + id),
                EntityId = new(id), Version = 1, ExpectedRevision = state.Value.Revision, ComponentCode = draft.Reference.ComponentCode,
                Name = draft.Name, Description = draft.Description, SchemaVersion = draft.SchemaVersion, PayloadJson = draft.PayloadJson }, token).ConfigureAwait(false);
            if (!published.Success) throw new InvalidOperationException(published.ErrorMessage);
            logger.LogInformation("Published Development strategy option cache parameters: ParameterSetId={ParameterSetId}, Version=1, StrategyDefinitionId={StrategyDefinitionId}, StrategyDefinitionVersion={StrategyDefinitionVersion}",
                id, catalog.Key.Id, catalog.Key.Version);
        }
        return true;
    }
}
