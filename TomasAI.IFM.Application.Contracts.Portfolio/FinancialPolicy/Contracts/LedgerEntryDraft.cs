using MessagePack;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

/// <summary>Versioned financial contract: LedgerEntryDraft; numeric keys are append-only.</summary>
[MessagePackObject]
public sealed record LedgerEntryDraft
{
    [Key(0)] public int Ordinal { get; init; }
    [Key(1)] public int AccountId { get; init; }
    [Key(2)] public int? FundId { get; init; }
    [Key(3)] public PostingSide PostingSide { get; init; }
    [Key(4)] public decimal Amount { get; init; }
    [Key(5)] public string Currency { get; init; } = string.Empty;
    [Key(6)] public int? OrderId { get; init; }
    [Key(7)] public int? TradeId { get; init; }
    [Key(8)] public string SourceLineReference { get; init; } = string.Empty;
}
