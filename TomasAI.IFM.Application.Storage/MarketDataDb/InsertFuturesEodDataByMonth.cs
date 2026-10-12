using Npgsql;
using NpgsqlTypes;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.Storage.MarketDataDb;
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertFuturesEodDataByMonth(int yearMonth, string contractId, DateOnly valueDate, string symbol, decimal openPrice, decimal highPrice, decimal lowPrice, decimal closePrice, long volume, double dailyPercentChange, double dailyStdDev, double dailyStdDevAmount, double upperBand, double mean, double lowerBand, string marketDirection, string marketVolatility, string priceDirection, string priceVolatility, double marketDirectionIndicator, int windowSize, decimal fiftyDMA, decimal twoHundredDMA) : IBindValue
{
    public bool PostgreSql { get; init; }
    public object Bind() => PostgreSql ? Values(Integer(yearMonth), Text(contractId), Date(valueDate), Text(symbol), new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = openPrice }, new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = highPrice }, new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = lowPrice }, new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = closePrice }, Bigint(volume), Double(dailyPercentChange), Double(dailyStdDev), Double(dailyStdDevAmount), Double(upperBand), Double(mean), Double(lowerBand), Text(marketDirection), Text(marketVolatility), Text(priceDirection), Text(priceVolatility), Double(marketDirectionIndicator), Integer(windowSize), new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = fiftyDMA }, new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = twoHundredDMA }) : new object?[] { yearMonth, contractId, valueDate, symbol, openPrice, highPrice, lowPrice, closePrice, volume, dailyPercentChange, dailyStdDev, dailyStdDevAmount, upperBand, mean, lowerBand, marketDirection, marketVolatility, priceDirection, priceVolatility, marketDirectionIndicator, windowSize, fiftyDMA, twoHundredDMA };
}
