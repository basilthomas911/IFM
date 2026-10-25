using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;

namespace TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;

[MessagePackObject]
public sealed record PortfolioExecutionLeg
{
    [Key(0)] public Guid TradeLegId { get; init; }
    [Key(1)] public PortfolioExecutionAssetFamily AssetFamily { get; init; }
    [Key(2)] public int SignedQuantity { get; init; }
    [Key(3)] public decimal? LimitPrice { get; init; }
    [Key(4)] public string ContractKey { get; init; } = string.Empty;
    [Key(5)] public DateOnly? Expiry { get; init; }
    [Key(6)] public decimal? Strike { get; init; }
    [Key(7)] public byte? PutCall { get; init; }
    [Key(8)] public string ContractId { get; init; } = string.Empty;
    [Key(9)] public decimal CashMultiplier { get; init; }
}
