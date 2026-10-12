using System.Collections.Concurrent;
using System.Collections.Immutable;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesBbSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesEmaSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVwapSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.MarketData.MarketOutlook;

public sealed record MarketOutlookInputState
{
    public MarketOutlookEntityId EntityId { get; init; } = new();
    public FuturesEodDataV2ReadModel? FuturesEodData { get; init; }
    public FuturesTradeSignalV2ReadModel? FuturesTradeSignal { get; init; }
    public FuturesRsiSignalReadModel? FuturesRsiSignal { get; init; }
    public FuturesTdiSignalReadModel? FuturesTdiSignal { get; init; }
    public FuturesItiSignalV2ReadModel? TrendDirectionChange { get; init; }
    public FuturesItiSignalV2ReadModel? TrendExtremeChange { get; init; }
    public FuturesItiSignalV2ReadModel? TrendReversalChange { get; init; }
    public FuturesItiSignalV2ReadModel? LatestItiTrendSignal { get; init; }
    public decimal? VixFuturesSessionOpenPrice { get; init; }
    public decimal? VixFuturesPrice { get; init; }
    public FuturesEmaSignalReadModel? FuturesEmaSignal { get; init; }
    public FuturesBbSignalReadModel? FuturesBbSignal { get; init; }
    public FuturesVwapSignalReadModel? FuturesVwapSignal { get; init; }
    public FuturesAdxSignalReadModel? FuturesAdxSignal { get; init; }
    public FuturesAtrSignalReadModel? FuturesAtrSignal { get; init; }
    public FuturesMacdSignalReadModel? FuturesMacdSignal { get; init; }
    public decimal? CurrentEsPrice { get; init; }
    public DateTime MarketDataAsOfUtc { get; init; }
    public string FeedHealth { get; init; } = "Unavailable";
    public string FeedHealthReason { get; init; } = string.Empty;
    public ImmutableDictionary<MarketOutlookComponentType, MarketOutlookSourcePosition> Positions { get; init; }
        = ImmutableDictionary<MarketOutlookComponentType, MarketOutlookSourcePosition>.Empty;
}
