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
    static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    extension(SystemAdminDbContext context)
    {
        /// <summary>Validates a required query before context parameter binding.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Validation belongs to the DbContext binding receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Validation belongs to the DbContext binding receiver contract.")]
        internal void ValidateRequiredArgument<T>([System.Diagnostics.CodeAnalysis.NotNull] T value,
            [System.Runtime.CompilerServices.CallerArgumentExpression("value")] string? argumentName = null)
        {
            if (value is null) throw new ArgumentNullException(argumentName);
        }

        /// <summary>Validates and atomically applies one database-backup projection event.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
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
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
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
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
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
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051", Justification = "Keep the transaction, resource ownership and failure ordering together during this provider migration; splitting it would obscure the atomic persistence boundary.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal Task ApplyRowsAsync(DatabaseBackupEventContract domainEvent, CancellationToken cancellationToken)
            => SystemAdminBackupSnapshotStore.WriteRowsAsync(domainEvent,
                (sql, values, token) => context.Use("DatabaseBackupProjection", sql)
                    .SetParameters(values.Bind())
                    .ExecuteCommandAsync(token), cancellationToken);

        /// <summary>Reads one projected recovery operation by its identifier.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal async ValueTask<DatabaseBackupOperationReadModel?> GetOperationCoreAsync(
            Guid operationId,
            CancellationToken cancellationToken)
            => await context.Use($"{nameof(SystemAdminDbSql)}.{nameof(SystemAdminDbSql.GetOperation)}", SystemAdminDbSql.GetOperation)
                .SetParameters(new OperationKey(operationId))
                .ExecuteSingleAsync(SystemAdminDbContext.MapToOperation, cancellationToken)
                .ConfigureAwait(false);

        /// <summary>Reads the latest matching projected restore point.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
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
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal bool ProjectsOperation()
            => domainEvent is not (DatabaseBackupPolicyRevisedEvent or DatabaseBackupPolicyEnforcedEvent
                or DatabaseBackupLegalHoldPlacedEvent or DatabaseBackupLegalHoldReleasedEvent
                or DatabaseBackupServiceCapabilityRecordedEvent or DatabaseBackupServiceReconciledEvent);

        /// <summary>Calculates the deterministic receipt hash for the event.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
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
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal TEnum ToEnum<TEnum>() where TEnum : struct, Enum
            => (TEnum)Enum.ToObject(typeof(TEnum), value);
    }

    extension(DateTime value)
    {
        /// <summary>Normalizes a provider timestamp as a UTC offset value.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal DateTimeOffset ToUtcOffset()
            => new(value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc));
    }

    extension(string? value)
    {
        /// <summary>Returns null when a persisted or requested string is empty.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
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
