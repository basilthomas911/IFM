using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Commands;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ServiceApi;

public interface IDatabaseBackupCommandApi
{
    ValueTask<ServiceResult<DatabaseOperationAcceptedResult>> RequestBackupAsync(RequestDatabaseBackupCommand command, CancellationToken cancellationToken = default);
    ValueTask<ServiceResult<DatabaseOperationAcceptedResult>> CancelBackupAsync(CancelDatabaseBackupCommand command, CancellationToken cancellationToken = default);
    ValueTask<ServiceResult<DatabaseOperationAcceptedResult>> RequestRestoreAsync(RequestDatabaseRestoreCommand command, CancellationToken cancellationToken = default);
    ValueTask<ServiceResult<DatabaseOperationAcceptedResult>> ApproveRestoreAsync(ApproveDatabaseRestoreCommand command, CancellationToken cancellationToken = default);
    ValueTask<ServiceResult<DatabaseOperationAcceptedResult>> CancelRestoreAsync(CancelDatabaseRestoreCommand command, CancellationToken cancellationToken = default);
    ValueTask<ServiceResult<DatabaseOperationAcceptedResult>> ApproveCutoverAsync(ApproveDatabaseCutoverCommand command, CancellationToken cancellationToken = default);
    ValueTask<ServiceResult<DatabaseOperationAcceptedResult>> RequestRestoreDrillAsync(RequestDatabaseRestoreDrillCommand command, CancellationToken cancellationToken = default);
    ValueTask<ServiceResult<DatabaseOperationAcceptedResult>> UpdatePolicyAsync(UpdateDatabaseBackupPolicyCommand command, CancellationToken cancellationToken = default);
    ValueTask<ServiceResult<DatabaseOperationAcceptedResult>> PlaceLegalHoldAsync(PlaceBackupLegalHoldCommand command, CancellationToken cancellationToken = default);
    ValueTask<ServiceResult<DatabaseOperationAcceptedResult>> ReleaseLegalHoldAsync(ReleaseBackupLegalHoldCommand command, CancellationToken cancellationToken = default);
    ValueTask<ServiceResult<DatabaseOperationAcceptedResult>> RequestRetentionEvaluationAsync(RequestBackupRetentionEvaluationCommand command, CancellationToken cancellationToken = default);
    ValueTask<ServiceResult<DatabaseOperationAcceptedResult>> ExecuteRetentionPlanAsync(ExecuteBackupRetentionPlanCommand command, CancellationToken cancellationToken = default);
}
