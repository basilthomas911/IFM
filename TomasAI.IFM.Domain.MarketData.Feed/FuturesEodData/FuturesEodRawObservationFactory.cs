using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.MarketSignals.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.MarketSignals.Observation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData;

/// <summary>Converts the legacy transport event to the raw persisted session contract during cutover.</summary>
internal static class FuturesEodRawObservationFactory
{
    /// <summary>Creates only raw session facts and lineage; no derived fields are copied.</summary>
    internal static FuturesEodObservationReadModel Create(
        FuturesEodDataInsertedEvent source,
        IMarketSessionCalendar calendar)
    {
        var value = source.FuturesEodData
            ?? throw new InvalidOperationException("Futures EOD payload is required.");
        return Create(value, source.EventId, calendar);
    }

    /// <summary>Creates a raw session from a compatibility model and source sequence.</summary>
    internal static FuturesEodObservationReadModel Create(
        FuturesEodDataV2ReadModel value,
        long eventId,
        IMarketSessionCalendar calendar)
    {
        var series = MarketSeriesIdentity.ForContract(value.ContractId);
        var session = calendar.GetSession(value.ValueDate);
        var sequence = Math.Max(eventId, 0);
        return new FuturesEodObservationReadModel
        {
            MarketSeriesIdentity = series, ContractId = value.ContractId, ValueDate = value.ValueDate,
            SessionStartUtc = session.StartUtc, SessionEndUtc = session.EndUtc,
            Open = value.OpenPrice, High = value.HighPrice, Low = value.LowPrice,
            Close = value.ClosePrice, Volume = value.Volume, TradeCount = 0,
            PriceVolumeSum = value.ClosePrice * value.Volume,
            ObservationId = FuturesAnalyticsObservationId.Create(
                series, TimeFrameType.Daily, session.EndUtc, sequence),
            FirstSourceSequence = sequence, LastSourceSequence = sequence,
            FirstMarketEventUtc = session.StartUtc, LastMarketEventUtc = session.EndUtc,
            IsComplete = true, IsValid = true
        };
    }
}
