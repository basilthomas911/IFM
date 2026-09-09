using MessagePack;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

[MessagePackObject]
public sealed record PrepareFinancialAuthorityRequest([property:Key(0)] bool PermitNewSpending=false);
[MessagePackObject]
public sealed record FinancialAuthorityDraft([property:Key(0)] LedgerConfigurationRequest Draft,[property:Key(1)] string[] Notes);
