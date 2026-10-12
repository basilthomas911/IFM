using Npgsql;
using NpgsqlTypes;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.Storage.MarketDataDb;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetLatestFuturesRegimeSignal(
    string seriesKey,
    string timePeriod,
    string configurationId,
    int yearMonth,
    DateTime marketDataAsOf) : IBindValue
{
    /// <summary>Selects native typed parameters for the PostgreSQL provider.</summary>
    public bool PostgreSql { get; init; }

    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Text(seriesKey),
        Text(timePeriod),
        Text(configurationId),
        Integer(yearMonth),
        TimestampTz(marketDataAsOf)
    } : new object[]
    {
        seriesKey, timePeriod, configurationId, yearMonth, marketDataAsOf
    };
}
