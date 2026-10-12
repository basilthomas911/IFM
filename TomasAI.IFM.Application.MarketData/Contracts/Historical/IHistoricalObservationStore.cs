using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;

namespace TomasAI.IFM.Application.MarketData.Contracts.Historical;

/// <summary>Persists immutable raw observations with insert-if-absent semantics.</summary>
public interface IHistoricalObservationStore
{
    /// <summary>Writes one normalized observation if its deterministic identity is new.</summary>
    ValueTask<bool> TryWriteObservationAsync(
        FuturesTradeSessionBarReadModel observation,
        CancellationToken cancellationToken);
    /// <summary>Writes one raw Daily EOD session if its deterministic identity is new.</summary>
    ValueTask<bool> TryWriteRawEodAsync(
        FuturesEodObservationReadModel observation,
        CancellationToken cancellationToken);
    /// <summary>Gets one exact raw EOD session.</summary>
    ValueTask<FuturesEodObservationReadModel?> GetRawEodAsync(
        MarketSeriesIdentity seriesIdentity,
        DateOnly valueDate,
        CancellationToken cancellationToken);
    /// <summary>Gets valid or invalid raw Daily EOD sessions in ascending value-date order.</summary>
    async ValueTask<IReadOnlyList<FuturesEodObservationReadModel>> GetRawEodRangeAsync(
        MarketSeriesIdentity seriesIdentity,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(startDate, endDate);
        List<FuturesEodObservationReadModel> values = [];
        for (var date = startDate; date <= endDate; date = date.AddDays(1))
            if (await GetRawEodAsync(seriesIdentity, date, cancellationToken).ConfigureAwait(false) is { } value)
                values.Add(value);
        return values;
    }
}
