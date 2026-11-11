using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Application.Storage.ConfigurationDb;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.Reference.StrategyCatalog;

/// <summary>Owns catalog legacy baseline facts and dependency-ordered reconstruction from accepted source events.</summary>
public static class StrategyCatalogReconstruction
{
    /// <summary>Captures each verified version lacking a creation stream, under the catalog write fence.</summary>
    public static async Task<int> CaptureBaselineAsync(IConfigurationDbContext database,
        EnlistedEventTransaction enlisted, CancellationToken token = default, TimeProvider? clock = null)
    {
        var keys = await database.ReadLockedStrategyCatalogBaselineKeysAsync(enlisted, token).ConfigureAwait(false);
        foreach (var key in keys)
        {
            var version = await database.GetLockedStrategyCatalogAsync(enlisted, key, token)
            .ConfigureAwait(false)
                ?? throw new InvalidDataException("A catalog version disappeared during baseline capture.");
            var id = Guid.NewGuid();
            var entityId = StrategyCatalogLifecycleCommand.Address(key);
            var fact = new StrategyCatalogSourceEvent
            {
                IsBaseline = true,
                Subject = new(ActorType.Event, "StrategyCatalogEvent", "BaselineCaptured", entityId.Format()),
                EntityId = entityId, Id = id, CommandId = id, AggregateId = entityId.Format(),
                EventSource = "StrategyCatalogMigration", ReceivedOn = (clock ?? TimeProvider.System).GetUtcNow().UtcDateTime,
                AfterJson = StrategyCatalogJson.Write(version), Principal = version.CreatedBy
            };
            await enlisted.AppendAsync(new EnlistedEventAppend($"StrategyCatalog.{entityId.Format()}", id, fact, 0), token)
            .ConfigureAwait(false);
        }
        return keys.Count;
    }

    /// <summary>Rebuilds accepted current versions in dependency order without re-running external validation.</summary>
    public static async Task RebuildAsync(IConfigurationDbContext database, EnlistedEventTransaction enlisted,
        IReadOnlyCollection<StrategyCatalogSourceEvent> sourceHeads, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(sourceHeads);
        var remaining = sourceHeads.Select(fact => (Fact: fact, Version: StrategyCatalogJson.Read<StoredStrategyCatalogDefinition>(fact.AfterJson)))
            .GroupBy(item => item.Version.Definition.Key)
            .ToDictionary(group => group.Key, group => group.MaxBy(item => item.Fact.EventId));
        while (remaining.Count > 0)
        {
            token.ThrowIfCancellationRequested();
            var ready = remaining.Values
                .Where(item => !TomasAI.IFM.Domain.Strategy.Contracts.Shared.Canonical.StrategyCatalogValidation.Dependencies(item.Version.Definition).Any(remaining.ContainsKey))
                .OrderBy(item => item.Version.CreatedUtc)
                .ThenBy(item => item.Version.Definition.Key.Version)
                .ToArray();
            if (ready.Length == 0)
                throw new InvalidDataException("Catalog source events contain a cyclic dependency.");
            foreach (var item in ready)
            {
                await database.ProjectStrategyCatalogSourceAsync(enlisted, item.Fact, true, token)
            .ConfigureAwait(false);
                remaining.Remove(item.Version.Definition.Key);
            }
        }
    }



}
