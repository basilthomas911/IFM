using TomasAI.IFM.Application.Storage.ConfigurationDb;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Reference.Configuration.Strategy.Command.Events;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Reference.Configuration.Strategy.Command.State;

/// <summary>Materializes immutable configuration facts without workflow activation.</summary>
internal static class RegimeDiscoveryAcceptedProjection
{
    internal static Task ApplyAsync(IConfigurationDbContext database, IEnlistedPostgresTransaction transaction, IEvent source, CancellationToken token)
        => source switch
        {
            RegimeDiscoveryParameterSetCreatedEvent created => database.InsertRegimeDiscoveryDraftAsync(transaction,
                created.ParameterSet, created.Description, created.CreatedBy, created.CreatedAtUtc == default
                    ? DateTime.SpecifyKind(created.ReceivedOn, DateTimeKind.Utc) : created.CreatedAtUtc, token),
            RegimeDiscoveryParameterSetPublishedEvent published => database.PublishRegimeDiscoveryAsync(transaction,
                published.EntityId.ParameterSetId, published.EntityId.Version, published.EffectiveFromUtc, token),
            RegimeDiscoveryParameterSetRetiredEvent retired => database.RetireRegimeDiscoveryAsync(transaction,
                retired.EntityId.ParameterSetId, retired.EntityId.Version, retired.RetiredAtUtc, token),
            _ => throw new InvalidDataException("Unsupported configuration source event.")
        };
}
