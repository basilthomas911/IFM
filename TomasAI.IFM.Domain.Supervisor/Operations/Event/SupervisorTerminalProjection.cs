using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Supervisor.Operations.Event.Actor;
using TomasAI.IFM.Domain.Supervisor.Shared.Enums;
using TomasAI.IFM.Domain.Supervisor.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.Supervisor.Operations.Event;

/// <summary>Applies one typed terminal event to the bounded Supervisor operation read model.</summary>
internal static class SupervisorTerminalProjection
{
    /// <summary>Records a successful operation only after its public Complete event is consumed.</summary>
    internal static ValueTask Complete(ISupervisorEventActorContext context, Guid commandId,
        ActorThreadId target, long expectedGeneration, SupervisorActorOperationKind operation,
        string requester, string reason, long timeoutTicks, string stage)
    {
        var request = new SupervisorActorOperationRequest(commandId, target, expectedGeneration,
            operation, requester, reason, TimeSpan.FromTicks(timeoutTicks));
        var result = new SupervisorActorOperationResult(SupervisorOperationOutcome.Succeeded,
            commandId, target, expectedGeneration, stage, null);
        context.Operations.Record(request, result);
        return ValueTask.CompletedTask;
    }

    /// <summary>Records and logs one known operation failure without lifecycle mutation authority.</summary>
    internal static ValueTask Fail(ISupervisorEventActorContext context,
        ILogger<SupervisorEventActor> logger, Guid commandId, ActorThreadId target,
        long expectedGeneration, SupervisorActorOperationKind operation, string requester,
        string reason, long timeoutTicks, SupervisorOperationOutcome outcome,
        string stage, string failureReason)
    {
        var request = new SupervisorActorOperationRequest(commandId, target, expectedGeneration,
            operation, requester, reason, TimeSpan.FromTicks(timeoutTicks));
        var result = new SupervisorActorOperationResult(outcome, commandId, target,
            expectedGeneration, stage, failureReason);
        context.Operations.Record(request, result);
        logger.LogError(
            "Supervisor operation {Operation} failed for {Target} at {Stage}: {FailureReason}",
            operation, target, stage, failureReason);
        return ValueTask.CompletedTask;
    }
}
