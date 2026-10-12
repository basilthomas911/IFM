using MessagePack;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

/// <summary>Resolved debit/credit facts in book currency with exact account version and source identity.</summary>
[MessagePackObject]
public sealed record AcceptedLedgerLine([property: Key(0)] int Ordinal,
    [property: Key(1)] LedgerAccountBinding Account, [property: Key(2)] int? FundId,
    [property: Key(3)] decimal Debit, [property: Key(4)] decimal Credit,
    [property: Key(5)] int? OrderId, [property: Key(6)] int? TradeId,
    [property: Key(7)] string SourceLineReference);
