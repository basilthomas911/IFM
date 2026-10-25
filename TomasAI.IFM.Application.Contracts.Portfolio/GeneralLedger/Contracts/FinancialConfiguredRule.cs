using MessagePack;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;
[MessagePackObject]
public sealed record FinancialConfiguredRule([property: Key(0)] LedgerPostingRule Definition, [property: Key(1)] string State,
    [property: Key(2)] DateOnly EffectiveFrom, [property: Key(3)] DateOnly? EffectiveTo);
