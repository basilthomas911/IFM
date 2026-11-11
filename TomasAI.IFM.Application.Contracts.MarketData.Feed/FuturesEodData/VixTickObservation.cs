using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared;

/// <summary>Pairs a VX tick with statistics for the same contract and exchange value date.</summary>
public sealed class VixTickObservation
{
    public VixTickObservation(FuturesTickDataV2ReadModel tick, FuturesSessionStatisticsSnapshot? statistics = null)
    {
        ArgumentNullException.ThrowIfNull(tick);
        if (statistics is { } supplied && (!string.Equals(supplied.ContractId, tick.ContractId, StringComparison.Ordinal)
            || supplied.ValueDate != tick.ValueDate))
            throw new ArgumentException("Session statistics must match the tick contract and value date.", nameof(statistics));
        Tick = tick;
        Statistics = statistics;
    }

    public FuturesTickDataV2ReadModel Tick { get; }
    public FuturesSessionStatisticsSnapshot? Statistics { get; }
}
