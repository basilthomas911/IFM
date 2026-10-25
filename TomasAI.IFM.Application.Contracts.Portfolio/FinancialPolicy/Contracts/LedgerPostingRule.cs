using MessagePack;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

/// <summary>Configured account bindings; ordinary transaction callers cannot choose alternative cash accounts.</summary>
[MessagePackObject]
public sealed record LedgerPostingRule([property: Key(0)] Guid RuleId, [property: Key(1)] int Version,
    [property: Key(2)] string ContentHash, [property: Key(3)] LedgerTransactionKind Kind,
    [property: Key(4)] LedgerAccountBinding Debit, [property: Key(5)] LedgerAccountBinding Credit,
    [property: Key(6)] bool RequiresConfirmedMovement, [property: Key(7)] LedgerAccountBinding? ValuationAsset = null,
    [property: Key(8)] LedgerAccountBinding? UnrealizedPnl = null);
