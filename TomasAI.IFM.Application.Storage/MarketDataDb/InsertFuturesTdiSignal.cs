using Npgsql;
using NpgsqlTypes;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.Storage.MarketDataDb;
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertFuturesTdiSignal(
    string contractId,
    string timePeriod,
    string configurationId,
    DateOnly valueDate,
    TimeOnly timestamp,
    int schemaVersion,
    int rsiPeriod,
    int priceLinePeriod,
    int signalLinePeriod,
    int marketBasePeriod,
    int volatilityBandPeriod,
    double volatilityBandDeviation,
    decimal price,
    double rsi,
    double priceLine,
    double signalLine,
    double marketBaseLine,
    double upperVolatilityBand,
    double lowerVolatilityBand,
    double bandWidth,
    double priceSignalDivergence,
    string crossType,
    string marketState,
    string trendDirection,
    string trendStrength,
    long sourceSequence,
    DateTime sourceEventTimestamp) : IBindValue
{
    public bool PostgreSql { get; init; }
    public object Bind() => PostgreSql ? Values(Text(contractId), Text(timePeriod), Text(configurationId), Date(valueDate), new NpgsqlParameter<TimeOnly> { NpgsqlDbType = NpgsqlDbType.Time, TypedValue = timestamp }, Integer(schemaVersion), Integer(rsiPeriod), Integer(priceLinePeriod), Integer(signalLinePeriod), Integer(marketBasePeriod), Integer(volatilityBandPeriod), Double(volatilityBandDeviation), new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = price }, Double(rsi), Double(priceLine), Double(signalLine), Double(marketBaseLine), Double(upperVolatilityBand), Double(lowerVolatilityBand), Double(bandWidth), Double(priceSignalDivergence), Text(crossType), Text(marketState), Text(trendDirection), Text(trendStrength), Bigint(sourceSequence), TimestampTz(sourceEventTimestamp)) : new object?[]
    {
        contractId, timePeriod, configurationId, valueDate, timestamp, schemaVersion,
        rsiPeriod, priceLinePeriod, signalLinePeriod, marketBasePeriod, volatilityBandPeriod,
        volatilityBandDeviation, price, rsi, priceLine, signalLine, marketBaseLine,
        upperVolatilityBand, lowerVolatilityBand, bandWidth, priceSignalDivergence,
        crossType, marketState, trendDirection, trendStrength, sourceSequence, sourceEventTimestamp
    };
}
