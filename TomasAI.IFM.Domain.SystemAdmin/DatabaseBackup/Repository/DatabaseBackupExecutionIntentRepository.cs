using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events;


namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Repository;

/// <summary>Durable backup dispatch intents enlisted with source facts; publication acknowledgments commit afterward.</summary>
public sealed class DatabaseBackupExecutionIntentRepository(ISystemAdminDbContext database)
{
    /// <summary>Records intent in the source transaction; never performs the backup operation.</summary>
    public static Task<int> EnqueueAsync(ISystemAdminDbContext database, IEnlistedPostgresTransaction transaction, DatabaseBackupEventContract source, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(source);
        if (source.EventId <= 0 || source.Id == Guid.Empty)
            throw new ArgumentException("A committed source identity is required.", nameof(source));
        return database.EnqueueDatabaseBackupExecutionIntentAsync(transaction, source.EventId, source.Id, token);
    }

    /// <summary>Reads a bounded page directly from persisted event bytes, including after a host restart.</summary>
    public async Task<IReadOnlyList<DatabaseBackupEventContract>> ReadPendingAsync(CancellationToken token = default)
    {
        var records = await database.ReadPendingDatabaseBackupExecutionIntentsAsync(token).ConfigureAwait(false);
        return records.Select(record => record.ToDomainEvent() as DatabaseBackupEventContract
            ?? throw new InvalidDataException("The backup outbox source is not a backup event.")).ToArray();
    }

    /// <summary>Acknowledges only the published source identity; rows remain available for audit.</summary>
    public Task<int> MarkPublishedAsync(DatabaseBackupEventContract source, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        return database.MarkDatabaseBackupExecutionIntentPublishedAsync(source.EventId, source.Id, DateTime.UtcNow, token);
    }
}
