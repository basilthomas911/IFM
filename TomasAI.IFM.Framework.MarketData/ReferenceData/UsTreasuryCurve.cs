using System.Collections.Immutable;
using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using TomasAI.IFM.Framework.MarketData.Contracts;

namespace TomasAI.IFM.Framework.MarketData.ReferenceData;

/// <summary>Official daily Treasury par/CMT observations. Missing values are never synthesized.</summary>
public sealed class UsTreasuryCurve(HttpClient client, TimeProvider? timeProvider = null)
    : ITreasuryCurve, ITreasuryCurveIdentity, IDisposable
{
    public const string Source = "USTreasury";
    public const string Series = "daily_treasury_yield_curve";
    public const string Endpoint = "https://home.treasury.gov/resource-center/data-chart-center/interest-rates/pages/xml";
    public const string ConventionEvidence = "https://home.treasury.gov/policy-issues/financing-the-government/interest-rate-statistics/interest-rates-frequently-asked-questions";
    public static TreasuryRateConversionPolicy ConversionPolicy { get; } = new(Source, Series,
        TreasuryRateConvention.UsTreasuryCmtNominalSemiannual, "USTreasury-ParCmt-Semiannual/v1", ConventionEvidence);
    static readonly XNamespace Atom = "http://www.w3.org/2005/Atom";
    static readonly XNamespace Data = "http://schemas.microsoft.com/ado/2007/08/dataservices";
    static readonly XNamespace Meta = "http://schemas.microsoft.com/ado/2007/08/dataservices/metadata";
    static readonly (string Name, TreasuryTenor Tenor)[] Columns =
    [
        ("BC_1MONTH", TreasuryTenor.OneMonth), ("BC_2MONTH", TreasuryTenor.TwoMonth),
        ("BC_3MONTH", TreasuryTenor.ThreeMonth), ("BC_6MONTH", TreasuryTenor.SixMonth),
        ("BC_1YEAR", TreasuryTenor.OneYear), ("BC_2YEAR", TreasuryTenor.TwoYear),
        ("BC_3YEAR", TreasuryTenor.ThreeYear), ("BC_5YEAR", TreasuryTenor.FiveYear),
        ("BC_7YEAR", TreasuryTenor.SevenYear), ("BC_10YEAR", TreasuryTenor.TenYear),
        ("BC_20YEAR", TreasuryTenor.TwentyYear), ("BC_30YEAR", TreasuryTenor.ThirtyYear)
    ];
    readonly TimeProvider clock = timeProvider ?? TimeProvider.System;
    readonly SemaphoreSlim gate = new(1, 1);
    readonly Dictionary<DateOnly, (DateTimeOffset Expires, ImmutableArray<TreasuryCurveSnapshot> Rows)> months = new();
    public string DownloadLogProvider => Source;

    /// <inheritdoc/>
    public TreasuryContinuousRateResult GetContinuouslyCompoundedAnnualRate(
        TreasuryCurveSnapshot snapshot, TreasuryTenor tenor, TreasuryRateConversionPolicy policy)
        => policy == ConversionPolicy && snapshot.Source == Source
            ? TreasuryRateConversion.Convert(snapshot, tenor, policy)
            : TreasuryContinuousRateResult.Failed("TreasuryConventionUnverified");

    /// <inheritdoc/>
    public async Task<TreasuryCurveSnapshot?> GetLatestAsync(DateOnly asOfDate, CancellationToken cancellationToken = default)
    {
        var from = DateOnly.FromDayNumber(Math.Max(new DateOnly(1990, 1, 1).DayNumber, asOfDate.DayNumber - 13));
        var rows = await GetRangeAsync(from, asOfDate, cancellationToken).ConfigureAwait(false);
        return rows.LastOrDefault();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<TreasuryCurveSnapshot>> GetRangeAsync(DateOnly fromInclusive, DateOnly toInclusive,
        CancellationToken cancellationToken = default)
    {
        if (fromInclusive.Year < 1990 || toInclusive < fromInclusive || toInclusive.DayNumber - fromInclusive.DayNumber >= 366)
            throw new ArgumentOutOfRangeException(nameof(fromInclusive), "Treasury requests require 1–366 days from 1990 onward.");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30), clock);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        await gate.WaitAsync(linked.Token).ConfigureAwait(false);
        try
        {
            var result = ImmutableArray.CreateBuilder<TreasuryCurveSnapshot>();
            var lastMonth = new DateOnly(toInclusive.Year, toInclusive.Month, 1);
            for (var month = new DateOnly(fromInclusive.Year, fromInclusive.Month, 1); ; month = month.AddMonths(1))
            {
                linked.Token.ThrowIfCancellationRequested();
                if (!months.TryGetValue(month, out var entry) || entry.Expires <= clock.GetUtcNow())
                {
                    var uri = $"{Endpoint}?data={Series}&field_tdr_date_value_month={month.ToString("yyyyMM", CultureInfo.InvariantCulture)}";
                    using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, linked.Token).ConfigureAwait(false);
                    response.EnsureSuccessStatusCode();
                    await response.Content.LoadIntoBufferAsync(1_048_576, linked.Token).ConfigureAwait(false);
                    await using var stream = await response.Content.ReadAsStreamAsync(linked.Token).ConfigureAwait(false);
                    using var reader = XmlReader.Create(stream, new XmlReaderSettings
                    {
                        Async = true, DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null,
                        MaxCharactersInDocument = 1_048_576
                    });
                    var document = await XDocument.LoadAsync(reader, LoadOptions.None, linked.Token).ConfigureAwait(false);
                    var retrieved = clock.GetUtcNow();
                    entry = (retrieved.AddMinutes(1), Parse(document, month, retrieved));
                    // Keep the cache bounded, including on-demand historical imports. Never retimestamp cache hits.
                    if (!months.ContainsKey(month) && months.Count >= 2)
                        months.Remove(months.MinBy(x => x.Value.Expires).Key);
                    months[month] = entry;
                }
                result.AddRange(entry.Rows.Where(x => x.ValueDate >= fromInclusive && x.ValueDate <= toInclusive));
                if (month == lastMonth) break;
            }
            return result.ToImmutable();
        }
        finally { gate.Release(); }
    }

    static ImmutableArray<TreasuryCurveSnapshot> Parse(XDocument document, DateOnly month, DateTimeOffset retrieved)
    {
        if (document.Root?.Name != Atom + "feed") throw new InvalidDataException("Expected Treasury Atom feed.");
        if (document.Root.Elements(Atom + "link").Any(x => (string?)x.Attribute("rel") == "next"))
            throw new InvalidDataException("Unexpected truncated monthly Treasury feed.");
        var rows = new SortedDictionary<DateOnly, TreasuryCurveSnapshot>();
        var entries = document.Root.Elements(Atom + "entry").ToArray();
        if (entries.Length > 31) throw new InvalidDataException("Monthly Treasury response exceeds its date bound.");
        foreach (var entry in entries)
        {
            var properties = entry.Element(Atom + "content")?.Element(Meta + "properties")
                ?? throw new InvalidDataException("Treasury entry has no properties.");
            if (properties.Elements().GroupBy(x => x.Name).Any(x => x.Count() != 1))
                throw new InvalidDataException("Duplicate Treasury field.");
            if (!DateTime.TryParseExact((string?)properties.Element(Data + "NEW_DATE"),
                    "yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
                || parsed.TimeOfDay != TimeSpan.Zero || parsed.Year != month.Year || parsed.Month != month.Month)
                throw new InvalidDataException("Treasury value date is missing or outside the requested month.");
            var points = ImmutableArray.CreateBuilder<TreasuryRatePoint>();
            foreach (var (name, tenor) in Columns)
            {
                var field = properties.Element(Data + name);
                if (field is null || (string?)field.Attribute(Meta + "null") == "true" || string.IsNullOrWhiteSpace(field.Value)) continue;
                if (!decimal.TryParse(field.Value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                        CultureInfo.InvariantCulture, out var rate) || rate <= -200m || rate > 1000m)
                    throw new InvalidDataException($"Invalid Treasury rate: {name}.");
                points.Add(new(tenor, rate));
            }
            var date = DateOnly.FromDateTime(parsed);
            var snapshot = new TreasuryCurveSnapshot(date, points.ToImmutable(), retrieved, Source);
            if (rows.TryGetValue(date, out var prior) && !prior.Rates.SequenceEqual(snapshot.Rates))
                throw new InvalidDataException("Conflicting Treasury observations for the same value date.");
            rows[date] = snapshot;
        }
        return rows.Values.ToImmutableArray();
    }

    public void Dispose() => gate.Dispose();
}
