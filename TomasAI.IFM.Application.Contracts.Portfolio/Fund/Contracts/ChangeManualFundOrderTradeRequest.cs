using MessagePack;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Contracts;

/// <summary>Replaces the editable terms of one versioned manual trade draft.</summary>
[MessagePackObject]
public sealed record ChangeManualFundOrderTradeRequest
{
    [Key(0)] public int PortfolioId { get; init; }
    [Key(1)] public int FundId { get; init; }
    [Key(2)] public int OrderId { get; init; }
    [Key(3)] public long ExpectedOrderVersion { get; init; }
    [Key(4)] public int TradeId { get; init; }
    [Key(5)] public long ExpectedTradeVersion { get; init; }
    [Key(6)] public string TradeType { get; init; } = string.Empty;
    [Key(7)] public DateOnly EffectiveDate { get; init; }
    [Key(8)] public string Reference { get; init; } = string.Empty;
    [Key(9)] public string BaseContractId { get; init; } = string.Empty;
    [Key(10)] public string BaseContractSymbol { get; init; } = string.Empty;
}
