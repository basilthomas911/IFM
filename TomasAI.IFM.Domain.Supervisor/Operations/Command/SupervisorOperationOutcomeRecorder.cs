using TomasAI.IFM.Domain.Supervisor.Operations.Command.Model;
using TomasAI.IFM.Domain.Supervisor.Operations.Command.State;
using TomasAI.IFM.Domain.Supervisor.Shared.Operations.Commands;
using TomasAI.IFM.Domain.Supervisor.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Supervisor.Operations.Command;

/// <summary>Applies a completed command decision without selecting or executing an operation.</summary>
internal static class SupervisorOperationOutcomeRecorder
{
    /// <summary>Applies the private outcome event and returns the command acknowledgement.</summary>
    internal static ServiceResult<GuidResult> Record(
        ISupervisorOperationCommand command, SupervisorCommandState state,
        SupervisorActorOperationResult result)
    {
        var outcomeEvent = SupervisorOperationOutcomeModel.Create(
            command, result, Guid.NewGuid(), DateTime.UtcNow);
        if (!state.Update(outcomeEvent, command))
            return new ServiceFailed<GuidResult>(command.ErrorCode,
                "The Supervisor operation outcome could not be applied to state.");
        return result.Succeeded
            ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId))
            : new ServiceFailed<GuidResult>(command.ErrorCode, result.FailureReason ?? result.Stage);
    }
}
