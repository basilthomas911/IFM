using Npgsql;
using NpgsqlTypes;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.Storage.MarketDataDb;
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetFuturesBarData(string contractId, string symbol, DateOnly valueDate, DateTime startDate, DateTime endDate) : IBindValue
{
    public bool PostgreSql { get; init; }

    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Text(contractId),
        Text(symbol),
        Date(valueDate),
        TimestampTz(startDate),
        TimestampTz(endDate)
    } : new object?[] { contractId, symbol, valueDate, startDate, endDate };
}
