using System.Collections.Concurrent;
using System.Collections.Immutable;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesBbSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesEmaSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVwapSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.MarketData.MarketOutlook;

/// <summary>Mutation capability reserved for the single Market Outlook update processor.</summary>
public interface IMarketOutlookHotCacheWriter
{
    MarketOutlookHotCacheWriteResult Write(
        MarketOutlookEntityId entityId,
        IReadOnlyCollection<MarketOutlookComponentWrite> components,
        Func<MarketOutlookInputState, MarketOutlookInputState> update,
        Func<MarketOutlookInputState, MarketOutlookReadModel> compose);
    void RecordNotificationFailure();
}
