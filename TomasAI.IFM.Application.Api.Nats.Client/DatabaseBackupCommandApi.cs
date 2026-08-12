using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Commands;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ServiceApi;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.Api.Nats.Client;

public sealed class DatabaseBackupCommandApi(IActorProducer actorProducer) : IDatabaseBackupCommandApi
{
    readonly IActorProducer _actorProducer = actorProducer ?? throw new ArgumentNullException(nameof(actorProducer));

    public ValueTask<ServiceResult<DatabaseOperationAcceptedResult>> RequestBackupAsync(RequestDatabaseBackupCommand command, CancellationToken cancellationToken = default) => SendAsync(command, RequestDatabaseBackupCommand.ErrorId, cancellationToken);
    public ValueTask<ServiceResult<DatabaseOperationAcceptedResult>> CancelBackupAsync(CancelDatabaseBackupCommand command, CancellationToken cancellationToken = default) => SendAsync(command, CancelDatabaseBackupCommand.ErrorId, cancellationToken);
    public ValueTask<ServiceResult<DatabaseOperationAcceptedResult>> RequestRestoreAsync(RequestDatabaseRestoreCommand command, CancellationToken cancellationToken = default) => SendAsync(command, RequestDatabaseRestoreCommand.ErrorId, cancellationToken);
    public ValueTask<ServiceResult<DatabaseOperationAcceptedResult>> ApproveRestoreAsync(ApproveDatabaseRestoreCommand command, CancellationToken cancellationToken = default) => SendAsync(command, ApproveDatabaseRestoreCommand.ErrorId, cancellationToken);
    public ValueTask<ServiceResult<DatabaseOperationAcceptedResult>> CancelRestoreAsync(CancelDatabaseRestoreCommand command, CancellationToken cancellationToken = default) => SendAsync(command, CancelDatabaseRestoreCommand.ErrorId, cancellationToken);
    public ValueTask<ServiceResult<DatabaseOperationAcceptedResult>> ApproveCutoverAsync(ApproveDatabaseCutoverCommand command, CancellationToken cancellationToken = default) => SendAsync(command, ApproveDatabaseCutoverCommand.ErrorId, cancellationToken);
    public ValueTask<ServiceResult<DatabaseOperationAcceptedResult>> RequestRestoreDrillAsync(RequestDatabaseRestoreDrillCommand command, CancellationToken cancellationToken = default) => SendAsync(command, RequestDatabaseRestoreDrillCommand.ErrorId, cancellationToken);
    public ValueTask<ServiceResult<DatabaseOperationAcceptedResult>> UpdatePolicyAsync(UpdateDatabaseBackupPolicyCommand command, CancellationToken cancellationToken = default) => SendAsync(command, UpdateDatabaseBackupPolicyCommand.ErrorId, cancellationToken);
    public ValueTask<ServiceResult<DatabaseOperationAcceptedResult>> PlaceLegalHoldAsync(PlaceBackupLegalHoldCommand command, CancellationToken cancellationToken = default) => SendAsync(command, PlaceBackupLegalHoldCommand.ErrorId, cancellationToken);
    public ValueTask<ServiceResult<DatabaseOperationAcceptedResult>> ReleaseLegalHoldAsync(ReleaseBackupLegalHoldCommand command, CancellationToken cancellationToken = default) => SendAsync(command, ReleaseBackupLegalHoldCommand.ErrorId, cancellationToken);
    public ValueTask<ServiceResult<DatabaseOperationAcceptedResult>> RequestRetentionEvaluationAsync(RequestBackupRetentionEvaluationCommand command, CancellationToken cancellationToken = default) => SendAsync(command, RequestBackupRetentionEvaluationCommand.ErrorId, cancellationToken);
    public ValueTask<ServiceResult<DatabaseOperationAcceptedResult>> ExecuteRetentionPlanAsync(ExecuteBackupRetentionPlanCommand command, CancellationToken cancellationToken = default) => SendAsync(command, ExecuteBackupRetentionPlanCommand.ErrorId, cancellationToken);

    async ValueTask<ServiceResult<DatabaseOperationAcceptedResult>> SendAsync<TCommand>(TCommand command, int errorCode, CancellationToken cancellationToken)
        where TCommand : DatabaseBackupCommand
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var entityId = command.EntityId.Value == Guid.Empty
                ? new DatabaseRecoveryOperationId(command.Request.RequestId)
                : command.EntityId;
            var normalized = (TCommand)(command with
            {
                CommandId = command.Request.RequestId,
                EntityId = entityId,
                ErrorCode = errorCode,
                Subject = new ActorSubject(ActorType.Command, DatabaseBackupCommand.Actor, command.Verb, entityId.Format())
            });
            normalized.Validate();
            var actorResult = await _actorProducer.RequestAsync<TCommand, DatabaseRecoveryOperationId, GuidResult>(
                normalized.Subject, normalized, entityId, cancellationToken);
            if (!actorResult.Success || actorResult.Value is null)
                return new ServiceFailed<DatabaseOperationAcceptedResult>(actorResult.ErrorCode, actorResult.ErrorMessage);
            return new ServiceOk<DatabaseOperationAcceptedResult>(new DatabaseOperationAcceptedResult
            {
                OperationId = new DatabaseRecoveryOperationId(actorResult.Value.Guid),
                BackupSetId = normalized.BackupSetId,
                Source = normalized.Source,
                PolicyRevision = normalized.ExpectedPolicyRevision,
                InitialPhase = DatabaseRecoveryPhase.Requested
            });
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception exception)
        {
            return new ServiceFailed<DatabaseOperationAcceptedResult>(errorCode, exception.Message);
        }
    }
}
