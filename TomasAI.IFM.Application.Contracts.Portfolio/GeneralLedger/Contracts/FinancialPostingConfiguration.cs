using MessagePack;
namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public financial model contract.")]
[MessagePackObject]
public sealed record FinancialPostingConfiguration([property: Key(0)] int BookId, [property: Key(1)] int FundId,
    [property: Key(2)] FinancialAuthorityReference Authority, [property: Key(3)] LedgerPostingRule[] Rules,
    [property: Key(4)] bool PeriodOpen, [property: Key(5)] DateOnly AccountingDate,
    [property: Key(6)] bool AllowDevelopmentOpeningCapital = false);
