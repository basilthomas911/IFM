using TomasAI.IFM.Domain.Supervisor.Shared.Enums;
using TomasAI.IFM.Domain.Supervisor.Shared.Health.ReadModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Supervisor.Shared.ServiceApi;

/// <summary>Nonthrowing result returned by managed actor startup.</summary>
public sealed record SupervisorActorsStartupResult(
    SupervisorOperationOutcome Outcome,
    Guid OperationId,
    int ExpectedActors,
    int StartedActors,
    string Stage,
    string? FailureReason)
{
    /// <summary>Gets whether startup completed successfully.</summary>
    public bool Succeeded => Outcome == SupervisorOperationOutcome.Succeeded;
}
