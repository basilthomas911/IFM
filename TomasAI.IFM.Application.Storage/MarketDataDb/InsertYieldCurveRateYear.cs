using Npgsql;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.Storage.MarketDataDb;
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertYieldCurveRateYear(int lookupId, int rateYear) : IBindValue
{
    public bool PostgreSql { get; init; }
    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Integer(lookupId),
        Integer(rateYear)
    } : new object?[] { lookupId, rateYear };
}
