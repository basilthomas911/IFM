using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events.Domain;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ReadModels;
using TomasAI.IFM.Shared.EventProjector;

namespace TomasAI.IFM.Application.Storage;

/// <summary>Provides the internal persistence helpers used by <see cref="SystemAdminDbContext"/>.</summary>
internal static class SystemAdminDbContextExtensions
{
    const string EmptyJsonArray = "[]";
    static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    extension(SystemAdminDbContext context)
    {
        /// <summary>Validates and atomically applies one database-backup projection event.</summary>
        internal async ValueTask<EventProjectionApplyOutcome> ApplyDatabaseBackupEventCoreAsync(
            string projectorName,
            DatabaseBackupEventContract domainEvent,
            CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(projectorName);
            ArgumentNullException.ThrowIfNull(domainEvent);
            domainEvent.Validate();
            if (domainEvent.EventId <= 0)
                throw new ArgumentOutOfRangeException(nameof(domainEvent), "A persisted positive event revision is required.");
            if (domainEvent.GetType().Namespace?.EndsWith(".Events.Domain", StringComparison.Ordinal) != true)
                throw new ArgumentException("Only authoritative DatabaseBackup domain events can be projected.", nameof(domainEvent));

            var hash = domainEvent.ComputeEventHash();
            var transaction = context.BeginTransaction();
            try
            {
                var existing = await context.Use($"{nameof(SystemAdminDbSql)}.{nameof(SystemAdminDbSql.GetProjectionReceiptForUpdate)}", SystemAdminDbSql.GetProjectionReceiptForUpdate)
                    .SetParameters(new ProjectionKey(projectorName, domainEvent.EventId))
                    .ExecuteSingleAsync(static row => row.GetString(0), cancellationToken)
                    .ConfigureAwait(false);
                if (existing is not null)
                {
                    if (!StringComparer.Ordinal.Equals(existing, hash))
                        throw new InvalidOperationException($"Projection event {domainEvent.EventId} conflicts with its durable receipt.");
                    transaction?.Commit();
                    return EventProjectionApplyOutcome.AlreadyApplied;
                }

                await context.ApplyRowsAsync(domainEvent, cancellationToken)
                    .ConfigureAwait(false);
                var now = DateTime.UtcNow;
                await context.Use($"{nameof(SystemAdminDbSql)}.{nameof(SystemAdminDbSql.InsertProjectionReceipt)}", SystemAdminDbSql.InsertProjectionReceipt)
                    .SetParameters(new InsertProjectionReceiptParameter(
                        projectorName, domainEvent.EventId, hash, domainEvent.Source.SourceEventId, now))
                    .ExecuteCommandAsync(cancellationToken)
                    .ConfigureAwait(false);
                await context.Use($"{nameof(SystemAdminDbSql)}.{nameof(SystemAdminDbSql.UpsertProjectionCheckpoint)}", SystemAdminDbSql.UpsertProjectionCheckpoint)
                    .SetParameters(new UpsertProjectionCheckpointParameter(projectorName, domainEvent.EventId, now))
                    .ExecuteCommandAsync(cancellationToken)
                    .ConfigureAwait(false);
                transaction?.Commit();
                return EventProjectionApplyOutcome.Applied;
            }
            catch
            {
                transaction?.Rollback();
                throw;
            }
        }

        /// <summary>Reads the durable checkpoint for the specified projector.</summary>
        internal async ValueTask<DatabaseBackupProjectionCheckpointReadModel?> GetDatabaseBackupProjectionCheckpointCoreAsync(
            string projectorName,
            CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(projectorName);
            return await context.Use($"{nameof(SystemAdminDbSql)}.{nameof(SystemAdminDbSql.GetProjectionCheckpoint)}", SystemAdminDbSql.GetProjectionCheckpoint)
                .SetParameters(new ProjectorKey(projectorName))
                .ExecuteSingleAsync(static row => new DatabaseBackupProjectionCheckpointReadModel(
                    row.GetString(0), row.GetLong(1), row.GetLong(2), row.GetDateTime(3).ToUtcOffset()), cancellationToken)
                .ConfigureAwait(false);
        }

        /// <summary>Clears all database-backup projections and the specified projector bookkeeping.</summary>
        internal async ValueTask ClearDatabaseBackupProjectionsCoreAsync(
            string projectorName,
            CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(projectorName);
            var transaction = context.BeginTransaction();
            try
            {
                await context.Use($"{nameof(SystemAdminDbSql)}.{nameof(SystemAdminDbSql.ClearProjections)}", SystemAdminDbSql.ClearProjections)
                    .ExecuteCommandAsync(cancellationToken)
                    .ConfigureAwait(false);
                await context.Use($"{nameof(SystemAdminDbSql)}.{nameof(SystemAdminDbSql.ClearProjectionReceipts)}", SystemAdminDbSql.ClearProjectionReceipts)
                    .SetParameters(new ProjectorKey(projectorName))
                    .ExecuteCommandAsync(cancellationToken)
                    .ConfigureAwait(false);
                await context.Use($"{nameof(SystemAdminDbSql)}.{nameof(SystemAdminDbSql.ClearProjectionCheckpoint)}", SystemAdminDbSql.ClearProjectionCheckpoint)
                    .SetParameters(new ProjectorKey(projectorName))
                    .ExecuteCommandAsync(cancellationToken)
                    .ConfigureAwait(false);
                transaction?.Commit();
            }
            catch
            {
                transaction?.Rollback();
                throw;
            }
        }

        /// <summary>Applies all projection rows represented by one authoritative domain event.</summary>
        internal async Task ApplyRowsAsync(
            DatabaseBackupEventContract domainEvent,
            CancellationToken cancellationToken)
        {
            if (domainEvent.ProjectsOperation())
            {
                await context.Use($"{nameof(SystemAdminDbSql)}.{nameof(SystemAdminDbSql.UpsertOperation)}", SystemAdminDbSql.UpsertOperation)
                    .SetParameters(new UpsertOperationParameter(domainEvent))
                    .ExecuteCommandAsync(cancellationToken)
                    .ConfigureAwait(false);
                await context.Use($"{nameof(SystemAdminDbSql)}.{nameof(SystemAdminDbSql.InsertPhase)}", SystemAdminDbSql.InsertPhase)
                    .SetParameters(new InsertPhaseParameter(domainEvent))
                    .ExecuteCommandAsync(cancellationToken)
                    .ConfigureAwait(false);
            }

            if (domainEvent.RestorePointId is not null)
            {
                var release = domainEvent is DatabaseBackupLegalHoldReleasedEvent;
                var hold = domainEvent is DatabaseBackupLegalHoldPlacedEvent;
                var restoreTested = domainEvent.Source.OperationKind == DatabaseRecoveryOperationKind.RestoreDrill
                    && domainEvent is DatabaseOperationCompletedEvent;
                var eligible = domainEvent.Outcome is not DatabaseRecoveryOutcome.Failed and not DatabaseRecoveryOutcome.Rejected;
                await context.Use($"{nameof(SystemAdminDbSql)}.{nameof(SystemAdminDbSql.UpsertRestorePoint)}", SystemAdminDbSql.UpsertRestorePoint)
                    .SetParameters(new UpsertRestorePointParameter(domainEvent, eligible, hold && !release, restoreTested))
                    .ExecuteCommandAsync(cancellationToken)
                    .ConfigureAwait(false);
            }

            if (domainEvent.ArtifactReplica is not null)
                await context.Use($"{nameof(SystemAdminDbSql)}.{nameof(SystemAdminDbSql.UpsertArtifactReplica)}", SystemAdminDbSql.UpsertArtifactReplica)
                    .SetParameters(new UpsertArtifactReplicaParameter(domainEvent))
                    .ExecuteCommandAsync(cancellationToken)
                    .ConfigureAwait(false);

            if (domainEvent is DatabaseOperationErrorRecordedEvent or DatabaseOperationFailedEvent)
                await context.Use($"{nameof(SystemAdminDbSql)}.{nameof(SystemAdminDbSql.UpsertRecoveryError)}", SystemAdminDbSql.UpsertRecoveryError)
                    .SetParameters(new UpsertRecoveryErrorParameter(domainEvent))
                    .ExecuteCommandAsync(cancellationToken)
                    .ConfigureAwait(false);

            if (domainEvent.Policy is not null && domainEvent.PolicyId is not null)
                await context.Use($"{nameof(SystemAdminDbSql)}.{nameof(SystemAdminDbSql.UpsertPolicy)}", SystemAdminDbSql.UpsertPolicy)
                    .SetParameters(new UpsertPolicyParameter(
                        domainEvent,
                        JsonSerializer.Serialize(domainEvent.Policy, JsonOptions),
                        domainEvent is DatabaseBackupPolicyEnforcedEvent))
                    .ExecuteCommandAsync(cancellationToken)
                    .ConfigureAwait(false);

            if (domainEvent.Source.ProducingHostId is not null
                && domainEvent is DatabaseBackupServiceCapabilityRecordedEvent or DatabaseBackupServiceReconciledEvent)
                await context.Use($"{nameof(SystemAdminDbSql)}.{nameof(SystemAdminDbSql.UpsertServiceHealth)}", SystemAdminDbSql.UpsertServiceHealth)
                    .SetParameters(new UpsertServiceHealthParameter(
                        domainEvent, domainEvent is DatabaseBackupServiceReconciledEvent))
                    .ExecuteCommandAsync(cancellationToken)
                    .ConfigureAwait(false);

            if (domainEvent.RetentionPlanId is not null)
                await context.Use($"{nameof(SystemAdminDbSql)}.{nameof(SystemAdminDbSql.UpsertRetention)}", SystemAdminDbSql.UpsertRetention)
                    .SetParameters(new UpsertRetentionParameter(
                        domainEvent, EmptyJsonArray, EmptyJsonArray,
                        domainEvent is DatabaseRetentionAuthorizedDomainEvent or DatabaseRetentionExecutionRequestedDomainEvent))
                    .ExecuteCommandAsync(cancellationToken)
                    .ConfigureAwait(false);

            if (domainEvent.Statistics is not null)
                await context.Use($"{nameof(SystemAdminDbSql)}.{nameof(SystemAdminDbSql.InsertRunStatistics)}", SystemAdminDbSql.InsertRunStatistics)
                    .SetParameters(new InsertRunStatisticsParameter(domainEvent))
                    .ExecuteCommandAsync(cancellationToken)
                    .ConfigureAwait(false);
        }

        /// <summary>Reads one projected recovery operation by its identifier.</summary>
        internal async ValueTask<DatabaseBackupOperationReadModel?> GetOperationCoreAsync(
            Guid operationId,
            CancellationToken cancellationToken)
            => await context.Use($"{nameof(SystemAdminDbSql)}.{nameof(SystemAdminDbSql.GetOperation)}", SystemAdminDbSql.GetOperation)
                .SetParameters(new OperationKey(operationId))
                .ExecuteSingleAsync(SystemAdminDbContext.MapToOperation, cancellationToken)
                .ConfigureAwait(false);

        /// <summary>Reads the latest matching projected restore point.</summary>
        internal async ValueTask<DatabaseRestorePointReadModel?> GetLatestRestorePointCoreAsync(
            string commandName,
            string sql,
            BackupSource source,
            string protectionSetId,
            CancellationToken cancellationToken)
            => await context.Use(commandName, sql)
                .SetParameters(new LatestRestorePointKey(source, protectionSetId))
                .ExecuteSingleAsync(SystemAdminDbContext.MapToRestorePoint, cancellationToken)
                .ConfigureAwait(false);
    }

    extension(DatabaseBackupEventContract domainEvent)
    {
        /// <summary>Determines whether the event contributes to the operation projection.</summary>
        internal bool ProjectsOperation()
            => domainEvent is not (DatabaseBackupPolicyRevisedEvent or DatabaseBackupPolicyEnforcedEvent
                or DatabaseBackupLegalHoldPlacedEvent or DatabaseBackupLegalHoldReleasedEvent
                or DatabaseBackupServiceCapabilityRecordedEvent or DatabaseBackupServiceReconciledEvent);

        /// <summary>Calculates the deterministic receipt hash for the event.</summary>
        internal string ComputeEventHash()
        {
            var json = JsonSerializer.Serialize(domainEvent, domainEvent.GetType(), JsonOptions);
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
        }
    }

    extension(OperationProjectionRow row)
    {
        /// <summary>Converts an operation projection row into the public restore-operation read model.</summary>
        internal DatabaseRestoreOperationReadModel ToRestoreOperation() => new()
        {
            Operation = row.Operation,
            RestorePointId = row.RestorePointId ?? new DatabaseRestorePointId("unknown"),
            RestoreClass = row.RestoreClass,
            FreshTargetProfile = row.FreshTargetProfile,
            ValidationRevision = row.ValidationRevision,
            CutoverState = row.CutoverState
        };
    }

    extension(short value)
    {
        /// <summary>Converts a persisted small integer into its declared enum value.</summary>
        internal TEnum ToEnum<TEnum>() where TEnum : struct, Enum
            => (TEnum)Enum.ToObject(typeof(TEnum), value);
    }

    extension(DateTime value)
    {
        /// <summary>Normalizes a provider timestamp as a UTC offset value.</summary>
        internal DateTimeOffset ToUtcOffset()
            => new(value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc));
    }

    extension(string? value)
    {
        /// <summary>Returns null when a persisted or requested string is empty.</summary>
        internal string? NullIfEmpty()
            => string.IsNullOrWhiteSpace(value) ? null : value;

        /// <summary>Deserializes a persisted database-backup lineage document.</summary>
        internal DatabaseBackupLineage? DeserializeLineage()
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            var document = JsonSerializer.Deserialize<DatabaseBackupLineageDocument>(value, JsonOptions)
                ?? throw new InvalidOperationException("Stored database backup lineage JSON is invalid.");
            return new()
            {
                RequestedMode = document.RequestedMode,
                ResolvedMode = document.ResolvedMode,
                NativeKind = document.NativeKind,
                BaseRestorePointId = string.IsNullOrWhiteSpace(document.BaseRestorePointId)
                    ? null : new DatabaseRestorePointId(document.BaseRestorePointId),
                ParentRestorePointId = string.IsNullOrWhiteSpace(document.ParentRestorePointId)
                    ? null : new DatabaseRestorePointId(document.ParentRestorePointId),
                ChainDepth = document.ChainDepth,
                NativeIdentity = document.NativeIdentity
            };
        }
    }

    extension(DatabaseBackupLineage? lineage)
    {
        /// <summary>Serializes database-backup lineage without provider-specific identifiers.</summary>
        internal string SerializeLineage()
            => lineage is null
                ? string.Empty
                : JsonSerializer.Serialize(new DatabaseBackupLineageDocument
                {
                    RequestedMode = lineage.RequestedMode,
                    ResolvedMode = lineage.ResolvedMode,
                    NativeKind = lineage.NativeKind,
                    BaseRestorePointId = lineage.BaseRestorePointId?.Value,
                    ParentRestorePointId = lineage.ParentRestorePointId?.Value,
                    ChainDepth = lineage.ChainDepth,
                    NativeIdentity = lineage.NativeIdentity
                }, JsonOptions);
    }

    sealed record DatabaseBackupLineageDocument
    {
        public DatabaseBackupMode RequestedMode { get; init; }
        public DatabaseBackupMode ResolvedMode { get; init; }
        public DatabaseNativeBackupKind NativeKind { get; init; }
        public string? BaseRestorePointId { get; init; }
        public string? ParentRestorePointId { get; init; }
        public int ChainDepth { get; init; }
        public string NativeIdentity { get; init; } = string.Empty;
    }
}
