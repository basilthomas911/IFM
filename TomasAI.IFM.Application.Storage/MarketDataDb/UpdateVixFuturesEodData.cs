using Npgsql;
using NpgsqlTypes;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.Storage.MarketDataDb;
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct UpdateVixFuturesEodData(string contractId, DateOnly valueDate, decimal openPrice, decimal highPrice, decimal lowPrice, decimal closePrice, long volume) : IBindValue
{
    public bool PostgreSql { get; init; }
    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = openPrice },
        new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = highPrice },
        new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = lowPrice },
        new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = closePrice },
        Bigint(volume),
        Text(contractId),
        Date(valueDate)
    } : new object?[] { openPrice, highPrice, lowPrice, closePrice, volume, contractId, valueDate };
}
