using TomasAI.IFM.Domain.Supervisor.Shared.Enums;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.Supervisor.Shared.Health.ReadModels;

/// <summary>Immutable audit result for one automatic Supervisor lifecycle action.</summary>
public sealed record SupervisorHealthOperation(
    Guid OperationId,
    ActorThreadId ThreadId,
    long ExpectedGeneration,
    DateTime StartedUtc,
    DateTime? CompletedUtc,
    SupervisorOperationOutcome? Outcome,
    string Reason);
