using MessagePack;
using TomasAI.IFM.Domain.Portfolio.Shared.Common;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Preparation;

[MessagePackObject]
public sealed partial record FinancialAuthorityPreparationCompletedEvent : PortfolioPreparationComplete<ActorEntityId>
{
    [Key(8)] public FinancialRead<FinancialAuthorityDraft> Value { get; init; } = default!;
    [Key(9)] public string InputHash { get; init; } = string.Empty;
}

[MessagePackObject]
public sealed partial record FinancialAuthorityPreparationFailedEvent : PortfolioPreparationFail<ActorEntityId>;
