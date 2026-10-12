using TomasAI.IFM.Domain.Supervisor.Shared.Recovery;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.Supervisor.Recovery.Event.Projection;

public sealed record RecoveryCanaryProjection(Guid CorrelationId, Guid GenerationId,
    DateOnly ValueDate, string Dataset, DateTime ProjectedUtc);
