using TomasAI.IFM.Domain.Supervisor.Shared.Enums;
using TomasAI.IFM.Domain.Supervisor.Shared.Health.ReadModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Supervisor.Shared.ServiceApi;

/// <summary>Aggregate privileged actor/projector reconciliation evidence.</summary>
public sealed record SupervisorRecoveryResult(
    Guid OperationId, bool Qualified, int Healthy, int Degraded, int Critical, int Unknown,
    IReadOnlyList<SupervisorRecoveryComponentResult> Components, string Detail);
