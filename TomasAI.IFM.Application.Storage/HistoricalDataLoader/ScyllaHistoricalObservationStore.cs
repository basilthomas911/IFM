using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.HistoricalDataLoader;

/// <summary>Persists immutable normalized observations to query-shaped ScyllaDB tables.</summary>
public sealed class ScyllaHistoricalObservationStore(
    IDbConnectionSettings connectionSettings,
    ILogger<DbProvider> logger)
    : ObjectDataRepository<ScyllaHistoricalObservationStore>(
        connectionSettings[MarketDataDbContext.MarketDataDbConnection], logger),
      IHistoricalObservationStore
{
    public override IObjectRepository Database => this;

    public async ValueTask<bool> TryWriteObservationAsync(
        FuturesTradeSessionBarReadModel observation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(observation);
        return await Database.Use($"{nameof(HistoricalObservationCql)}.{nameof(HistoricalObservationCql.TryInsertObservation)}", HistoricalObservationCql.TryInsertObservation)
            .SetParameters(new ObservationParameter(observation))
            .ExecuteSingleAsync(static row => row.GetBool(0), cancellationToken)
            .ConfigureAwait(false) == true;
    }

    public async ValueTask<bool> TryWriteRawEodAsync(
        FuturesEodObservationReadModel observation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(observation);
        return await Database.Use($"{nameof(HistoricalObservationCql)}.{nameof(HistoricalObservationCql.TryInsertRawEod)}", HistoricalObservationCql.TryInsertRawEod)
            .SetParameters(new RawEodParameter(observation))
            .ExecuteSingleAsync(static row => row.GetBool(0), cancellationToken)
            .ConfigureAwait(false) == true;
    }

    public ValueTask<FuturesEodObservationReadModel?> GetRawEodAsync(
        MarketSeriesIdentity seriesIdentity,
        DateOnly valueDate,
        CancellationToken cancellationToken) =>
        new(Database.Use($"{nameof(HistoricalObservationCql)}.{nameof(HistoricalObservationCql.GetRawEod)}", HistoricalObservationCql.GetRawEod)
            .SetParameters(new RawEodKey(seriesIdentity.Format(), YearMonth(valueDate), valueDate))
            .ExecuteSingleAsync<FuturesEodObservationReadModel?>(MapRawEod, cancellationToken));

    public async ValueTask<IReadOnlyList<FuturesEodObservationReadModel>> GetRawEodRangeAsync(
        MarketSeriesIdentity seriesIdentity,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken)
    {
        if (startDate > endDate)
            throw new ArgumentOutOfRangeException(nameof(startDate));
        List<FuturesEodObservationReadModel> values = [];
        for (var month = new DateOnly(startDate.Year, startDate.Month, 1); month <= endDate; month = month.AddMonths(1))
        {
            var monthEnd = month.AddMonths(1).AddDays(-1);
            var lower = startDate > month ? startDate : month;
            var upper = endDate < monthEnd ? endDate : monthEnd;
            var rows = await Database
                .Use($"{nameof(HistoricalObservationCql)}.{nameof(HistoricalObservationCql.GetRawEodRange)}", HistoricalObservationCql.GetRawEodRange)
                .SetParameters(new RawEodRangeKey(seriesIdentity.Format(), YearMonth(month), lower, upper))
                .ExecuteQueryAsync(MapRawEod, cancellationToken)
                .ConfigureAwait(false);
            values.AddRange(rows);
        }
        return values
            .OrderBy(static value => value.ValueDate)
            .ThenBy(static value => value.ContractId, StringComparer.Ordinal)
            .ToArray();
    }

    static FuturesEodObservationReadModel MapRawEod(IObjectDataRecord row) => new()
    {
        MarketSeriesIdentity = MarketSeriesIdentity.Parse(row.GetString(0)), ContractId = row.GetString(1),
        ValueDate = row.GetDateOnly(2), SessionStartUtc = Utc(row.GetDateTime(3)), SessionEndUtc = Utc(row.GetDateTime(4)),
        Open = row.GetDecimal(5), High = row.GetDecimal(6), Low = row.GetDecimal(7), Close = row.GetDecimal(8),
        Volume = row.GetDecimal(9), TradeCount = row.GetLong(10), PriceVolumeSum = row.GetDecimal(11),
        ObservationId = new(row.GetGuid(12)), FirstSourceSequence = row.GetLong(13), LastSourceSequence = row.GetLong(14),
        FirstMarketEventUtc = Utc(row.GetDateTime(15)), LastMarketEventUtc = Utc(row.GetDateTime(16)),
        SchemaVersion = checked((ushort)row.GetInt(17)), IsComplete = row.GetBool(18), IsValid = row.GetBool(19)
    };

    static DateTimeOffset Utc(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
    static int YearMonth(DateOnly value) => checked(value.Year * 100 + value.Month);

    readonly record struct ObservationParameter(FuturesTradeSessionBarReadModel Value) : IBindValue
    {
        public object Bind() => new object?[]
        {
            Value.MarketSeriesIdentity.Format(), Value.TimeFrame.ToString(), YearMonth(Value.ValueDate),
            Value.IntervalEndUtc.UtcDateTime, Value.ObservationId.Value, Value.ContractId, Value.ValueDate,
            Value.IntervalStartUtc.UtcDateTime, Value.IntervalEndUtc.UtcDateTime, Value.Open, Value.High,
            Value.Low, Value.Close, Value.Volume, Value.TradeCount, Value.PriceVolumeSum,
            Value.FirstSourceSequence, Value.LastSourceSequence, Value.FirstMarketEventUtc.UtcDateTime,
            Value.LastMarketEventUtc.UtcDateTime, Value.CalculatedAtUtc.UtcDateTime,
            (int)Value.SchemaVersion, Value.CalculationVersion, Value.IsComplete, Value.IsValid
        };
    }

    readonly record struct RawEodParameter(FuturesEodObservationReadModel Value) : IBindValue
    {
        public object Bind() => new object?[]
        {
            Value.MarketSeriesIdentity.Format(), YearMonth(Value.ValueDate), Value.ValueDate, Value.ContractId,
            Value.SessionStartUtc.UtcDateTime, Value.SessionEndUtc.UtcDateTime, Value.Open, Value.High,
            Value.Low, Value.Close, Value.Volume, Value.TradeCount, Value.PriceVolumeSum,
            Value.ObservationId.Value, Value.FirstSourceSequence, Value.LastSourceSequence,
            Value.FirstMarketEventUtc.UtcDateTime, Value.LastMarketEventUtc.UtcDateTime,
            (int)Value.SchemaVersion, Value.IsComplete, Value.IsValid
        };
    }

    readonly record struct RawEodKey(string SeriesKey, int YearMonth, DateOnly ValueDate) : IBindValue
    {
        public object Bind() => new object?[] { SeriesKey, YearMonth, ValueDate };
    }

    readonly record struct RawEodRangeKey(
        string SeriesKey,
        int YearMonth,
        DateOnly StartDate,
        DateOnly EndDate) : IBindValue
    {
        public object Bind() => new object?[] { SeriesKey, YearMonth, StartDate, EndDate };
    }
}
