using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.Exceptions;
using TomasAI.IFM.Domain.MarketData.Shared.QueryParameters;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Framework.Storage.Extensions;

namespace TomasAI.IFM.Application.Storage.MarketDataDb;

public partial class MarketDataDbContext
{
    internal const int EconomicCalendarMaximumRangeMonths = EconomicCalendarQueryLimits.MaximumRangeMonths;
    internal const int EconomicCalendarMaximumRows = 10_000;
    internal const int EconomicCalendarMaximumRowsPerMonth = EconomicCalendarQueryLimits.MaximumRowsPerPartition;
    internal const int EconomicCalendarMaximumConcurrentQueries = 4;
    const int EconomicCalendarLookupId = 1;
    const int EconomicCalendarCutoverId = 1;

    static EconomicCalendarReadModel MapToEconomicCalendar(IObjectDataRecord row) => new()
    {
        EventDate = NormalizeEconomicCalendarTimestamp(row.GetDateTime(0)),
        CountryCode = row.GetString(1),
        EventName = row.GetString(2),
        Actual = GetNullableString(row, 3), Forecast = GetNullableString(row, 4), Prior = GetNullableString(row, 5),
        Impact = GetNullableString(row, 6), Unit = GetNullableString(row, 7),
        Change = GetNullableString(row, 8), ChangePercentage = GetNullableString(row, 9),
        CreatedOn = row.GetDateTime(10), CreatedBy = row.GetString(11)
    };

    static string? GetNullableString(IObjectDataRecord row, int index)
        => row.IsNull(index) ? null : row.GetString(index);

    static EconomicCalendarCountryCodeReadModel MapToEconomicCalendarCountryCode(IObjectDataRecord row)
        => new(row.GetString(0));

    static DateTime NormalizeEconomicCalendarTimestamp(DateTime value)
    {
        var utc = ProjectionMutationSafety.AsUtc(value);
        return new DateTime(utc.Ticks - (utc.Ticks % TimeSpan.TicksPerMillisecond), DateTimeKind.Utc);
    }

    static int EconomicCalendarMonthBucket(DateTime value) => value.Year * 100 + value.Month;

    static IEnumerable<int> EconomicCalendarMonthBucketsDescending(DateTime startDate, DateTime endDate)
    {
        var firstMonth = new DateTime(startDate.Year, startDate.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        for (var month = new DateTime(endDate.Year, endDate.Month, 1, 0, 0, 0, DateTimeKind.Utc);
             month >= firstMonth; month = month.AddMonths(-1))
            yield return EconomicCalendarMonthBucket(month);
    }

    public Task<EconomicCalendarReadModel?> GetEconomicCalendarAsync(EconomicCalendarId id)
        => GetEconomicCalendarAsync(id, CancellationToken.None);

    public async Task<EconomicCalendarReadModel?> GetEconomicCalendarAsync(
        EconomicCalendarId id, CancellationToken cancellationToken)
    {
        var eventDate = NormalizeEconomicCalendarTimestamp(id.EventDate);
        return await _dbFactory.MarketDataDb.Use(MarketDataDbCql.GetEconomicCalendarV2ById)
            .SetParameters(new GetEconomicCalendarV2ById(
                id.CountryCode, EconomicCalendarMonthBucket(eventDate), eventDate, id.EventName))
            .ExecuteSingleAsync(MapToEconomicCalendar!, cancellationToken);
    }

    public Task<ICollection<EconomicCalendarReadModel>> GetEconomicCalendarsAsync(DateTime eventDate, string countryCode)
        => GetEconomicCalendarsAsync(eventDate, countryCode, CancellationToken.None);

    public Task<ICollection<EconomicCalendarReadModel>> GetEconomicCalendarsAsync(
        DateTime eventDate, string countryCode, CancellationToken cancellationToken)
    {
        var startDate = eventDate.Date;
        var endDate = startDate == DateTime.MaxValue.Date ? DateTime.MaxValue : startDate.AddDays(1).AddTicks(-1);
        return GetEconomicCalendarsAsync(startDate, endDate, countryCode, cancellationToken);
    }

    public Task<ICollection<EconomicCalendarReadModel>> GetEconomicCalendarsAsync(
        DateTime startDate, DateTime endDate, string countryCode)
        => GetEconomicCalendarsAsync(startDate, endDate, countryCode, CancellationToken.None);

    public async Task<ICollection<EconomicCalendarReadModel>> GetEconomicCalendarsAsync(
        DateTime startDate, DateTime endDate, string countryCode, CancellationToken cancellationToken)
    {
        startDate = NormalizeEconomicCalendarTimestamp(startDate);
        endDate = NormalizeEconomicCalendarTimestamp(endDate);
        if (endDate < startDate) return [];
        var request = new EconomicCalendarPageRequest
        {
            StartDateUtc = startDate,
            EndDateUtc = endDate,
            CountryCodes = [countryCode],
            PageSize = EconomicCalendarQueryLimits.MaximumPageSize
        };
        var rows = new List<EconomicCalendarReadModel>();
        do
        {
            var page = await GetEconomicCalendarPageAsync(request, cancellationToken).ConfigureAwait(false);
            rows.AddRange(page.Items);
            if (!page.HasMore || rows.Count >= EconomicCalendarMaximumRows) break;
            request = request with { ContinuationToken = page.ContinuationToken };
        } while (true);
        return [.. rows.Take(EconomicCalendarMaximumRows)];
    }

    public async Task<EconomicCalendarPageReadModel> GetEconomicCalendarPageAsync(
        EconomicCalendarPageRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();
        var countries = request.CountryCodes
            .Select(static code => code.Trim().ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var partitions = EconomicCalendarMonthBucketsDescending(request.StartDateUtc, request.EndDateUtc)
            .SelectMany(month => countries.Select(country => new CalendarPartition(country, month)))
            .ToArray();
        var fingerprint = GetPageRequestFingerprint(request, countries);
        var cursor = DecodePageToken(request.ContinuationToken, fingerprint, partitions.Length);
        var rows = new List<EconomicCalendarReadModel>(request.PageSize);

        for (var partitionIndex = cursor.PartitionIndex; partitionIndex < partitions.Length; partitionIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var partition = partitions[partitionIndex];
            var partitionRows = await _dbFactory.MarketDataDb.Use(MarketDataDbCql.GetEconomicCalendars)
                .SetParameters(new GetEconomicCalendars(
                    partition.CountryCode, partition.MonthBucket, request.StartDateUtc, request.EndDateUtc))
                .ExecuteQueryAsync(MapToEconomicCalendar!, cancellationToken)
                .ConfigureAwait(false);
            if (partitionRows.Count > EconomicCalendarMaximumRowsPerMonth)
                throw new InvalidOperationException(
                    $"Economic-calendar partition '{partition.CountryCode}/{partition.MonthBucket}' exceeds the configured row bound.");

            var available = partitionRows
                .OrderByDescending(static row => row.EventDate)
                .ThenBy(static row => row.EventName, StringComparer.Ordinal)
                .Where(row => partitionIndex != cursor.PartitionIndex || IsAfterCursor(row, cursor))
                .ToArray();
            var take = Math.Min(request.PageSize - rows.Count, available.Length);
            rows.AddRange(available.Take(take));
            if (rows.Count == request.PageSize)
            {
                var last = rows[^1];
                var hasMore = take < available.Length || partitionIndex + 1 < partitions.Length;
                return new EconomicCalendarPageReadModel
                {
                    Items = [.. rows],
                    ContinuationToken = hasMore
                        ? EncodePageToken(new CalendarPageToken(
                            fingerprint, partitionIndex, last.EventDate.Ticks, last.EventName))
                        : null
                };
            }
            cursor = new CalendarPageToken(fingerprint, partitionIndex + 1, null, null);
        }
        return new EconomicCalendarPageReadModel { Items = [.. rows] };
    }

    [Obsolete("Use GetEconomicCalendarPageAsync with explicit UTC bounds and country codes.")]
    public Task<ICollection<EconomicCalendarReadModel>> GetEconomicCalendarAllAsync()
        => GetEconomicCalendarAllAsync(CancellationToken.None);

    [Obsolete("Use GetEconomicCalendarPageAsync with explicit UTC bounds and country codes.")]
    public async Task<ICollection<EconomicCalendarReadModel>> GetEconomicCalendarAllAsync(CancellationToken cancellationToken)
    {
        var countries = await GetEconomicCalendarCountryCodesAsync(cancellationToken).ConfigureAwait(false);
        if (countries.Count == 0) return [];
        var now = DateTime.UtcNow;
        var start = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-107);
        var end = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(13).AddTicks(-1);
        var request = new EconomicCalendarPageRequest
        {
            StartDateUtc = start,
            EndDateUtc = end,
            CountryCodes = [.. countries
                .Take(EconomicCalendarQueryLimits.MaximumPartitions / EconomicCalendarQueryLimits.MaximumRangeMonths)
                .Select(static row => row.CountryCode)],
            PageSize = EconomicCalendarQueryLimits.MaximumPageSize
        };
        var rows = new List<EconomicCalendarReadModel>();
        do
        {
            var page = await GetEconomicCalendarPageAsync(request, cancellationToken).ConfigureAwait(false);
            rows.AddRange(page.Items);
            if (!page.HasMore || rows.Count >= EconomicCalendarMaximumRows) break;
            request = request with { ContinuationToken = page.ContinuationToken };
        } while (true);
        return [.. rows.Take(EconomicCalendarMaximumRows)];
    }

    public Task<ICollection<EconomicCalendarCountryCodeReadModel>> GetEconomicCalendarCountryCodesAsync()
        => GetEconomicCalendarCountryCodesAsync(CancellationToken.None);

    public async Task<ICollection<EconomicCalendarCountryCodeReadModel>> GetEconomicCalendarCountryCodesAsync(
        CancellationToken cancellationToken)
        => await _dbFactory.MarketDataDb.Use(MarketDataDbCql.GetEconomicCalendarCountryCodes)
            .SetParameters(new GetEconomicCalendarCountryCodes(EconomicCalendarLookupId))
            .ExecuteQueryAsync(MapToEconomicCalendarCountryCode, cancellationToken);

    public async Task DeleteEconomicCalendarAsync(EconomicCalendarId id)
    {
        var eventDate = NormalizeEconomicCalendarTimestamp(id.EventDate);
        await _dbFactory.MarketDataDb.Use(MarketDataDbCql.DeleteEconomicCalendarV2)
            .SetParameters(new DeleteEconomicCalendarV2(
                id.CountryCode, EconomicCalendarMonthBucket(eventDate), eventDate, id.EventName))
            .ExecuteCommandAsync();
    }

    public Task InsertEconomicCalendarAsync(EconomicCalendarReadModel economicCalendar)
        => InsertEconomicCalendarsAsync([economicCalendar]);

    public Task InsertEconomicCalendarsAsync(EconomicCalendarReadModel[] economicCalendars)
        => InsertEconomicCalendarsAsync(economicCalendars, ImportDuplicatePolicy.Overwrite, Guid.Empty);

    public async Task InsertEconomicCalendarsAsync(
        EconomicCalendarReadModel[] economicCalendars,
        ImportDuplicatePolicy duplicatePolicy,
        Guid commandId)
    {
        ArgumentNullException.ThrowIfNull(economicCalendars);
        if (economicCalendars.Length == 0) return;
        ValidateImportPolicy(duplicatePolicy, commandId);
        var db = _dbFactory.MarketDataDb;
        var commands = new List<object>(economicCalendars.Length * 2);
        var countryCodes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in economicCalendars)
        {
            var eventDate = NormalizeEconomicCalendarTimestamp(row.EventDate);
            var parameters = new InsertEconomicCalendarV2(
                row.CountryCode, EconomicCalendarMonthBucket(eventDate), eventDate, row.EventName,
                row.Actual, row.Forecast, row.Prior, row.Impact, row.Unit, row.Change,
                row.ChangePercentage, row.CreatedOn, row.CreatedBy, commandId);
            if (duplicatePolicy == ImportDuplicatePolicy.Reject)
            {
                var applied = await db.Use(MarketDataDbCql.InsertEconomicCalendarV2IfNotExists)
                    .SetParameters(parameters)
                    .ExecuteScalarAsync(MapToBoolean!);
                if (!applied)
                {
                    var owner = await db.Use(MarketDataDbCql.GetEconomicCalendarV2CommandId)
                        .SetParameters(new GetEconomicCalendarV2CommandId(
                            row.CountryCode, EconomicCalendarMonthBucket(eventDate), eventDate, row.EventName))
                        .ExecuteSingleAsync(MapToGuid!);
                    if (owner != commandId)
                        throw new MarketDataImportDuplicateException(
                            $"An economic-calendar row with logical key '{eventDate:O}|{row.CountryCode}|{row.EventName}' already exists.");
                }
            }
            else
            {
                commands.Add(db.Use(MarketDataDbCql.InsertEconomicCalendarV2)
                    .SetParameters(parameters).QueueCommand());
            }
            countryCodes.Add(row.CountryCode);
        }
        commands.AddRange(countryCodes.Select(countryCode => db
            .Use(MarketDataDbCql.InsertEconomicCalendarCountryCode)
            .SetParameters(new InsertEconomicCalendarCountryCode(EconomicCalendarLookupId, countryCode))
            .QueueCommand()));
        if (commands.Count > 0) await db.ExecuteQueuedCommandsAsync(commands);
    }

    public async Task UpdateEconomicCalendarAsync(EconomicCalendarId id, EconomicCalendarReadModel economicCalendar)
    {
        await DeleteEconomicCalendarAsync(id);
        await InsertEconomicCalendarAsync(economicCalendar);
    }

    public async Task<EconomicCalendarCutoverResult> BackfillEconomicCalendarV2Async(
        int batchSize = 256, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);
        var db = _dbFactory.MarketDataDb;
        await db.Use(MarketDataDbCql.TruncateEconomicCalendarV2)
            .ExecuteCommandAsync(cancellationToken);
        await db.Use(MarketDataDbCql.TruncateEconomicCalendarCountryCode)
            .ExecuteCommandAsync(cancellationToken);
        long sourceRows = 0;
        var sourceIdentity = new ProjectionIdentityBuilder();
        var countries = new HashSet<string>(StringComparer.Ordinal);
        var batch = new List<InsertEconomicCalendarV2>(batchSize);
        await foreach (var row in db.Use(MarketDataDbCql.GetEconomicCalendarLegacySource)
            .ExecuteStreamAsync(MapToEconomicCalendar!, cancellationToken))
        {
            sourceRows++;
            sourceIdentity.Add(GetEconomicCalendarProjectionIdentity(row));
            countries.Add(row.CountryCode);
            var eventDate = NormalizeEconomicCalendarTimestamp(row.EventDate);
            batch.Add(new InsertEconomicCalendarV2(
                row.CountryCode, EconomicCalendarMonthBucket(eventDate), eventDate, row.EventName,
                row.Actual, row.Forecast, row.Prior, row.Impact, row.Unit, row.Change,
                row.ChangePercentage, row.CreatedOn, row.CreatedBy, Guid.Empty));
            if (batch.Count == batchSize) await FlushCalendarBatchAsync();
        }
        await FlushCalendarBatchAsync();
        if (countries.Count > 0)
        {
            await db.Use(MarketDataDbCql.InsertEconomicCalendarCountryCode)
                .SetParameters(countries.Select(country =>
                    new InsertEconomicCalendarCountryCode(EconomicCalendarLookupId, country)))
                .ExecuteCommandAsync(cancellationToken);
        }
        long targetRows = 0;
        var targetIdentity = new ProjectionIdentityBuilder();
        await foreach (var row in db.Use(MarketDataDbCql.GetEconomicCalendarV2All)
            .ExecuteStreamAsync(MapToEconomicCalendar!, cancellationToken))
        {
            targetRows++;
            targetIdentity.Add(GetEconomicCalendarProjectionIdentity(row));
        }
        var source = sourceIdentity.Build();
        var target = targetIdentity.Build();
        var verified = sourceRows == targetRows && source.Fingerprint == target.Fingerprint;
        await db.Use(MarketDataDbCql.UpsertEconomicCalendarCutoverV2)
            .SetParameters(new UpsertEconomicCalendarCutoverV2(
                EconomicCalendarCutoverId, sourceRows, targetRows, source.Fingerprint,
                target.Fingerprint, verified, DateTime.UtcNow))
            .ExecuteCommandAsync(cancellationToken);
        return new EconomicCalendarCutoverResult(
            sourceRows, targetRows, source.Fingerprint, target.Fingerprint, countries.Count, verified);

        async Task FlushCalendarBatchAsync()
        {
            if (batch.Count == 0) return;
            await db.Use(MarketDataDbCql.InsertEconomicCalendarV2)
                .SetParameters(batch).ExecuteCommandAsync(cancellationToken);
            batch.Clear();
        }
    }

    public async Task<FmpQueryProjectionBackfillResult> BackfillFmpQueryProjectionsAsync(
        int batchSize = 256, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);
        var db = _dbFactory.MarketDataDb;
        await db.Use(MarketDataDbCql.TruncateYieldCurveRateByDate).ExecuteCommandAsync(cancellationToken);
        await db.Use(MarketDataDbCql.TruncateYieldCurveRateYear).ExecuteCommandAsync(cancellationToken);
        long sourceRows = 0;
        var sourceIdentity = new ProjectionIdentityBuilder();
        var years = new HashSet<int>();
        var batch = new List<InsertYieldCurveRate>(batchSize);
        await foreach (var row in db.Use(MarketDataDbCql.GetYieldCurveRateProjectionSource)
            .ExecuteStreamAsync(MapToYieldCurveRate!, cancellationToken))
        {
            sourceRows++;
            sourceIdentity.Add(GetYieldCurveProjectionIdentity(row));
            years.Add(row.ValueDate.Year);
            batch.Add(new InsertYieldCurveRate(
                YieldCurveLookupId, row.ValueDate, row.OneMonth, row.TwoMonth, row.ThreeMonth,
                row.SixMonth, row.OneYear, row.TwoYear, row.ThreeYear, row.FiveYear,
                row.SevenYear, row.TenYear, row.TwentyYear, row.ThirtyYear));
            if (batch.Count == batchSize) await FlushYieldBatchAsync();
        }
        await FlushYieldBatchAsync();
        if (years.Count > 0)
        {
            await db.Use(MarketDataDbCql.InsertYieldCurveRateYear)
                .SetParameters(years.Select(year => new InsertYieldCurveRateYear(YieldCurveLookupId, year)))
                .ExecuteCommandAsync(cancellationToken);
        }
        long targetRows = 0;
        var targetIdentity = new ProjectionIdentityBuilder();
        await foreach (var row in db.Use(MarketDataDbCql.GetYieldCurveRateByDateAll)
            .ExecuteStreamAsync(MapToYieldCurveRate!, cancellationToken))
        {
            targetRows++;
            targetIdentity.Add(GetYieldCurveProjectionIdentity(row));
        }
        var projectedYears = await db.Use(MarketDataDbCql.GetYieldCurveRateYearAll)
            .ExecuteQueryAsync(MapToYearMonth, cancellationToken);
        var source = sourceIdentity.Build();
        var target = targetIdentity.Build();
        var sourceYears = BuildIntegerSetIdentity(years);
        var targetYears = BuildIntegerSetIdentity(projectedYears);
        return new FmpQueryProjectionBackfillResult(
            sourceRows, targetRows, source.Fingerprint, target.Fingerprint,
            years.Count, projectedYears.Count, sourceYears.Fingerprint, targetYears.Fingerprint);

        async Task FlushYieldBatchAsync()
        {
            if (batch.Count == 0) return;
            await db.Use(MarketDataDbCql.InsertYieldCurveRateByDate)
                .SetParameters(batch).ExecuteCommandAsync(cancellationToken);
            batch.Clear();
        }
    }

    static bool IsAfterCursor(EconomicCalendarReadModel row, CalendarPageToken cursor)
        => cursor.LastEventDateTicks is null
            || row.EventDate.Ticks < cursor.LastEventDateTicks.Value
            || (row.EventDate.Ticks == cursor.LastEventDateTicks.Value
                && string.CompareOrdinal(row.EventName, cursor.LastEventName) > 0);

    static string GetPageRequestFingerprint(EconomicCalendarPageRequest request, string[] countries)
    {
        var identity = $"v1|{request.StartDateUtc.Ticks}|{request.EndDateUtc.Ticks}|{string.Join(',', countries)}|{request.PageSize}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
    }

    static string EncodePageToken(CalendarPageToken token)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(token);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    static CalendarPageToken DecodePageToken(string? token, string fingerprint, int partitionCount)
    {
        if (string.IsNullOrEmpty(token)) return new CalendarPageToken(fingerprint, 0, null, null);
        try
        {
            var value = token.Replace('-', '+').Replace('_', '/');
            value = value.PadRight(value.Length + ((4 - value.Length % 4) % 4), '=');
            var cursor = JsonSerializer.Deserialize<CalendarPageToken>(Convert.FromBase64String(value))
                ?? throw new FormatException();
            if (!string.Equals(cursor.Fingerprint, fingerprint, StringComparison.Ordinal)
                || cursor.PartitionIndex < 0 || cursor.PartitionIndex > partitionCount
                || cursor.LastEventName?.Any(char.IsControl) == true)
                throw new FormatException();
            return cursor;
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            throw new ArgumentException(
                "The economic-calendar continuation token is invalid for this request.", nameof(token), ex);
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

    static ProjectionIdentity BuildIntegerSetIdentity(IEnumerable<int> values)
    {
        var identity = new ProjectionIdentityBuilder();
        foreach (var value in values)
            identity.Add(MarketDataProjectionHash.Add(MarketDataProjectionHash.Start(), value));
        return identity.Build();
    }

    readonly record struct CalendarPartition(string CountryCode, int MonthBucket);
    sealed record CalendarPageToken(
        string Fingerprint, int PartitionIndex, long? LastEventDateTicks, string? LastEventName);
}
