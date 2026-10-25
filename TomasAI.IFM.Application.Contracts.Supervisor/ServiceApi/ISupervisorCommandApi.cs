using TomasAI.IFM.Domain.Supervisor.Shared.Enums;
using TomasAI.IFM.Domain.Supervisor.Shared.Health.ReadModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Supervisor.Shared.ServiceApi;

/// <summary>Sends authorized Supervisor lifecycle commands through the actor messaging transport.</summary>
public interface ISupervisorCommandApi
{
    /// <summary>Executes one audited, generation-fenced operation for an actor entity mailbox.</summary>
    ValueTask<ServiceResult<GuidResult>> ExecuteActorOperationAsync(
        ActorThreadId target,
        long expectedGeneration,
        SupervisorActorOperationKind operation,
        string requester,
        string reason,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}
