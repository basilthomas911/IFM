using System.Text.Json;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Domain.MarketData.Shared.OptionChainCache;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Reference.ParameterSets.Command.State;

/// <summary>Materializes accepted parameter facts without subscriptions, startup actions or cache refresh.</summary>
internal static class ParameterAcceptedProjection
{
    internal static async Task ApplyAsync(IDbContextFactory databases, IEnlistedPostgresTransaction transaction,
        IEvent source, CancellationToken token)
    {
        switch (source)
        {
            case IParameterSetFact fact:
                await databases.ConfigurationDb.ProjectParameterSetAsync(transaction, fact, token).ConfigureAwait(false);
                if (fact is not (ParameterVersionPublishedEvent or ParameterVersionRetiredEvent)) return;
                var version = JsonSerializer.Deserialize<ParameterSetVersion>(fact.VersionJson)
                    ?? throw new InvalidDataException("Parameter version event payload is missing.");
                if (!string.Equals(version.Reference.ComponentCode, ParameterSchemaRegistry.StrategyOptionChainCacheComponent, StringComparison.Ordinal)) return;
                var parameters = StrategyOptionChainParameterSet.Read(version.PayloadJson);
                if (parameters.ParameterSetId != version.Reference.SetId || parameters.Version != version.Reference.Version)
                    throw new InvalidDataException("Option-chain parameter event identity differs from its payload.");
                if (PostgresDatabaseIdentity.Parse(databases.MarketDataDb.ConnectionString) != PostgresDatabaseIdentity.Parse(databases.ConfigurationDb.ConnectionString))
                    throw new InvalidOperationException("Option-chain policy projections must share the configuration PostgreSQL database/security identity.");
                await PostgresOptionChainParameterStore.ProjectAsync(transaction, parameters, fact.Revision,
                    version.Status == ParameterVersionStatus.Published, token).ConfigureAwait(false);
                break;
            case ParameterAssignmentChangedEvent fact:
                await databases.ConfigurationDb.ProjectParameterAssignmentAsync(transaction, fact, token).ConfigureAwait(false);
                break;
            case ParameterStartupChangedEvent fact:
                await databases.ConfigurationDb.ProjectParameterStartupAsync(transaction, fact, token).ConfigureAwait(false);
                break;
            default: throw new InvalidOperationException("Unexpected parameter source event.");
        }
    }
}
