using Npgsql;
using NpgsqlTypes;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.Storage.MarketDataDb;
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertFuturesOptionTickData(string contractId, DateOnly valueDate, long tickId, TimeOnly tickTime, double optionPrice, double bidPrice, double askPrice, int bidSize, int askSize, double impliedVolatility, double underlyingPrice, double delta, double gamma, double vega, double theta, double rho) : IBindValue
{
    public bool PostgreSql { get; init; }

    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Text(contractId),
        Date(valueDate),
        Bigint(tickId),
        new NpgsqlParameter<TimeOnly> { NpgsqlDbType = NpgsqlDbType.Time, TypedValue = tickTime },
        Double(optionPrice),
        Double(bidPrice),
        Double(askPrice),
        Integer(bidSize),
        Integer(askSize),
        Double(impliedVolatility),
        Double(underlyingPrice),
        Double(delta),
        Double(gamma),
        Double(vega),
        Double(theta),
        Double(rho)
    } : new object?[] { contractId, valueDate, tickId, tickTime, optionPrice, bidPrice, askPrice, bidSize, askSize, impliedVolatility, underlyingPrice, delta, gamma, vega, theta, rho };
}
