using MessagePack;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

[MessagePackObject]
public sealed record PrepareFinancialAuthorityRequest([property: Key(0)] bool PermitNewSpending = false);
