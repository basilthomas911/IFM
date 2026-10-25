using MessagePack;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;
[MessagePackObject] public sealed record FinancialConfiguredAccount([property: Key(0)] LedgerAccountDefinition Definition, [property: Key(1)] string State);
