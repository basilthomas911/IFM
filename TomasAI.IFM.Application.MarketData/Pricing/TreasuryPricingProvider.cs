using System.Collections.Immutable;
using TomasAI.IFM.Framework.MarketData.Contracts;

namespace TomasAI.IFM.Application.MarketData.Pricing;

/// <summary>One reviewed expected publication deadline, including the configured provider allowance.</summary>
public sealed record TreasuryPublicationDate(DateOnly ValueDate, DateTimeOffset AvailableByUtc);

/// <summary>Complete publication schedule for a bounded period; distinct from the exchange trading calendar.</summary>
public sealed record TreasuryPublicationPolicy(string Version, DateTimeOffset CoverageFromUtc,
    DateTimeOffset CoverageUntilUtc, ImmutableArray<TreasuryPublicationDate> Publications,
    string TimeZoneId = "America/New_York")
{
    public DateOnly RequiredValueDate(DateTimeOffset at)
    {
        if (string.IsNullOrWhiteSpace(Version) || at.Offset != TimeSpan.Zero
            || CoverageFromUtc.Offset != TimeSpan.Zero || CoverageUntilUtc.Offset != TimeSpan.Zero
            || at < CoverageFromUtc || at >= CoverageUntilUtc || Publications.IsDefaultOrEmpty
            || Publications.Length > 1000 || Publications.Any(x => x.ValueDate == default || x.AvailableByUtc.Offset != TimeSpan.Zero)
            || !Publications.Select(x => x.ValueDate).SequenceEqual(Publications.Select(x => x.ValueDate).Distinct().Order())
            || !Publications.Select(x => x.AvailableByUtc).SequenceEqual(Publications.Select(x => x.AvailableByUtc).Distinct().Order()))
            throw new ArgumentException("TreasuryPublicationCoverageUnavailable");
        var required = Publications.LastOrDefault(x => x.AvailableByUtc <= at);
        return required?.ValueDate ?? throw new ArgumentException("TreasuryPublicationCoverageUnavailable");
    }

    public DateTimeOffset ValidUntil(DateTimeOffset at) =>
        Publications.FirstOrDefault(x => x.AvailableByUtc > at)?.AvailableByUtc ?? CoverageUntilUtc;
}

/// <summary>Validated daily observation and its publication-validity boundary.</summary>
public sealed record TreasuryPricingResult(TreasuryContinuousRate? Rate, DateTimeOffset ValidUntilUtc, string? Error)
{
    public bool Succeeded => Rate is not null && Error is null;
}

/// <summary>Asynchronous bounded daily refresh; no network operations occur in pricing/enrichment.</summary>
public sealed class TreasuryPricingProvider
{
    readonly ITreasuryCurve source;
    readonly TimeProvider clock;
    readonly TimeSpan refreshInterval;
    readonly TimeSpan fetchTimeout;
    readonly SemaphoreSlim refresh = new(1, 1);
    TreasuryCurveSnapshot? cached;
    DateTimeOffset retryAfter;

    public TreasuryPricingProvider(ITreasuryCurve source, TimeProvider? clock = null,
        TimeSpan? refreshInterval = null, TimeSpan? fetchTimeout = null)
    {
        this.source = source ?? throw new ArgumentNullException(nameof(source));
        this.clock = clock ?? TimeProvider.System;
        this.refreshInterval = refreshInterval ?? TimeSpan.FromMinutes(1);
        this.fetchTimeout = fetchTimeout ?? TimeSpan.FromSeconds(10);
        if (this.refreshInterval <= TimeSpan.Zero || this.fetchTimeout <= TimeSpan.Zero
            || this.fetchTimeout > TimeSpan.FromMinutes(1)) throw new ArgumentOutOfRangeException(nameof(fetchTimeout));
    }

    /// <summary>Returns only observations available by valuation; outages can reuse a still-qualified cache.</summary>
    public async Task<TreasuryPricingResult> GetAsync(DateTimeOffset at, int tradingDays,
        TreasuryPublicationPolicy publication, TreasuryRateConversionPolicy conversion, CancellationToken cancellationToken)
    {
        var tenor = Framework.MarketData.ReferenceData.TreasuryRateConversion.SelectTenor(tradingDays);
        if (tenor is null) return new(null, default, tradingDays < 0 ? "InvalidTradingDayCount" : "TreasuryHorizonUnsupported");
        DateOnly required, valuationDate;
        try
        {
            required = publication.RequiredValueDate(at);
            valuationDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(at, TimeZoneInfo.FindSystemTimeZoneById(publication.TimeZoneId)).DateTime);
        }
        catch (Exception e) when (e is ArgumentException or TimeZoneNotFoundException or InvalidTimeZoneException)
        { return new(null, default, "TreasuryPublicationCoverageUnavailable"); }
        using var timeout = new CancellationTokenSource(fetchTimeout, clock);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token, cancellationToken);
        try
        {
            await refresh.WaitAsync(linked.Token).ConfigureAwait(false);
            try
            {
                if (clock.GetUtcNow() >= retryAfter)
                {
                    retryAfter = clock.GetUtcNow() + refreshInterval;
                    try
                    {
                        // Historical valuations must never read tomorrow's curve.
                        var fresh = await source.GetLatestAsync(valuationDate, linked.Token).ConfigureAwait(false);
                        if (fresh is not null && fresh.Rates is not null)
                        {
                            fresh = fresh with { Rates = fresh.Rates.ToImmutableArray() };
                            if (fresh.ValueDate <= valuationDate
                                // A refresh may finish after this request's frozen valuation. Retain it for
                                // the next pass, but the admission check below forbids look-ahead in this one.
                                && fresh.RetrievedAtUtc <= clock.GetUtcNow() && fresh.RetrievedAtUtc.Offset == TimeSpan.Zero
                                && (cached is null || fresh.ValueDate > cached.ValueDate
                                    || fresh.ValueDate == cached.ValueDate && fresh.RetrievedAtUtc >= cached.RetrievedAtUtc)) cached = fresh;
                        }
                    }
                    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { }
                    catch (Exception) when (!cancellationToken.IsCancellationRequested) { /* Existing cache still requires admission below. */ }
                }
                cancellationToken.ThrowIfCancellationRequested();
                if (cached is null) return new(null, default, "TreasuryUnavailable");
                if (cached.ValueDate < required || cached.ValueDate > valuationDate
                    || cached.RetrievedAtUtc > at) return new(null, default, "TreasuryStale");
                var result = source.GetContinuouslyCompoundedAnnualRate(cached, tenor.Value, conversion);
                return new(result.Value, publication.ValidUntil(at), result.Error);
            }
            finally { refresh.Release(); }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new(null, default, "TreasuryUnavailable");
        }
    }
}
