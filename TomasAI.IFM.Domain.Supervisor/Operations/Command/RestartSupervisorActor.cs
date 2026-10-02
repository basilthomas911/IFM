using TomasAI.IFM.Domain.Supervisor.Operations.Command.Actor;
using TomasAI.IFM.Domain.Supervisor.Operations.Command.State;
using TomasAI.IFM.Domain.Supervisor.Shared.Operations.Commands;
using TomasAI.IFM.Domain.Supervisor.Shared.Enums;
using TomasAI.IFM.Domain.Supervisor.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Supervisor.Operations.Command;

/// <summary>Handles the concrete Restart Supervisor operation command.</summary>
public static class RestartSupervisorActor
{
    /// <summary>Executes Restart and applies the resulting private outcome event.</summary>
    public static async ValueTask<ServiceResult<GuidResult>> ExecuteAsync(
        this RestartSupervisorActorCommand command, SupervisorCommandState state,
        ISupervisorCommandActorContext context, CancellationToken cancellationToken)
    {
        SupervisorActorOperationResult result;
        if (!context.Authorizer.IsAuthorized(command.Requester, SupervisorActorOperationKind.Restart))
        {
            result = new(SupervisorOperationOutcome.Rejected, command.CommandId, command.Target,
                command.ExpectedGeneration, "Authorization", "The requester is not authorized.");
        }
        else
        {
            var request = new SupervisorActorOperationRequest(command.CommandId, command.Target,
                command.ExpectedGeneration, SupervisorActorOperationKind.Restart, command.Requester,
                command.Reason, TimeSpan.FromTicks(command.TimeoutTicks));
            result = await context.ManagedActors.RestartAsync(request, cancellationToken)
                .ConfigureAwait(false);
        }
        return SupervisorOperationOutcomeRecorder.Record(command, state, result);
    }
}
