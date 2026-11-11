using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Application.EventSourcing;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.MarketData.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;
using TomasAI.IFM.Application.Storage.ReferenceDb;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Domain.Reference.Repository;

/// <summary>Owns the committed publication pointer for the staged instrument definitions.</summary>
public sealed class InstrumentDefinitionCatalogPublisher(IReferenceDbContext database, IPostgresEventTransaction transactions, TimeProvider? clock = null)
{
    internal const string Stream = "Command.InstrumentDefinitionCatalog.Publish";
    /// <summary>Publishes the supplied selection and immutable source event atomically.</summary>
    public async Task PublishAsync(InstrumentDefinitionSnapshot snapshot, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.Id == Guid.Empty || snapshot.RecordCount <= 0 || snapshot.Datasets is not { Length: > 0 }
            || snapshot.CompletedUtc.Kind != DateTimeKind.Utc || snapshot.Datasets.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Instrument-definition publication requires a complete UTC snapshot.", nameof(snapshot));
        await transactions.ExecuteOnceAsync(async (transaction, token) =>
        {
            await database.LockInstrumentCatalogAsync(transaction, Stream, token)
                .ConfigureAwait(false);
            var version = await database.ReadInstrumentCatalogRevisionAsync(transaction, Stream, token).ConfigureAwait(false);
            var current = version == 0 ? null :
                (await database.ReadInstrumentCatalogEventAsync(transaction, Stream, version, token).ConfigureAwait(false))?.ToDomainEvent()
                    as InstrumentDefinitionCatalogPublishedEvent
                ?? throw new InvalidDataException("The current instrument-definition publication event is missing or invalid.");
            var legacyCompleted = current is null
                ? await database.ReadPreviousInstrumentCompletionAsync(transaction, token).ConfigureAwait(false)
                : null;
            if ((current?.CompletedUtc ?? legacyCompleted) > snapshot.CompletedUtc)
                throw new InvalidOperationException("A newer instrument-definition catalog is already published.");
            if (current?.Id == snapshot.Id)
            {
                if (current.RecordCount != snapshot.RecordCount || current.CompletedUtc != snapshot.CompletedUtc
                    || !current.Datasets.SequenceEqual(snapshot.Datasets))
                    throw new InvalidOperationException("Instrument-definition snapshot identity was reused with different evidence.");
                await ApplyAsync(transaction, current, token).ConfigureAwait(false);
                return false;
            }
            if (version > 0 && await database.HasInstrumentCatalogReceiptAsync(transaction, Stream, snapshot.Id, token).ConfigureAwait(false))
                throw new InvalidOperationException("Instrument-definition snapshot identity was already published.");
            if (!await database.HasInstrumentDefinitionSelectionAsync(transaction, snapshot.Id, token).ConfigureAwait(false))
                throw new InvalidOperationException("Instrument-definition selection is empty and cannot be published.");

            var fact = new InstrumentDefinitionCatalogPublishedEvent
            {
                Id = snapshot.Id, CommandId = snapshot.Id, AggregateId = Stream,
                EventSource = nameof(InstrumentDefinitionCatalogPublisher), ReceivedOn = (clock ?? TimeProvider.System).GetUtcNow().UtcDateTime,
                Subject = new(ActorType.Event, "InstrumentDefinitionCatalogEvent", "Published", "current"),
                CompletedUtc = snapshot.CompletedUtc, RecordCount = snapshot.RecordCount, Datasets = snapshot.Datasets
            };
            await transaction.AppendAsync(new EnlistedEventAppend(Stream, fact.CommandId, fact, version), token).ConfigureAwait(false);
            await ApplyAsync(transaction, fact, token).ConfigureAwait(false);
            return true;
        }, cancellationToken).ConfigureAwait(false);
    }

    Task ApplyAsync(IEnlistedPostgresTransaction transaction, InstrumentDefinitionCatalogPublishedEvent fact, CancellationToken token)
        => database.WriteInstrumentCatalogPointerAsync(transaction,
            new InstrumentDefinitionSnapshot(fact.Id, fact.CompletedUtc, fact.RecordCount, fact.Datasets), token);

    /// <summary>Uses the same PostgreSQL identity for catalog source events and the reference projection.</summary>
    public static InstrumentDefinitionCatalogPublisher For(IReferenceDbContext database, TimeProvider? clock = null)
        => new(database, new PostgresEventTransaction(new DbConnectionSettings()
            .Add(EventSourceActorDbContext.EventSourceActorDbConnection, database.Database.ConnectionString, database.Database.ProviderName)), clock);
}
