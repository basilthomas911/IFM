using MessagePack;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

/// <summary>An empty account reads configured choices; selecting one prepares generated identities without creating a book.</summary>
[MessagePackObject]
public sealed record PrepareFinancialBookRequest([property:Key(0)] string? ExecutionAccountReference=null,
    [property:Key(1)] DateOnly PeriodStart=default,[property:Key(2)] DateOnly PeriodEnd=default);

[MessagePackObject]
public sealed record FinancialBookSetup([property:Key(0)] string[] ExecutionAccounts,
    [property:Key(1)] string[] FundNames,[property:Key(2)] LedgerConfigurationRequest? Draft);
