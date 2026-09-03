using FluentAssertions;
using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.Databento;
using TomasAI.IFM.Application.MarketData.MarketOutlook;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

public sealed class DatabentoResiliencyTests
{
    [Fact]
    public async Task Exhausted_core_failure_runs_exactly_three_serial_recovery_attempts_and_latches_red()
    {
        var runtime = new TestRuntime { Snapshot = Down(), FailStarts = true };
        var store = new InMemoryMarketDataServiceStore();
        var service = Create(runtime, store);

        await service.ProbeAsync();

        runtime.StartCount.Should().Be(3);
        runtime.MaximumConcurrentMutations.Should().Be(1);
        service.Current.State.Should().Be(DatabentoLifecycleState.Failed);
        service.Current.RecoveryAttempt.Should().Be(3);
        service.Current.LastObservation!.DisplayHealth.Should().Be(DatabentoDisplayHealth.Red);
        (await store.ListObservationsAsync()).Where(x => x.MajorStatus == DatabentoMajorStatus.Resetting)
            .Select(x => x.RecoveryAttempt).Distinct().Order().Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task Optional_failure_is_orange_and_does_not_reset_core_runtime()
    {
        var runtime = new TestRuntime { Snapshot = Up(optionalDown: true) };
        var service = Create(runtime, new InMemoryMarketDataServiceStore());

        await service.ProbeAsync();

        runtime.StartCount.Should().Be(0);
        service.Current.State.Should().Be(DatabentoLifecycleState.Degraded);
        service.Current.LastObservation!.DisplayHealth.Should().Be(DatabentoDisplayHealth.Orange);
    }

    [Fact]
    public async Task Manual_and_automatic_operations_share_one_serial_executor()
    {
        var runtime = new TestRuntime { Snapshot = Up(), MutationDelay = TimeSpan.FromMilliseconds(10) };
        var service = Create(runtime, new InMemoryMarketDataServiceStore());

        await Task.WhenAll(
            service.StartAsync(ValueDate),
            service.ResetAsync(ValueDate, Guid.NewGuid()),
            service.ProbeAsync());

        runtime.MaximumConcurrentMutations.Should().Be(1);
        service.Current.State.Should().Be(DatabentoLifecycleState.Healthy);
    }

    [Fact]
    public async Task Vx_assignments_are_replaced_atomically_and_require_distinct_ordered_maturities()
    {
        var store = new InMemoryMarketDataServiceStore();
        var front = Assignment(DatabentoContractRole.VxFrontMonth, "VX-1", new(2026, 9, 16));
        var second = Assignment(DatabentoContractRole.VxSecondMonth, "VX-2", new(2026, 10, 21));

        var saved = await store.ReplaceVxAssignmentsAsync(front, second, 0, 0);

        saved.Select(x => x.RowVersion).Should().Equal(1, 1);
        var invalid = second with { ContractId = front.ContractId };
        var action = () => store.ReplaceVxAssignmentsAsync(front, invalid, 1, 1);
        await action.Should().ThrowAsync<InvalidOperationException>();
        (await store.ListAssignmentsAsync()).Select(x => x.ContractId).Should().Equal("VX-1", "VX-2");
    }

    [Fact]
    public async Task Off_trading_does_not_treat_quiet_provider_data_as_a_failure()
    {
        var stale = Up() with
        {
            Feeds = [Feed(Guid.NewGuid(), DatabentoFeedCriticality.Core, true) with
                { LastProviderMessageAge = TimeSpan.FromHours(8) }]
        };
        var runtime = new TestRuntime { Snapshot = stale };
        var service = Create(runtime, new InMemoryMarketDataServiceStore(), FuturesMarketState.OffTrading);

        await service.ProbeAsync();

        runtime.StartCount.Should().Be(0);
        service.Current.State.Should().Be(DatabentoLifecycleState.Healthy);
    }

    [Fact]
    public async Task Value_date_rollover_is_fenced_through_the_same_serial_recovery_owner()
    {
        var runtime = new TestRuntime { Snapshot = Up() };
        runtime.SetActive(ValueDate.AddDays(-1));
        var store = new InMemoryMarketDataServiceStore();
        var service = Create(runtime, store);

        await service.ProbeAsync();

        runtime.ActiveValueDate.Should().Be(ValueDate);
        runtime.StartCount.Should().Be(1);
        (await store.ListObservationsAsync()).Should().Contain(x =>
            x.OperationReason == DatabentoOperationReason.ValueDateRollover);
    }

    [Fact]
    public async Task Observation_publication_failure_does_not_undo_persistence()
    {
        var store = new InMemoryMarketDataServiceStore();
        var service = Create(new TestRuntime { Snapshot = Up() }, store,
            publisher: new ThrowingPublisher());

        await service.ProbeAsync();

        (await store.ListObservationsAsync()).Should().ContainSingle();
        service.Current.Reason.Should().Contain("publication failed");
    }

    [Fact]
    public async Task Transient_observation_failure_is_retried_without_changing_feed_health()
    {
        var store = Substitute.For<IMarketDataServiceStore>();
        var calls = 0;
        store.AppendObservationAsync(Arg.Any<DatabentoWatchdogObservation>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                if (Interlocked.Increment(ref calls) < 3)
                    throw new InvalidOperationException("Injected PostgreSQL interruption.");
                return Task.FromResult(call.Arg<DatabentoWatchdogObservation>() with
                    { WatchdogStatusLogId = 1, RowVersion = 1 });
            });
        var service = Create(new TestRuntime { Snapshot = Up() }, store);

        await service.ProbeAsync();

        calls.Should().Be(3);
        service.Current.State.Should().Be(DatabentoLifecycleState.Healthy);
        service.Current.LastObservation.Should().NotBeNull();
    }

    [Fact]
    public async Task Contract_authority_is_idempotent_and_publishes_only_committed_three_role_set()
    {
        var store = new InMemoryMarketDataServiceStore();
        var catalog = Substitute.For<ICurrentFuturesContractCatalog>();
        catalog.GetByRootAsync("ES", Arg.Any<CancellationToken>()).Returns([
            Contract("ES20261218", "ES", new(2026, 12, 18))]);
        catalog.GetByRootAsync("VX", Arg.Any<CancellationToken>()).Returns([
            Contract("VX20260916", "VX", new(2026, 9, 16)),
            Contract("VX20261021", "VX", new(2026, 10, 21))]);
        var registry = Substitute.For<IDatabentoContractRegistrationRegistry>();
        var authority = new DatabentoContractAuthority(store, catalog, registry, TimeProvider.System);

        var first = await authority.ReconcileAsync(ValueDate, "test", CancellationToken.None);
        var second = await authority.ReconcileAsync(ValueDate, "test", CancellationToken.None);

        first.Select(value => value.ContractRole).Should().BeEquivalentTo(Enum.GetValues<DatabentoContractRole>());
        second.Select(value => value.RowVersion).Should().Equal(1, 1, 1);
        registry.Received(2).ReplaceFuturesRolloverSet("ES", Arg.Is<IReadOnlyCollection<FuturesContractV3ReadModel>>(
            values => values.Count == 1));
        registry.Received(2).ReplaceFuturesRolloverSet("VX", Arg.Is<IReadOnlyCollection<FuturesContractV3ReadModel>>(
            values => values.Count == 2));
    }

    [Fact]
    public async Task Watchdog_observation_crud_enforces_optimistic_concurrency()
    {
        var store = new InMemoryMarketDataServiceStore();
        var observation = Observation();
        var saved = await store.AppendObservationAsync(observation);
        var updated = await store.UpdateObservationAsync(saved with { FailureDetail = "reviewed" }, 1, "operator");
        updated.RowVersion.Should().Be(2);
        var staleDelete = () => store.DeleteObservationAsync(updated.WatchdogStatusLogId, 1, "operator");
        await staleDelete.Should().ThrowAsync<InvalidOperationException>();
        await store.DeleteObservationAsync(updated.WatchdogStatusLogId, 2, "operator");
        (await store.ListObservationsAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Stage_two_metrics_expose_native_interop_aggregation_lifecycle_and_refresh_activity()
    {
        var metrics = new DatabentoWatchdogMetrics();
        var service = Create(new TestRuntime { Snapshot = Up() }, new InMemoryMarketDataServiceStore(), metrics: metrics);

        await service.RefreshAsync(Guid.NewGuid());
        await service.ResetAsync(ValueDate, Guid.NewGuid());

        var snapshot = metrics.Snapshot();
        snapshot.Stages[MarketDataOperationStage.DatabentoRefresh].Requested.Should().Be(1);
        snapshot.Stages[MarketDataOperationStage.DatabentoRefresh].Started.Should().Be(1);
        snapshot.Stages[MarketDataOperationStage.DatabentoRefresh].Completed.Should().Be(1);
        snapshot.Stages[MarketDataOperationStage.DatabentoNative].Completed.Should().BeGreaterThan(0);
        snapshot.Stages[MarketDataOperationStage.DatabentoInterop].Completed.Should().BeGreaterThan(0);
        snapshot.Stages[MarketDataOperationStage.DatabentoAggregation].Completed.Should().BeGreaterThan(0);
        snapshot.Stages[MarketDataOperationStage.DatabentoLifecycle].Completed.Should().BeGreaterThan(0);
    }

    [Theory]
    [InlineData("connection-loss")]
    [InlineData("heartbeat-timeout")]
    [InlineData("terminal-fault")]
    [InlineData("worker-completion")]
    public async Task Every_core_fault_class_enters_the_same_three_attempt_policy(string fault)
    {
        var snapshot = fault switch
        {
            "connection-loss" => Down(),
            "heartbeat-timeout" => Up() with { Feeds = [Feed(Guid.NewGuid(), DatabentoFeedCriticality.Core, true)
                with { LastProviderMessageAge = TimeSpan.FromHours(1) }] },
            "terminal-fault" => Up() with { Feeds = [Feed(Guid.NewGuid(), DatabentoFeedCriticality.Core, false)] },
            _ => Up() with { Feeds = [Feed(Guid.NewGuid(), DatabentoFeedCriticality.Core, true)
                with { AggregationWorkerRunning = false }] }
        };
        var runtime = new TestRuntime { Snapshot = snapshot, FailStarts = true };

        await Create(runtime, new InMemoryMarketDataServiceStore()).ProbeAsync();

        runtime.StartCount.Should().Be(3);
        runtime.MaximumConcurrentMutations.Should().Be(1);
    }

    [Fact]
    public async Task Missing_required_contract_role_fails_startup_closed()
    {
        var incomplete = Up() with
        {
            Feeds = [Feed(Guid.NewGuid(), DatabentoFeedCriticality.Core, true) with
                { ContractRoles = [DatabentoContractRole.EsQuarterly, DatabentoContractRole.VxFrontMonth] }]
        };
        var runtime = new TestRuntime { Snapshot = incomplete, FailStarts = true };

        await Create(runtime, new InMemoryMarketDataServiceStore()).ProbeAsync();

        runtime.StartCount.Should().Be(3);
    }

    [Fact]
    public async Task Terminal_worker_signal_runs_an_out_of_cycle_probe()
    {
        var signal = new DatabentoTerminalFaultSignal();
        var store = new InMemoryMarketDataServiceStore();
        var service = Create(new TestRuntime { Snapshot = Up() }, store, signal: signal);
        var stopwatch = Stopwatch.StartNew();

        await service.StartAsync(CancellationToken.None);
        signal.Notify("Injected terminal completion.");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        while ((await store.ListObservationsAsync(cancellationToken: timeout.Token)).Count == 0)
            await Task.Delay(10, timeout.Token);
        await service.StopAsync(CancellationToken.None);

        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task Accelerated_active_session_and_overnight_soak_remains_bounded_and_serial()
    {
        const int simulatedMinutes = 24 * 60;
        var store = new InMemoryMarketDataServiceStore();
        var runtime = new TestRuntime { Snapshot = Up() };
        var service = Create(runtime, store);
        var process = Process.GetCurrentProcess();
        process.Refresh();
        var handlesBefore = process.HandleCount;
        var memoryBefore = process.PrivateMemorySize64;
        var allocatedBefore = GC.GetTotalAllocatedBytes(true);
        var stopwatch = Stopwatch.StartNew();

        for (var minute = 0; minute < simulatedMinutes; minute++)
            await service.ProbeAsync();
        for (var restart = 0; restart < 50; restart++)
            await service.ResetAsync(ValueDate, Guid.NewGuid());

        stopwatch.Stop();
        process.Refresh();
        var allocated = GC.GetTotalAllocatedBytes(true) - allocatedBefore;
        var memoryGrowth = process.PrivateMemorySize64 - memoryBefore;
        var handleGrowth = process.HandleCount - handlesBefore;
        Console.WriteLine(
            $"Stage2 managed soak: minutes={simulatedMinutes}; restarts=50; elapsedMs={stopwatch.Elapsed.TotalMilliseconds:F3}; allocatedBytes={allocated}; privateMemoryGrowthBytes={memoryGrowth}; handleGrowth={handleGrowth}");
        (await store.ListObservationsAsync(pageSize: 1000)).Should().HaveCount(1000,
            "history queries are bounded while the store retains the complete append history");
        runtime.MaximumConcurrentMutations.Should().Be(1);
        runtime.StartCount.Should().Be(50);
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(10));
        allocated.Should().BeLessThan(128L * 1024 * 1024);
        memoryGrowth.Should().BeLessThan(64L * 1024 * 1024);
        handleGrowth.Should().BeLessThanOrEqualTo(8);
    }

    static readonly DateOnly ValueDate = new(2026, 9, 2);

    static DatabentoMarketDataWatchdogService Create(TestRuntime runtime, IMarketDataServiceStore store,
        FuturesMarketState state = FuturesMarketState.LiveTrading,
        IDatabentoWatchdogPublisher? publisher = null, DatabentoWatchdogMetrics? metrics = null,
        DatabentoTerminalFaultSignal? signal = null)
    {
        var authority = Substitute.For<IFuturesMarketSessionAuthority>();
        authority.Current.Returns(new MarketSessionReadModel
        {
            OperationalValueDate = ValueDate, ActiveValueDate = ValueDate,
            State = state, Revision = 1,
            MarketTime = DateTime.UtcNow, SessionStartUtc = DateTime.UtcNow.AddHours(-1),
            SessionEndUtc = DateTime.UtcNow.AddHours(1), NextTransitionUtc = DateTime.UtcNow.AddHours(1),
            AsOfUtc = DateTime.UtcNow
        });
        return new(runtime, store, authority, publisher ?? new NullDatabentoWatchdogPublisher(),
            metrics ?? new DatabentoWatchdogMetrics(),
            new DatabentoWatchdogOptions
            {
                PollInterval = TimeSpan.FromHours(1), AttemptTwoDelay = TimeSpan.Zero,
                AttemptThreeDelay = TimeSpan.Zero, PersistenceRetryDelay = TimeSpan.Zero
            }, signal ?? new DatabentoTerminalFaultSignal(), TimeProvider.System,
            NullLogger<DatabentoMarketDataWatchdogService>.Instance);
    }

    static DatabentoBulkWatchdogSnapshot Down() => new()
    {
        Complete = false, NativeBackend = "Test", NativeAbiVersion = 3,
        NativeGeneration = Guid.NewGuid(), ObservedOnUtc = DateTime.UtcNow,
        Feeds = [], FailureDetail = "Injected terminal failure."
    };

    static DatabentoBulkWatchdogSnapshot Up(bool optionalDown = false)
    {
        var generation = Guid.NewGuid();
        var feeds = new List<DatabentoFeedWatchdogStatus> { Feed(generation, DatabentoFeedCriticality.Core, true) };
        if (optionalDown) feeds.Add(Feed(generation, DatabentoFeedCriticality.Optional, false));
        return new()
        {
            Complete = true, NativeBackend = "Test", NativeAbiVersion = 3,
            NativeGeneration = generation, ObservedOnUtc = DateTime.UtcNow, Feeds = feeds
        };
    }

    static DatabentoFeedWatchdogStatus Feed(Guid generation, DatabentoFeedCriticality criticality, bool up) => new()
    {
        FeedInstanceId = criticality == DatabentoFeedCriticality.Core ? 1UL : 2UL,
        GenerationId = generation, Dataset = "TEST", FeedKind = "Ticker", Criticality = criticality,
        MajorStatus = up ? DatabentoMajorStatus.Up : DatabentoMajorStatus.Down,
        NativeState = up ? "Running" : "Faulted", TerminalStatus = up ? 0 : 10,
        ProducerAlive = up, AggregationWorkerRunning = up, TransportRunning = up,
        ExpectedSubscriptions = 1, ReceivedSubscriptions = up ? 1 : 0,
        HeartbeatCount = 1, ProviderMessageCount = 1, LastHeartbeatAge = TimeSpan.Zero,
        LastProviderMessageAge = TimeSpan.Zero, RecordsProduced = 1, RecordsConsumed = 1,
        RingCapacity = 1024, RingUsed = 0, RingHighWater = 1, RingOverruns = 0,
        FailureDetail = up ? string.Empty : "Injected optional failure.",
        ContractRoles = criticality == DatabentoFeedCriticality.Core
            ? Enum.GetValues<DatabentoContractRole>() : []
    };

    static FuturesRolloverContractAssignment Assignment(DatabentoContractRole role, string id, DateOnly maturity) => new()
    {
        ContractRole = role, RootSymbol = role == DatabentoContractRole.EsQuarterly ? "ES" : "VX",
        ContractId = id, Description = id, LocalSymbol = id, SecurityType = "FUT",
        Currency = "USD", Exchange = "CME", Multiplier = "1000", LastTradeDate = maturity,
        NextRolloverDate = maturity, SourceContractHash = new('a', 64), CreatedOnUtc = DateTime.UtcNow,
        CreatedBy = "test", UpdatedOnUtc = DateTime.UtcNow, UpdatedBy = "test", RowVersion = 0
    };

    static FuturesContractV3ReadModel Contract(string id, string root, DateOnly maturity) => new(
        id, id, root, id, "FUT", "USD", "CME", "1000", maturity, true, true);

    static DatabentoWatchdogObservation Observation() => new()
    {
        ObservationId = Guid.NewGuid(), CorrelationId = Guid.NewGuid(), ValueDate = ValueDate,
        ObservedOnUtc = DateTime.UtcNow, OperationReason = DatabentoOperationReason.WatchdogPoll,
        MajorStatus = DatabentoMajorStatus.Up, DisplayHealth = DatabentoDisplayHealth.Green,
        CoreContractsReady = true, RecoveryAttempt = 0, NativeBackend = "Test", NativeAbiVersion = 3,
        NativeGeneration = Guid.NewGuid(), FeedStatusDetails = []
    };

    sealed class ThrowingPublisher : IDatabentoWatchdogPublisher
    {
        public ValueTask PublishAsync(DatabentoWatchdogObservation observation, CancellationToken cancellationToken)
            => ValueTask.FromException(new InvalidOperationException("Injected publication failure."));
    }

    sealed class TestRuntime : IDatabentoLifecycleRuntime
    {
        int _activeMutations;
        public DateOnly? ActiveValueDate { get; private set; } = ValueDate;
        public required DatabentoBulkWatchdogSnapshot Snapshot { get; init; }
        public bool FailStarts { get; init; }
        public TimeSpan MutationDelay { get; init; }
        public int StartCount { get; private set; }
        public int MaximumConcurrentMutations { get; private set; }
        public void SetActive(DateOnly? valueDate) => ActiveValueDate = valueDate;
        public Task PrepareContractsAsync(DateOnly valueDate, CancellationToken cancellationToken) => Mutate();
        public async Task StartAsync(DateOnly valueDate, CancellationToken cancellationToken)
        {
            StartCount++; await Mutate();
            if (FailStarts) throw new InvalidOperationException("Injected start failure.");
            ActiveValueDate = valueDate;
        }
        public async Task StopAsync(CancellationToken cancellationToken) { await Mutate(); ActiveValueDate = null; }
        public ValueTask<DatabentoBulkWatchdogSnapshot> GetWatchdogSnapshotAsync(TimeSpan timeout, CancellationToken cancellationToken)
            => ValueTask.FromResult(Snapshot);
        async Task Mutate()
        {
            var current = Interlocked.Increment(ref _activeMutations);
            MaximumConcurrentMutations = Math.Max(MaximumConcurrentMutations, current);
            try { if (MutationDelay > TimeSpan.Zero) await Task.Delay(MutationDelay); }
            finally { Interlocked.Decrement(ref _activeMutations); }
        }
    }
}
