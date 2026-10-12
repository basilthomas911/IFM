using System.Collections.Concurrent;
using System.Collections.Immutable;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesBbSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesEmaSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVwapSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.MarketData.MarketOutlook;

public interface IMarketOutlookHotCache
{
    bool TryGetInputs(MarketOutlookEntityId entityId, out MarketOutlookInputState state);
    bool TryGetCurrent(MarketOutlookEntityId entityId, out MarketOutlookReadModel value);
    MarketOutlookHotCacheMetrics GetMetrics();
    void Clear();
}
