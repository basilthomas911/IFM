using TomasAI.IFM.Shared.EventSourcing.ViewModels;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Queries;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ReadModels;

namespace TomasAI.IFM.Application.Storage;
/// <summary>Defines SystemAdmin projection queries.</summary>
public interface ISystemAdminDbReadContext
{
    /// <summary>Reads persisted GetDatabaseBackupProjectionCheckpointAsync results.</summary>
    ValueTask<DatabaseBackupProjectionCheckpointReadModel?> GetDatabaseBackupProjectionCheckpointAsync(string projectorName, CancellationToken cancellationToken = default);
    /// <summary>Reads phase observations after the specified persisted revision.</summary>
    ValueTask<DatabaseBackupPhaseReadModel[]> GetBackupPhasesAsync(GetDatabaseBackupLogQuery query, CancellationToken cancellationToken) => throw new NotSupportedException("Backup phase paging is unavailable.");
    /// <summary>Reads persisted GetProtectionSetsAsync results.</summary>
    ValueTask<DatabaseProtectionSetReadModel[]> GetProtectionSetsAsync(GetDatabaseProtectionSetsQuery query, CancellationToken cancellationToken);
    /// <summary>Reads persisted GetPolicyAsync results.</summary>
    ValueTask<DatabaseBackupPolicyReadModel?> GetPolicyAsync(GetDatabaseBackupPolicyQuery query, CancellationToken cancellationToken);
    /// <summary>Reads persisted GetBackupOperationAsync results.</summary>
    ValueTask<DatabaseBackupOperationReadModel?> GetBackupOperationAsync(GetDatabaseBackupOperationQuery query, CancellationToken cancellationToken);
    /// <summary>Reads persisted ListBackupOperationsAsync results.</summary>
    ValueTask<DatabaseBackupOperationReadModel[]> ListBackupOperationsAsync(ListDatabaseBackupOperationsQuery query, CancellationToken cancellationToken);
    /// <summary>Reads all persisted operation facts for one backup set.</summary>
    ValueTask<DatabaseBackupOperationReadModel[]> GetBackupSetOperationsAsync(Guid backupSetId, CancellationToken cancellationToken);
    /// <summary>Reads persisted ListRestorePointsAsync results.</summary>
    ValueTask<DatabaseRestorePointReadModel[]> ListRestorePointsAsync(ListDatabaseRestorePointsQuery query, CancellationToken cancellationToken);
    /// <summary>Reads persisted GetRestorePointAsync results.</summary>
    ValueTask<DatabaseRestorePointReadModel?> GetRestorePointAsync(GetDatabaseRestorePointQuery query, CancellationToken cancellationToken);
    /// <summary>Reads persisted GetLatestVerifiedBackupAsync results.</summary>
    ValueTask<DatabaseRestorePointReadModel?> GetLatestVerifiedBackupAsync(GetLatestVerifiedDatabaseBackupQuery query, CancellationToken cancellationToken);
    /// <summary>Reads persisted GetLatestRestoreTestedBackupAsync results.</summary>
    ValueTask<DatabaseRestorePointReadModel?> GetLatestRestoreTestedBackupAsync(GetLatestRestoreTestedDatabaseBackupQuery query, CancellationToken cancellationToken);
    /// <summary>Reads persisted GetRestoreOperationAsync results.</summary>
    ValueTask<DatabaseRestoreOperationReadModel?> GetRestoreOperationAsync(GetDatabaseRestoreOperationQuery query, CancellationToken cancellationToken);
    /// <summary>Reads persisted ListRestoreDrillsAsync results.</summary>
    ValueTask<DatabaseRestoreOperationReadModel[]> ListRestoreDrillsAsync(ListDatabaseRestoreDrillsQuery query, CancellationToken cancellationToken);
    /// <summary>Reads persisted GetRetentionForecastAsync results.</summary>
    ValueTask<DatabaseRetentionReadModel?> GetRetentionForecastAsync(GetDatabaseRetentionForecastQuery query, CancellationToken cancellationToken);
    /// <summary>Reads persisted GetServiceHealthAsync results.</summary>
    ValueTask<DatabaseBackupHealthReadModel[]> GetServiceHealthAsync(GetDatabaseBackupServiceHealthQuery query, CancellationToken cancellationToken);
    /// <summary>Reads persisted run statistics observations for one operation.</summary>
    ValueTask<DatabaseBackupStatisticsObservation[]> GetRecoveryRunStatisticsAsync(Guid operationId, CancellationToken cancellationToken);
    /// <summary>Reads at most 100 pending persisted event records for domain-owned dispatch.</summary>
    ValueTask<EventStreamReadModel[]> ReadPendingDatabaseBackupExecutionIntentsAsync(CancellationToken cancellationToken = default);
}