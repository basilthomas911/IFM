using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Preparation;

[MessagePackObject]
public sealed partial record PortfolioReferenceValidationCompletedEvent : PortfolioPreparationComplete<ActorEntityId>
{
    [Key(8)] public string InputHash { get; init; } = string.Empty;
}

[MessagePackObject]
public sealed partial record PortfolioReferenceValidationFailedEvent : PortfolioPreparationFail<ActorEntityId>;
