using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;

namespace TomasAI.IFM.Application.Storage;

/// <summary>Durable backup dispatch intents enlisted with source facts; publication acknowledgments commit afterward.</summary>
public sealed class DatabaseBackupExecutionIntentStore(IPostgresEventTransaction transactions)
{
    /// <summary>Records intent in the source transaction; never performs the backup operation.</summary>
    public static Task<int> EnqueueAsync(IEnlistedPostgresTransaction transaction, DatabaseBackupEventContract source, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(source);
        if (source.EventId <= 0 || source.Id == Guid.Empty)
            throw new ArgumentException("A committed source identity is required.", nameof(source));
        return transaction.ExecuteAsync("INSERT INTO system_admin.database_backup_execution_outbox(event_id,event_uuid) VALUES($1,$2) ON CONFLICT(event_id) DO NOTHING;", [source.EventId, source.Id], token);
    }

    /// <summary>Reads a bounded page directly from persisted event bytes, including after a host restart.</summary>
    public Task<IReadOnlyList<DatabaseBackupEventContract>> ReadPendingAsync(CancellationToken token = default) =>
        transactions.ExecuteOnceAsync((transaction, cancellation) => transaction.QueryAsync("""
SELECT n.eventtypename,l.eventversion,l.eventpayload
FROM system_admin.database_backup_execution_outbox o
JOIN event_log l ON l.eventversion=o.event_id
JOIN event_name_id n USING(eventnameid)
WHERE o.published_utc IS NULL ORDER BY o.event_id LIMIT 100;
""", [], reader => new EventStreamReadModel { EventTypeName = reader.GetString(0), EventVersion = reader.GetInt64(1), EventData = reader.GetFieldValue<byte[]>(2) }.ToDomainEvent() as DatabaseBackupEventContract
            ?? throw new InvalidDataException("The backup outbox source is not a backup event."), cancellation), token);

    /// <summary>Acknowledges only the published source identity; rows remain available for audit.</summary>
    public Task<int> MarkPublishedAsync(DatabaseBackupEventContract source, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        return transactions.ExecuteOnceAsync((transaction, cancellation) => transaction.ExecuteAsync(
            "UPDATE system_admin.database_backup_execution_outbox SET published_utc=$1 WHERE event_id=$2 AND event_uuid=$3 AND published_utc IS NULL;", [DateTime.UtcNow, source.EventId, source.Id], cancellation), token);
    }
}
