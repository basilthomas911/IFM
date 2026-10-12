using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.OptionVolatility;

/// <summary>Publishes real, expiry-specific ATM call/put IV observations outside chain request handling.
/// This display series never substitutes for the separately configured constant-tenor workflow series.</summary>
public sealed class LiveOptionChainIvCollector(IOptionVolatilityRepository database, IHostEnvironment environment,
    ILogger<LiveOptionChainIvCollector> logger) : BackgroundService, IOptionChainIvCollector
{
    const string Method = "expiry-atm-call-put-midpoint-v1";
    const string Policy = "prior-252-weekdays-minimum-20-v1";
    readonly Channel<(EvaluatedOptionChainReadModel Chain, DateOnly Date)> pending =
        Channel.CreateBounded<(EvaluatedOptionChainReadModel, DateOnly)>(new BoundedChannelOptions(64)
        { SingleReader = true, FullMode = BoundedChannelFullMode.DropOldest });

    /// <summary>Offers a current chain without waiting for persistence; frozen or invalid IVs are excluded during qualification.</summary>
    public void Observe(EvaluatedOptionChainReadModel chain, DateOnly valueDate) => pending.Writer.TryWrite((chain, valueDate));
    static VolatilitySeriesIdentity Series(EvaluatedOptionChainReadModel chain) =>
        new($"{chain.UnderlyingContractId}:expiry:{chain.ExpiryDate:yyyyMMdd}", Method);
    VolatilityStorageScope Scope(EvaluatedOptionChainReadModel chain) => new(environment.EnvironmentName, Series(chain), Policy);
    public Task<LatestVolatilityResult> GetLatestAsync(EvaluatedOptionChainReadModel chain, CancellationToken token) =>
        database.GetLatestAsync(new(Scope(chain), DateTimeOffset.UtcNow, TimeSpan.FromMinutes(6)), token);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var item in pending.Reader.ReadAllAsync(stoppingToken).ConfigureAwait(false))
        {
            try { await PublishAsync(item.Chain, item.Date, stoppingToken).ConfigureAwait(false); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception) { logger.LogError(exception, "IV observation dropped for {ContractId} expiry {Expiry}", item.Chain.UnderlyingContractId, item.Chain.ExpiryDate); }
        }
    }

    /// <summary>ATM IV = arithmetic mean of fresh qualified call and put IV at the same nearest strike.</summary>
    public static EvaluatedOptionContractReadModel[] SelectAtmPair(EvaluatedOptionChainReadModel chain)
    {
        if (chain.UnderlyingPrice is not > 0 || chain.WindowMethod.Contains("Frozen", StringComparison.OrdinalIgnoreCase)) return [];
        return chain.Contracts.Where(x => !x.IsStale && x.GreeksValid && x.Bid > 0 && x.Ask >= x.Bid
            && x.ImpliedVolatility is > 0 && double.IsFinite(x.ImpliedVolatility.Value)
            && x.QuoteAtUtc is { } at && at <= chain.AsOfUtc && chain.AsOfUtc - at <= TimeSpan.FromSeconds(60))
            .GroupBy(x => x.Strike).Where(g => g.Any(x => x.IsCall) && g.Any(x => !x.IsCall))
            .OrderBy(g => Math.Abs(g.Key - chain.UnderlyingPrice.Value)).ThenBy(g => g.Key)
            .Select(g => new[] { g.First(x => x.IsCall), g.First(x => !x.IsCall) }).FirstOrDefault() ?? [];
    }

    public async Task PublishAsync(EvaluatedOptionChainReadModel chain, DateOnly date, CancellationToken token = default)
    {
        var pair = SelectAtmPair(chain);
        if (pair.Length != 2 || date == default) return;
        var latest = (await GetLatestAsync(chain, token).ConfigureAwait(false)).Metric;
        if (latest is not null && latest.Snapshot.ExchangeValueDate == date
            && chain.AsOfUtc - latest.Snapshot.ObservedAtUtc < TimeSpan.FromMinutes(5)) return;
        var scope = Scope(chain);
        var dates = new List<DateOnly>();
        for (var d = date.AddDays(-1); dates.Count < 252; d = d.AddDays(-1))
            if (d.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)) dates.Add(d);
        var sources = new List<OptionIvObservation>();
        for (var month = new DateOnly(dates.Min().Year, dates.Min().Month, 1); month <= date; month = month.AddMonths(1))
        {
            byte[]? paging = null;
            do
            {
                var page = await database.GetObservationHistoryAsync(new(scope, VolatilityCalendarBucket.From(month),
                    month < dates.Min() ? dates.Min() : month, month.AddMonths(1).AddDays(-1) > date ? date : month.AddMonths(1).AddDays(-1), "Daily", VolatilityHistoricalMode.Restated, null, 100, paging), token).ConfigureAwait(false);
                sources.AddRange(page.Items); paging = page.PagingState;
            } while (paging is { Length: > 0 });
        }
        var prior = sources.Where(x => x.ExchangeValueDate == date).MaxBy(x => x.Revision);
        sources = sources.Where(x => x.ExchangeValueDate < date).GroupBy(x => x.ExchangeValueDate)
            .Select(g => g.MaxBy(x => x.Revision)!).ToList();
        var now = DateTimeOffset.UtcNow;
        var id = Hash($"{scope}:{date:yyyyMMdd}:{chain.AsOfUtc:O}:{pair[0].ContractId}:{pair[1].ContractId}:{pair[0].ImpliedVolatility:R}:{pair[1].ImpliedVolatility:R}");
        var iv = ((decimal)pair[0].ImpliedVolatility!.Value + (decimal)pair[1].ImpliedVolatility!.Value) / 2m;
        var provenance = new VolatilityObservationProvenance(pair.Select(x => new VolatilityContributor(x.ContractId,
            chain.UnderlyingContractId, x.QuoteAtUtc!.Value, chain.AsOfUtc, 0, 0, Guid.Empty)).ToImmutableArray(),
            "qualified-composition-iv", id, Method);
        var observation = new OptionIvObservation(1, id, scope.Series, date, "Daily", iv,
            VolatilityValueUnit.AnnualDecimal, VolatilityObservationStatus.Qualified, "", pair.Max(x => x.QuoteAtUtc!.Value),
            now, now, (prior?.Revision ?? 0) + 1, prior?.ObservationId, provenance);
        var policy = new VolatilityMetricPolicy(Policy, 252, 20, 20m / 252m, VolatilityGapPolicy.PreserveExpectedSessionGap,
            VolatilityHistoricalWindowConvention.PriorExchangeSessions, VolatilityRankRangeConvention.CurrentAndPriorWindow,
            VolatilityPercentileTieConvention.StrictlyLessThanCurrent);
        var result = VolatilityRankPercentileCalculator.Calculate(policy, new(date, dates.ToImmutableArray()),
            VolatilityCalculationObservation.From(observation), sources.Select(VolatilityCalculationObservation.From));
        sources.Add(observation);
        var digest = Hash(string.Join('|', sources.Select(x => x.ObservationId).Order(StringComparer.Ordinal)));
        var snapshot = new OptionIvMetricSnapshot(1, Hash(id + digest), Hash(id + digest + Policy), scope.Series, Policy,
            date, "Daily", iv, result.ImpliedVolatilityUnit, result.IvRank, result.RankStatus, result.IvPercentile,
            result.PercentileStatus, result.MetricUnit, result.RankLowImpliedVolatility, result.RankHighImpliedVolatility,
            result.HistoricalBelowCurrentCount, result.HistoricalTieCount, result.ValidHistoricalObservationCount,
            result.ExpectedHistoricalObservationCount, result.CoverageRatio, result.WindowStartValueDate, result.WindowEndValueDate,
            sources.Select(x => x.ObservationId).Order(StringComparer.Ordinal).ToImmutableArray(), digest, Method,
            observation.ObservedAtUtc, now, now, now);
        // Source rows, snapshot/history and latest pointer commit together in the existing PostgreSQL publication transaction.
        await database.PublishAsync(new(environment.EnvironmentName, new(snapshot, 1, null,
            Math.Max(now.UtcTicks, (latest?.PublicationSequence ?? 0) + 1)), sources.ToImmutableArray()), token).ConfigureAwait(false);
        logger.LogInformation("Published IV observation {ObservationId} for {ContractId} expiry {Expiry}: IV {IV}, rank {Rank}, status {Status}, history {Count}",
            id, chain.UnderlyingContractId, chain.ExpiryDate, iv, result.IvRank, result.RankStatus, result.ValidHistoricalObservationCount);
    }
    static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
