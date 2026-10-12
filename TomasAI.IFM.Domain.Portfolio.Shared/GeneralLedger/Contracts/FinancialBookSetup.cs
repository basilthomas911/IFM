using MessagePack;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public financial model contract.")]
[MessagePackObject]
public sealed record FinancialBookSetup([property: Key(0)] string[] ExecutionAccounts,
    [property: Key(1)] string[] FundNames, [property: Key(2)] LedgerConfigurationRequest? Draft);
