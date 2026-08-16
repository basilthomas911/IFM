using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Framework.Storage.Extensions;

namespace TomasAI.IFM.Application.Storage.MarketDataDb;

public partial class MarketDataDbContext
{
    internal const int EconomicCalendarMaximumRangeMonths = 120;
    internal const int EconomicCalendarMaximumAllMonths = 120;
    internal const int EconomicCalendarMaximumRows = 10_000;
    internal const int EconomicCalendarMaximumRowsPerMonth = 2_500;
    internal const int EconomicCalendarMaximumCountryCodes = 512;
    internal const int EconomicCalendarMaximumConcurrentQueries = 4;
    const int EconomicCalendarLookupId = 1;

    static EconomicCalendarReadModel MapToEconomicCalendar(IObjectDataRecord row) => new()
    {
        EventDate = NormalizeEconomicCalendarTimestamp(row.GetDateTime(0)), CountryCode = row.GetString(1), EventName = row.GetString(2),
        Actual = GetNullableString(row, 3), Forecast = GetNullableString(row, 4), Prior = GetNullableString(row, 5),
        Impact = GetNullableString(row, 6), Unit = GetNullableString(row, 7),
        Change = GetNullableString(row, 8), ChangePercentage = GetNullableString(row, 9),
        CreatedOn = row.GetDateTime(10), CreatedBy = row.GetString(11)
    };

    static string? GetNullableString(IObjectDataRecord row, int index)
        => row.IsNull(index) ? null : row.GetString(index);

    static EconomicCalendarCountryCodeReadModel MapToEconomicCalendarCountryCode(IObjectDataRecord row)
        => new(row.GetString(0));

    static int MapToEconomicCalendarMonth(IObjectDataRecord row) => row.GetInt(0);

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
        var monthBuckets = EconomicCalendarMonthBuckets(startDate, endDate).ToArray();
        if (monthBuckets.Length > EconomicCalendarMaximumRangeMonths)
        {
            throw new ArgumentOutOfRangeException(
                nameof(endDate),
                $"Economic-calendar ranges may span at most {EconomicCalendarMaximumRangeMonths} UTC months.");
        }

        return await ReadEconomicCalendarMonthsAsync(
            monthBuckets,
            bucket => _dbFactory.MarketDataDb.Use(MarketDataDbCql.GetEconomicCalendars)
                .SetParameters(new GetEconomicCalendars(countryCode, bucket, startDate, endDate))
                .ExecuteQueryAsync(MapToEconomicCalendar!, cancellationToken),
            cancellationToken);
    }

    public Task<ICollection<EconomicCalendarReadModel>> GetEconomicCalendarAllAsync()
        => GetEconomicCalendarAllAsync(CancellationToken.None);

    public async Task<ICollection<EconomicCalendarReadModel>> GetEconomicCalendarAllAsync(CancellationToken cancellationToken)
    {
        var months = await _dbFactory.MarketDataDb.Use(MarketDataDbCql.GetEconomicCalendarMonths)
            .SetParameters(new GetEconomicCalendarMonths(EconomicCalendarLookupId))
            .ExecuteQueryAsync(MapToEconomicCalendarMonth, cancellationToken);

        return await ReadEconomicCalendarMonthsAsync(
            months,
            bucket => _dbFactory.MarketDataDb.Use(MarketDataDbCql.GetEconomicCalendarsByMonth)
                .SetParameters(new GetEconomicCalendarsByMonth(bucket))
                .ExecuteQueryAsync(MapToEconomicCalendar!, cancellationToken),
            cancellationToken);
    }

    public Task<ICollection<EconomicCalendarCountryCodeReadModel>> GetEconomicCalendarCountryCodesAsync()
        => GetEconomicCalendarCountryCodesAsync(CancellationToken.None);

    public async Task<ICollection<EconomicCalendarCountryCodeReadModel>> GetEconomicCalendarCountryCodesAsync(CancellationToken cancellationToken)
        => await _dbFactory.MarketDataDb.Use(MarketDataDbCql.GetEconomicCalendarCountryCodes)
            .SetParameters(new GetEconomicCalendarCountryCodes(EconomicCalendarLookupId))
            .ExecuteQueryAsync(MapToEconomicCalendarCountryCode, cancellationToken);

    public async Task DeleteEconomicCalendarAsync(EconomicCalendarId id)
    {
        var eventDate = NormalizeEconomicCalendarTimestamp(id.EventDate);
        var db = _dbFactory.MarketDataDb;
        await db.ExecuteQueuedCommandsAsync([
            db.Use(MarketDataDbCql.DeleteEconomicCalendar).SetParameters(new DeleteEconomicCalendar(eventDate, id.CountryCode, id.EventName)).QueueCommand(),
            db.Use(MarketDataDbCql.DeleteEconomicCalendarByCountryMonthV2).SetParameters(new DeleteEconomicCalendarByCountryMonthV2(id.CountryCode, EconomicCalendarMonthBucket(eventDate), eventDate, id.EventName)).QueueCommand(),
            db.Use(MarketDataDbCql.DeleteEconomicCalendarByMonthV1).SetParameters(new DeleteEconomicCalendarByMonthV1(EconomicCalendarMonthBucket(eventDate), eventDate, id.CountryCode, id.EventName)).QueueCommand()
        ]);
    }

    public Task InsertEconomicCalendarAsync(EconomicCalendarReadModel economicCalendar)
        => InsertEconomicCalendarsAsync([economicCalendar]);

    public async Task InsertEconomicCalendarsAsync(ICollection<EconomicCalendarReadModel> economicCalendars)
        => await InsertEconomicCalendarsAsync(
            economicCalendars,
            ImportDuplicatePolicy.Overwrite,
            Guid.Empty);

    public async Task InsertEconomicCalendarsAsync(
        ICollection<EconomicCalendarReadModel> economicCalendars,
        ImportDuplicatePolicy duplicatePolicy,
        Guid commandId)
    {
        if (economicCalendars.Count == 0) return;
        ValidateImportPolicy(duplicatePolicy, commandId);
        var db = _dbFactory.MarketDataDb;
        var commands = new List<object>(economicCalendars.Count * 3);
        var countryCodes = new HashSet<string>(StringComparer.Ordinal);
        var monthBuckets = new HashSet<int>();
        foreach (var row in economicCalendars)
        {
            var eventDate = NormalizeEconomicCalendarTimestamp(row.EventDate);
            if (duplicatePolicy == ImportDuplicatePolicy.Reject)
            {
                await EnsureImportOwnershipAsync(
                    "economic-calendar",
                    $"{eventDate:O}|{row.CountryCode}|{row.EventName}",
                    commandId,
                    await GetEconomicCalendarAsync(
                        new EconomicCalendarId(eventDate, row.CountryCode, row.EventName),
                        CancellationToken.None).ConfigureAwait(false) is not null)
                    .ConfigureAwait(false);
            }
            var monthBucket = EconomicCalendarMonthBucket(eventDate);
            commands.Add(db.Use(MarketDataDbCql.InsertEconomicCalendar)
                .SetParameters(new InsertEconomicCalendar(eventDate, row.CountryCode, row.EventName,
                    row.Actual, row.Forecast, row.Prior, row.Impact, row.Unit, row.Change,
                    row.ChangePercentage, row.CreatedOn, row.CreatedBy)).QueueCommand());
            commands.Add(db.Use(MarketDataDbCql.InsertEconomicCalendarByCountryMonthV2)
                .SetParameters(new InsertEconomicCalendarByCountryMonthV2(row.CountryCode,
                    monthBucket, eventDate, row.EventName, row.Actual,
                    row.Forecast, row.Prior, row.Impact, row.Unit, row.Change,
                    row.ChangePercentage, row.CreatedOn, row.CreatedBy)).QueueCommand());
            commands.Add(db.Use(MarketDataDbCql.InsertEconomicCalendarByMonthV1)
                .SetParameters(new InsertEconomicCalendarByMonthV1(monthBucket, eventDate,
                    row.CountryCode, row.EventName, row.Actual, row.Forecast, row.Prior,
                    row.Impact, row.Unit, row.Change, row.ChangePercentage,
                    row.CreatedOn, row.CreatedBy)).QueueCommand());
            countryCodes.Add(row.CountryCode);
            monthBuckets.Add(monthBucket);
        }
        commands.AddRange(countryCodes.Select(countryCode => db
            .Use(MarketDataDbCql.InsertEconomicCalendarCountryCodeV1)
            .SetParameters(new InsertEconomicCalendarCountryCodeV1(
                EconomicCalendarLookupId,
                countryCode))
            .QueueCommand()));
        commands.AddRange(monthBuckets.Select(monthBucket => db
            .Use(MarketDataDbCql.InsertEconomicCalendarMonthV1)
            .SetParameters(new InsertEconomicCalendarMonthV1(
                EconomicCalendarLookupId,
                monthBucket))
            .QueueCommand()));
        await db.ExecuteQueuedCommandsAsync(commands);
    }

    public async Task UpdateEconomicCalendarAsync(EconomicCalendarId id, EconomicCalendarReadModel economicCalendar)
    {
        await DeleteEconomicCalendarAsync(id);
        await InsertEconomicCalendarAsync(economicCalendar);
    }

    /// <summary>
    /// Rebuilds the bounded FMP calendar and yield-curve lookup projections from their canonical tables.
    /// Import writers must be paused for the duration of this offline operation.
    /// </summary>
    public async Task<FmpQueryProjectionBackfillResult> BackfillFmpQueryProjectionsAsync(
        int batchSize = 256,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);
        var db = _dbFactory.MarketDataDb;

        await db.Use(MarketDataDbCql.TruncateEconomicCalendarByMonthV1)
            .ExecuteCommandAsync(cancellationToken);
        await db.Use(MarketDataDbCql.TruncateEconomicCalendarCountryCodeV1)
            .ExecuteCommandAsync(cancellationToken);
        await db.Use(MarketDataDbCql.TruncateEconomicCalendarMonthV1)
            .ExecuteCommandAsync(cancellationToken);
        await db.Use(MarketDataDbCql.TruncateYieldCurveRateByDateV1)
            .ExecuteCommandAsync(cancellationToken);
        await db.Use(MarketDataDbCql.TruncateYieldCurveRateYearV1)
            .ExecuteCommandAsync(cancellationToken);

        long economicCalendarRowsSource = 0;
        var economicCalendarSourceIdentity = new ProjectionIdentityBuilder();
        var countryCodes = new HashSet<string>(StringComparer.Ordinal);
        var monthBuckets = new HashSet<int>();
        var calendarBatch = new List<InsertEconomicCalendarByMonthV1>(batchSize);
        await foreach (var row in db.Use(MarketDataDbCql.GetEconomicCalendarProjectionSource)
            .ExecuteStreamAsync(MapToEconomicCalendar!, cancellationToken))
        {
            economicCalendarRowsSource++;
            economicCalendarSourceIdentity.Add(GetEconomicCalendarProjectionIdentity(row));
            var monthBucket = EconomicCalendarMonthBucket(row.EventDate);
            countryCodes.Add(row.CountryCode);
            monthBuckets.Add(monthBucket);
            calendarBatch.Add(new InsertEconomicCalendarByMonthV1(
                monthBucket,
                row.EventDate,
                row.CountryCode,
                row.EventName,
                row.Actual,
                row.Forecast,
                row.Prior,
                row.Impact,
                row.Unit,
                row.Change,
                row.ChangePercentage,
                row.CreatedOn,
                row.CreatedBy));
            if (calendarBatch.Count == batchSize)
                await FlushCalendarBatchAsync();
        }
        await FlushCalendarBatchAsync();

        if (countryCodes.Count > 0)
        {
            await db.Use(MarketDataDbCql.InsertEconomicCalendarCountryCodeV1)
                .SetParameters(countryCodes.Select(countryCode =>
                    new InsertEconomicCalendarCountryCodeV1(EconomicCalendarLookupId, countryCode)))
                .ExecuteCommandAsync(cancellationToken);
        }
        if (monthBuckets.Count > 0)
        {
            await db.Use(MarketDataDbCql.InsertEconomicCalendarMonthV1)
                .SetParameters(monthBuckets.Select(monthBucket =>
                    new InsertEconomicCalendarMonthV1(EconomicCalendarLookupId, monthBucket)))
                .ExecuteCommandAsync(cancellationToken);
        }

        long yieldCurveRowsSource = 0;
        var yieldCurveSourceIdentity = new ProjectionIdentityBuilder();
        var yieldCurveYears = new HashSet<int>();
        var yieldCurveBatch = new List<InsertYieldCurveRate>(batchSize);
        await foreach (var row in db.Use(MarketDataDbCql.GetYieldCurveRateProjectionSource)
            .ExecuteStreamAsync(MapToYieldCurveRate!, cancellationToken))
        {
            yieldCurveRowsSource++;
            yieldCurveSourceIdentity.Add(GetYieldCurveProjectionIdentity(row));
            yieldCurveYears.Add(row.ValueDate.Year);
            yieldCurveBatch.Add(new InsertYieldCurveRate(
                YieldCurveLookupId,
                row.ValueDate,
                row.OneMonth,
                row.TwoMonth,
                row.ThreeMonth,
                row.SixMonth,
                row.OneYear,
                row.TwoYear,
                row.ThreeYear,
                row.FiveYear,
                row.SevenYear,
                row.TenYear,
                row.TwentyYear,
                row.ThirtyYear));
            if (yieldCurveBatch.Count == batchSize)
                await FlushYieldCurveBatchAsync();
        }
        await FlushYieldCurveBatchAsync();
        if (yieldCurveYears.Count > 0)
        {
            await db.Use(MarketDataDbCql.InsertYieldCurveRateYearV1)
                .SetParameters(yieldCurveYears.Select(rateYear =>
                    new InsertYieldCurveRateYearV1(YieldCurveLookupId, rateYear)))
                .ExecuteCommandAsync(cancellationToken);
        }

        long economicCalendarRowsProjected = 0;
        var economicCalendarProjectedIdentity = new ProjectionIdentityBuilder();
        await foreach (var row in db.Use(MarketDataDbCql.GetEconomicCalendarByMonthV1All)
            .ExecuteStreamAsync(MapToEconomicCalendar!, cancellationToken))
        {
            economicCalendarRowsProjected++;
            economicCalendarProjectedIdentity.Add(GetEconomicCalendarProjectionIdentity(row));
        }
        var projectedCountryCodes = await db.Use(MarketDataDbCql.GetEconomicCalendarCountryCodeV1All)
            .ExecuteQueryAsync(MapToEconomicCalendarCountryCode, cancellationToken);
        var projectedMonths = await db.Use(MarketDataDbCql.GetEconomicCalendarMonthV1All)
            .ExecuteQueryAsync(MapToEconomicCalendarMonth, cancellationToken);
        var projectedYieldCurveYears = await db.Use(MarketDataDbCql.GetYieldCurveRateYearV1All)
            .ExecuteQueryAsync(MapToYearMonth, cancellationToken);
        long yieldCurveRowsProjected = 0;
        var yieldCurveProjectedIdentity = new ProjectionIdentityBuilder();
        await foreach (var row in db.Use(MarketDataDbCql.GetYieldCurveRateByDateV1All)
            .ExecuteStreamAsync(MapToYieldCurveRate!, cancellationToken))
        {
            yieldCurveRowsProjected++;
            yieldCurveProjectedIdentity.Add(GetYieldCurveProjectionIdentity(row));
        }
        var calendarSource = economicCalendarSourceIdentity.Build();
        var calendarProjected = economicCalendarProjectedIdentity.Build();
        var countryCodesSource = BuildStringSetIdentity(countryCodes);
        var countryCodesProjected = BuildStringSetIdentity(
            projectedCountryCodes.Select(static row => row.CountryCode));
        var monthsSource = BuildIntegerSetIdentity(monthBuckets);
        var monthsProjected = BuildIntegerSetIdentity(projectedMonths);
        var yieldYearsSource = BuildIntegerSetIdentity(yieldCurveYears);
        var yieldYearsProjected = BuildIntegerSetIdentity(projectedYieldCurveYears);
        var yieldCurveSource = yieldCurveSourceIdentity.Build();
        var yieldCurveProjected = yieldCurveProjectedIdentity.Build();

        return new FmpQueryProjectionBackfillResult(
            economicCalendarRowsSource,
            economicCalendarRowsProjected,
            calendarSource.Fingerprint,
            calendarProjected.Fingerprint,
            countryCodes.Count,
            projectedCountryCodes.Count,
            countryCodesSource.Fingerprint,
            countryCodesProjected.Fingerprint,
            monthBuckets.Count,
            projectedMonths.Count,
            monthsSource.Fingerprint,
            monthsProjected.Fingerprint,
            yieldCurveRowsSource,
            yieldCurveRowsProjected,
            yieldCurveSource.Fingerprint,
            yieldCurveProjected.Fingerprint,
            yieldCurveYears.Count,
            projectedYieldCurveYears.Count,
            yieldYearsSource.Fingerprint,
            yieldYearsProjected.Fingerprint);

        async Task FlushCalendarBatchAsync()
        {
            if (calendarBatch.Count == 0)
                return;
            await db.Use(MarketDataDbCql.InsertEconomicCalendarByMonthV1)
                .SetParameters(calendarBatch)
                .ExecuteCommandAsync(cancellationToken);
            calendarBatch.Clear();
        }

        async Task FlushYieldCurveBatchAsync()
        {
            if (yieldCurveBatch.Count == 0)
                return;
            await db.Use(MarketDataDbCql.InsertYieldCurveRateByDateV1)
                .SetParameters(yieldCurveBatch)
                .ExecuteCommandAsync(cancellationToken);
            yieldCurveBatch.Clear();
        }
    }

    static ulong GetEconomicCalendarProjectionIdentity(EconomicCalendarReadModel row)
    {
        var hash = MarketDataProjectionHash.Start();
        hash = MarketDataProjectionHash.Add(hash, row.EventDate.Ticks);
        hash = MarketDataProjectionHash.Add(hash, row.CountryCode);
        hash = MarketDataProjectionHash.Add(hash, row.EventName);
        hash = MarketDataProjectionHash.Add(hash, row.Actual);
        hash = MarketDataProjectionHash.Add(hash, row.Forecast);
        hash = MarketDataProjectionHash.Add(hash, row.Prior);
        hash = MarketDataProjectionHash.Add(hash, row.Impact);
        hash = MarketDataProjectionHash.Add(hash, row.Unit);
        hash = MarketDataProjectionHash.Add(hash, row.Change);
        hash = MarketDataProjectionHash.Add(hash, row.ChangePercentage);
        hash = MarketDataProjectionHash.Add(hash, row.CreatedOn.Ticks);
        return MarketDataProjectionHash.Add(hash, row.CreatedBy);
    }

    static ulong GetYieldCurveProjectionIdentity(YieldCurveRateReadModel row)
    {
        var hash = MarketDataProjectionHash.Add(MarketDataProjectionHash.Start(), row.ValueDate);
        hash = MarketDataProjectionHash.Add(hash, row.OneMonth);
        hash = MarketDataProjectionHash.Add(hash, row.TwoMonth);
        hash = MarketDataProjectionHash.Add(hash, row.ThreeMonth);
        hash = MarketDataProjectionHash.Add(hash, row.SixMonth);
        hash = MarketDataProjectionHash.Add(hash, row.OneYear);
        hash = MarketDataProjectionHash.Add(hash, row.TwoYear);
        hash = MarketDataProjectionHash.Add(hash, row.ThreeYear);
        hash = MarketDataProjectionHash.Add(hash, row.FiveYear);
        hash = MarketDataProjectionHash.Add(hash, row.SevenYear);
        hash = MarketDataProjectionHash.Add(hash, row.TenYear);
        hash = MarketDataProjectionHash.Add(hash, row.TwentyYear);
        return MarketDataProjectionHash.Add(hash, row.ThirtyYear);
    }

    static ProjectionIdentity BuildStringSetIdentity(IEnumerable<string> values)
    {
        var identity = new ProjectionIdentityBuilder();
        foreach (var value in values)
            identity.Add(MarketDataProjectionHash.Add(MarketDataProjectionHash.Start(), value));
        return identity.Build();
    }

    static ProjectionIdentity BuildIntegerSetIdentity(IEnumerable<int> values)
    {
        var identity = new ProjectionIdentityBuilder();
        foreach (var value in values)
            identity.Add(MarketDataProjectionHash.Add(MarketDataProjectionHash.Start(), value));
        return identity.Build();
    }

    async Task<ICollection<EconomicCalendarReadModel>> ReadEconomicCalendarMonthsAsync(
        IEnumerable<int> monthBuckets,
        Func<int, Task<ICollection<EconomicCalendarReadModel>>> queryMonthAsync,
        CancellationToken cancellationToken)
    {
        var rows = new List<EconomicCalendarReadModel>();
        foreach (var batch in monthBuckets.Chunk(EconomicCalendarMaximumConcurrentQueries))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var remaining = EconomicCalendarMaximumRows - rows.Count;
            if (remaining <= 0)
                break;

            var pages = await Task.WhenAll(batch.Select(queryMonthAsync));
            foreach (var page in pages)
            {
                var rowsToAdd = Math.Min(page.Count, EconomicCalendarMaximumRows - rows.Count);
                if (rowsToAdd <= 0)
                    break;
                rows.AddRange(page.Take(rowsToAdd));
            }
        }

        return [.. rows
            .OrderByDescending(static row => row.EventDate)
            .ThenBy(static row => row.CountryCode, StringComparer.Ordinal)
            .ThenBy(static row => row.EventName, StringComparer.Ordinal)];
    }
}
