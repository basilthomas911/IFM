using MessagePack;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

[MessagePackObject] public sealed record GetFinancialLedgerConfigurationRequest;
[MessagePackObject] public sealed record FinancialLedgerPeriod([property:Key(0)] Guid PeriodId,
    [property:Key(1)] DateOnly StartDate,[property:Key(2)] DateOnly EndDate,[property:Key(3)] long Version,[property:Key(4)] string State);
[MessagePackObject] public sealed record FinancialConfiguredAccount([property:Key(0)] LedgerAccountDefinition Definition,[property:Key(1)] string State);
[MessagePackObject] public sealed record FinancialConfiguredRule([property:Key(0)] LedgerPostingRule Definition,[property:Key(1)] string State,
    [property:Key(2)] DateOnly EffectiveFrom,[property:Key(3)] DateOnly? EffectiveTo);
/// <summary>Bounded current ledger controls at one authoritative financial revision, for configured selectors.</summary>
[MessagePackObject] public sealed record FinancialLedgerConfiguration([property:Key(0)] int BookId,
    [property:Key(1)] string Currency,[property:Key(2)] string Environment,[property:Key(3)] string OperatingState,
    [property:Key(4)] string SourceWatermark,[property:Key(5)] FinancialLedgerPeriod[] Periods,
    [property:Key(6)] FinancialConfiguredAccount[] Accounts,[property:Key(7)] FinancialConfiguredRule[] Rules,
    [property:Key(8)] LedgerReconciliationResult? LatestReconciliation,
    [property:Key(9)] FinancialBookConfiguration? DevelopmentQualificationBook=null);
