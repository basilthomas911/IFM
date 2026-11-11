using MessagePack;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;

/// <summary>Projected Fund defaults and contracts qualified against one published Databento definition generation.</summary>
[MessagePackObject]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Published MessagePack collection shape.")]
public sealed record FundBaseContractsReadModel
{
    [Key(0)] public int PortfolioId { get; init; }
    [Key(1)] public int FundId { get; init; }
    [Key(2)] public long FundMandateVersion { get; init; }
    [Key(3)] public DateOnly ValueDate { get; init; }
    [Key(4)] public Guid DefinitionSnapshotId { get; init; }
    [Key(5)] public FundBaseContractRule[] Defaults { get; init; } = [];
    [Key(6)] public TradeStrategySymbolReadModel[] Symbols { get; init; } = [];
    [Key(7)] public FuturesContractV3ReadModel[] Contracts { get; init; } = [];
    [Key(8)] public string Status { get; init; } = string.Empty;
}
