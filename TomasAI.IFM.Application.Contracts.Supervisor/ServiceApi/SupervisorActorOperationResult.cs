using TomasAI.IFM.Domain.Supervisor.Shared.Enums;
using TomasAI.IFM.Domain.Supervisor.Shared.Health.ReadModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Supervisor.Shared.ServiceApi;

/// <summary>Nonthrowing result returned by an individual actor operation.</summary>
public sealed record SupervisorActorOperationResult(
    SupervisorOperationOutcome Outcome,
    Guid OperationId,
    ActorThreadId Target,
    long ExpectedGeneration,
    string Stage,
    string? FailureReason)
{
    /// <summary>Gets whether the requested operation completed successfully.</summary>
    public bool Succeeded => Outcome == SupervisorOperationOutcome.Succeeded;
}
