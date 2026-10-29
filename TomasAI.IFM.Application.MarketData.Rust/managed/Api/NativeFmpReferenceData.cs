using TomasAI.IFM.Application.MarketData.Rust.Delivery;
using TomasAI.IFM.Framework.MarketData.FinancialModelingPrep;
using TomasAI.IFM.Framework.MarketData.ReferenceData;
using TomasAI.IFM.Shared.MarketData.Contracts;
namespace TomasAI.IFM.Application.MarketData.Rust.Api;

/// <summary>Existing Treasury/calendar contracts over the native FMP owner. Both
/// services share the same native admissions, retry and circuit state. Construction
/// snapshots query settings; later mutation of the options cannot change a live owner.</summary>
public sealed class NativeFmpReferenceData : ITreasuryCurve,ITreasuryCurveIdentity,IEconomicCalendar
{
    readonly NativeMarketDataRuntime runtime;
    readonly TimeSpan budget;
    readonly int maximumResponseBytes;
    /// <summary>Uses an already-configured native owner and the same configuration
    /// supplied at configuration. No managed HTTP provider is constructed.</summary>
    public NativeFmpReferenceData(NativeMarketDataRuntime runtime,FinancialModelingPrepOptions options)
    {
        this.runtime=runtime??throw new ArgumentNullException(nameof(runtime));ArgumentNullException.ThrowIfNull(options);
        if(options.TotalOperationTimeout<=TimeSpan.Zero||options.TotalOperationTimeout>TimeSpan.FromMinutes(2))throw new FinancialModelingPrepConfigurationException("Native reference queries require a positive budget no greater than two minutes.");
        budget=options.TotalOperationTimeout;maximumResponseBytes=options.MaximumResponseBytes;
    }
    /// <inheritdoc/>
    public string DownloadLogProvider=>"FMP";
    /// <inheritdoc/>
    public async Task<TreasuryCurveSnapshot?> GetLatestAsync(DateOnly asOfDate,CancellationToken cancellationToken=default)
        =>(await runtime.ReadFmpTreasuryAsync(asOfDate,asOfDate,true,budget,maximumResponseBytes,cancellationToken).ConfigureAwait(false)).FirstOrDefault();
    /// <inheritdoc/>
    public async Task<IReadOnlyList<TreasuryCurveSnapshot>> GetRangeAsync(DateOnly fromInclusive,DateOnly toInclusive,CancellationToken cancellationToken=default)
        =>await runtime.ReadFmpTreasuryAsync(fromInclusive,toInclusive,false,budget,maximumResponseBytes,cancellationToken).ConfigureAwait(false);
    /// <inheritdoc/>
    public async Task<IReadOnlyList<EconomicCalendarEntry>> GetAsync(DateOnly fromInclusive,DateOnly toInclusive,IReadOnlySet<string>? countryCodes=null,CancellationToken cancellationToken=default)
        =>await runtime.ReadFmpCalendarAsync(fromInclusive,toInclusive,countryCodes,budget,maximumResponseBytes,cancellationToken).ConfigureAwait(false);
    /// <inheritdoc/>
    public TreasuryContinuousRateResult GetContinuouslyCompoundedAnnualRate(TreasuryCurveSnapshot snapshot,TreasuryTenor tenor,TreasuryRateConversionPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(snapshot);ArgumentNullException.ThrowIfNull(policy);
        if(policy.Convention!=TreasuryRateConvention.UsTreasuryCmtNominalSemiannual||string.IsNullOrWhiteSpace(policy.SourceSeriesId)||string.IsNullOrWhiteSpace(policy.Version)||string.IsNullOrWhiteSpace(policy.EvidenceId)||policy.Source!=snapshot.Source||snapshot.CountryCode!="US"||snapshot.CurrencyCode!="USD")return TreasuryContinuousRateResult.Failed("RateConventionUnsupported");
        if(snapshot.ValueDate==default||snapshot.RetrievedAtUtc.Offset!=TimeSpan.Zero||snapshot.Rates is null||snapshot.Rates.Count is <1 or >32||snapshot.Rates.Any(x=>!Enum.IsDefined(x.Tenor))||snapshot.Rates.Select(x=>x.Tenor).Distinct().Count()!=snapshot.Rates.Count)return TreasuryContinuousRateResult.Failed("TreasuryInvalid");
        if(tenor is not(TreasuryTenor.OneMonth or TreasuryTenor.TwoMonth or TreasuryTenor.ThreeMonth))return TreasuryContinuousRateResult.Failed("TreasuryHorizonUnsupported");
        if(!snapshot.TryGetRate(tenor,out var point))return TreasuryContinuousRateResult.Failed("TreasuryTenorMissing");
        var half=(double)(point.RatePercent/200m);if(half<=-1)return TreasuryContinuousRateResult.Failed("RateConversionDomainInvalid");
        var rate=runtime.ConvertTreasuryHalfYield(half);if(!double.IsFinite(rate))return TreasuryContinuousRateResult.Failed("RateConversionDomainInvalid");
        return new(new(tenor,point.RatePercent,rate,snapshot.ValueDate,snapshot.RetrievedAtUtc,TreasuryRateConversion.Digest(snapshot),policy,"FlatSelectedCmtProxy/v1"),null);
    }
}
