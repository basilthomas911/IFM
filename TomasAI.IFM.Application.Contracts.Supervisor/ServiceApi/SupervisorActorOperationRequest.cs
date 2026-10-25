using TomasAI.IFM.Domain.Supervisor.Shared.Enums;
using TomasAI.IFM.Domain.Supervisor.Shared.Health.ReadModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Supervisor.Shared.ServiceApi;

/// <summary>Requests one validated, generation-fenced actor lifecycle operation.</summary>
public sealed record SupervisorActorOperationRequest(
    Guid OperationId,
    ActorThreadId Target,
    long ExpectedGeneration,
    SupervisorActorOperationKind Operation,
    string Requester,
    string Reason,
    TimeSpan Timeout);
