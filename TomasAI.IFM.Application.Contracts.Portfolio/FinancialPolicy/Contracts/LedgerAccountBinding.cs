using MessagePack;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

/// <summary>Exact account version selected by an immutable posting rule.</summary>
[MessagePackObject]
public sealed record LedgerAccountBinding([property: Key(0)] int AccountId, [property: Key(1)] long Version);
