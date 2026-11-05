using System.Globalization;
using System.Text.Json;
using TomasAI.IFM.Application.EventSourcing;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.MarketData.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;
using static TomasAI.IFM.Shared.Storage.BindValues;

namespace TomasAI.IFM.Application.Storage.ReferenceDb;

/// <summary>Owns the committed publication pointer for the staged instrument definitions.</summary>
internal sealed class InstrumentDefinitionCatalogPublisher(IPostgresEventTransaction transactions)
{
    internal const string Stream = "Command.InstrumentDefinitionCatalog.Publish";
    internal async Task PublishAsync(InstrumentDefinitionSnapshot snapshot, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.Id == Guid.Empty || snapshot.RecordCount <= 0 || snapshot.Datasets is not { Length: > 0 }
            || snapshot.CompletedUtc.Kind != DateTimeKind.Utc || snapshot.Datasets.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Instrument-definition publication requires a complete UTC snapshot.", nameof(snapshot));
        await transactions.ExecuteOnceAsync(async (transaction, token) =>
        {
            await transaction.ExecuteAsync("SELECT pg_advisory_xact_lock(hashtextextended($1, 0));", [Stream], token)
                .ConfigureAwait(false);
            var version = Convert.ToInt64(await transaction.ScalarAsync(
                "SELECT currentversion FROM public.event_stream_id WHERE eventstream=$1;", [Stream], token).ConfigureAwait(false),
                CultureInfo.InvariantCulture);
            var current = await ReadLatestAsync(transaction, version, token).ConfigureAwait(false);
            var legacyCompleted = current is null
                ? await transaction.ScalarAsync("""
                    SELECT completed_utc FROM reference.instrument_definition_snapshot WHERE catalog='current';
                    """, [], token).ConfigureAwait(false) as DateTime?
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
            if (version > 0 && await transaction.ScalarAsync("""
                    SELECT EXISTS (
                        SELECT 1 FROM public.event_log l JOIN public.event_stream_id s USING(eventstreamid)
                        WHERE s.eventstream=$1 AND l.commandid=$2);
                    """, [Stream, snapshot.Id], token).ConfigureAwait(false) is true)
                throw new InvalidOperationException("Instrument-definition snapshot identity was already published.");
            if (await transaction.ScalarAsync("""
                    SELECT EXISTS (SELECT 1 FROM reference.instrument_definition_selection WHERE snapshot_id=$1);
                    """, [snapshot.Id], token).ConfigureAwait(false) is not true)
                throw new InvalidOperationException("Instrument-definition selection is empty and cannot be published.");

            var fact = new InstrumentDefinitionCatalogPublishedEvent
            {
                Id = snapshot.Id, CommandId = snapshot.Id, AggregateId = Stream,
                EventSource = nameof(InstrumentDefinitionCatalogPublisher), ReceivedOn = DateTime.UtcNow,
                Subject = new(ActorType.Event, "InstrumentDefinitionCatalogEvent", "Published", "current"),
                CompletedUtc = snapshot.CompletedUtc, RecordCount = snapshot.RecordCount, Datasets = snapshot.Datasets
            };
            await transaction.AppendAsync(Stream, fact.CommandId, fact, version, token).ConfigureAwait(false);
            await ApplyAsync(transaction, fact, token).ConfigureAwait(false);
            return true;
        }, cancellationToken).ConfigureAwait(false);
    }

    static async Task<InstrumentDefinitionCatalogPublishedEvent?> ReadLatestAsync(
        EnlistedEventTransaction transaction, long version, CancellationToken token)
    {
        if (version == 0)
            return null;
        var rows = await transaction.QueryAsync("""
            SELECT n.eventtypename, l.eventversion, l.eventpayload
            FROM public.event_log l JOIN public.event_stream_id s USING(eventstreamid)
            JOIN public.event_name_id n USING(eventnameid)
            WHERE s.eventstream=$1 AND l.streamversion=$2;
            """, [Stream, version], static row => new EventStreamReadModel
            {
                EventTypeName = row.GetString(0),
                EventVersion = row.GetInt64(1),
                EventData = row.GetFieldValue<byte[]>(2)
            }.ToDomainEvent(), token).ConfigureAwait(false);
        return rows.SingleOrDefault() as InstrumentDefinitionCatalogPublishedEvent
            ?? throw new InvalidDataException("The current instrument-definition publication event is missing or invalid.");
    }

    internal static async Task ApplyAsync(IEnlistedPostgresTransaction transaction,
        InstrumentDefinitionCatalogPublishedEvent fact, CancellationToken token)
    {
        await transaction.ExecuteAsync(ReferenceDbSql.CompleteInstrumentDefinitionSelection, [Uuid(fact.Id)], token)
            .ConfigureAwait(false);
        await transaction.ExecuteAsync(ReferenceDbSql.PublishInstrumentDefinitionSnapshot,
            [Uuid(fact.Id), TimestampTz(fact.CompletedUtc), Bigint(fact.RecordCount),
                Text(JsonSerializer.Serialize(fact.Datasets))], token).ConfigureAwait(false);
    }
}
