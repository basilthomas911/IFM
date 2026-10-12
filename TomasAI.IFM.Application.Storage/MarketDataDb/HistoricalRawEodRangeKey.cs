using Npgsql;
using NpgsqlTypes;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.Storage.MarketDataDb;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct HistoricalRawEodRangeKey(
    string SeriesKey,
    int YearMonth,
    DateOnly StartDate,
    DateOnly EndDate) : IBindValue
{
    public bool PostgreSql { get; init; }
    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Text(SeriesKey),
        Integer(YearMonth),
        Date(StartDate),
        Date(EndDate)
    } : new object?[] { SeriesKey, YearMonth, StartDate, EndDate };
}
