using System.Collections.Immutable;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Framework.Storage.Extensions;

namespace TomasAI.IFM.Application.Storage.MarketDataDb;

public partial class MarketDataDbContext
{
    static EconomicCalendarReadModel MapToEconomicCalendar(IObjectDataRecord row) => new()
    {
        EventDate = NormalizeEconomicCalendarTimestamp(row.GetDateTime(0)), CountryCode = row.GetString(1), EventName = row.GetString(2),
        Actual = row.GetString(3), Forecast = row.GetString(4), Prior = row.GetString(5),
        CreatedOn = row.GetDateTime(6), CreatedBy = row.GetString(7)
    };

    static EconomicCalendarCountryCodeReadModel MapToEconomicCalendarCountryCode(IObjectDataRecord row)
        => new(row.GetString(0));

    static DateTime NormalizeEconomicCalendarTimestamp(DateTime value)
    {
        var utc = ProjectionMutationSafety.AsUtc(value);
        return new DateTime(
            utc.Ticks - (utc.Ticks % TimeSpan.TicksPerMillisecond),
            DateTimeKind.Utc);
    }

    static int EconomicCalendarMonthBucket(DateTime value) => value.Year * 100 + value.Month;

    static IEnumerable<int> EconomicCalendarMonthBuckets(DateTime startDate, DateTime endDate)
    {
        for (var month = new DateTime(startDate.Year, startDate.Month, 1, 0, 0, 0, DateTimeKind.Utc);
             month <= endDate; month = month.AddMonths(1))
            yield return EconomicCalendarMonthBucket(month);
    }

    public Task<EconomicCalendarReadModel?> GetEconomicCalendarAsync(EconomicCalendarId id)
        => GetEconomicCalendarAsync(id, CancellationToken.None);

    public async Task<EconomicCalendarReadModel?> GetEconomicCalendarAsync(EconomicCalendarId id, CancellationToken cancellationToken)
        => await _dbFactory.MarketDataDb.Use(MarketDataDbCql.GetEconomicCalendarById)
            .SetParameters(new GetEconomicCalendarById(NormalizeEconomicCalendarTimestamp(id.EventDate), id.CountryCode, id.EventName))
            .ExecuteSingleAsync(MapToEconomicCalendar!, cancellationToken);

    public Task<ICollection<EconomicCalendarReadModel>> GetEconomicCalendarsAsync(DateTime eventDate, string countryCode)
        => GetEconomicCalendarsAsync(eventDate, countryCode, CancellationToken.None);

    public Task<ICollection<EconomicCalendarReadModel>> GetEconomicCalendarsAsync(DateTime eventDate, string countryCode, CancellationToken cancellationToken)
    {
        var startDate = eventDate.Date;
        var endDate = startDate == DateTime.MaxValue.Date ? DateTime.MaxValue : startDate.AddDays(1).AddTicks(-1);
        return GetEconomicCalendarsAsync(startDate, endDate, countryCode, cancellationToken);
    }

    public Task<ICollection<EconomicCalendarReadModel>> GetEconomicCalendarsAsync(DateTime startDate, DateTime endDate, string countryCode)
        => GetEconomicCalendarsAsync(startDate, endDate, countryCode, CancellationToken.None);

    public async Task<ICollection<EconomicCalendarReadModel>> GetEconomicCalendarsAsync(DateTime startDate, DateTime endDate, string countryCode, CancellationToken cancellationToken)
    {
        startDate = NormalizeEconomicCalendarTimestamp(startDate);
        endDate = NormalizeEconomicCalendarTimestamp(endDate);
        if (endDate < startDate) return [];
        var pages = await Task.WhenAll(EconomicCalendarMonthBuckets(startDate, endDate).Select(bucket =>
            _dbFactory.MarketDataDb.Use(MarketDataDbCql.GetEconomicCalendars)
                .SetParameters(new GetEconomicCalendars(countryCode, bucket, startDate, endDate))
                .ExecuteQueryAsync(MapToEconomicCalendar!, cancellationToken)));
        return [.. pages.SelectMany(static page => page).OrderByDescending(static row => row.EventDate)];
    }

    public Task<ICollection<EconomicCalendarReadModel>> GetEconomicCalendarAllAsync()
        => GetEconomicCalendarAllAsync(CancellationToken.None);

    public async Task<ICollection<EconomicCalendarReadModel>> GetEconomicCalendarAllAsync(CancellationToken cancellationToken)
        => [.. (await _dbFactory.MarketDataDb.Use(MarketDataDbCql.GetEconomicCalendarsAll)
            .ExecuteQueryAsync(MapToEconomicCalendar!, cancellationToken)).OrderByDescending(static row => row.EventDate)];

    public Task<ICollection<EconomicCalendarCountryCodeReadModel>> GetEconomicCalendarCountryCodesAsync()
        => GetEconomicCalendarCountryCodesAsync(CancellationToken.None);

    public async Task<ICollection<EconomicCalendarCountryCodeReadModel>> GetEconomicCalendarCountryCodesAsync(CancellationToken cancellationToken)
        => (await _dbFactory.MarketDataDb.Use(MarketDataDbCql.GetEconomicCalendarCountryCodes)
            .ExecuteQueryAsync(MapToEconomicCalendarCountryCode, cancellationToken))
            .DistinctBy(static row => row.CountryCode).ToImmutableList();

    public async Task DeleteEconomicCalendarAsync(EconomicCalendarId id)
    {
        var eventDate = NormalizeEconomicCalendarTimestamp(id.EventDate);
        var db = _dbFactory.MarketDataDb;
        await db.ExecuteQueuedCommandsAsync([
            db.Use(MarketDataDbCql.DeleteEconomicCalendar).SetParameters(new DeleteEconomicCalendar(eventDate, id.CountryCode, id.EventName)).QueueCommand(),
            db.Use(MarketDataDbCql.DeleteEconomicCalendarByCountryMonthV2).SetParameters(new DeleteEconomicCalendarByCountryMonthV2(id.CountryCode, EconomicCalendarMonthBucket(eventDate), eventDate, id.EventName)).QueueCommand()
        ]);
    }

    public Task InsertEconomicCalendarAsync(EconomicCalendarReadModel economicCalendar)
        => InsertEconomicCalendarsAsync([economicCalendar]);

    public async Task InsertEconomicCalendarsAsync(ICollection<EconomicCalendarReadModel> economicCalendars)
    {
        if (economicCalendars.Count == 0) return;
        var db = _dbFactory.MarketDataDb;
        var commands = new List<object>(economicCalendars.Count * 2);
        foreach (var row in economicCalendars)
        {
            var eventDate = NormalizeEconomicCalendarTimestamp(row.EventDate);
            commands.Add(db.Use(MarketDataDbCql.InsertEconomicCalendar)
                .SetParameters(new InsertEconomicCalendar(eventDate, row.CountryCode, row.EventName,
                    row.Actual, row.Forecast, row.Prior, row.CreatedOn, row.CreatedBy)).QueueCommand());
            commands.Add(db.Use(MarketDataDbCql.InsertEconomicCalendarByCountryMonthV2)
                .SetParameters(new InsertEconomicCalendarByCountryMonthV2(row.CountryCode,
                    EconomicCalendarMonthBucket(eventDate), eventDate, row.EventName, row.Actual,
                    row.Forecast, row.Prior, row.CreatedOn, row.CreatedBy)).QueueCommand());
        }
        await db.ExecuteQueuedCommandsAsync(commands);
    }

    public async Task UpdateEconomicCalendarAsync(EconomicCalendarId id, EconomicCalendarReadModel economicCalendar)
    {
        await DeleteEconomicCalendarAsync(id);
        await InsertEconomicCalendarAsync(economicCalendar);
    }
}
