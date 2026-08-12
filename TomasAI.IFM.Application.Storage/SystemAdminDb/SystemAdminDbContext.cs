using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events.Domain;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Queries;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ReadModels;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.EventProjector;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage;

/// <summary>
/// PostgreSQL repository for SystemAdmin database-recovery projections.
/// </summary>
public sealed class SystemAdminDbContext(
    IDbConnectionSettings connectionSettings,
    ILogger<DbProvider> logger)
    : ObjectDataRepository<SystemAdminDbContext>(connectionSettings[SystemAdminDbConnection], logger),
      ISystemAdminDbContext
{
    public const string SystemAdminDbConnection = "SystemAdminDbConnection";
    const string EmptyJsonArray = "[]";
    static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public override SystemAdminDbContext Database => this;

    public async ValueTask<EventProjectionApplyOutcome> ApplyDatabaseBackupEventAsync(
        string projectorName,
        DatabaseBackupEventContract domainEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectorName);
        ArgumentNullException.ThrowIfNull(domainEvent);
        domainEvent.Validate();
        if (domainEvent.EventId <= 0)
            throw new ArgumentOutOfRangeException(nameof(domainEvent), "A persisted positive event revision is required.");
        if (domainEvent.GetType().Namespace?.EndsWith(".Events.Domain", StringComparison.Ordinal) != true)
            throw new ArgumentException("Only authoritative DatabaseBackup domain events can be projected.", nameof(domainEvent));

        var hash = ComputeEventHash(domainEvent);
        var transaction = BeginTransaction();
        try
        {
            var existing = await Use(SystemAdminDbSql.GetProjectionReceiptForUpdate)
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

            await ApplyRowsAsync(domainEvent, cancellationToken).ConfigureAwait(false);
            var now = DateTime.UtcNow;
            await Use(SystemAdminDbSql.InsertProjectionReceipt)
                .SetParameters(new InsertProjectionReceiptParameter(
                    projectorName, domainEvent.EventId, hash, domainEvent.Source.SourceEventId, now))
                .ExecuteCommandAsync(cancellationToken)
                .ConfigureAwait(false);
            await Use(SystemAdminDbSql.UpsertProjectionCheckpoint)
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

    public async ValueTask<DatabaseBackupProjectionCheckpoint?> GetDatabaseBackupProjectionCheckpointAsync(
        string projectorName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectorName);
        return await Use(SystemAdminDbSql.GetProjectionCheckpoint)
            .SetParameters(new ProjectorKey(projectorName))
            .ExecuteSingleAsync(static row => new DatabaseBackupProjectionCheckpoint(
                row.GetString(0), row.GetLong(1), row.GetLong(2), AsOffset(row.GetDateTime(3))), cancellationToken)
            .ConfigureAwait(false);
    }

    public async ValueTask ClearDatabaseBackupProjectionsAsync(
        string projectorName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectorName);
        var transaction = BeginTransaction();
        try
        {
            await Use(SystemAdminDbSql.ClearProjections)
                .ExecuteCommandAsync(cancellationToken)
                .ConfigureAwait(false);
            await Use(SystemAdminDbSql.ClearProjectionReceipts)
                .SetParameters(new ProjectorKey(projectorName))
                .ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);
            await Use(SystemAdminDbSql.ClearProjectionCheckpoint)
                .SetParameters(new ProjectorKey(projectorName))
                .ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);
            transaction?.Commit();
        }
        catch
        {
            transaction?.Rollback();
            throw;
        }
    }

    async Task ApplyRowsAsync(DatabaseBackupEventContract domainEvent, CancellationToken cancellationToken)
    {
        if (ProjectsOperation(domainEvent))
        {
            await Use(SystemAdminDbSql.UpsertOperation)
                .SetParameters(new UpsertOperationParameter(domainEvent))
                .ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);
            await Use(SystemAdminDbSql.InsertPhase)
                .SetParameters(new InsertPhaseParameter(domainEvent))
                .ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);
        }

        if (domainEvent.RestorePointId is not null)
        {
            var release = domainEvent is DatabaseBackupLegalHoldReleasedEvent;
            var hold = domainEvent is DatabaseBackupLegalHoldPlacedEvent;
            var restoreTested = domainEvent.Source.OperationKind == DatabaseRecoveryOperationKind.RestoreDrill
                && domainEvent is DatabaseOperationCompletedEvent;
            var eligible = domainEvent.Outcome is not DatabaseRecoveryOutcome.Failed and not DatabaseRecoveryOutcome.Rejected;
            await Use(SystemAdminDbSql.UpsertRestorePoint)
                .SetParameters(new UpsertRestorePointParameter(domainEvent, eligible, hold && !release, restoreTested))
                .ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);
        }

        if (domainEvent.ArtifactReplica is not null)
            await Use(SystemAdminDbSql.UpsertArtifactReplica)
                .SetParameters(new UpsertArtifactReplicaParameter(domainEvent))
                .ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);

        if (domainEvent is DatabaseOperationErrorRecordedEvent or DatabaseOperationFailedEvent)
            await Use(SystemAdminDbSql.UpsertRecoveryError)
                .SetParameters(new UpsertRecoveryErrorParameter(domainEvent))
                .ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);

        if (domainEvent.Policy is not null && domainEvent.PolicyId is not null)
            await Use(SystemAdminDbSql.UpsertPolicy)
                .SetParameters(new UpsertPolicyParameter(
                    domainEvent,
                    JsonSerializer.Serialize(domainEvent.Policy, JsonOptions),
                    domainEvent is DatabaseBackupPolicyEnforcedEvent))
                .ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);

        if (domainEvent.Source.ProducingHostId is not null
            && domainEvent is DatabaseBackupServiceCapabilityRecordedEvent or DatabaseBackupServiceReconciledEvent)
            await Use(SystemAdminDbSql.UpsertServiceHealth)
                .SetParameters(new UpsertServiceHealthParameter(
                    domainEvent, domainEvent is DatabaseBackupServiceReconciledEvent))
                .ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);

        if (domainEvent.RetentionPlanId is not null)
            await Use(SystemAdminDbSql.UpsertRetention)
                .SetParameters(new UpsertRetentionParameter(
                    domainEvent, EmptyJsonArray, EmptyJsonArray,
                    domainEvent is DatabaseRetentionAuthorizedDomainEvent or DatabaseRetentionExecutionRequestedDomainEvent))
                .ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);

        if (domainEvent.Statistics is not null)
            await Use(SystemAdminDbSql.InsertRunStatistics)
                .SetParameters(new InsertRunStatisticsParameter(domainEvent))
                .ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<DatabaseProtectionSetReadModel[]> GetProtectionSetsAsync(
        GetDatabaseProtectionSetsQuery query, CancellationToken cancellationToken)
        => [.. await Use(SystemAdminDbSql.GetProtectionSets)
            .SetParameters(new SourceFilter(query.Source))
            .ExecuteQueryAsync(MapProtectionSet, cancellationToken).ConfigureAwait(false)];

    public async ValueTask<DatabaseBackupPolicyReadModel?> GetPolicyAsync(
        GetDatabaseBackupPolicyQuery query, CancellationToken cancellationToken)
        => await Use(SystemAdminDbSql.GetPolicy)
            .SetParameters(new PolicyQueryParameter(query.Request.EnvironmentIdentity, query.PolicyId!.Value.Value))
            .ExecuteSingleAsync(MapPolicy, cancellationToken).ConfigureAwait(false);

    public async ValueTask<DatabaseBackupOperationReadModel?> GetBackupOperationAsync(
        GetDatabaseBackupOperationQuery query, CancellationToken cancellationToken)
        => await GetOperationAsync(query.OperationId!.Value.Value, cancellationToken).ConfigureAwait(false);

    public async ValueTask<DatabaseBackupOperationReadModel[]> ListBackupOperationsAsync(
        ListDatabaseBackupOperationsQuery query, CancellationToken cancellationToken)
    {
        Guid? continuation = Guid.TryParse(query.ContinuationIdentity, out var parsed) ? parsed : null;
        return [.. await Use(SystemAdminDbSql.ListOperations)
            .SetParameters(new OperationListParameter(
                query.Source, query.ProtectionSetId?.Value, query.FromUtc?.UtcDateTime,
                query.ToUtc?.UtcDateTime, continuation, query.PageSize))
            .ExecuteQueryAsync(MapOperation, cancellationToken).ConfigureAwait(false)];
    }

    public async ValueTask<DatabaseBackupSetReadModel?> GetBackupSetAsync(
        GetDatabaseBackupSetQuery query, CancellationToken cancellationToken)
    {
        var operations = (await Use(SystemAdminDbSql.GetBackupSetOperations)
            .SetParameters(new BackupSetKey(query.BackupSetId!.Value.Value))
            .ExecuteQueryAsync(MapOperation, cancellationToken).ConfigureAwait(false)).ToArray();
        if (operations.Length == 0) return null;
        return new DatabaseBackupSetReadModel
        {
            BackupSetId = query.BackupSetId.Value,
            Source = operations[0].Source,
            OperationIds = [.. operations.Select(static operation => operation.OperationId)],
            RequiredOperationCount = operations.Length,
            CompletedOperationCount = operations.Count(static operation => operation.Outcome == DatabaseRecoveryOutcome.Succeeded),
            Complete = operations.All(static operation => operation.Outcome == DatabaseRecoveryOutcome.Succeeded),
            Revision = operations.Max(static operation => operation.StateRevision)
        };
    }

    public async ValueTask<DatabaseRestorePointReadModel[]> ListRestorePointsAsync(
        ListDatabaseRestorePointsQuery query, CancellationToken cancellationToken)
        => [.. await Use(SystemAdminDbSql.ListRestorePoints)
            .SetParameters(new RestorePointListParameter(
                query.Source, query.ProtectionSetId?.Value, query.FromUtc?.UtcDateTime,
                query.ToUtc?.UtcDateTime, NullIfEmpty(query.ContinuationIdentity), query.PageSize))
            .ExecuteQueryAsync(MapRestorePoint, cancellationToken).ConfigureAwait(false)];

    public async ValueTask<DatabaseRestorePointReadModel?> GetRestorePointAsync(
        GetDatabaseRestorePointQuery query, CancellationToken cancellationToken)
        => await Use(SystemAdminDbSql.GetRestorePoint)
            .SetParameters(new RestorePointKey(query.RestorePointId!.Value.Value, query.Source))
            .ExecuteSingleAsync(MapRestorePoint, cancellationToken).ConfigureAwait(false);

    public async ValueTask<DatabaseRestorePointReadModel?> GetLatestVerifiedBackupAsync(
        GetLatestVerifiedDatabaseBackupQuery query, CancellationToken cancellationToken)
        => await GetLatestRestorePointAsync(SystemAdminDbSql.GetLatestVerified, query.Source,
            query.ProtectionSetId!.Value.Value, cancellationToken).ConfigureAwait(false);

    public async ValueTask<DatabaseRestorePointReadModel?> GetLatestRestoreTestedBackupAsync(
        GetLatestRestoreTestedDatabaseBackupQuery query, CancellationToken cancellationToken)
        => await GetLatestRestorePointAsync(SystemAdminDbSql.GetLatestRestoreTested, query.Source,
            query.ProtectionSetId!.Value.Value, cancellationToken).ConfigureAwait(false);

    public ValueTask<DatabaseProtectionSetReadModel[]> GetRecoveryObjectiveComplianceAsync(
        GetDatabaseRecoveryObjectiveComplianceQuery query, CancellationToken cancellationToken)
        => GetProtectionSetsAsync(new GetDatabaseProtectionSetsQuery
        {
            EntityId = query.EntityId, Request = query.Request, Source = query.Source,
            Subject = query.Subject, PageSize = query.PageSize
        }, cancellationToken);

    public async ValueTask<DatabaseRestoreOperationReadModel?> GetRestoreOperationAsync(
        GetDatabaseRestoreOperationQuery query, CancellationToken cancellationToken)
    {
        var operation = await Use(SystemAdminDbSql.GetRestoreOperation)
            .SetParameters(new OperationKey(query.OperationId!.Value.Value))
            .ExecuteSingleAsync(MapOperationRow, cancellationToken).ConfigureAwait(false);
        return operation is null ? null : ToRestoreOperation(operation);
    }

    public async ValueTask<DatabaseRestoreOperationReadModel[]> ListRestoreDrillsAsync(
        ListDatabaseRestoreDrillsQuery query, CancellationToken cancellationToken)
        => [.. (await Use(SystemAdminDbSql.ListRestoreDrills)
            .SetParameters(new RestoreDrillListParameter(query.Source, query.PageSize))
            .ExecuteQueryAsync(MapOperationRow, cancellationToken).ConfigureAwait(false))
            .Select(ToRestoreOperation)];

    public async ValueTask<DatabaseRetentionReadModel?> GetRetentionForecastAsync(
        GetDatabaseRetentionForecastQuery query, CancellationToken cancellationToken)
        => await Use(SystemAdminDbSql.GetRetention)
            .SetParameters(new RetentionQueryParameter(query.Source, query.RetentionPlanId?.Value))
            .ExecuteSingleAsync(MapRetention, cancellationToken).ConfigureAwait(false);

    public async ValueTask<DatabaseBackupHealthReadModel[]> GetServiceHealthAsync(
        GetDatabaseBackupServiceHealthQuery query, CancellationToken cancellationToken)
        => [.. await Use(SystemAdminDbSql.GetServiceHealth)
            .SetParameters(new ServiceHealthQueryParameter(query.Request.EnvironmentIdentity, query.Source))
            .ExecuteQueryAsync(MapHealth, cancellationToken).ConfigureAwait(false)];

    public async ValueTask<DatabaseRecoveryRunStatsReadModel?> GetRecoveryRunStatsAsync(
        GetDatabaseRecoveryRunStatsQuery query, CancellationToken cancellationToken)
    {
        var rows = (await Use(SystemAdminDbSql.GetRunStatistics)
            .SetParameters(new OperationKey(query.OperationId!.Value.Value))
            .ExecuteQueryAsync(MapStatisticsRow, cancellationToken).ConfigureAwait(false)).ToArray();
        if (rows.Length == 0) return null;
        return new DatabaseRecoveryRunStatsReadModel
        {
            OperationId = query.OperationId.Value,
            Source = rows[0].Source,
            StatisticsRevision = rows.Max(static row => row.Revision),
            Statistics = [.. rows.Select(static row => row.Statistics)]
        };
    }

    async ValueTask<DatabaseBackupOperationReadModel?> GetOperationAsync(Guid operationId, CancellationToken cancellationToken)
        => await Use(SystemAdminDbSql.GetOperation)
            .SetParameters(new OperationKey(operationId))
            .ExecuteSingleAsync(MapOperation, cancellationToken).ConfigureAwait(false);

    async ValueTask<DatabaseRestorePointReadModel?> GetLatestRestorePointAsync(
        string sql, BackupSource source, string protectionSetId, CancellationToken cancellationToken)
        => await Use(sql)
            .SetParameters(new LatestRestorePointKey(source, protectionSetId))
            .ExecuteSingleAsync(MapRestorePoint, cancellationToken).ConfigureAwait(false);

    static DatabaseProtectionSetReadModel MapProtectionSet(IObjectDataRecord row) => new()
    {
        ProtectionSetId = new DatabaseProtectionSetId(row.GetString(0)),
        Source = EnumValue<BackupSource>(row, 1),
        Engines = [], Enabled = true, PolicyRevision = row.GetLong(2)
    };

    static DatabaseBackupPolicyReadModel MapPolicy(IObjectDataRecord row) => new()
    {
        PolicyId = new DatabaseBackupPolicyId(row.GetString(0)),
        EnvironmentIdentity = row.GetString(1), Revision = row.GetLong(2),
        Definition = JsonSerializer.Deserialize<DatabaseBackupPolicyDefinition>(row.GetString(3), JsonOptions)
            ?? throw new InvalidOperationException("Stored database backup policy JSON is invalid."),
        Enforced = row.GetBool(4)
    };

    static DatabaseBackupOperationReadModel MapOperation(IObjectDataRecord row) => MapOperationRow(row).Operation;

    static OperationProjectionRow MapOperationRow(IObjectDataRecord row)
        => new(new DatabaseBackupOperationReadModel
        {
            OperationId = new DatabaseRecoveryOperationId(row.GetGuid(0)),
            BackupSetId = row.IsNull(1) ? null : new DatabaseBackupSetId(row.GetGuid(1)),
            ProtectionSetId = new DatabaseProtectionSetId(row.GetString(2)),
            Source = EnumValue<BackupSource>(row, 3), Kind = EnumValue<DatabaseRecoveryOperationKind>(row, 4),
            Phase = EnumValue<DatabaseRecoveryPhase>(row, 5), Outcome = EnumValue<DatabaseRecoveryOutcome>(row, 6),
            ProgressPercent = row.GetInt(7), StateRevision = row.GetLong(8),
            CreatedUtc = AsOffset(row.GetDateTime(9)), CompletedUtc = row.IsNull(10) ? null : AsOffset(row.GetDateTime(10)),
            SafeDiagnosticReference = row.GetString(11)
        },
        row.IsNull(12) ? null : new DatabaseRestorePointId(row.GetString(12)),
        EnumValue<DatabaseRestoreClass>(row, 13), row.GetString(14), row.GetLong(15), EnumValue<DatabaseCutoverState>(row, 16));

    static DatabaseRestorePointReadModel MapRestorePoint(IObjectDataRecord row) => new()
    {
        RestorePointId = new DatabaseRestorePointId(row.GetString(0)),
        BackupSetId = row.IsNull(1) ? null : new DatabaseBackupSetId(row.GetGuid(1)),
        ProtectionSetId = new DatabaseProtectionSetId(row.GetString(2)), Source = EnumValue<BackupSource>(row, 3),
        RecoveryPointUtc = AsOffset(row.GetDateTime(4)), VerificationLevel = EnumValue<DatabaseVerificationLevel>(row, 5),
        VerifiedUtc = row.IsNull(6) ? null : AsOffset(row.GetDateTime(6)),
        RestoreTestedUtc = row.IsNull(7) ? null : AsOffset(row.GetDateTime(7)),
        Eligible = row.GetBool(8), LegalHold = row.GetBool(9), ManifestRevision = row.GetLong(10)
    };

    static DatabaseRestoreOperationReadModel ToRestoreOperation(OperationProjectionRow row) => new()
    {
        Operation = row.Operation,
        RestorePointId = row.RestorePointId ?? new DatabaseRestorePointId("unknown"),
        RestoreClass = row.RestoreClass,
        FreshTargetProfile = row.FreshTargetProfile,
        ValidationRevision = row.ValidationRevision,
        CutoverState = row.CutoverState
    };

    static DatabaseRetentionReadModel MapRetention(IObjectDataRecord row) => new()
    {
        PlanId = new DatabaseRetentionPlanId(row.GetGuid(0)), Source = EnumValue<BackupSource>(row, 1),
        PlanRevision = row.GetLong(2), EvaluationBoundaryUtc = AsOffset(row.GetDateTime(3)),
        Retain = JsonSerializer.Deserialize<DatabaseRestorePointId[]>(row.GetString(4), JsonOptions) ?? [],
        Delete = JsonSerializer.Deserialize<DatabaseRestorePointId[]>(row.GetString(5), JsonOptions) ?? [],
        Approved = row.GetBool(6), Outcome = EnumValue<DatabaseRecoveryOutcome>(row, 7)
    };

    static DatabaseBackupHealthReadModel MapHealth(IObjectDataRecord row) => new()
    {
        Source = EnumValue<BackupSource>(row, 0), HostId = new DatabaseBackupHostId(row.GetString(1)),
        CapabilityState = EnumValue<DatabaseServiceCapabilityState>(row, 2), Ready = row.GetBool(3),
        LastServiceSequence = row.GetLong(4), ObservedUtc = AsOffset(row.GetDateTime(5)),
        SafeDiagnosticReference = row.GetString(6)
    };

    static StatisticsProjectionRow MapStatisticsRow(IObjectDataRecord row)
        => new(EnumValue<BackupSource>(row, 0), row.GetLong(1), new DatabaseRecoveryRunStatistics
        {
            Engine = EnumValue<DatabaseEngine>(row, 2), Phase = EnumValue<DatabaseRecoveryPhase>(row, 3),
            StartedUtc = row.IsNull(4) ? null : AsOffset(row.GetDateTime(4)),
            CompletedUtc = row.IsNull(5) ? null : AsOffset(row.GetDateTime(5)),
            Elapsed = row.IsNull(6) ? null : TimeSpan.FromTicks(row.GetLong(6)),
            SourceBytes = row.IsNull(7) ? null : row.GetLong(7), StoredBytes = row.IsNull(8) ? null : row.GetLong(8),
            TransferredBytes = row.IsNull(9) ? null : row.GetLong(9), RestoredBytes = row.IsNull(10) ? null : row.GetLong(10),
            ArtifactCount = row.IsNull(11) ? null : row.GetInt(11),
            AverageThroughputBytesPerSecond = row.IsNull(12) ? null : row.GetDouble(12),
            PeakThroughputBytesPerSecond = row.IsNull(13) ? null : row.GetDouble(13),
            RetryCount = row.IsNull(14) ? null : row.GetInt(14), WarningCount = row.IsNull(15) ? null : row.GetInt(15),
            AchievedRpo = row.IsNull(16) ? null : TimeSpan.FromTicks(row.GetLong(16)),
            AchievedRto = row.IsNull(17) ? null : TimeSpan.FromTicks(row.GetLong(17))
        });

    static bool ProjectsOperation(DatabaseBackupEventContract domainEvent)
        => domainEvent is not (DatabaseBackupPolicyRevisedEvent or DatabaseBackupPolicyEnforcedEvent
            or DatabaseBackupLegalHoldPlacedEvent or DatabaseBackupLegalHoldReleasedEvent
            or DatabaseBackupServiceCapabilityRecordedEvent or DatabaseBackupServiceReconciledEvent);

    static string ComputeEventHash(DatabaseBackupEventContract domainEvent)
    {
        var json = JsonSerializer.Serialize(domainEvent, domainEvent.GetType(), JsonOptions);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
    }

    static DateTimeOffset AsOffset(DateTime value)
        => new(value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc));

    static TEnum EnumValue<TEnum>(IObjectDataRecord row, int index) where TEnum : struct, Enum
        => (TEnum)Enum.ToObject(typeof(TEnum), row.GetShort(index));

    static string? NullIfEmpty(string value) => string.IsNullOrWhiteSpace(value) ? null : value;

    sealed record OperationProjectionRow(
        DatabaseBackupOperationReadModel Operation,
        DatabaseRestorePointId? RestorePointId,
        DatabaseRestoreClass RestoreClass,
        string FreshTargetProfile,
        long ValidationRevision,
        DatabaseCutoverState CutoverState);

    sealed record StatisticsProjectionRow(
        BackupSource Source,
        long Revision,
        DatabaseRecoveryRunStatistics Statistics);
}
