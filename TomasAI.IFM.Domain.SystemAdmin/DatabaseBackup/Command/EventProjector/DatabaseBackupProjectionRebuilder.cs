using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events;
using TomasAI.IFM.Shared.EventProjector;

namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Command.EventProjector;

public sealed class DatabaseBackupProjectionRebuilder(ISystemAdminDbContext systemAdminDb)
{
    public async ValueTask<DatabaseBackupProjectionRebuildResult> RebuildAsync(
        IEnumerable<DatabaseBackupEventContract> authoritativeEvents,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(authoritativeEvents);
        const string projectorName = nameof(DatabaseBackupEventProjector);
        var events = authoritativeEvents.OrderBy(static domainEvent => domainEvent.EventId).ToArray();
        if (events.Any(static domainEvent => domainEvent.EventId <= 0))
            throw new ArgumentException("A rebuild requires persisted positive event revisions.", nameof(authoritativeEvents));
        if (events.GroupBy(static domainEvent => domainEvent.EventId).Any(static group => group.Count() > 1))
            throw new ArgumentException("A rebuild cannot contain duplicate event revisions.", nameof(authoritativeEvents));

        await systemAdminDb.ClearDatabaseBackupProjectionsAsync(projectorName, cancellationToken).ConfigureAwait(false);
        var applied = 0;
        var alreadyApplied = 0;
        var superseded = 0;
        foreach (var domainEvent in events)
        {
            cancellationToken.ThrowIfCancellationRequested();
            switch (await systemAdminDb.ApplyDatabaseBackupEventAsync(
                projectorName, domainEvent, cancellationToken).ConfigureAwait(false))
            {
                case EventProjectionApplyOutcome.Applied: applied++; break;
                case EventProjectionApplyOutcome.AlreadyApplied: alreadyApplied++; break;
                case EventProjectionApplyOutcome.Superseded: superseded++; break;
                default: throw new InvalidOperationException($"Projection failed for event {domainEvent.EventId}.");
            }
        }

        var checkpoint = await systemAdminDb.GetDatabaseBackupProjectionCheckpointAsync(
            projectorName, cancellationToken).ConfigureAwait(false);
        return new DatabaseBackupProjectionRebuildResult(
            applied, alreadyApplied, superseded, checkpoint?.LastEventId ?? 0);
    }
}
