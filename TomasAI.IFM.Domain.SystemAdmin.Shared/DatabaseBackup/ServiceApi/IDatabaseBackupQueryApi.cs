using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Queries;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ReadModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ServiceApi;

public interface IDatabaseBackupQueryApi
{
    ValueTask<ServiceResult<DatabaseProtectionSetReadModel[]>> GetProtectionSetsAsync(GetDatabaseProtectionSetsQuery query, CancellationToken cancellationToken = default);
    ValueTask<ServiceResult<DatabaseBackupPolicyReadModel>> GetPolicyAsync(GetDatabaseBackupPolicyQuery query, CancellationToken cancellationToken = default);
    ValueTask<ServiceResult<DatabaseBackupOperationReadModel>> GetBackupOperationAsync(GetDatabaseBackupOperationQuery query, CancellationToken cancellationToken = default);
    ValueTask<ServiceResult<DatabaseBackupOperationReadModel[]>> ListBackupOperationsAsync(ListDatabaseBackupOperationsQuery query, CancellationToken cancellationToken = default);
    ValueTask<ServiceResult<DatabaseBackupSetReadModel>> GetBackupSetAsync(GetDatabaseBackupSetQuery query, CancellationToken cancellationToken = default);
    ValueTask<ServiceResult<DatabaseRestorePointReadModel[]>> ListRestorePointsAsync(ListDatabaseRestorePointsQuery query, CancellationToken cancellationToken = default);
    ValueTask<ServiceResult<DatabaseRestorePointReadModel>> GetRestorePointAsync(GetDatabaseRestorePointQuery query, CancellationToken cancellationToken = default);
    ValueTask<ServiceResult<DatabaseRestorePointReadModel>> GetLatestVerifiedBackupAsync(GetLatestVerifiedDatabaseBackupQuery query, CancellationToken cancellationToken = default);
    ValueTask<ServiceResult<DatabaseRestorePointReadModel>> GetLatestRestoreTestedBackupAsync(GetLatestRestoreTestedDatabaseBackupQuery query, CancellationToken cancellationToken = default);
    ValueTask<ServiceResult<DatabaseProtectionSetReadModel[]>> GetRecoveryObjectiveComplianceAsync(GetDatabaseRecoveryObjectiveComplianceQuery query, CancellationToken cancellationToken = default);
    ValueTask<ServiceResult<DatabaseRestoreOperationReadModel>> GetRestoreOperationAsync(GetDatabaseRestoreOperationQuery query, CancellationToken cancellationToken = default);
    ValueTask<ServiceResult<DatabaseRestoreOperationReadModel[]>> ListRestoreDrillsAsync(ListDatabaseRestoreDrillsQuery query, CancellationToken cancellationToken = default);
    ValueTask<ServiceResult<DatabaseRetentionReadModel>> GetRetentionForecastAsync(GetDatabaseRetentionForecastQuery query, CancellationToken cancellationToken = default);
    ValueTask<ServiceResult<DatabaseBackupHealthReadModel[]>> GetServiceHealthAsync(GetDatabaseBackupServiceHealthQuery query, CancellationToken cancellationToken = default);
    ValueTask<ServiceResult<DatabaseRecoveryRunStatsReadModel>> GetRecoveryRunStatsAsync(GetDatabaseRecoveryRunStatsQuery query, CancellationToken cancellationToken = default);
}
