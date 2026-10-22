using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Framework.MarketData.Contracts;
namespace TomasAI.IFM.Application.MarketData.Remote;
/// <summary>Compatibility facade; all external reference acquisition uses the single backend API.</summary>
public sealed class RemoteReferenceDataApi(IMarketDataApi api) : IReferenceDataApi, ITreasuryCurve, IEconomicCalendar, ITreasuryCurveIdentity
{
    public string DownloadLogProvider => "USTreasury";
    public ITreasuryCurve TreasuryCurve => this;
    public IEconomicCalendar EconomicCalendar => this;
    public TreasuryContinuousRateResult GetContinuouslyCompoundedAnnualRate(TreasuryCurveSnapshot snapshot, TreasuryTenor tenor, TreasuryRateConversionPolicy policy)
        => Framework.MarketData.ReferenceData.TreasuryRateConversion.Convert(snapshot, tenor, policy);
    public Task<TreasuryCurveSnapshot?> GetLatestAsync(DateOnly asOfDate, CancellationToken cancellationToken = default)
        => api.ReadTreasuryCurveAsync(asOfDate, cancellationToken);
    public async Task<IReadOnlyList<TreasuryCurveSnapshot>> GetRangeAsync(DateOnly fromInclusive, DateOnly toInclusive, CancellationToken cancellationToken = default)
        => await api.ReadTreasuryCurvesAsync(fromInclusive, toInclusive, cancellationToken).ConfigureAwait(false);
    public async Task<IReadOnlyList<EconomicCalendarEntry>> GetAsync(DateOnly fromInclusive, DateOnly toInclusive, IReadOnlySet<string>? countryCodes = null, CancellationToken cancellationToken = default)
        => await api.ReadEconomicCalendarAsync(fromInclusive, toInclusive, countryCodes?.ToArray(), cancellationToken).ConfigureAwait(false);
}
