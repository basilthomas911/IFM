using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Queries;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ReadModels;

namespace TomasAI.IFM.Application.Storage;

/// <summary>Defines SystemAdmin projection queries.</summary>
public interface ISystemAdminDbReadContext
{
    ValueTask<DatabaseBackupProjectionCheckpointReadModel?> GetDatabaseBackupProjectionCheckpointAsync(
        string projectorName,
        CancellationToken cancellationToken = default);
    ValueTask<DatabaseProtectionSetReadModel[]> GetProtectionSetsAsync(GetDatabaseProtectionSetsQuery query, CancellationToken cancellationToken);
    ValueTask<DatabaseBackupPolicyReadModel?> GetPolicyAsync(GetDatabaseBackupPolicyQuery query, CancellationToken cancellationToken);
    ValueTask<DatabaseBackupOperationReadModel?> GetBackupOperationAsync(GetDatabaseBackupOperationQuery query, CancellationToken cancellationToken);
    ValueTask<DatabaseBackupOperationReadModel[]> ListBackupOperationsAsync(ListDatabaseBackupOperationsQuery query, CancellationToken cancellationToken);
    ValueTask<DatabaseBackupSetReadModel?> GetBackupSetAsync(GetDatabaseBackupSetQuery query, CancellationToken cancellationToken);
    ValueTask<DatabaseRestorePointReadModel[]> ListRestorePointsAsync(ListDatabaseRestorePointsQuery query, CancellationToken cancellationToken);
    ValueTask<DatabaseRestorePointReadModel?> GetRestorePointAsync(GetDatabaseRestorePointQuery query, CancellationToken cancellationToken);
    ValueTask<DatabaseRestorePointReadModel?> GetLatestVerifiedBackupAsync(GetLatestVerifiedDatabaseBackupQuery query, CancellationToken cancellationToken);
    ValueTask<DatabaseRestorePointReadModel?> GetLatestRestoreTestedBackupAsync(GetLatestRestoreTestedDatabaseBackupQuery query, CancellationToken cancellationToken);
    ValueTask<DatabaseProtectionSetReadModel[]> GetRecoveryObjectiveComplianceAsync(GetDatabaseRecoveryObjectiveComplianceQuery query, CancellationToken cancellationToken);
    ValueTask<DatabaseRestoreOperationReadModel?> GetRestoreOperationAsync(GetDatabaseRestoreOperationQuery query, CancellationToken cancellationToken);
    ValueTask<DatabaseRestoreOperationReadModel[]> ListRestoreDrillsAsync(ListDatabaseRestoreDrillsQuery query, CancellationToken cancellationToken);
    ValueTask<DatabaseRetentionReadModel?> GetRetentionForecastAsync(GetDatabaseRetentionForecastQuery query, CancellationToken cancellationToken);
    ValueTask<DatabaseBackupHealthReadModel[]> GetServiceHealthAsync(GetDatabaseBackupServiceHealthQuery query, CancellationToken cancellationToken);
    ValueTask<DatabaseRecoveryRunStatsReadModel?> GetRecoveryRunStatsAsync(GetDatabaseRecoveryRunStatsQuery query, CancellationToken cancellationToken);
}
