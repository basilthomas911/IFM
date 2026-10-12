using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;

namespace TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;

[MessagePackObject]
public sealed record PortfolioPositionSnapshot
{
    [Key(0)] public PortfolioPositionReference Id { get; init; }
    [Key(1)] public PortfolioExecutionStrategyKind StrategyKind { get; init; }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public financial model contract.")]
    [Key(2)] public PortfolioPositionLeg[] Legs { get; init; } = [];
    [Key(3)] public bool IsOpen { get; init; }
}
