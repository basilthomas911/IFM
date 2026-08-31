namespace TomasAI.IFM.Application.Storage.HistoricalDataLoader;

/// <summary>Contains idempotent ScyllaDB statements for raw and shared Analytics observations.</summary>
public static class HistoricalObservationCql
{
    public const string TryInsertObservation = """
    INSERT INTO futures_trade_session_bar (
        seriesKey, timePeriod, yearMonth, marketDataAsOf, observationId,
        contractId, valueDate, intervalStart, intervalEnd, openPrice, highPrice,
        lowPrice, closePrice, volume, tradeCount, priceVolumeSum,
        firstSourceSequence, lastSourceSequence, firstMarketEvent, lastMarketEvent,
        calculatedAt, schemaVersion, calculationVersion, isComplete, isValid)
    VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
    IF NOT EXISTS;
    """;

    public const string TryInsertRawEod = """
    INSERT INTO futures_eod_observation (
        seriesKey, yearMonth, valueDate, contractId, sessionStart, sessionEnd,
        openPrice, highPrice, lowPrice, closePrice, volume, tradeCount,
        priceVolumeSum, observationId, firstSourceSequence, lastSourceSequence,
        firstMarketEvent, lastMarketEvent, schemaVersion, isComplete, isValid)
    VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
    IF NOT EXISTS;
    """;

    public const string GetRawEod = """
    SELECT seriesKey, contractId, valueDate, sessionStart, sessionEnd,
           openPrice, highPrice, lowPrice, closePrice, volume, tradeCount,
           priceVolumeSum, observationId, firstSourceSequence, lastSourceSequence,
           firstMarketEvent, lastMarketEvent, schemaVersion, isComplete, isValid
    FROM futures_eod_observation
    WHERE seriesKey = ? AND yearMonth = ? AND valueDate = ?;
    """;

    public const string GetRawEodRange = """
    SELECT seriesKey, contractId, valueDate, sessionStart, sessionEnd,
           openPrice, highPrice, lowPrice, closePrice, volume, tradeCount,
           priceVolumeSum, observationId, firstSourceSequence, lastSourceSequence,
           firstMarketEvent, lastMarketEvent, schemaVersion, isComplete, isValid
    FROM futures_eod_observation
    WHERE seriesKey = ? AND yearMonth = ? AND valueDate >= ? AND valueDate <= ?;
    """;
}
