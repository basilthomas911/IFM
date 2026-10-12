using Npgsql;
using NpgsqlTypes;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.Storage.MarketDataDb;
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct UpsertFuturesOptionChainQuoteData(
    string underlyingContractId, DateOnly expiryDate, FuturesOptionTickDataV2ReadModel tick) : IBindValue
{
    public bool PostgreSql { get; init; }
    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Text(underlyingContractId),
        Date(expiryDate),
        Date(tick.ValueDate),
        Text(tick.ContractId),
        Bigint(tick.TickId),
        new NpgsqlParameter<TimeOnly> { NpgsqlDbType = NpgsqlDbType.Time, TypedValue = tick.TickTime },
        Double(tick.OptionPrice),
        Double(tick.BidPrice),
        Double(tick.AskPrice),
        Integer(tick.BidSize),
        Integer(tick.AskSize),
        Double(tick.ImpliedVolatility),
        Double(tick.UnderlyingPrice),
        Double(tick.Delta),
        Double(tick.Gamma),
        Double(tick.Vega),
        Double(tick.Theta),
        Double(tick.Rho)
    } : new object?[] { underlyingContractId, expiryDate, tick.ValueDate,
        tick.ContractId, tick.TickId, tick.TickTime, tick.OptionPrice, tick.BidPrice,
        tick.AskPrice, tick.BidSize, tick.AskSize, tick.ImpliedVolatility,
        tick.UnderlyingPrice, tick.Delta, tick.Gamma, tick.Vega, tick.Theta, tick.Rho };
}
