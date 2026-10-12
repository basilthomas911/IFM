using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;

namespace TomasAI.IFM.Application.MarketData.MarketOutlook;

public interface IMarketOutlookOperations
{
    MarketOutlookProcessorMetricsSnapshot GetMetrics();
    bool RequestRecompose(MarketOutlookEntityId entityId);
    ValueTask<bool> WaitForIdleAsync(TimeSpan timeout, CancellationToken cancellationToken = default);
}
