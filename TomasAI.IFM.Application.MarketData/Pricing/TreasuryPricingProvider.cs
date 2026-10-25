using System.Collections.Immutable;
using TomasAI.IFM.Shared.MarketData.Contracts;

namespace TomasAI.IFM.Application.MarketData.Pricing;
/// <summary>Asynchronous bounded daily refresh; no network operations occur in pricing/enrichment.</summary>
public sealed class TreasuryPricingProvider : IDisposable
{
    readonly ITreasuryCurve source;
    readonly TimeProvider clock;
    readonly TimeSpan refreshInterval;
    readonly TimeSpan fetchTimeout;
    readonly SemaphoreSlim refresh = new(1, 1);
    TreasuryCurveSnapshot? cached;
    DateTimeOffset retryAfter;
    public TreasuryPricingProvider(ITreasuryCurve source, TimeProvider? clock = null, TimeSpan? refreshInterval = null, TimeSpan? fetchTimeout = null)
    {
        this.source = source ?? throw new ArgumentNullException(nameof(source));
        this.clock = clock ?? TimeProvider.System;
        this.refreshInterval = refreshInterval ?? TimeSpan.FromMinutes(1);
        this.fetchTimeout = fetchTimeout ?? TimeSpan.FromSeconds(10);
        if (this.refreshInterval <= TimeSpan.Zero || this.fetchTimeout <= TimeSpan.Zero || this.fetchTimeout > TimeSpan.FromMinutes(1))
            throw new ArgumentOutOfRangeException(nameof(fetchTimeout));
    }

    /// <summary>Returns only observations available by valuation; outages can reuse a still-qualified cache.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051", Justification = "Preserve native shutdown and containment ownership/failure ordering in this compatibility pass.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "The service boundary returns a classified unavailable or failed result and records diagnostics instead of aborting its supervisor.")]
    public async Task<TreasuryPricingResult> GetAsync(DateTimeOffset at, int tradingDays, TreasuryPublicationPolicy publication, TreasuryRateConversionPolicy conversion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(publication);
        var tenor = Framework.MarketData.ReferenceData.TreasuryRateConversion.SelectTenor(tradingDays);
        if (tenor is null)
            return new(null, default, tradingDays < 0 ? "InvalidTradingDayCount" : "TreasuryHorizonUnsupported");
        DateOnly required, valuationDate;
        try
        {
            required = publication.RequiredValueDate(at);
            valuationDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(at, TimeZoneInfo.FindSystemTimeZoneById(publication.TimeZoneId)).DateTime);
        }
        catch (Exception e)when (e is ArgumentException or TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return new(null, default, "TreasuryPublicationCoverageUnavailable");
        }

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
                            fresh = fresh with
                            {
                                Rates = fresh.Rates.ToImmutableArray()
                            };
                            if (fresh.ValueDate <= valuationDate // A refresh may finish after this request's frozen valuation. Retain it for
                            // the next pass, but the admission check below forbids look-ahead in this one.
                            && fresh.RetrievedAtUtc <= clock.GetUtcNow() && fresh.RetrievedAtUtc.Offset == TimeSpan.Zero && (cached is null || fresh.ValueDate > cached.ValueDate || fresh.ValueDate == cached.ValueDate && fresh.RetrievedAtUtc >= cached.RetrievedAtUtc))
                                cached = fresh;
                        }
                    }
                    catch (OperationCanceledException)when (!cancellationToken.IsCancellationRequested)
                    { /* Cancellation or optional observation must not abort the enclosing shutdown/recovery boundary. */ }
                    catch (Exception)when (!cancellationToken.IsCancellationRequested)
                    { /* Existing cache still requires admission below. */
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();
                if (cached is null)
                    return new(null, default, "TreasuryUnavailable");
                if (cached.ValueDate < required || cached.ValueDate > valuationDate || cached.RetrievedAtUtc > at)
                    return new(null, default, "TreasuryStale");
                var result = source.GetContinuouslyCompoundedAnnualRate(cached, tenor.Value, conversion);
                return new(result.Value, publication.ValidUntil(at), result.Error);
            }
            finally
            {
                refresh.Release();
            }
        }
        catch (OperationCanceledException)when (!cancellationToken.IsCancellationRequested)
        {
            return new(null, default, "TreasuryUnavailable");
        }
    }

    private int disposedGateOwner;
    /// <summary>Releases the owned synchronization gate after the host has stopped its operations.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposedGateOwner, 1) != 0)
            return;
        refresh.Dispose();
        GC.SuppressFinalize(this);
    }
}