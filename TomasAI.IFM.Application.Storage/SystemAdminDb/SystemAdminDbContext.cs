using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Queries;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ReadModels;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Shared.EventProjector;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage;
/// <summary>
/// PostgreSQL repository for SystemAdmin database-recovery projections.
/// </summary>
/// <param name = "connectionSettings">The named database connection settings.</param>
/// <param name = "logger">The database-provider logger.</param>
public sealed class SystemAdminDbContext(IDbConnectionSettings connectionSettings, ILogger<DbProvider> logger) : ObjectDataRepository<SystemAdminDbContext>(connectionSettings[SystemAdminDbConnection], logger), ISystemAdminDbContext
{
    /// <summary>The named SystemAdmin PostgreSQL connection.</summary>
    public const string SystemAdminDbConnection = "SystemAdminDbConnection";
    static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    /// <summary>Gets the concrete SystemAdmin database context.</summary>
    public override SystemAdminDbContext Database => this;
    /// <summary>Gets the SystemAdmin read capability.</summary>
    public ISystemAdminDbReadContext DbReader => this;
    /// <summary>Gets the SystemAdmin write capability.</summary>
    public ISystemAdminDbWriteContext DbWriter => this;

    /// <inheritdoc/>
    public Task<EventProjectionApplyOutcome> ApplyDatabaseBackupSnapshotAsync(TomasAI.IFM.Application.Storage.EventSourceDb.IEnlistedPostgresTransaction transaction, string projectorName, DatabaseBackupProjectionSnapshot snapshot, CancellationToken cancellationToken = default) => transaction.ProjectBackupSnapshotAsync(projectorName, snapshot, cancellationToken);
    /// <inheritdoc/>
    public ValueTask<EventProjectionApplyOutcome> ApplyDatabaseBackupSnapshotAsync(string projectorName, DatabaseBackupProjectionSnapshot snapshot, CancellationToken cancellationToken = default) => this.ApplyDatabaseBackupSnapshotCoreAsync(projectorName, snapshot, cancellationToken);
    /// <inheritdoc/>
    public ValueTask<DatabaseBackupProjectionCheckpointReadModel?> GetDatabaseBackupProjectionCheckpointAsync(string projectorName, CancellationToken cancellationToken = default) => this.GetDatabaseBackupProjectionCheckpointCoreAsync(projectorName, cancellationToken);
    /// <inheritdoc/>
    public ValueTask ClearDatabaseBackupProjectionsAsync(string projectorName, CancellationToken cancellationToken = default) => this.ClearDatabaseBackupProjectionsCoreAsync(projectorName, cancellationToken);
    /// <inheritdoc/>
    public async ValueTask<DatabaseProtectionSetReadModel[]> GetProtectionSetsAsync(GetDatabaseProtectionSetsQuery query, CancellationToken cancellationToken)
    {
        this.ValidateRequiredArgument(query);
        return[..await Use($"{nameof(SystemAdminDbSql)}.{nameof(SystemAdminDbSql.GetProtectionSets)}", SystemAdminDbSql.GetProtectionSets)
            .SetParameters(new SourceFilter(query.Source))
            .ExecuteQueryAsync(MapToProtectionSet, cancellationToken)
            .ConfigureAwait(false)];
    }

    /// <inheritdoc/>
    public async ValueTask<DatabaseBackupPolicyReadModel?> GetPolicyAsync(GetDatabaseBackupPolicyQuery query, CancellationToken cancellationToken)
    {
        this.ValidateRequiredArgument(query);
        return await Use($"{nameof(SystemAdminDbSql)}.{nameof(SystemAdminDbSql.GetPolicy)}", SystemAdminDbSql.GetPolicy)
            .SetParameters(new PolicyQueryParameter(query.Request.EnvironmentIdentity, query.PolicyId!.Value.Value))
            .ExecuteSingleAsync(MapToPolicy, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<DatabaseBackupOperationReadModel?> GetBackupOperationAsync(GetDatabaseBackupOperationQuery query, CancellationToken cancellationToken)
    {
        this.ValidateRequiredArgument(query);
        return await this.GetOperationCoreAsync(query.OperationId!.Value.Value, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<DatabaseBackupOperationReadModel[]> ListBackupOperationsAsync(ListDatabaseBackupOperationsQuery query, CancellationToken cancellationToken)
    {
        this.ValidateRequiredArgument(query);
        Guid? continuation = Guid.TryParse(query.ContinuationIdentity, out var parsed) ? parsed : null;
        return[..await Use($"{nameof(SystemAdminDbSql)}.{nameof(SystemAdminDbSql.ListOperations)}", SystemAdminDbSql.ListOperations)
            .SetParameters(new OperationListParameter(query.Source, query.ProtectionSetId?.Value, query.FromUtc?.UtcDateTime, query.ToUtc?.UtcDateTime, continuation, query.PageSize))
            .ExecuteQueryAsync(MapToOperation, cancellationToken)
            .ConfigureAwait(false)];
    }

    /// <inheritdoc/>
    public async ValueTask<DatabaseBackupOperationReadModel[]> GetBackupSetOperationsAsync(Guid backupSetId, CancellationToken cancellationToken) => (await Use("SystemAdmin.BackupSetOperations", SystemAdminDbSql.GetBackupSetOperations)
            .SetParameters(new BackupSetKey(backupSetId))
            .ExecuteQueryAsync(MapToOperation, cancellationToken)
            .ConfigureAwait(false)).ToArray();
    /// <inheritdoc/>
    public async ValueTask<DatabaseRestorePointReadModel[]> ListRestorePointsAsync(ListDatabaseRestorePointsQuery query, CancellationToken cancellationToken)
    {
        this.ValidateRequiredArgument(query);
        return[..await Use($"{nameof(SystemAdminDbSql)}.{nameof(SystemAdminDbSql.ListRestorePoints)}", SystemAdminDbSql.ListRestorePoints)
            .SetParameters(new RestorePointListParameter(query.Source, query.ProtectionSetId?.Value, query.FromUtc?.UtcDateTime, query.ToUtc?.UtcDateTime, query.ContinuationIdentity.NullIfEmpty(), query.PageSize))
            .ExecuteQueryAsync(MapToRestorePoint, cancellationToken)
            .ConfigureAwait(false)];
    }

    /// <inheritdoc/>
    public async ValueTask<DatabaseRestorePointReadModel?> GetRestorePointAsync(GetDatabaseRestorePointQuery query, CancellationToken cancellationToken)
    {
        this.ValidateRequiredArgument(query);
        return await Use($"{nameof(SystemAdminDbSql)}.{nameof(SystemAdminDbSql.GetRestorePoint)}", SystemAdminDbSql.GetRestorePoint)
            .SetParameters(new RestorePointKey(query.RestorePointId!.Value.Value, query.Source))
            .ExecuteSingleAsync(MapToRestorePoint, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<DatabaseRestorePointReadModel?> GetLatestVerifiedBackupAsync(GetLatestVerifiedDatabaseBackupQuery query, CancellationToken cancellationToken)
    {
        this.ValidateRequiredArgument(query);
        return await this.GetLatestRestorePointCoreAsync($"{nameof(SystemAdminDbSql)}.{nameof(SystemAdminDbSql.GetLatestVerified)}", SystemAdminDbSql.GetLatestVerified, query.Source, query.ProtectionSetId!.Value.Value, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<DatabaseRestorePointReadModel?> GetLatestRestoreTestedBackupAsync(GetLatestRestoreTestedDatabaseBackupQuery query, CancellationToken cancellationToken)
    {
        this.ValidateRequiredArgument(query);
        return await this.GetLatestRestorePointCoreAsync($"{nameof(SystemAdminDbSql)}.{nameof(SystemAdminDbSql.GetLatestRestoreTested)}", SystemAdminDbSql.GetLatestRestoreTested, query.Source, query.ProtectionSetId!.Value.Value, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<DatabaseRestoreOperationReadModel?> GetRestoreOperationAsync(GetDatabaseRestoreOperationQuery query, CancellationToken cancellationToken)
    {
        this.ValidateRequiredArgument(query);
        var operation = await Use($"{nameof(SystemAdminDbSql)}.{nameof(SystemAdminDbSql.GetRestoreOperation)}", SystemAdminDbSql.GetRestoreOperation)
            .SetParameters(new OperationKey(query.OperationId!.Value.Value))
            .ExecuteSingleAsync(MapToOperationProjectionRow, cancellationToken)
            .ConfigureAwait(false);
        return operation?.ToRestoreOperation();
    }

    /// <inheritdoc/>
    public async ValueTask<DatabaseRestoreOperationReadModel[]> ListRestoreDrillsAsync(ListDatabaseRestoreDrillsQuery query, CancellationToken cancellationToken)
    {
        this.ValidateRequiredArgument(query);
        return[..(await Use($"{nameof(SystemAdminDbSql)}.{nameof(SystemAdminDbSql.ListRestoreDrills)}", SystemAdminDbSql.ListRestoreDrills)
            .SetParameters(new RestoreDrillListParameter(query.Source, query.PageSize))
            .ExecuteQueryAsync(MapToOperationProjectionRow, cancellationToken)
            .ConfigureAwait(false)).Select(static row => row.ToRestoreOperation())];
    }

    /// <inheritdoc/>
    public async ValueTask<DatabaseRetentionReadModel?> GetRetentionForecastAsync(GetDatabaseRetentionForecastQuery query, CancellationToken cancellationToken)
    {
        this.ValidateRequiredArgument(query);
        return await Use($"{nameof(SystemAdminDbSql)}.{nameof(SystemAdminDbSql.GetRetention)}", SystemAdminDbSql.GetRetention)
            .SetParameters(new RetentionQueryParameter(query.Source, query.RetentionPlanId?.Value))
            .ExecuteSingleAsync(MapToRetention, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<DatabaseBackupHealthReadModel[]> GetServiceHealthAsync(GetDatabaseBackupServiceHealthQuery query, CancellationToken cancellationToken)
    {
        this.ValidateRequiredArgument(query);
        return[..await Use($"{nameof(SystemAdminDbSql)}.{nameof(SystemAdminDbSql.GetServiceHealth)}", SystemAdminDbSql.GetServiceHealth)
            .SetParameters(new ServiceHealthQueryParameter(query.Request.EnvironmentIdentity, query.Source))
            .ExecuteQueryAsync(MapToHealth, cancellationToken)
            .ConfigureAwait(false)];
    }

    /// <inheritdoc/>
    public async ValueTask<DatabaseBackupStatisticsObservation[]> GetRecoveryRunStatisticsAsync(Guid operationId, CancellationToken cancellationToken) => (await Use("SystemAdmin.RecoveryRunStatistics", SystemAdminDbSql.GetRunStatistics)
            .SetParameters(new OperationKey(operationId))
            .ExecuteQueryAsync(MapToStatisticsProjectionRow, cancellationToken)
            .ConfigureAwait(false)).ToArray();
    /// <inheritdoc/>
    public async ValueTask<DatabaseBackupPhaseReadModel[]> GetBackupPhasesAsync(GetDatabaseBackupLogQuery query, CancellationToken cancellationToken)
    {
        this.ValidateRequiredArgument(query);
        return[..await Use("SystemAdminDbSql.GetBackupPhases", SystemAdminDbSql.GetBackupPhases)
            .SetParameters(new PhasePageParameter(query.OperationId!.Value.Value, query.AfterPhaseRevision, query.PageSize))
            .ExecuteQueryAsync(MapToBackupPhase, cancellationToken)
            .ConfigureAwait(false)];
    }

    /// <summary>Maps persisted columns in query ordinal order.</summary>
    internal static DatabaseProtectionSetReadModel MapToProtectionSet(IObjectDataRecord row) => new()
    {
        ProtectionSetId = new DatabaseProtectionSetId(row.GetString(0)),
        Source = row.GetShort(1).ToEnum<BackupSource>(),
        Engines = [],
        Enabled = true,
        PolicyRevision = row.GetLong(2)
    };
    /// <summary>Maps persisted columns in query ordinal order.</summary>
    internal static DatabaseBackupPolicyReadModel MapToPolicy(IObjectDataRecord row) => new()
    {
        PolicyId = new DatabaseBackupPolicyId(row.GetString(0)),
        EnvironmentIdentity = row.GetString(1),
        Revision = row.GetLong(2),
        Definition = JsonSerializer.Deserialize<DatabaseBackupPolicyDefinition>(row.GetString(3), JsonOptions) ?? throw new InvalidOperationException("Stored database backup policy JSON is invalid."),
        Enforced = row.GetBool(4)
    };
    /// <summary>Maps persisted columns in query ordinal order.</summary>
    internal static DatabaseBackupOperationReadModel MapToOperation(IObjectDataRecord row) => MapToOperationProjectionRow(row).Operation;
    /// <summary>Maps persisted columns in query ordinal order.</summary>
    internal static OperationProjectionRow MapToOperationProjectionRow(IObjectDataRecord row) => new(new DatabaseBackupOperationReadModel { OperationId = new DatabaseRecoveryOperationId(row.GetGuid(0)), BackupSetId = row.IsNull(1) ? null : new DatabaseBackupSetId(row.GetGuid(1)), ProtectionSetId = new DatabaseProtectionSetId(row.GetString(2)), Source = row.GetShort(3).ToEnum<BackupSource>(), Kind = row.GetShort(4).ToEnum<DatabaseRecoveryOperationKind>(), Phase = row.GetShort(5).ToEnum<DatabaseRecoveryPhase>(), Outcome = row.GetShort(6).ToEnum<DatabaseRecoveryOutcome>(), ProgressPercent = row.GetInt(7), Engine = row.GetShort(18).ToEnum<DatabaseEngine>(), StateRevision = row.GetLong(8), CreatedUtc = row.GetDateTime(9).ToUtcOffset(), CompletedUtc = row.IsNull(10) ? null : row.GetDateTime(10).ToUtcOffset(), SafeDiagnosticReference = row.GetString(11), BackupLineage = row.IsNull(17) || string.IsNullOrWhiteSpace(row.GetString(17)) ? null : row.GetString(17).DeserializeLineage() }, row.IsNull(12) ? null : new DatabaseRestorePointId(row.GetString(12)), row.GetShort(13).ToEnum<DatabaseRestoreClass>(), row.GetString(14), row.GetLong(15), row.GetShort(16).ToEnum<DatabaseCutoverState>());
    /// <summary>Maps persisted columns in query ordinal order.</summary>
    internal static DatabaseRestorePointReadModel MapToRestorePoint(IObjectDataRecord row) => new()
    {
        RestorePointId = new DatabaseRestorePointId(row.GetString(0)),
        BackupSetId = row.IsNull(1) ? null : new DatabaseBackupSetId(row.GetGuid(1)),
        ProtectionSetId = new DatabaseProtectionSetId(row.GetString(2)),
        Source = row.GetShort(3).ToEnum<BackupSource>(),
        RecoveryPointUtc = row.GetDateTime(4).ToUtcOffset(),
        VerificationLevel = row.GetShort(5).ToEnum<DatabaseVerificationLevel>(),
        VerifiedUtc = row.IsNull(6) ? null : row.GetDateTime(6).ToUtcOffset(),
        RestoreTestedUtc = row.IsNull(7) ? null : row.GetDateTime(7).ToUtcOffset(),
        Eligible = row.GetBool(8),
        LegalHold = row.GetBool(9),
        ManifestRevision = row.GetLong(10),
        BackupLineage = row.IsNull(11) || string.IsNullOrWhiteSpace(row.GetString(11)) ? null : row.GetString(11).DeserializeLineage()
    };
    /// <summary>Maps persisted columns in query ordinal order.</summary>
    internal static DatabaseRetentionReadModel MapToRetention(IObjectDataRecord row) => new()
    {
        PlanId = new DatabaseRetentionPlanId(row.GetGuid(0)),
        Source = row.GetShort(1).ToEnum<BackupSource>(),
        PlanRevision = row.GetLong(2),
        EvaluationBoundaryUtc = row.GetDateTime(3).ToUtcOffset(),
        Retain = JsonSerializer.Deserialize<DatabaseRestorePointId[]>(row.GetString(4), JsonOptions) ?? [],
        Delete = JsonSerializer.Deserialize<DatabaseRestorePointId[]>(row.GetString(5), JsonOptions) ?? [],
        Approved = row.GetBool(6),
        Outcome = row.GetShort(7).ToEnum<DatabaseRecoveryOutcome>()
    };
    /// <summary>Maps persisted columns in query ordinal order.</summary>
    internal static DatabaseBackupHealthReadModel MapToHealth(IObjectDataRecord row) => new()
    {
        Source = row.GetShort(0).ToEnum<BackupSource>(),
        HostId = new DatabaseBackupHostId(row.GetString(1)),
        CapabilityState = row.GetShort(2).ToEnum<DatabaseServiceCapabilityState>(),
        Ready = row.GetBool(3),
        LastServiceSequence = row.GetLong(4),
        ObservedUtc = row.GetDateTime(5).ToUtcOffset(),
        SafeDiagnosticReference = row.GetString(6)
    };
    /// <summary>Maps persisted columns in query ordinal order.</summary>
    internal static DatabaseBackupStatisticsObservation MapToStatisticsProjectionRow(IObjectDataRecord row) => new(row.GetShort(0).ToEnum<BackupSource>(), row.GetLong(1), new DatabaseRecoveryRunStatistics { Engine = row.GetShort(2).ToEnum<DatabaseEngine>(), Phase = row.GetShort(3).ToEnum<DatabaseRecoveryPhase>(), StartedUtc = row.IsNull(4) ? null : row.GetDateTime(4).ToUtcOffset(), CompletedUtc = row.IsNull(5) ? null : row.GetDateTime(5).ToUtcOffset(), Elapsed = row.IsNull(6) ? null : TimeSpan.FromTicks(row.GetLong(6)), SourceBytes = row.IsNull(7) ? null : row.GetLong(7), StoredBytes = row.IsNull(8) ? null : row.GetLong(8), TransferredBytes = row.IsNull(9) ? null : row.GetLong(9), RestoredBytes = row.IsNull(10) ? null : row.GetLong(10), ArtifactCount = row.IsNull(11) ? null : row.GetInt(11), AverageThroughputBytesPerSecond = row.IsNull(12) ? null : row.GetDouble(12), PeakThroughputBytesPerSecond = row.IsNull(13) ? null : row.GetDouble(13), RetryCount = row.IsNull(14) ? null : row.GetInt(14), WarningCount = row.IsNull(15) ? null : row.GetInt(15), AchievedRpo = row.IsNull(16) ? null : TimeSpan.FromTicks(row.GetLong(16)), AchievedRto = row.IsNull(17) ? null : TimeSpan.FromTicks(row.GetLong(17)) });
    /// <inheritdoc/>
    public Task<int> EnqueueDatabaseBackupExecutionIntentAsync(IEnlistedPostgresTransaction transaction, long eventId, Guid eventUuid, CancellationToken cancellationToken = default) => transaction.ExecuteAsync(SystemAdminDbSql.EnqueueExecutionIntent, transaction.BindBackupParameters(new BackupExecutionIdentity(eventId, eventUuid)), cancellationToken);
    /// <inheritdoc/>
    public async ValueTask<EventStreamReadModel[]> ReadPendingDatabaseBackupExecutionIntentsAsync(CancellationToken cancellationToken = default) => (await Use("SystemAdmin.PendingExecutionIntents", SystemAdminDbSql.ReadPendingExecutionIntents)
            .ExecuteQueryAsync(MapToPendingExecutionIntent, cancellationToken)
            .ConfigureAwait(false)).ToArray();
    /// <inheritdoc/>
    public async Task<int> MarkDatabaseBackupExecutionIntentPublishedAsync(long eventId, Guid eventUuid, DateTime publishedUtc, CancellationToken cancellationToken = default) => checked((int)(await Use("SystemAdmin.MarkExecutionIntentPublished", SystemAdminDbSql.MarkExecutionIntentPublished)
            .SetParameters(new BackupExecutionIdentity(eventId, eventUuid, publishedUtc))
            .ExecuteCommandAsync(cancellationToken)
            .ConfigureAwait(false)).Single());
    /// <summary>Maps source event bytes for domain-owned decoding and dispatch.</summary>
    internal static EventStreamReadModel MapToPendingExecutionIntent(IObjectDataRecord row) => new()
    {
        EventTypeName = row.GetString(0),
        EventVersion = row.GetLong(1),
        EventData = row.GetBytes(2)
    };
    /// <summary>Maps persisted phase columns in query ordinal order.</summary>
    internal static DatabaseBackupPhaseReadModel MapToBackupPhase(IObjectDataRecord row) => new()
    {
        Revision = row.GetLong(0),
        ObservedUtc = row.GetDateTime(1).ToUtcOffset(),
        Phase = row.GetShort(2).ToEnum<DatabaseRecoveryPhase>(),
        Outcome = row.GetShort(3).ToEnum<DatabaseRecoveryOutcome>(),
        ProgressPercent = row.GetInt(4)
    };
}