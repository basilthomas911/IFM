using Npgsql;
using NpgsqlTypes;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.Storage.MarketDataDb;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct UpdateOptionChainGreeks(string underlyingContractId, DateOnly expiryDate,
    FuturesOptionTickDataV2ReadModel tick) : IBindValue
{
    public bool PostgreSql { get; init; }
    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Double(tick.ImpliedVolatility),
        Double(tick.UnderlyingPrice),
        Double(tick.Delta),
        Double(tick.Gamma),
        Double(tick.Vega),
        Double(tick.Theta),
        Double(tick.Rho),
        Text(underlyingContractId),
        Date(expiryDate),
        Date(tick.ValueDate),
        Text(tick.ContractId),
        Bigint(tick.TickId)
    } : new object?[] { tick.ImpliedVolatility, tick.UnderlyingPrice, tick.Delta,
        tick.Gamma, tick.Vega, tick.Theta, tick.Rho, underlyingContractId, expiryDate,
        tick.ValueDate, tick.ContractId, tick.TickId };
}
