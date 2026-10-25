using TomasAI.IFM.Domain.Supervisor.Shared.Enums;
using TomasAI.IFM.Domain.Supervisor.Shared.Health.ReadModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Supervisor.Shared.ServiceApi;

/// <summary>Nonthrowing result returned by managed actor shutdown.</summary>
public sealed record SupervisorActorsShutdownResult(
    SupervisorOperationOutcome Outcome,
    Guid OperationId,
    int ExpectedActors,
    int StoppedActors,
    string Stage,
    string? FailureReason)
{
    /// <summary>Gets whether shutdown completed successfully.</summary>
    public bool Succeeded => Outcome == SupervisorOperationOutcome.Succeeded;
}
