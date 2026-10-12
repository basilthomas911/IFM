using Npgsql;
using NpgsqlTypes;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.Storage.MarketDataDb;
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertEconomicCalendarV2(string countryCode, int monthBucket, DateTime eventDate, string eventName, string? actual, string? forecast, string? prior, string? impact, string? unit, string? change, string? changePercentage, DateTime createdOn, string createdBy, Guid commandId) : IBindValue
{
    public bool PostgreSql { get; init; }

    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Text(countryCode),
        Integer(monthBucket),
        TimestampTz(eventDate),
        Text(eventName),
        Text(actual),
        Text(forecast),
        Text(prior),
        Text(impact),
        Text(unit),
        Text(change),
        Text(changePercentage),
        TimestampTz(createdOn),
        Text(createdBy),
        Uuid(commandId)
    } : new object?[] { countryCode, monthBucket, eventDate, eventName, actual, forecast, prior, impact, unit, change, changePercentage, createdOn, createdBy, commandId };
}
