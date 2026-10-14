using Npgsql;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Framework.Storage;
using System.Text.Json;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ReadModels;
using TomasAI.IFM.Shared.EventProjector;

namespace TomasAI.IFM.Application.Storage;
/// <summary>Provides the internal persistence helpers used by <see cref = "SystemAdminDbContext"/>.</summary>
internal static class SystemAdminDbContextExtensions
{
    static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    extension(SystemAdminDbContext context)
    {
        /// <summary>Validates a required query before context parameter binding.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Validation belongs to the DbContext binding receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Validation belongs to the DbContext binding receiver contract.")]
        internal void ValidateRequiredArgument<T>([System.Diagnostics.CodeAnalysis.NotNull] T value, [System.Runtime.CompilerServices.CallerArgumentExpression("value")] string? argumentName = null)
        {
            if (value is null)
                throw new ArgumentNullException(argumentName);
        }

        /// <summary>Atomically applies accepted backup rows and technical projection receipts.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal async ValueTask<EventProjectionApplyOutcome> ApplyDatabaseBackupSnapshotCoreAsync(string projectorName, DatabaseBackupProjectionSnapshot snapshot, CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(projectorName);
            ArgumentNullException.ThrowIfNull(snapshot);
            var hash = snapshot.EventHash;
            var transaction = context.BeginTransaction();
            try
            {
                var existing = await context.Use($"{nameof(SystemAdminDbSql)}.{nameof(SystemAdminDbSql.GetProjectionReceiptForUpdate)}", SystemAdminDbSql.GetProjectionReceiptForUpdate)
            .SetParameters(new ProjectionKey(projectorName, snapshot.EventId))
            .ExecuteSingleAsync(static row => row.GetString(0), cancellationToken)
            .ConfigureAwait(false);
                if (existing is not null)
                {
                    if (!StringComparer.Ordinal.Equals(existing, hash))
                        throw new InvalidOperationException($"Projection event {snapshot.EventId} conflicts with its durable receipt.");
                    transaction?.Commit();
                    return EventProjectionApplyOutcome.AlreadyApplied;
                }

                await context.ApplyRowsAsync(snapshot, cancellationToken)
            .ConfigureAwait(false);
                var now = DateTime.UtcNow;
                await context.Use($"{nameof(SystemAdminDbSql)}.{nameof(SystemAdminDbSql.InsertProjectionReceipt)}", SystemAdminDbSql.InsertProjectionReceipt)
            .SetParameters(new InsertProjectionReceiptParameter(projectorName, snapshot.EventId, hash, snapshot.Source.SourceEventId, now))
            .ExecuteCommandAsync(cancellationToken)
            .ConfigureAwait(false);
                await context.Use($"{nameof(SystemAdminDbSql)}.{nameof(SystemAdminDbSql.UpsertProjectionCheckpoint)}", SystemAdminDbSql.UpsertProjectionCheckpoint)
            .SetParameters(new UpsertProjectionCheckpointParameter(projectorName, snapshot.EventId, now))
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
        internal async ValueTask<DatabaseBackupProjectionCheckpointReadModel?> GetDatabaseBackupProjectionCheckpointCoreAsync(string projectorName, CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(projectorName);
            return await context.Use($"{nameof(SystemAdminDbSql)}.{nameof(SystemAdminDbSql.GetProjectionCheckpoint)}", SystemAdminDbSql.GetProjectionCheckpoint)
            .SetParameters(new ProjectorKey(projectorName))
            .ExecuteSingleAsync(static row => new DatabaseBackupProjectionCheckpointReadModel(row.GetString(0), row.GetLong(1), row.GetLong(2), row.GetDateTime(3).ToUtcOffset()), cancellationToken)
            .ConfigureAwait(false);
        }

        /// <summary>Clears all database-backup projections and the specified projector bookkeeping.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal async ValueTask ClearDatabaseBackupProjectionsCoreAsync(string projectorName, CancellationToken cancellationToken)
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

        /// <summary>Applies supplied backup projection rows.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051", Justification = "Keep the transaction, resource ownership and failure ordering together during this provider migration; splitting it would obscure the atomic persistence boundary.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal Task ApplyRowsAsync(DatabaseBackupProjectionSnapshot snapshot, CancellationToken cancellationToken) => snapshot.WriteRowsAsync((sql, values, token) => context.Use("DatabaseBackupProjection", sql)
            .SetParameters(values.Bind())
            .ExecuteCommandAsync(token), cancellationToken);
        /// <summary>Reads one projected recovery operation by its identifier.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal async ValueTask<DatabaseBackupOperationReadModel?> GetOperationCoreAsync(Guid operationId, CancellationToken cancellationToken) => await context.Use($"{nameof(SystemAdminDbSql)}.{nameof(SystemAdminDbSql.GetOperation)}", SystemAdminDbSql.GetOperation)
            .SetParameters(new OperationKey(operationId))
            .ExecuteSingleAsync(SystemAdminDbContext.MapToOperation, cancellationToken)
            .ConfigureAwait(false);
        /// <summary>Reads the latest matching projected restore point.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal async ValueTask<DatabaseRestorePointReadModel?> GetLatestRestorePointCoreAsync(string commandName, string sql, BackupSource source, string protectionSetId, CancellationToken cancellationToken) => await context.Use(commandName, sql)
            .SetParameters(new LatestRestorePointKey(source, protectionSetId))
            .ExecuteSingleAsync(SystemAdminDbContext.MapToRestorePoint, cancellationToken)
            .ConfigureAwait(false);
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
        internal TEnum ToEnum<TEnum>()
            where TEnum : struct, Enum => (TEnum)Enum.ToObject(typeof(TEnum), value);
    }

    extension(DateTime value)
    {
        /// <summary>Normalizes a provider timestamp as a UTC offset value.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal DateTimeOffset ToUtcOffset() => new(value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc));
    }

    extension(string? value)
    {
        /// <summary>Returns null when a persisted or requested string is empty.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal string? NullIfEmpty() => string.IsNullOrWhiteSpace(value) ? null : value;
        /// <summary>Deserializes a persisted database-backup lineage document.</summary>
        internal DatabaseBackupLineage? DeserializeLineage()
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;
            var document = JsonSerializer.Deserialize<DatabaseBackupLineageDocument>(value, JsonOptions) ?? throw new InvalidOperationException("Stored database backup lineage JSON is invalid.");
            return new()
            {
                RequestedMode = document.RequestedMode,
                ResolvedMode = document.ResolvedMode,
                NativeKind = document.NativeKind,
                BaseRestorePointId = string.IsNullOrWhiteSpace(document.BaseRestorePointId) ? null : new DatabaseRestorePointId(document.BaseRestorePointId),
                ParentRestorePointId = string.IsNullOrWhiteSpace(document.ParentRestorePointId) ? null : new DatabaseRestorePointId(document.ParentRestorePointId),
                ChainDepth = document.ChainDepth,
                NativeIdentity = document.NativeIdentity
            };
        }
    }

    extension(DatabaseBackupLineage? lineage)
    {
        /// <summary>Serializes database-backup lineage without provider-specific identifiers.</summary>
        internal string SerializeLineage() => lineage is null ? string.Empty : JsonSerializer.Serialize(new DatabaseBackupLineageDocument { RequestedMode = lineage.RequestedMode, ResolvedMode = lineage.ResolvedMode, NativeKind = lineage.NativeKind, BaseRestorePointId = lineage.BaseRestorePointId?.Value, ParentRestorePointId = lineage.ParentRestorePointId?.Value, ChainDepth = lineage.ChainDepth, NativeIdentity = lineage.NativeIdentity }, JsonOptions);
    }

    /// <summary>Provider-neutral persisted lineage document.</summary>
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

    extension(IEnlistedPostgresTransaction transaction)
    {
        /// <summary>Converts typed positional bindings for an enlisted transaction.</summary>
        internal object? [] BindBackupParameters(IBindValue values) => values.Bind()is NpgsqlParameter[] parameters ? parameters : throw new InvalidDataException("Typed backup parameters are required.");
        /// <summary>Writes supplied projection rows and their idempotency receipt without owning commit.</summary>
        internal async Task<EventProjectionApplyOutcome> ProjectBackupSnapshotAsync(string projectorName, DatabaseBackupProjectionSnapshot snapshot, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(transaction);
            ArgumentException.ThrowIfNullOrWhiteSpace(projectorName);
            ArgumentNullException.ThrowIfNull(snapshot);
            var hash = snapshot.EventHash;
            var existing = await transaction.ScalarAsync(SystemAdminDbSql.GetProjectionReceiptForUpdate, transaction.BindBackupParameters(new ProjectionKey(projectorName, snapshot.EventId)), token)
            .ConfigureAwait(false) as string;
            if (existing is not null)
            {
                if (!string.Equals(existing, hash, StringComparison.Ordinal))
                    throw new InvalidOperationException($"Projection event {snapshot.EventId} conflicts with its durable receipt.");
                return EventProjectionApplyOutcome.AlreadyApplied;
            }

            await snapshot.WriteRowsAsync((sql, values, ct) => transaction.ExecuteAsync(sql, transaction.BindBackupParameters(values), ct), token)
            .ConfigureAwait(false);
            var now = snapshot.Source.ObservedUtc.UtcDateTime;
            await transaction.ExecuteAsync(SystemAdminDbSql.InsertProjectionReceipt, transaction.BindBackupParameters(new InsertProjectionReceiptParameter(projectorName, snapshot.EventId, hash, snapshot.Source.SourceEventId, now)), token)
            .ConfigureAwait(false);
            await transaction.ExecuteAsync(SystemAdminDbSql.UpsertProjectionCheckpoint, transaction.BindBackupParameters(new UpsertProjectionCheckpointParameter(projectorName, snapshot.EventId, now)), token)
            .ConfigureAwait(false);
            return EventProjectionApplyOutcome.Applied;
        }
    }

    extension(DatabaseBackupProjectionSnapshot snapshot)
    {
        /// <summary>Writes the accepted row set without interpreting source event types.</summary>
        internal async Task WriteRowsAsync(Func<string, IBindValue, CancellationToken, Task> execute, CancellationToken cancellationToken)
        {
            if (snapshot.WriteOperation)
            {
                await execute(SystemAdminDbSql.UpsertOperation, new UpsertOperationParameter(snapshot), cancellationToken)
            .ConfigureAwait(false);
                await execute(SystemAdminDbSql.InsertPhase, new InsertPhaseParameter(snapshot), cancellationToken)
            .ConfigureAwait(false);
            }

            if (snapshot.RestorePointId is not null)
            {
                await execute(SystemAdminDbSql.UpsertRestorePoint, new UpsertRestorePointParameter(snapshot, snapshot.Eligible, snapshot.LegalHold, snapshot.RestoreTested), cancellationToken)
            .ConfigureAwait(false);
            }

            if (snapshot.ArtifactReplica is not null)
                await execute(SystemAdminDbSql.UpsertArtifactReplica, new UpsertArtifactReplicaParameter(snapshot), cancellationToken)
            .ConfigureAwait(false);
            if (snapshot.WriteError)
                await execute(SystemAdminDbSql.UpsertRecoveryError, new UpsertRecoveryErrorParameter(snapshot), cancellationToken)
            .ConfigureAwait(false);
            if (snapshot.Policy is not null && snapshot.PolicyId is not null)
                await execute(SystemAdminDbSql.UpsertPolicy, new UpsertPolicyParameter(snapshot, JsonSerializer.Serialize(snapshot.Policy, JsonOptions), snapshot.Enforced), cancellationToken)
            .ConfigureAwait(false);
            if (snapshot.WriteServiceHealth)
                await execute(SystemAdminDbSql.UpsertServiceHealth, new UpsertServiceHealthParameter(snapshot, snapshot.Reconciled), cancellationToken)
            .ConfigureAwait(false);
            if (snapshot.RetentionPlanId is not null)
                await execute(SystemAdminDbSql.UpsertRetention, new UpsertRetentionParameter(snapshot, EmptyJsonArray, EmptyJsonArray, snapshot.Approved), cancellationToken)
            .ConfigureAwait(false);
            if (snapshot.Statistics is not null)
                await execute(SystemAdminDbSql.InsertRunStatistics, new InsertRunStatisticsParameter(snapshot), cancellationToken)
            .ConfigureAwait(false);
        }
    }

    const string EmptyJsonArray = "[]";
}

/// <summary>Persisted operation and restore metadata in query ordinal order.</summary>
internal sealed record OperationProjectionRow(DatabaseBackupOperationReadModel Operation, DatabaseRestorePointId? RestorePointId, DatabaseRestoreClass RestoreClass, string FreshTargetProfile, long ValidationRevision, DatabaseCutoverState CutoverState);