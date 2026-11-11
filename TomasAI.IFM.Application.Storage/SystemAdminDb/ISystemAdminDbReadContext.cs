using TomasAI.IFM.Shared.EventSourcing.ViewModels;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Queries;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ReadModels;

namespace TomasAI.IFM.Application.Storage;
/// <summary>Defines SystemAdmin projection queries.</summary>
public interface ISystemAdminDbReadContext
{
    /// <summary>Reads persisted GetDatabaseBackupProjectionCheckpointAsync results.</summary>
    /// <param name="projectorName">The stable projector identity supplied by the owning domain.</param>
    /// <param name="cancellationToken">Cancels the database operation without accepting a partial result.</param>
    /// <returns>The persisted result; null when no row matches.</returns>
    ValueTask<DatabaseBackupProjectionCheckpointReadModel?> GetDatabaseBackupProjectionCheckpointAsync(string projectorName, CancellationToken cancellationToken = default);
    /// <summary>Reads phase observations after the specified persisted revision.</summary>
    /// <param name="query">The query already validated by its domain query handler.</param>
    /// <param name="cancellationToken">Cancels the database operation without accepting a partial result.</param>
    /// <returns>The persisted rows or idempotent projection outcome.</returns>
    ValueTask<DatabaseBackupPhaseReadModel[]> GetBackupPhasesAsync(GetDatabaseBackupLogQuery query, CancellationToken cancellationToken) => throw new NotSupportedException("Backup phase paging is unavailable.");
    /// <summary>Reads persisted GetProtectionSetsAsync results.</summary>
    /// <param name="query">The query already validated by its domain query handler.</param>
    /// <param name="cancellationToken">Cancels the database operation without accepting a partial result.</param>
    /// <returns>The persisted rows or idempotent projection outcome.</returns>
    ValueTask<DatabaseProtectionSetReadModel[]> GetProtectionSetsAsync(GetDatabaseProtectionSetsQuery query, CancellationToken cancellationToken);
    /// <summary>Reads persisted GetPolicyAsync results.</summary>
    /// <param name="query">The query already validated by its domain query handler.</param>
    /// <param name="cancellationToken">Cancels the database operation without accepting a partial result.</param>
    /// <returns>The persisted result; null when no row matches.</returns>
    ValueTask<DatabaseBackupPolicyReadModel?> GetPolicyAsync(GetDatabaseBackupPolicyQuery query, CancellationToken cancellationToken);
    /// <summary>Reads persisted GetBackupOperationAsync results.</summary>
    /// <param name="query">The query already validated by its domain query handler.</param>
    /// <param name="cancellationToken">Cancels the database operation without accepting a partial result.</param>
    /// <returns>The persisted result; null when no row matches.</returns>
    ValueTask<DatabaseBackupOperationReadModel?> GetBackupOperationAsync(GetDatabaseBackupOperationQuery query, CancellationToken cancellationToken);
    /// <summary>Reads persisted ListBackupOperationsAsync results.</summary>
    /// <param name="query">The query already validated by its domain query handler.</param>
    /// <param name="cancellationToken">Cancels the database operation without accepting a partial result.</param>
    /// <returns>The persisted rows or idempotent projection outcome.</returns>
    ValueTask<DatabaseBackupOperationReadModel[]> ListBackupOperationsAsync(ListDatabaseBackupOperationsQuery query, CancellationToken cancellationToken);
    /// <summary>Reads all persisted operation facts for one backup set.</summary>
    /// <param name="backupSetId">The accepted backup set identity.</param>
    /// <param name="cancellationToken">Cancels the database operation without accepting a partial result.</param>
    /// <returns>The persisted rows or idempotent projection outcome.</returns>
    ValueTask<DatabaseBackupOperationReadModel[]> GetBackupSetOperationsAsync(Guid backupSetId, CancellationToken cancellationToken);
    /// <summary>Reads persisted ListRestorePointsAsync results.</summary>
    /// <param name="query">The query already validated by its domain query handler.</param>
    /// <param name="cancellationToken">Cancels the database operation without accepting a partial result.</param>
    /// <returns>The persisted rows or idempotent projection outcome.</returns>
    ValueTask<DatabaseRestorePointReadModel[]> ListRestorePointsAsync(ListDatabaseRestorePointsQuery query, CancellationToken cancellationToken);
    /// <summary>Reads persisted GetRestorePointAsync results.</summary>
    /// <param name="query">The query already validated by its domain query handler.</param>
    /// <param name="cancellationToken">Cancels the database operation without accepting a partial result.</param>
    /// <returns>The persisted result; null when no row matches.</returns>
    ValueTask<DatabaseRestorePointReadModel?> GetRestorePointAsync(GetDatabaseRestorePointQuery query, CancellationToken cancellationToken);
    /// <summary>Reads persisted GetLatestVerifiedBackupAsync results.</summary>
    /// <param name="query">The query already validated by its domain query handler.</param>
    /// <param name="cancellationToken">Cancels the database operation without accepting a partial result.</param>
    /// <returns>The persisted result; null when no row matches.</returns>
    ValueTask<DatabaseRestorePointReadModel?> GetLatestVerifiedBackupAsync(GetLatestVerifiedDatabaseBackupQuery query, CancellationToken cancellationToken);
    /// <summary>Reads persisted GetLatestRestoreTestedBackupAsync results.</summary>
    /// <param name="query">The query already validated by its domain query handler.</param>
    /// <param name="cancellationToken">Cancels the database operation without accepting a partial result.</param>
    /// <returns>The persisted result; null when no row matches.</returns>
    ValueTask<DatabaseRestorePointReadModel?> GetLatestRestoreTestedBackupAsync(GetLatestRestoreTestedDatabaseBackupQuery query, CancellationToken cancellationToken);
    /// <summary>Reads persisted GetRestoreOperationAsync results.</summary>
    /// <param name="query">The query already validated by its domain query handler.</param>
    /// <param name="cancellationToken">Cancels the database operation without accepting a partial result.</param>
    /// <returns>The persisted result; null when no row matches.</returns>
    ValueTask<DatabaseRestoreOperationReadModel?> GetRestoreOperationAsync(GetDatabaseRestoreOperationQuery query, CancellationToken cancellationToken);
    /// <summary>Reads persisted ListRestoreDrillsAsync results.</summary>
    /// <param name="query">The query already validated by its domain query handler.</param>
    /// <param name="cancellationToken">Cancels the database operation without accepting a partial result.</param>
    /// <returns>The persisted rows or idempotent projection outcome.</returns>
    ValueTask<DatabaseRestoreOperationReadModel[]> ListRestoreDrillsAsync(ListDatabaseRestoreDrillsQuery query, CancellationToken cancellationToken);
    /// <summary>Reads persisted GetRetentionForecastAsync results.</summary>
    /// <param name="query">The query already validated by its domain query handler.</param>
    /// <param name="cancellationToken">Cancels the database operation without accepting a partial result.</param>
    /// <returns>The persisted result; null when no row matches.</returns>
    ValueTask<DatabaseRetentionReadModel?> GetRetentionForecastAsync(GetDatabaseRetentionForecastQuery query, CancellationToken cancellationToken);
    /// <summary>Reads persisted GetServiceHealthAsync results.</summary>
    /// <param name="query">The query already validated by its domain query handler.</param>
    /// <param name="cancellationToken">Cancels the database operation without accepting a partial result.</param>
    /// <returns>The persisted rows or idempotent projection outcome.</returns>
    ValueTask<DatabaseBackupHealthReadModel[]> GetServiceHealthAsync(GetDatabaseBackupServiceHealthQuery query, CancellationToken cancellationToken);
    /// <summary>Reads persisted run statistics observations for one operation.</summary>
    /// <param name="operationId">The accepted recovery operation identity.</param>
    /// <param name="cancellationToken">Cancels the database operation without accepting a partial result.</param>
    /// <returns>The persisted rows or idempotent projection outcome.</returns>
    ValueTask<DatabaseBackupStatisticsObservation[]> GetRecoveryRunStatisticsAsync(Guid operationId, CancellationToken cancellationToken);
    /// <summary>Reads at most 100 pending persisted event records for domain-owned dispatch.</summary>
    /// <param name="cancellationToken">Cancels the database operation without accepting a partial result.</param>
    /// <returns>The persisted rows or idempotent projection outcome.</returns>
    ValueTask<EventStreamReadModel[]> ReadPendingDatabaseBackupExecutionIntentsAsync(CancellationToken cancellationToken = default);
}
