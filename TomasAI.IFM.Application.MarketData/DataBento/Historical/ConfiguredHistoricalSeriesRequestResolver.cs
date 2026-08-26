using System.Security.Cryptography;
using System.Text;
using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Framework.MarketData.Contracts.Historical;

namespace TomasAI.IFM.Application.MarketData.Databento.Historical;

/// <summary>Resolves configured domain identities to provider datasets and symbols.</summary>
public sealed class ConfiguredHistoricalSeriesRequestResolver : IHistoricalSeriesRequestResolver
{
    readonly IReadOnlyDictionary<string, DatabentoHistoricalSeriesProfile> _profiles;
    readonly IMarketSessionCalendar _calendar;

    /// <summary>Initializes the configured resolver.</summary>
    public ConfiguredHistoricalSeriesRequestResolver(
        DatabentoHistoricalOptions options,
        IMarketSessionCalendar calendar)
    {
        ArgumentNullException.ThrowIfNull(options);
        _calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
        _profiles = options.SeriesProfiles.ToDictionary(
            x => x.MarketSeriesIdentity,
            StringComparer.Ordinal);
    }

    /// <inheritdoc/>
    public HistoricalProviderRequest Resolve(
        MarketDataHistoricalRequest request,
        MarketDataHistoricalSeriesRequest series)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(series);
        if (!_profiles.TryGetValue(series.SeriesIdentity.Format(), out var profile))
            throw new KeyNotFoundException($"No historical profile exists for {series.SeriesIdentity.Format()}.");
        var start = _calendar.GetSession(request.StartDate).StartUtc;
        var end = _calendar.GetSession(request.EndDate).EndUtc;
        var canonical = string.Join('|',
            profile.Dataset,
            string.Join(',', profile.Symbols.Order(StringComparer.Ordinal)),
            series.Schema,
            profile.Symbology,
            start.ToString("O"),
            end.ToString("O"),
            request.NormalizationVersion);
        return new HistoricalProviderRequest
        {
            Dataset = profile.Dataset,
            Symbols = [.. profile.Symbols],
            Schema = series.Schema,
            Symbology = profile.Symbology,
            StartUtc = start,
            EndUtc = end,
            RequestHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))
        };
    }

    /// <inheritdoc/>
    public string ResolveContractId(
        MarketDataHistoricalSeriesRequest series,
        HistoricalProviderRecord record)
    {
        if (!string.IsNullOrWhiteSpace(series.ContractId)) return series.ContractId;
        if (_profiles.TryGetValue(series.SeriesIdentity.Format(), out var profile)
            && !string.IsNullOrWhiteSpace(profile.ContractId))
            return profile.ContractId;
        if (!string.IsNullOrWhiteSpace(record.Symbol)) return record.Symbol;
        throw new InvalidOperationException("The provider record cannot be mapped to a domain contract.");
    }
}
