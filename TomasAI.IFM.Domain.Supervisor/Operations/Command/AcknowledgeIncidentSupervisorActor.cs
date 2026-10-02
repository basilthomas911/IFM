using TomasAI.IFM.Domain.Supervisor.Operations.Command.Actor;
using TomasAI.IFM.Domain.Supervisor.Operations.Command.State;
using TomasAI.IFM.Domain.Supervisor.Shared.Operations.Commands;
using TomasAI.IFM.Domain.Supervisor.Shared.Enums;
using TomasAI.IFM.Domain.Supervisor.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Supervisor.Operations.Command;

/// <summary>Handles the concrete AcknowledgeIncident Supervisor operation command.</summary>
public static class AcknowledgeIncidentSupervisorActor
{
    /// <summary>Executes AcknowledgeIncident and applies the resulting private outcome event.</summary>
    public static ValueTask<ServiceResult<GuidResult>> ExecuteAsync(
        this AcknowledgeIncidentSupervisorActorCommand command, SupervisorCommandState state,
        ISupervisorCommandActorContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SupervisorActorOperationResult result;
        if (!context.Authorizer.IsAuthorized(command.Requester,
                SupervisorActorOperationKind.AcknowledgeIncident))
        {
            result = new(SupervisorOperationOutcome.Rejected, command.CommandId, command.Target,
                command.ExpectedGeneration, "Authorization", "The requester is not authorized.");
        }
        else
        {
            var acknowledged = context.Incidents.ActiveIncidents
                .Any(incident => incident.ThreadId == command.Target);
            result = new(acknowledged ? SupervisorOperationOutcome.Succeeded : SupervisorOperationOutcome.Rejected,
                command.CommandId, command.Target, command.ExpectedGeneration,
                "IncidentAcknowledgement", acknowledged ? null : "The incident could not be acknowledged.");
        }
        return ValueTask.FromResult(
            SupervisorOperationOutcomeRecorder.Record(command, state, result));
    }
}
