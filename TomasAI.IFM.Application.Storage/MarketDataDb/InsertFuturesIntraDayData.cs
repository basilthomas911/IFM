using Npgsql;
using NpgsqlTypes;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.Storage.MarketDataDb;
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertFuturesIntraDayData(string contractId, DateOnly valueDate, long sequenceId, string symbol, decimal openPrice, decimal highPrice, decimal lowPrice, decimal closePrice, long volume, double dailyPercentChange, double dailyStdDev, double dailyStdDevAmount, double upperBand, double mean, double lowerBand, string marketDirection, string marketVolatility, string priceDirection, string priceVolatility, double marketDirectionIndicator, int windowSize) : IBindValue
{
    public bool PostgreSql { get; init; }
    public object Bind() => PostgreSql ? Values(Text(contractId), Date(valueDate), Bigint(sequenceId), Text(symbol), new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = openPrice }, new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = highPrice }, new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = lowPrice }, new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = closePrice }, Bigint(volume), Double(dailyPercentChange), Double(dailyStdDev), Double(dailyStdDevAmount), Double(upperBand), Double(mean), Double(lowerBand), Text(marketDirection), Text(marketVolatility), Text(priceDirection), Text(priceVolatility), Double(marketDirectionIndicator), Integer(windowSize)) : new object?[] { contractId, valueDate, sequenceId, symbol, openPrice, highPrice, lowPrice, closePrice, volume, dailyPercentChange, dailyStdDev, dailyStdDevAmount, upperBand, mean, lowerBand, marketDirection, marketVolatility, priceDirection, priceVolatility, marketDirectionIndicator, windowSize };
}
