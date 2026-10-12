using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;

namespace TomasAI.IFM.Application.MarketData.Contracts.Historical;

/// <summary>Defines the compatibility query boundary used while UI/API consumers leave the derived EOD model.</summary>
public interface IFuturesEodAnalyticsAssembler
{
    /// <summary>Loads raw EOD and exact same-observation Analytics signals without recalculation.</summary>
    ValueTask<FuturesEodAnalyticsAssembly?> AssembleAsync(
        MarketSeriesIdentity seriesIdentity,
        DateOnly valueDate,
        IReadOnlyCollection<MarketAnalyticsSignalFamily> requestedSignals,
        CancellationToken cancellationToken);
}
