using System.Globalization;
using System.Security.Cryptography;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.TradePlanDb;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;
using TomasAI.IFM.Framework.Serialization;

namespace TomasAI.IFM.Application.EventSourcing.Projections.TradePlan;

/// <summary>Captures older exit-stage rows as accepted source facts during an explicitly quiesced migration.</summary>
public sealed class ExitWorkflowProjectionBaseline(IPostgresEventTransaction transactions, ITradePlanDbContext target)
{
    const string Stream = "ExitWorkflowProjectionBaseline";

    /// <summary>Locks stage rows and saves a verified complete snapshot baseline; does not dispatch any exit workflow.</summary>
    public Task<ExitWorkflowProjectionBaselineEvent> CaptureAsync(bool sourceWritesQuiesced, CancellationToken cancellationToken = default)
    {
        if (!sourceWritesQuiesced)
            throw new ArgumentException("Stop application source writers before capturing the workflow baseline.", nameof(sourceWritesQuiesced));
        return transactions.ExecuteOnceAsync(async (transaction, token) =>
        {
            var rows = await target.ReadExitWorkflowBaselineAsync(transaction, token)
                .ConfigureAwait(false);
            var snapshots = rows.ToArray();
            foreach (var snapshot in snapshots) ExitWorkflowProjectionFacts.RequireValid(snapshot);
            var cut = Convert.ToInt64(await transaction.ScalarAsync("SELECT COALESCE(max(eventversion),0) FROM event_log;", [], token)
                .ConfigureAwait(false), CultureInfo.InvariantCulture);
            var source = new ExitWorkflowProjectionBaselineEvent { Id = Guid.NewGuid(), CommandId = Guid.NewGuid(),
                ReceivedOn = DateTime.UtcNow, SourceCut = cut, Snapshots = snapshots, PayloadSha256 = Hash(snapshots) };
            var version = Convert.ToInt64(await transaction.ScalarAsync("SELECT COALESCE(max(l.streamversion),0) FROM event_log l JOIN event_stream_id s USING(eventstreamid) WHERE s.eventstream=$1;", [Stream], token)
                .ConfigureAwait(false), CultureInfo.InvariantCulture);
            await transaction.AppendAsync(Stream, source.CommandId, source, version, token)
                .ConfigureAwait(false);
            return source;
        }, cancellationToken);
    }

    /// <summary>Reads the latest baseline from persisted event bytes rather than from its read table.</summary>
    public Task<ExitWorkflowProjectionBaselineEvent?> ReadLatestAsync(CancellationToken cancellationToken = default) =>
        transactions.ExecuteOnceAsync(async (transaction, token) =>
        {
            var rows = await transaction.QueryAsync("SELECT n.eventtypename,l.eventversion,l.eventpayload FROM event_log l JOIN event_name_id n USING(eventnameid) JOIN event_stream_id s USING(eventstreamid) WHERE s.eventstream=$1 ORDER BY l.streamversion DESC LIMIT 1;",
                [Stream], reader => new TomasAI.IFM.Shared.EventSourcing.ViewModels.EventStreamReadModel
                { EventTypeName = reader.GetString(0), EventVersion = reader.GetInt64(1), EventData = reader.GetFieldValue<byte[]>(2) }.ToDomainEvent(), token)
                    .ConfigureAwait(false);
            return rows.SingleOrDefault() as ExitWorkflowProjectionBaselineEvent;
        }, cancellationToken);

    /// <summary>Rebuilds only the exact captured stage facts; conflicting rows fail rather than being overwritten.</summary>
    public Task MaterializeAsync(ExitWorkflowProjectionBaselineEvent source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.Id == Guid.Empty || source.SchemaVersion != 1 || source.EventId <= source.SourceCut
            || !string.Equals(source.PayloadSha256, Hash(source.Snapshots), StringComparison.Ordinal))
            throw new InvalidDataException("A committed verified exit-workflow baseline is required.");
        return transactions.ExecuteOnceAsync(async (transaction, token) =>
        {
            foreach (var snapshot in source.Snapshots)
                await target.ProjectExitWorkflowAsync(transaction, snapshot, token)
                    .ConfigureAwait(false);
            return true;
        }, cancellationToken);
    }

    static string Hash(ExitPositionWorkflowProjection[] snapshots) =>
        Convert.ToHexString(SHA256.HashData(MessagePackBinarySerializer.Shared.Serialize(snapshots)));
}
