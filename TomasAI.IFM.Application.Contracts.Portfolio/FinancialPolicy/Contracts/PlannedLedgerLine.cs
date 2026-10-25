using MessagePack;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

public sealed record PlannedLedgerLine(int Ordinal, LedgerAccountBinding Account, int? FundId,
    decimal Debit, decimal Credit, int? OrderId, int? TradeId, string SourceLineReference);
