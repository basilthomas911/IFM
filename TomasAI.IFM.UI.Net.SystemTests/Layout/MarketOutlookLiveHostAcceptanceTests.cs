using FluentAssertions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.UI.Net.SystemTests.Infrastructure;

namespace TomasAI.IFM.UI.Net.SystemTests.Layout;

[Trait("Category", "MarketOutlookLiveHost")]
public sealed class MarketOutlookLiveHostAcceptanceTests
{
    [Fact]
    public async Task WarmupAndConsecutiveEsTrades_AdvanceEveryDisplayedDailyProjection()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("IFM_RUN_MOSC_LIVE"),
                "1",
                StringComparison.Ordinal))
            return;

        var natsUri = new Uri(Environment.GetEnvironmentVariable("IFM_NATS_URL")
                              ?? "nats://localhost:4222");
        var contractId = Environment.GetEnvironmentVariable("IFM_MOSC_ES_CONTRACT")
                         ?? "ES20260918";
        var valueDate = DateOnly.TryParse(
            Environment.GetEnvironmentVariable("IFM_MOSC_VALUE_DATE"),
            out var configuredDate)
                ? configuredDate
                : new DateOnly(2026, 9, 1);

        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(8));
        await using var session = new G0QuerySession(natsUri);
        await session.StartAsync(Guid.NewGuid().ToString("N"), timeout.Token, "MOSC");

        var esContracts = await session.MarketData
            .GetCurrentlyTradedFuturesContractsAsync("ES");
        esContracts.Success.Should().BeTrue(esContracts.ErrorMessage);
        var vxContracts = await session.MarketData
            .GetCurrentlyTradedFuturesContractsAsync("VX");
        vxContracts.Success.Should().BeTrue(vxContracts.ErrorMessage);
        var contracts = esContracts.Value
            .Concat(vxContracts.Value)
            .DistinctBy(contract => contract.ContractId)
            .ToArray();
        contracts.Should().Contain(contract => contract.ContractId == contractId);

        var feedStart = await session.MarketDataFeedCommands
            .StartMarketDataFeedAsync(contracts, valueDate);
        feedStart.Success.Should().BeTrue(feedStart.ErrorMessage);

        var warmup = await session.MarketDataAnalyticsCommands
            .EnsureHistoricalAnalyticsWarmupAsync(valueDate, contractId);
        warmup.Success.Should().BeTrue(warmup.ErrorMessage);

        var first = await WaitForSnapshotAsync(
            session, contractId, valueDate,
            static snapshot => HasDailyAnalytics(snapshot),
            timeout.Token);
        var second = await WaitForSnapshotAsync(
            session, contractId, valueDate,
            snapshot => HasDailyAnalytics(snapshot)
                        && ProjectionValues(snapshot)
                            .Zip(ProjectionValues(first))
                            .All(pair => pair.First != pair.Second),
            timeout.Token);

        first.FeedHealth.Should().Be("Green");
        second.FeedHealth.Should().Be("Green");
        ProjectionValues(second).Should().NotEqual(ProjectionValues(first));
    }

    static async Task<MarketOutlookReadModel> WaitForSnapshotAsync(
        G0QuerySession session,
        string contractId,
        DateOnly valueDate,
        Func<MarketOutlookReadModel, bool> predicate,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var response = await session.MarketDataAnalytics
                .GetMarketOutlookSnapshotAsync(contractId, valueDate);
            response.Success.Should().BeTrue(response.ErrorMessage);
            if (response.Value is { } snapshot && predicate(snapshot))
                return snapshot;
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
        }
    }

    static bool HasDailyAnalytics(MarketOutlookReadModel snapshot) =>
        snapshot.FuturesEmaSignal is { IsWarm: true }
        && snapshot.FuturesBbSignal is { IsWarm: true };

    static decimal[] ProjectionValues(MarketOutlookReadModel snapshot) =>
    [
        snapshot.FuturesEodData.ClosePrice,
        (decimal)snapshot.FuturesEodData.DailyPercentChange,
        snapshot.FuturesEmaSignal!.Ema50.GetValueOrDefault(),
        snapshot.FuturesEmaSignal.Ema200.GetValueOrDefault(),
        snapshot.FuturesBbSignal!.StandardDeviation20.GetValueOrDefault(),
        snapshot.FuturesBbSignal.Upper20.GetValueOrDefault(),
        snapshot.FuturesBbSignal.Ema20Center.GetValueOrDefault(),
        snapshot.FuturesBbSignal.Lower20.GetValueOrDefault()
    ];
}
