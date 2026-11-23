using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.RegimeDiscovery;

/// <summary>Read-only access to original published ATR observations; never warms or fetches history.</summary>
public interface IFuturesAtrSnapshotReader
{
    bool TryGetLatest(string contract, TimeFrameType frame, out FuturesAtrSignalReadModel signal);
}
