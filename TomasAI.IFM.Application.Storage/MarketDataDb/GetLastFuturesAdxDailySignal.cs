using Npgsql;
using NpgsqlTypes;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.Storage.MarketDataDb;
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetLastFuturesAdxDailySignal(string contractId, string timePeriod, int periodLength) : IBindValue
{
    /// <summary>Selects typed native PostgreSQL parameters during provider migration.</summary>
    public bool PostgreSql { get; init; }

    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Text(contractId),
        Text(timePeriod),
        Integer(periodLength)
    } : new object?[] { contractId, timePeriod, periodLength };
}
