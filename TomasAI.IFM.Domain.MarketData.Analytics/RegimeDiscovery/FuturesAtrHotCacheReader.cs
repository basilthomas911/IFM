using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.RegimeDiscovery;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.RegimeDiscovery;

/// <summary>Shares the normal indicator producer's cache with Daily futures qualification.</summary>
public sealed class FuturesAtrHotCacheReader : IFuturesAtrSnapshotReader
{
    public bool TryGetLatest(string contract, TimeFrameType frame, out FuturesAtrSignalReadModel signal)
        => RegimeDiscoverySignalCacheAdapter.TryGetLatestAtr(contract, frame, out signal);
}
