using System.Collections.Immutable;
using TomasAI.IFM.Shared.MarketData.Contracts;

namespace TomasAI.IFM.Application.MarketData.Pricing;

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
            throw new ArgumentException("TreasuryPublicationCoverageUnavailable",nameof(at));
        var required = Publications.LastOrDefault(x => x.AvailableByUtc <= at);
        return required?.ValueDate ?? throw new ArgumentException("TreasuryPublicationCoverageUnavailable",nameof(at));
    }

    public DateTimeOffset ValidUntil(DateTimeOffset at) =>
        Publications.FirstOrDefault(x => x.AvailableByUtc > at)?.AvailableByUtc ?? CoverageUntilUtc;
}
