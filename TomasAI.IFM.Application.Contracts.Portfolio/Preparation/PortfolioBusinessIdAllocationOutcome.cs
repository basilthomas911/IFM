using TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi;
using MessagePack;
using TomasAI.IFM.Domain.Portfolio.Shared.Common;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Preparation;

[MessagePackObject]
public sealed partial record PortfolioBusinessIdAllocationCompletedEvent : PortfolioPreparationComplete<ActorEntityId>
{
    [Key(8)] public PortfolioBusinessIdAllocation Value { get; init; } = default!;
    [Key(9)] public string InputHash { get; init; } = string.Empty;
}

[MessagePackObject]
public sealed partial record PortfolioBusinessIdAllocationFailedEvent : PortfolioPreparationFail<ActorEntityId>;
