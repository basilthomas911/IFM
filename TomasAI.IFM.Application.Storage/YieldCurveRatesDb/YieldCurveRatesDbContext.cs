using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.Contracts;
using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.YieldCurveRatesDb;

/// <summary>
/// Compatibility facade for callers of the former external-URI repository.
/// Acquisition is owned by the provider-neutral Treasury contract.
/// </summary>
public sealed class YieldCurveRatesDbContext(
    ITreasuryCurve treasuryCurve,
    ExternalMarketDataCompatibilityOptions options,
    TimeProvider timeProvider,
    ILogger<DbProvider> logger)
    : ObjectDataRepository<YieldCurveRatesDbContext>(null!, logger), IYieldCurveRatesDbContext
{
    private readonly ITreasuryCurve _treasuryCurve = treasuryCurve
        ?? throw new ArgumentNullException(nameof(treasuryCurve));
    private readonly ExternalMarketDataCompatibilityOptions _options = Validate(options);
    private readonly TimeProvider _timeProvider = timeProvider
        ?? throw new ArgumentNullException(nameof(timeProvider));

    public override YieldCurveRatesDbContext Database => this;

    public Task<ICollection<YieldCurveRateReadModel>> ReadAsync() =>
        ReadAsync(CancellationToken.None);

    public Task<ICollection<YieldCurveRateReadModel>> ReadAsync(
        CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);
        return ReadAsync(
            today.AddDays(-_options.TreasuryLookbackDays + 1),
            today,
            cancellationToken);
    }

    public async Task<ICollection<YieldCurveRateReadModel>> ReadAsync(
        DateOnly fromInclusive,
        DateOnly toInclusive,
        CancellationToken cancellationToken = default)
    {
        var snapshots = await _treasuryCurve
            .GetRangeAsync(fromInclusive, toInclusive, cancellationToken)
            .ConfigureAwait(false);

        return snapshots.Select(Map).ToArray();
    }

    private static YieldCurveRateReadModel Map(TreasuryCurveSnapshot snapshot) =>
        new(
            snapshot.ValueDate,
            Rate(snapshot, TreasuryTenor.OneMonth),
            Rate(snapshot, TreasuryTenor.TwoMonth),
            Rate(snapshot, TreasuryTenor.ThreeMonth),
            Rate(snapshot, TreasuryTenor.SixMonth),
            Rate(snapshot, TreasuryTenor.OneYear),
            Rate(snapshot, TreasuryTenor.TwoYear),
            Rate(snapshot, TreasuryTenor.ThreeYear),
            Rate(snapshot, TreasuryTenor.FiveYear),
            Rate(snapshot, TreasuryTenor.SevenYear),
            Rate(snapshot, TreasuryTenor.TenYear),
            Rate(snapshot, TreasuryTenor.TwentyYear),
            Rate(snapshot, TreasuryTenor.ThirtyYear));

    private static double Rate(TreasuryCurveSnapshot snapshot, TreasuryTenor tenor)
    {
        if (!snapshot.TryGetRate(tenor, out var point))
        {
            throw new InvalidOperationException(
                $"Treasury curve {snapshot.ValueDate:yyyy-MM-dd} is missing required tenor {tenor}.");
        }

        return decimal.ToDouble(point.RatePercent);
    }

    private static ExternalMarketDataCompatibilityOptions Validate(
        ExternalMarketDataCompatibilityOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        return options;
    }
}
