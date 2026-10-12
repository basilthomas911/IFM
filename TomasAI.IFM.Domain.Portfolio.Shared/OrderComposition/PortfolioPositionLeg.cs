using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;

namespace TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;

[MessagePackObject]
public sealed record PortfolioPositionLeg
{
    [Key(0)] public Guid TradeLegId { get; init; }
    [Key(1)] public int SignedQuantity { get; init; }
    [Key(2)] public string ContractId { get; init; } = string.Empty;
    [Key(3)] public PortfolioExecutionAssetFamily AssetFamily { get; init; }
}
