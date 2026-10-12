using System.Text.Json;
using Npgsql;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events.Domain;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.EventProjector;

namespace TomasAI.IFM.Application.Storage;

/// <summary>Pure backup snapshot and receipt writes on the caller's PostgreSQL event transaction.</summary>
internal static class SystemAdminBackupSnapshotStore
{
    const string EmptyJsonArray = "[]";
    static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    internal static async Task<EventProjectionApplyOutcome> ProjectAsync(IEnlistedPostgresTransaction transaction,
        string projectorName, DatabaseBackupEventContract domainEvent, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectorName);
        ArgumentNullException.ThrowIfNull(domainEvent);
        domainEvent.Validate();
        if (domainEvent.EventId <= 0 || domainEvent.GetType().Namespace?.EndsWith(".Events.Domain", StringComparison.Ordinal) != true)
            throw new ArgumentException("A persisted authoritative backup domain event is required.", nameof(domainEvent));
        var hash = domainEvent.ComputeEventHash();
        var existing = await transaction.ScalarAsync(SystemAdminDbSql.GetProjectionReceiptForUpdate, Bind(new ProjectionKey(projectorName, domainEvent.EventId)), token)
            .ConfigureAwait(false) as string;
        if (existing is not null)
        {
            if (!string.Equals(existing, hash, StringComparison.Ordinal))
                throw new InvalidOperationException($"Projection event {domainEvent.EventId} conflicts with its durable receipt.");
            return EventProjectionApplyOutcome.AlreadyApplied;
        }
        await WriteRowsAsync(domainEvent, (sql, values, ct) => transaction.ExecuteAsync(sql, Bind(values), ct), token)
            .ConfigureAwait(false);
        var now = domainEvent.Source.ObservedUtc.UtcDateTime;
        await transaction.ExecuteAsync(SystemAdminDbSql.InsertProjectionReceipt, Bind(new InsertProjectionReceiptParameter(projectorName, domainEvent.EventId, hash, domainEvent.Source.SourceEventId, now)), token)
            .ConfigureAwait(false);
        await transaction.ExecuteAsync(SystemAdminDbSql.UpsertProjectionCheckpoint, Bind(new UpsertProjectionCheckpointParameter(projectorName, domainEvent.EventId, now)), token)
            .ConfigureAwait(false);
        return EventProjectionApplyOutcome.Applied;
    }

    static object?[] Bind(IBindValue values) => values.Bind() is NpgsqlParameter[] parameters ? parameters : throw new InvalidDataException("Typed backup parameters are required.");

        internal static async Task WriteRowsAsync(
            DatabaseBackupEventContract domainEvent,
            Func<string, IBindValue, CancellationToken, Task> execute,
            CancellationToken cancellationToken)
        {
            if (domainEvent.ProjectsOperation())
            {
                await execute(SystemAdminDbSql.UpsertOperation, new UpsertOperationParameter(domainEvent), cancellationToken)
                    .ConfigureAwait(false);
                await execute(SystemAdminDbSql.InsertPhase, new InsertPhaseParameter(domainEvent), cancellationToken)
                    .ConfigureAwait(false);
            }

            if (domainEvent.RestorePointId is not null)
            {
                var release = domainEvent is DatabaseBackupLegalHoldReleasedEvent;
                var hold = domainEvent is DatabaseBackupLegalHoldPlacedEvent;
                var restoreTested = domainEvent.Source.OperationKind == DatabaseRecoveryOperationKind.RestoreDrill
                    && domainEvent is DatabaseOperationCompletedEvent;
                var eligible = domainEvent.Outcome is not DatabaseRecoveryOutcome.Failed and not DatabaseRecoveryOutcome.Rejected;
                await execute(SystemAdminDbSql.UpsertRestorePoint, new UpsertRestorePointParameter(domainEvent, eligible, hold && !release, restoreTested), cancellationToken)
                    .ConfigureAwait(false);
            }

            if (domainEvent.ArtifactReplica is not null)
                await execute(SystemAdminDbSql.UpsertArtifactReplica, new UpsertArtifactReplicaParameter(domainEvent), cancellationToken)
                    .ConfigureAwait(false);

            if (domainEvent is DatabaseOperationErrorRecordedEvent or DatabaseOperationFailedEvent)
                await execute(SystemAdminDbSql.UpsertRecoveryError, new UpsertRecoveryErrorParameter(domainEvent), cancellationToken)
                    .ConfigureAwait(false);

            if (domainEvent.Policy is not null && domainEvent.PolicyId is not null)
                await execute(SystemAdminDbSql.UpsertPolicy, new UpsertPolicyParameter(
                        domainEvent,
                        JsonSerializer.Serialize(domainEvent.Policy, JsonOptions),
                        domainEvent is DatabaseBackupPolicyEnforcedEvent), cancellationToken)
                    .ConfigureAwait(false);

            if (domainEvent.Source.ProducingHostId is not null
                && domainEvent is DatabaseBackupServiceCapabilityRecordedEvent or DatabaseBackupServiceReconciledEvent)
                await execute(SystemAdminDbSql.UpsertServiceHealth, new UpsertServiceHealthParameter(
                        domainEvent, domainEvent is DatabaseBackupServiceReconciledEvent), cancellationToken)
                    .ConfigureAwait(false);

            if (domainEvent.RetentionPlanId is not null)
                await execute(SystemAdminDbSql.UpsertRetention, new UpsertRetentionParameter(
                        domainEvent, EmptyJsonArray, EmptyJsonArray,
                        domainEvent is DatabaseRetentionAuthorizedDomainEvent or DatabaseRetentionExecutionRequestedDomainEvent), cancellationToken)
                    .ConfigureAwait(false);

            if (domainEvent.Statistics is not null)
                await execute(SystemAdminDbSql.InsertRunStatistics, new InsertRunStatisticsParameter(domainEvent), cancellationToken)
                    .ConfigureAwait(false);
        }

}
