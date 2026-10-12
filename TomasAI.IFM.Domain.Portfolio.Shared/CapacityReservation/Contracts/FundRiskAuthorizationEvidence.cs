using MessagePack;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;
[MessagePackObject]
public sealed record FundRiskAuthorizationEvidence([property: Key(0)] Guid CommandId, [property: Key(1)] Guid EventId,
    [property: Key(2)] FundRiskAuthorizationReference Authorization);
