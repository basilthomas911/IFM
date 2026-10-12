using TomasAI.IFM.Application.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Commands;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Api.DatabaseBackup.Host.Services;

/// <summary>Requests automatic retention through System Admin so source events exist before host execution.</summary>
public sealed class AwsAutomaticRetentionCommandSender(IActorProducer producer)
{
    public async ValueTask SendAsync(DatabaseExecutionIntent intent, CancellationToken token)
    {
        var execution = intent.ExecutionEvent;
        var command = new RequestBackupRetentionEvaluationCommand
        {
            Subject = new ActorSubject(ActorType.Command, DatabaseBackupCommandRoute.Actor,
                RequestBackupRetentionEvaluationCommand.VerbValue, intent.OperationId.Format()),
            CommandId = execution.CommandId, EntityId = intent.OperationId, Request = execution.Request!,
            Source = execution.Source.Source, ProtectionSetId = execution.Source.ProtectionSetId,
            ExpectedPolicyRevision = execution.Source.PolicyRevision, RetentionPlanId = execution.RetentionPlanId,
            RetentionPlanRevision = execution.RetentionPlanRevision, EvaluationBoundaryUtc = execution.EvaluationBoundaryUtc,
            RequiredDestinations = execution.RequiredDestinations
        };
        command.Validate();
        await producer.StartAsync(new ActorMailboxId(ActorType.Command, DatabaseBackupCommandRoute.Actor), token).ConfigureAwait(false);
        var result = await producer.RequestAsync<RequestBackupRetentionEvaluationCommand,
            Domain.SystemAdmin.Shared.DatabaseBackup.Contracts.DatabaseRecoveryOperationId, GuidResult>(
            command.Subject, command, command.EntityId, token).ConfigureAwait(false);
        if (!result.Success) throw new InvalidOperationException($"Automatic retention command rejected: {result.ErrorMessage}");
    }
}