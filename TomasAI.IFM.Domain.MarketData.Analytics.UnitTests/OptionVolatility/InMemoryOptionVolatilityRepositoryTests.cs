using FluentAssertions;
using TomasAI.IFM.Domain.MarketData.Analytics.OptionVolatility;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.OptionVolatility;

public sealed class InMemoryOptionVolatilityRepositoryTests
{
    [Fact]
    public async Task Publish_IdempotentRetryDoesNotDuplicateHistory()
    {
        var repository = new InMemoryOptionVolatilityRepository();
        var publication = OptionVolatilityTestData.Publication("snapshot-1", 1);

        await repository.PublishAsync(publication);
        await repository.PublishAsync(publication);

        (await repository.GetMetricHistoryAsync(OptionVolatilityTestData.History())).Items
            .Should().ContainSingle().Which.Should().Be(publication.Metric);
        (await repository.GetObservationHistoryAsync(OptionVolatilityTestData.History())).Items
            .Should().ContainSingle().Which.Should().Be(publication.SourceObservations[0]);
    }

    [Fact]
    public async Task Publish_CorrectionIsAppendOnlyAndExactOriginalSnapshotRemainsRetrievable()
    {
        var repository = new InMemoryOptionVolatilityRepository();
        var original = OptionVolatilityTestData.Publication("snapshot-1", 1);
        var correction = OptionVolatilityTestData.Publication("snapshot-2", 2, revision: 2,
            supersedes: "snapshot-1", available: OptionVolatilityTestData.Now.AddMinutes(2));

        await repository.PublishAsync(original);
        await repository.PublishAsync(correction);

        (await repository.GetSnapshotAsync("simulation", "snapshot-1")).Should().Be(original.Metric);
        (await repository.GetSnapshotAsync("simulation", "snapshot-2")).Should().Be(correction.Metric);
        (await repository.GetMetricHistoryAsync(OptionVolatilityTestData.History())).Items
            .Should().ContainSingle().Which.Should().Be(correction.Metric);
    }

    [Theory]
    [InlineData(VolatilityPublicationStage.BeforeSnapshotSeal, false, false)]
    [InlineData(VolatilityPublicationStage.BeforeMetricHistory, true, false)]
    [InlineData(VolatilityPublicationStage.BeforeLatestAdvertisement, true, true)]
    public async Task Publish_OrdersSourcesThenSnapshotThenHistoryBeforeLatestAndRecoversOnRetry(
        VolatilityPublicationStage failureStage, bool snapshotSealed, bool historyVisible)
    {
        var inject = true;
        var repository = new InMemoryOptionVolatilityRepository(stage =>
        {
            if (inject && stage == failureStage) throw new InjectedPublicationException();
        });
        var publication = OptionVolatilityTestData.Publication("snapshot-1", 1);

        await repository.Invoking(x => x.PublishAsync(publication)).Should()
            .ThrowAsync<InjectedPublicationException>();

        (await repository.GetObservationHistoryAsync(OptionVolatilityTestData.History())).Items
            .Should().ContainSingle("source evidence is written first");
        (await repository.GetSnapshotAsync("simulation", "snapshot-1") is not null)
            .Should().Be(snapshotSealed);
        (await repository.GetMetricHistoryAsync(OptionVolatilityTestData.History())).Items.Any()
            .Should().Be(historyVisible);
        (await repository.GetLatestAsync(LatestRequest())).Metric.Should().BeNull();

        inject = false;
        await repository.PublishAsync(publication);

        (await repository.GetLatestAsync(LatestRequest())).Metric.Should().Be(publication.Metric);
    }

    [Fact]
    public async Task Publish_StaleCompletionCannotMoveLatestBackward()
    {
        var repository = new InMemoryOptionVolatilityRepository();
        var newer = OptionVolatilityTestData.Publication("newer", 2,
            available: OptionVolatilityTestData.Now.AddMinutes(1));
        var older = OptionVolatilityTestData.Publication("older", 1);

        await repository.PublishAsync(newer);
        await repository.PublishAsync(older);

        (await repository.GetLatestAsync(LatestRequest(OptionVolatilityTestData.Now.AddMinutes(2))))
            .Metric.Should().Be(newer.Metric);
    }

    [Fact]
    public async Task GetLatest_ReportsAcceptedStaleAndUnavailableFreshness()
    {
        var repository = new InMemoryOptionVolatilityRepository();
        var publication = OptionVolatilityTestData.Publication("snapshot-1", 1);

        (await repository.GetLatestAsync(LatestRequest())).FreshnessStatus
            .Should().Be(VolatilityFreshnessStatus.Unavailable);
        await repository.PublishAsync(publication);
        (await repository.GetLatestAsync(LatestRequest(OptionVolatilityTestData.Now.AddSeconds(30))))
            .FreshnessStatus.Should().Be(VolatilityFreshnessStatus.Accepted);
        (await repository.GetLatestAsync(LatestRequest(OptionVolatilityTestData.Now.AddMinutes(2))))
            .FreshnessStatus.Should().Be(VolatilityFreshnessStatus.Stale);
    }

    [Fact]
    public async Task History_PagesWithinBoundAndUsesOpaqueContinuation()
    {
        var repository = new InMemoryOptionVolatilityRepository();
        for (var i = 0; i < 3; i++)
        {
            var source = OptionVolatilityTestData.Observation($"observation-{i}",
                OptionVolatilityTestData.ValueDate.AddDays(i));
            await repository.PublishAsync(OptionVolatilityTestData.Publication($"snapshot-{i}", i + 1, source));
        }

        var first = await repository.GetMetricHistoryAsync(OptionVolatilityTestData.History(pageSize: 2,
            to: OptionVolatilityTestData.ValueDate.AddDays(2)));
        var second = await repository.GetMetricHistoryAsync(OptionVolatilityTestData.History(pageSize: 2,
            pagingState: first.PagingState, to: OptionVolatilityTestData.ValueDate.AddDays(2)));

        first.Items.Should().HaveCount(2);
        first.PagingState.Should().NotBeNull();
        second.Items.Should().ContainSingle();
        second.PagingState.Should().BeNull();
        first.Items.Concat(second.Items).Select(x => x.Snapshot.SnapshotId).Should()
            .Equal("snapshot-0", "snapshot-1", "snapshot-2");
    }

    [Fact]
    public async Task History_AsKnownExcludesLaterRevisionAndRestatedSelectsIt()
    {
        var repository = new InMemoryOptionVolatilityRepository();
        var original = OptionVolatilityTestData.Publication("snapshot-1", 1,
            available: OptionVolatilityTestData.Now);
        var correction = OptionVolatilityTestData.Publication("snapshot-2", 2, revision: 2,
            supersedes: "snapshot-1", available: OptionVolatilityTestData.Now.AddHours(1));
        await repository.PublishAsync(original);
        await repository.PublishAsync(correction);

        var asKnown = await repository.GetMetricHistoryAsync(OptionVolatilityTestData.History(
            VolatilityHistoricalMode.AsKnown, OptionVolatilityTestData.Now.AddMinutes(30)));
        var restated = await repository.GetMetricHistoryAsync(OptionVolatilityTestData.History());

        asKnown.Items.Should().ContainSingle().Which.Should().Be(original.Metric);
        restated.Items.Should().ContainSingle().Which.Should().Be(correction.Metric);
    }

    [Fact]
    public async Task SnapshotLookup_ReturnsExactIdentityAndUnknownIsNull()
    {
        var repository = new InMemoryOptionVolatilityRepository();
        var publication = OptionVolatilityTestData.Publication("snapshot-1", 1);
        await repository.PublishAsync(publication);

        (await repository.GetSnapshotAsync("simulation", "snapshot-1")).Should().Be(publication.Metric);
        (await repository.GetSnapshotAsync("simulation", "missing")).Should().BeNull();
    }

    [Fact]
    public async Task CacheLossMakesLatestUnavailableUntilDurablePointerRebuild()
    {
        var repository = new InMemoryOptionVolatilityRepository();
        var publication = OptionVolatilityTestData.Publication("snapshot-1", 1);
        await repository.PublishAsync(publication);

        repository.SimulateCacheLoss();
        (await repository.GetLatestAsync(LatestRequest())).FreshnessStatus
            .Should().Be(VolatilityFreshnessStatus.Unavailable);
        var rebuilt = await repository.RebuildLatestCacheAsync([OptionVolatilityTestData.Scope]);

        rebuilt.Should().ContainKey(OptionVolatilityTestData.Scope);
        (await repository.GetLatestAsync(LatestRequest())).Metric.Should().Be(publication.Metric);
    }

    [Fact]
    public async Task InvalidCorrectionDoesNotLeakIntoHistory()
    {
        var repository = new InMemoryOptionVolatilityRepository();
        var orphan = OptionVolatilityTestData.Observation("orphan", revision: 2, supersedes: "missing");

        await repository.Invoking(x => x.AppendObservationAsync("simulation", orphan)).Should()
            .ThrowAsync<InvalidOperationException>();

        (await repository.GetObservationHistoryAsync(OptionVolatilityTestData.History())).Items.Should().BeEmpty();
    }

    [Fact]
    public async Task ServiceAndRepositoryHonorCancellation()
    {
        var service = new OptionVolatilityService(new InMemoryOptionVolatilityRepository());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await service.Invoking(x => x.GetLatestAsync(LatestRequest(), cancellation.Token)).Should()
            .ThrowAsync<OperationCanceledException>();
        await service.Invoking(x => x.PublishAsync(OptionVolatilityTestData.Publication("snapshot-1", 1),
            cancellation.Token)).Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task HistoryRejectsInvalidOrUnboundedQueries()
    {
        var repository = new InMemoryOptionVolatilityRepository();
        var crossBucket = OptionVolatilityTestData.History(from: new(2026, 9, 30), to: new(2026, 10, 1));
        var zeroPage = OptionVolatilityTestData.History(pageSize: 0);
        var oversizedPage = OptionVolatilityTestData.History(pageSize: 501);
        var missingKnownAt = OptionVolatilityTestData.History(VolatilityHistoricalMode.AsKnown);
        var unexpectedKnownAt = OptionVolatilityTestData.History(knownAt: OptionVolatilityTestData.Now);

        foreach (var request in new[] { crossBucket, zeroPage, oversizedPage, missingKnownAt, unexpectedKnownAt })
            await repository.Invoking(x => x.GetMetricHistoryAsync(request)).Should().ThrowAsync<ArgumentException>();
    }

    static LatestVolatilityRequest LatestRequest(DateTimeOffset? requested = null) =>
        new(OptionVolatilityTestData.Scope, requested ?? OptionVolatilityTestData.Now.AddSeconds(30),
            TimeSpan.FromMinutes(1));

    sealed class InjectedPublicationException : Exception { }
}
