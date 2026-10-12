using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;

namespace TomasAI.IFM.Application.MarketData.MarketOutlook;

/// <summary>Reader capability reserved for the sole Market Outlook processor.</summary>
public interface IMarketOutlookUpdateReader
{
    IAsyncEnumerable<MarketOutlookUpdate> ReadAllAsync(CancellationToken cancellationToken);
    int PendingCount { get; }
    DateTime? OldestPendingUtc { get; }
}
