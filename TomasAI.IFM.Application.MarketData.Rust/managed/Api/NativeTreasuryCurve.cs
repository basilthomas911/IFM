using TomasAI.IFM.Application.MarketData.Rust.Delivery;
using TomasAI.IFM.Framework.MarketData.ReferenceData;
using TomasAI.IFM.Shared.MarketData.Contracts;

namespace TomasAI.IFM.Application.MarketData.Rust.Api;

/// <summary>Existing Treasury service contract over native official-source I/O,
/// parsing, cache and numerical conversion. Application source qualification,
/// decimal units and semantic hashing preserve the existing policy contract.</summary>
public sealed class NativeTreasuryCurve : ITreasuryCurve,ITreasuryCurveIdentity
{
    readonly NativeMarketDataRuntime runtime;
    readonly TimeSpan budget;
    /// <summary>Uses a caller-selected query budget, at most two minutes. A
    /// recovery caller must also pass its original deadline cancellation token;
    /// this adapter cannot renew or extend that outer deadline.</summary>
    public NativeTreasuryCurve(NativeMarketDataRuntime runtime,TimeSpan queryBudget)
    {
        this.runtime=runtime??throw new ArgumentNullException(nameof(runtime));
        if(queryBudget<TimeSpan.Zero||queryBudget>TimeSpan.FromMinutes(2))throw new ArgumentOutOfRangeException(nameof(queryBudget));budget=queryBudget;
    }
    /// <inheritdoc/>
    public string DownloadLogProvider=>UsTreasuryCurve.Source;
    /// <inheritdoc/>
    public Task<TreasuryCurveSnapshot?> GetLatestAsync(DateOnly asOfDate,CancellationToken cancellationToken=default)
        =>runtime.ReadTreasuryCurveAsync(asOfDate,budget,cancellationToken);
    /// <inheritdoc/>
    public async Task<IReadOnlyList<TreasuryCurveSnapshot>> GetRangeAsync(DateOnly fromInclusive,DateOnly toInclusive,CancellationToken cancellationToken=default)
        =>await runtime.ReadTreasuryCurvesAsync(fromInclusive,toInclusive,budget,cancellationToken).ConfigureAwait(false);
    /// <inheritdoc/>
    public TreasuryContinuousRateResult GetContinuouslyCompoundedAnnualRate(TreasuryCurveSnapshot snapshot,TreasuryTenor tenor,TreasuryRateConversionPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if(policy!=UsTreasuryCurve.ConversionPolicy||snapshot.Source!=UsTreasuryCurve.Source)return TreasuryContinuousRateResult.Failed("TreasuryConventionUnverified");
        if(snapshot.CountryCode!="US"||snapshot.CurrencyCode!="USD")return TreasuryContinuousRateResult.Failed("RateConventionUnsupported");
        if(snapshot.ValueDate==default||snapshot.RetrievedAtUtc.Offset!=TimeSpan.Zero||snapshot.Rates is null||snapshot.Rates.Count is <1 or >32
            ||snapshot.Rates.Any(x=>!Enum.IsDefined(x.Tenor))||snapshot.Rates.Select(x=>x.Tenor).Distinct().Count()!=snapshot.Rates.Count)
            return TreasuryContinuousRateResult.Failed("TreasuryInvalid");
        if(tenor is not(TreasuryTenor.OneMonth or TreasuryTenor.TwoMonth or TreasuryTenor.ThreeMonth))return TreasuryContinuousRateResult.Failed("TreasuryHorizonUnsupported");
        if(!snapshot.TryGetRate(tenor,out var point))return TreasuryContinuousRateResult.Failed("TreasuryTenorMissing");
        var half=(double)(point.RatePercent/200m);if(half<=-1)return TreasuryContinuousRateResult.Failed("RateConversionDomainInvalid");
        var rate=runtime.ConvertTreasuryHalfYield(half);if(!double.IsFinite(rate))return TreasuryContinuousRateResult.Failed("RateConversionDomainInvalid");
        return new(new(tenor,point.RatePercent,rate,snapshot.ValueDate,snapshot.RetrievedAtUtc,TreasuryRateConversion.Digest(snapshot),policy,"FlatSelectedCmtProxy/v1"),null);
    }
}
