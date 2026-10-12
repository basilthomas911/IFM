using Npgsql;
using NpgsqlTypes;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.Storage.MarketDataDb;
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertFuturesBarData(string contractId, string symbol, DateOnly valueDate, DateTime barDate, string barRateType, decimal barValue, double upTrendTrigger, double downTrendTrigger) : IBindValue
{
    public bool PostgreSql { get; init; }

    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Text(contractId),
        Text(symbol),
        Date(valueDate),
        TimestampTz(barDate),
        Text(barRateType),
        new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = barValue },
        Double(upTrendTrigger),
        Double(downTrendTrigger)
    } : new object?[] { contractId, symbol, valueDate, barDate, barRateType, barValue, upTrendTrigger, downTrendTrigger };
}
