using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TomasAI.IFM.Framework.MarketData.Contracts;

namespace TomasAI.IFM.Framework.MarketData.ReferenceData;

/// <summary>Pure conversion of an observed Treasury snapshot. Never performs network I/O.</summary>
public static class TreasuryRateConversion
{
    /// <summary>Selects the approved tenor from remaining exchange trading dates, not calendar DTE.</summary>
    public static TreasuryTenor? SelectTenor(int remainingTradingDays) => remainingTradingDays switch
    {
        >= 0 and < 30 => TreasuryTenor.OneMonth,
        >= 30 and < 60 => TreasuryTenor.TwoMonth,
        >= 60 and < 90 => TreasuryTenor.ThreeMonth,
        _ => null
    };

    /// <summary>Converts verified semiannual CMT percent units once and retains a canonical curve digest.</summary>
    public static TreasuryContinuousRateResult Convert(
        TreasuryCurveSnapshot snapshot, TreasuryTenor tenor, TreasuryRateConversionPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(policy);
        if (policy.Convention != TreasuryRateConvention.UsTreasuryCmtNominalSemiannual
            || string.IsNullOrWhiteSpace(policy.SourceSeriesId) || string.IsNullOrWhiteSpace(policy.Version)
            || string.IsNullOrWhiteSpace(policy.EvidenceId) || policy.Source != snapshot.Source
            || snapshot.CountryCode != "US" || snapshot.CurrencyCode != "USD")
            return TreasuryContinuousRateResult.Failed("RateConventionUnsupported");
        if (snapshot.ValueDate == default || snapshot.RetrievedAtUtc.Offset != TimeSpan.Zero
            || snapshot.Rates is null || snapshot.Rates.Count is < 1 or > 32
            || snapshot.Rates.Any(x => !Enum.IsDefined(x.Tenor))
            || snapshot.Rates.Select(x => x.Tenor).Distinct().Count() != snapshot.Rates.Count)
            return TreasuryContinuousRateResult.Failed("TreasuryInvalid");
        if (tenor is not (TreasuryTenor.OneMonth or TreasuryTenor.TwoMonth or TreasuryTenor.ThreeMonth))
            return TreasuryContinuousRateResult.Failed("TreasuryHorizonUnsupported");
        if (!snapshot.TryGetRate(tenor, out var point))
            return TreasuryContinuousRateResult.Failed("TreasuryTenorMissing");
        double halfYield = (double)(point.RatePercent / 200m);
        if (halfYield <= -1)
            return TreasuryContinuousRateResult.Failed("RateConversionDomainInvalid");
        double rate = 2 * double.LogP1(halfYield);
        if (!double.IsFinite(rate))
            return TreasuryContinuousRateResult.Failed("RateConversionDomainInvalid");
        return new(new(tenor, point.RatePercent, rate, snapshot.ValueDate,
            snapshot.RetrievedAtUtc, Digest(snapshot), policy, "FlatSelectedCmtProxy/v1"), null);
    }

    /// <summary>Semantic v1 digest; source corrections change identity while retrieval times do not.</summary>
    public static string Digest(TreasuryCurveSnapshot snapshot)
    {
        var canonical = JsonSerializer.Serialize(new
        {
            Version = 1, snapshot.Source, snapshot.CountryCode, snapshot.CurrencyCode,
            ValueDate = snapshot.ValueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            Rates = snapshot.Rates.OrderBy(x => x.Tenor).Select(x => new
            {
                Tenor = (int)x.Tenor, Rate = x.RatePercent.ToString("G29", CultureInfo.InvariantCulture)
            })
        });
        return ConvertDigest(canonical);
    }

    private static string ConvertDigest(string value) =>
        System.Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
