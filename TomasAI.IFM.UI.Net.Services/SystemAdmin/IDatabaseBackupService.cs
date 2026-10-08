using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.UI.Net.Models.SystemAdmin;
using TomasAI.IFM.UI.Net.Services.Operations;
using TomasAI.IFM.UI.Net.Services.Subscriptions;

namespace TomasAI.IFM.UI.Net.Services.SystemAdmin;

/// <summary>Defines database-backup operations required by presentation workflows.</summary>
public interface IDatabaseBackupService
{
    /// <summary>Loads bounded dashboard state for the selected backup source.</summary>
    /// <param name="source">The backup source to query.</param>
    /// <param name="selectedProtectionSet">The optional protection set used for restore-point queries.</param>
    /// <param name="cancellationToken">Cancels the query operation.</param>
    ValueTask<UiOperationResult<DatabaseBackupDashboardUiModel>> LoadAsync(
        BackupSource source,
        string? selectedProtectionSet,
        CancellationToken cancellationToken = default);

    /// <summary>Requests a coordinated backup and returns after actor acceptance.</summary>
    /// <param name="source">The selected backup source.</param>
    /// <param name="protectionSet">The protection set to back up.</param>
    /// <param name="expectedPolicyRevision">The optimistic policy revision.</param>
    /// <param name="requestedMode">The requested backup mode.</param>
    /// <param name="cancellationToken">Cancels command submission.</param>
    ValueTask<UiOperationResult<DatabaseBackupAcceptedUiModel>> RequestBackupAsync(
        BackupSource source,
        string protectionSet,
        long expectedPolicyRevision,
        DatabaseBackupMode requestedMode = DatabaseBackupMode.Full,
        CancellationToken cancellationToken = default);

    /// <summary>Loads safe host-owned setup references.</summary>
    ValueTask<UiOperationResult<DatabaseBackupSetupUiModel>> LoadSetupAsync(BackupSource source, CancellationToken cancellationToken = default);
    /// <summary>Reads the next bounded operation-history page for one source.</summary>
    ValueTask<UiOperationResult<DatabaseBackupHistoryUiModel>> LoadHistoryAsync(BackupSource source, string continuation, CancellationToken cancellationToken = default);
    /// <summary>Reads persisted phase observations and retained output for one operation.</summary>
    ValueTask<UiOperationResult<DatabaseBackupLogUiModel>> LoadLogAsync(BackupSource source, Guid operationId, long offset, long afterRevision, CancellationToken cancellationToken = default);
    /// <summary>Loads the named policy's accepted/applied settings.</summary>
    ValueTask<UiOperationResult<DatabaseBackupPolicyUiModel>> LoadPolicyAsync(string policyId, CancellationToken cancellationToken = default);
    /// <summary>Submits a revision-matched policy update.</summary>
    ValueTask<UiOperationResult<DatabaseBackupAcceptedUiModel>> SavePolicyAsync(DatabaseBackupPolicyUiModel policy, CancellationToken cancellationToken = default);
    /// <summary>Loads source host readiness observations.</summary>
    ValueTask<UiOperationResult<DatabaseBackupHealthUiModel[]>> LoadHealthAsync(BackupSource source, CancellationToken cancellationToken = default);
    /// <summary>Requests a fresh-target restore drill with a configured validation profile.</summary>
    ValueTask<UiOperationResult<DatabaseBackupAcceptedUiModel>> RequestRestoreDrillAsync(BackupSource source, string protectionSet, string restorePoint, string targetProfile, string validationProfile, long policyRevision, CancellationToken cancellationToken = default);
    /// <summary>Cancels a selected backup through the owning command actor.</summary>
    ValueTask<UiOperationResult<DatabaseBackupAcceptedUiModel>> CancelBackupAsync(DatabaseBackupOperationUiModel operation, CancellationToken cancellationToken = default);

    /// <summary>Creates an independently owned backup-notification subscription.</summary>
    /// <param name="handler">Receives mapped refresh notifications.</param>
    IUiEventSubscription CreateNotificationSubscription(
        Func<DatabaseBackupNotificationUiModel, ValueTask> handler);
}
