using FluentAssertions;
using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.Databento;
using TomasAI.IFM.Application.MarketData.MarketOutlook;
using TomasAI.IFM.Application.MarketData.OperationsHealth;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

[Collection(ProcessResourceQualificationCollection.Name)]
public sealed class DatabentoResiliencyTests
{
    [Fact]
    public async Task Dataset_incident_store_is_idempotent_and_returns_only_open_current_incidents()
    {
        var store = new InMemoryMarketDataServiceStore();
        var transitionId = Guid.NewGuid();
        var snapshot = new DatasetIncidentSnapshot
        {
            Dataset = "GLBX.MDP3", ValueDate = ValueDate, IncidentId = Guid.NewGuid(),
            GenerationId = Guid.NewGuid(), IsOpen = true,
            FailureReason = DatabentoDatasetFailureReason.NativeDrainStalled,
            LastAction = DatasetRecoveryAction.CooperativeReset,
            ObservedOnUtc = DateTime.UtcNow
        };
        var transition = new DatasetIncidentTransition(
            transitionId, Guid.NewGuid(), snapshot);

        var first = await store.PersistDatasetIncidentAsync(transition);
        var duplicate = await store.PersistDatasetIncidentAsync(transition);
        var closed = await store.PersistDatasetIncidentAsync(new(
            Guid.NewGuid(), Guid.NewGuid(), snapshot with { IsOpen = false }));

        first.RowVersion.Should().Be(1);
        duplicate.RowVersion.Should().Be(1);
        closed.RowVersion.Should().Be(2);
        (await store.ListOpenDatasetIncidentsAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Stage3_live_policy_retries_once_per_minute_then_replaces_only_the_dataset_process()
    {
        var time = new ManualTimeProvider();
        var runtime = new TestRuntime { Snapshot = Up() with
        {
            Feeds = [Feed(Guid.NewGuid(), DatabentoFeedCriticality.Core, false) with { TerminalStatus = 0, MajorStatus = DatabentoMajorStatus.Up }]
        }, FailDatasetResets = true };
        var recovery = new TestProcessRecovery(runtime);
        var service = Create(runtime, new InMemoryMarketDataServiceStore(),
            stage3: new DatabentoStage3Options { Enabled = true },
            processRecovery: recovery, timeProvider: time);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            await service.ProbeAsync();
            recovery.Count.Should().Be(attempt == 4 ? 1 : 0);
            time.Advance(TimeSpan.FromMinutes(1));
        }
        await service.ProbeAsync();

        runtime.ResetDatasets.Should().HaveCount(5);
        recovery.Count.Should().Be(1);
        service.Current.State.Should().Be(DatabentoLifecycleState.Healthy);
    }

    [Fact]
    public async Task Stage3_off_hours_waits_fifteen_minutes_then_failed_reset_replaces_process()
    {
        var time = new ManualTimeProvider();
        var runtime = new TestRuntime { Snapshot = Up() with
        {
            Feeds = [Feed(Guid.NewGuid(), DatabentoFeedCriticality.Core, false) with { TerminalStatus = 0, MajorStatus = DatabentoMajorStatus.Up }]
        }, FailDatasetResets = true };
        var recovery = new TestProcessRecovery(runtime);
        var service = Create(runtime, new InMemoryMarketDataServiceStore(),
            FuturesMarketState.OffTrading,
            stage3: new DatabentoStage3Options { Enabled = true },
            processRecovery: recovery, timeProvider: time);

        await service.ProbeAsync();
        time.Advance(TimeSpan.FromMinutes(15));
        await service.ProbeAsync();

        runtime.ResetDatasets.Should().ContainSingle();
        recovery.Count.Should().Be(1);
        service.Current.State.Should().Be(DatabentoLifecycleState.Healthy);
    }

    [Fact]
    public async Task Stage3_terminal_failure_escalates_failed_cooperative_reset_in_same_probe()
    {
        var runtime = new TestRuntime { Snapshot = Up() with
        { Feeds = [Feed(Guid.NewGuid(), DatabentoFeedCriticality.Core, false)] }, FailDatasetResets = true };
        var recovery = new TestProcessRecovery(runtime);
        var service = Create(runtime, new InMemoryMarketDataServiceStore(), FuturesMarketState.OffTrading,
            stage3: new() { Enabled = true }, processRecovery: recovery);
        await service.ProbeAsync();
        runtime.ResetDatasets.Should().ContainSingle();
        recovery.Count.Should().Be(1);
    }

    [Fact]
    public async Task Stage3_confirmed_process_exit_skips_cooperative_reset()
    {
        var runtime = new TestRuntime { Snapshot = Up() with
        { Feeds = [Feed(Guid.NewGuid(), DatabentoFeedCriticality.Core, false)] } };
        var recovery = new TestProcessRecovery(runtime) { Exited = true };
        var service = Create(runtime, new InMemoryMarketDataServiceStore(),
            stage3: new() { Enabled = true }, processRecovery: recovery);
        await service.ProbeAsync();
        runtime.ResetDatasets.Should().BeEmpty();
        recovery.Count.Should().Be(1);
    }

    [Fact]
    public async Task Stage3_stop_persists_closure_of_open_incident()
    {
        var runtime = new TestRuntime { Snapshot = Up() with
        { Feeds = [Feed(Guid.NewGuid(), DatabentoFeedCriticality.Core, false) with { TerminalStatus = 0, MajorStatus = DatabentoMajorStatus.Up }] },
            FailDatasetResets = true };
        var store = new InMemoryMarketDataServiceStore();
        var service = Create(runtime, store, stage3: new() { Enabled = true });
        await service.ProbeAsync();
        (await store.ListOpenDatasetIncidentsAsync()).Should().ContainSingle();
        await service.StopAsync(ValueDate);
        (await store.ListOpenDatasetIncidentsAsync()).Should().BeEmpty();
    }

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
    public async Task Optional_dataset_failure_is_replaced_without_resetting_core_runtime()
    {
        var runtime = new TestRuntime { Snapshot = Up(optionalDown: true) };
        var service = Create(runtime, new InMemoryMarketDataServiceStore());

        await service.ProbeAsync();

        runtime.StartCount.Should().Be(0);
        runtime.ResetDatasets.Should().Equal("OPTIONAL");
        service.Current.State.Should().Be(DatabentoLifecycleState.Healthy);
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
    public async Task Epoch_level_faults_enter_the_same_three_attempt_policy(string fault)
    {
        var snapshot = fault switch
        {
            "connection-loss" => Down(),
            "heartbeat-timeout" => Up() with { Feeds = [Feed(Guid.NewGuid(), DatabentoFeedCriticality.Core, true)
                with { LastProviderMessageAge = TimeSpan.FromHours(1) }] },
            _ => Up() with { Feeds = [Feed(Guid.NewGuid(), DatabentoFeedCriticality.Core, true)
                with { LastProviderMessageAge = TimeSpan.FromHours(1) }] }
        };
        var runtime = new TestRuntime { Snapshot = snapshot, FailStarts = true };

        await Create(runtime, new InMemoryMarketDataServiceStore()).ProbeAsync();

        runtime.StartCount.Should().Be(3);
        runtime.MaximumConcurrentMutations.Should().Be(1);
    }

    [Theory]
    [InlineData("terminal-fault")]
    [InlineData("worker-completion")]
    public async Task Dataset_faults_replace_only_the_failed_generation(string fault)
    {
        var failed = Feed(Guid.NewGuid(), DatabentoFeedCriticality.Core, true) with
        {
            MajorStatus = fault == "terminal-fault" ? DatabentoMajorStatus.Down : DatabentoMajorStatus.Up,
            TerminalStatus = fault == "terminal-fault" ? 10 : 0,
            ProducerAlive = fault != "terminal-fault",
            AggregationWorkerRunning = fault != "worker-completion"
        };
        var runtime = new TestRuntime { Snapshot = Up() with { Feeds = [failed] } };

        await Create(runtime, new InMemoryMarketDataServiceStore()).ProbeAsync();

        runtime.ResetDatasets.Should().Equal("TEST");
        runtime.StartCount.Should().Be(0);
        runtime.StopCount.Should().Be(0);
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

    [Fact]
    public void Dataset_evaluator_requires_causal_stall_and_resets_its_timer_on_progress()
    {
        var evaluator = new DatabentoDatasetHealthEvaluator(TimeSpan.FromMinutes(5));
        var generation = Guid.NewGuid();
        var observed = new DateTime(2026, 9, 2, 13, 0, 0, DateTimeKind.Utc);
        var baseline = Feed(generation, DatabentoFeedCriticality.Core, true) with
        {
            RecordsProduced = 100,
            RecordsConsumed = 100,
            RingUsed = 0,
            BatchesPublished = 10,
            AggregationMetrics = Metrics(100, 100)
        };

        evaluator.Evaluate(baseline, observed).State.Should().Be(DatabentoDatasetState.Up);
        var suspect = evaluator.Evaluate(baseline with
        {
            RecordsProduced = 110,
            RingUsed = 10
        }, observed.AddSeconds(1));
        suspect.State.Should().Be(DatabentoDatasetState.Suspect);
        suspect.Reason.Should().Be(DatabentoDatasetFailureReason.NativeDrainStalled);

        evaluator.Evaluate(baseline with
        {
            RecordsProduced = 120,
            RingUsed = 20
        }, observed.AddMinutes(5)).State.Should().Be(DatabentoDatasetState.Suspect);
        evaluator.Evaluate(baseline with
        {
            RecordsProduced = 121,
            RingUsed = 21
        }, observed.AddMinutes(5).AddSeconds(1)).State.Should().Be(DatabentoDatasetState.Down);

        evaluator.Evaluate(baseline with
        {
            RecordsProduced = 121,
            RecordsConsumed = 121,
            RingUsed = 0,
            BatchesPublished = 11,
            AggregationMetrics = Metrics(121, 121)
        }, observed.AddMinutes(5).AddSeconds(2)).State.Should().Be(DatabentoDatasetState.Up);
    }

    [Fact]
    public void Dataset_evaluator_does_not_fail_a_causally_quiet_market()
    {
        var evaluator = new DatabentoDatasetHealthEvaluator(TimeSpan.FromMinutes(5));
        var feed = Feed(Guid.NewGuid(), DatabentoFeedCriticality.Core, true) with
        {
            LastProviderMessageAge = TimeSpan.FromHours(12),
            RecordsProduced = 500,
            RecordsConsumed = 500,
            RingUsed = 0,
            AggregationMetrics = Metrics(500, 500)
        };
        var observed = DateTime.UtcNow;

        evaluator.Evaluate(feed, observed).State.Should().Be(DatabentoDatasetState.Up);
        evaluator.Evaluate(feed, observed.AddHours(6)).State.Should().Be(DatabentoDatasetState.Up);
    }

    [Fact]
    public async Task Confirmed_native_drain_stall_replaces_only_affected_dataset_and_qualifies_new_generation()
    {
        var observed = new DateTime(2026, 9, 2, 13, 0, 0, DateTimeKind.Utc);
        var failedGeneration = Guid.NewGuid();
        var healthyGeneration = Guid.NewGuid();
        var failed = Feed(failedGeneration, DatabentoFeedCriticality.Core, true) with
        {
            RecordsProduced = 100, RecordsConsumed = 100,
            AggregationMetrics = Metrics(100, 100)
        };
        var healthy = Feed(healthyGeneration, DatabentoFeedCriticality.Optional, true) with
        {
            Dataset = "HEALTHY", FeedInstanceId = 2,
            AggregationMetrics = Metrics(50, 50)
        };
        var runtime = new TestRuntime
        {
            Snapshot = Up() with { ObservedOnUtc = observed, Feeds = [failed, healthy] }
        };
        var store = new InMemoryMarketDataServiceStore();
        var service = Create(runtime, store);

        await service.ProbeAsync();
        runtime.Snapshot = runtime.Snapshot with
        {
            ObservedOnUtc = observed.AddSeconds(1),
            Feeds = [failed with { RecordsProduced = 110, RingUsed = 10 }, healthy]
        };
        await service.ProbeAsync();
        runtime.ResetDatasets.Should().BeEmpty();
        service.Current.State.Should().Be(DatabentoLifecycleState.Healthy,
            "suspect status is non-terminal during the confirmation window");

        runtime.Snapshot = runtime.Snapshot with
        {
            ObservedOnUtc = observed.AddMinutes(5).AddSeconds(1),
            Feeds = [failed with { RecordsProduced = 120, RingUsed = 20 }, healthy]
        };
        await service.ProbeAsync();

        runtime.ResetDatasets.Should().Equal("TEST");
        runtime.StartCount.Should().Be(0);
        runtime.StopCount.Should().Be(0);
        runtime.Snapshot.Feeds.Single(feed => feed.Dataset == "HEALTHY")
            .GenerationId.Should().Be(healthyGeneration);
        runtime.Snapshot.Feeds.Single(feed => feed.Dataset == "TEST")
            .GenerationId.Should().NotBe(failedGeneration);
        service.Current.State.Should().Be(DatabentoLifecycleState.Healthy);
        (await store.ListObservationsAsync()).Should().Contain(observation =>
            observation.FailureStage == "DatasetDiagnosis"
            && observation.FailureDetail.Contains(nameof(DatabentoDatasetFailureReason.NativeDrainStalled)));
    }

    static readonly DateOnly ValueDate = new(2026, 9, 2);

    static DatabentoMarketDataWatchdogService Create(TestRuntime runtime, IMarketDataServiceStore store,
        FuturesMarketState state = FuturesMarketState.LiveTrading,
        IDatabentoWatchdogPublisher? publisher = null, DatabentoWatchdogMetrics? metrics = null,
        DatabentoTerminalFaultSignal? signal = null,
        DatabentoStage3Options? stage3 = null,
        IDatabentoDatasetProcessRecovery? processRecovery = null,
        TimeProvider? timeProvider = null)
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
        var clock = timeProvider ?? TimeProvider.System;
        var admissions = new DatasetWorkerAdmissionRegistry();
        return new(runtime, store, authority, publisher ?? new NullDatabentoWatchdogPublisher(),
            metrics ?? new DatabentoWatchdogMetrics(),
            new DatabentoWatchdogOptions
            {
                PollInterval = TimeSpan.FromHours(1), AttemptTwoDelay = TimeSpan.Zero,
                AttemptThreeDelay = TimeSpan.Zero, PersistenceRetryDelay = TimeSpan.Zero
            }, signal ?? new DatabentoTerminalFaultSignal(), clock,
            NullLogger<DatabentoMarketDataWatchdogService>.Instance,
            stage3, processRecovery, new MarketDataOperationsHealthService(admissions));
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
        if (optionalDown) feeds.Add(Feed(generation, DatabentoFeedCriticality.Optional, false) with
            { Dataset = "OPTIONAL", FeedInstanceId = 2 });
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

    static TomasAI.IFM.Framework.MarketData.DataBento.TickAggregation.Contracts.TickAggregationMetricsSnapshot Metrics(
        long started,
        long completed) => new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0)
        {
            RecordsStarted = started,
            RecordsCompleted = completed
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
        public required DatabentoBulkWatchdogSnapshot Snapshot { get; set; }
        public bool FailStarts { get; init; }
        public bool FailDatasetResets { get; init; }
        public TimeSpan MutationDelay { get; init; }
        public int StartCount { get; private set; }
        public int StopCount { get; private set; }
        public List<string> ResetDatasets { get; } = [];
        public int MaximumConcurrentMutations { get; private set; }
        public void SetActive(DateOnly? valueDate) => ActiveValueDate = valueDate;
        public Task PrepareContractsAsync(DateOnly valueDate, CancellationToken cancellationToken) => Mutate();
        public async Task StartAsync(DateOnly valueDate, CancellationToken cancellationToken)
        {
            StartCount++; await Mutate();
            if (FailStarts) throw new InvalidOperationException("Injected start failure.");
            ActiveValueDate = valueDate;
        }
        public async Task StopAsync(CancellationToken cancellationToken) { StopCount++; await Mutate(); ActiveValueDate = null; }
        public async Task<DatabentoDatasetResetResult> ResetDatasetAsync(
            DatabentoDatasetResetRequest request,
            CancellationToken cancellationToken)
        {
            await Mutate();
            ResetDatasets.Add(request.Dataset);
            if (FailDatasetResets)
                return new(request.Dataset, request.ExpectedGenerationId, request.ExpectedGenerationId,
                    false, "Injected dataset reset failure.");
            var generation = Guid.NewGuid();
            Snapshot = Snapshot with
            {
                ObservedOnUtc = Snapshot.ObservedOnUtc.AddSeconds(1),
                Feeds = Snapshot.Feeds.Select(feed => feed.Dataset == request.Dataset
                    ? Feed(generation, feed.Criticality, true) with
                    {
                        Dataset = feed.Dataset,
                        FeedInstanceId = feed.FeedInstanceId,
                        ContractRoles = feed.ContractRoles
                    }
                    : feed).ToArray()
            };
            return new(request.Dataset, request.ExpectedGenerationId, generation, true, "reset");
        }
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

    sealed class TestProcessRecovery(TestRuntime runtime) : IDatabentoDatasetProcessRecovery
    {
        public bool Exited { get; init; }
        public bool HasExited(string dataset, Guid expectedGeneration) => Exited;
        public int Count { get; private set; }
        public Task<DatabentoDatasetResetResult> ReplaceProcessAsync(
            DatabentoDatasetResetRequest request, CancellationToken cancellationToken)
        {
            Count++;
            var generation = Guid.NewGuid();
            runtime.Snapshot = runtime.Snapshot with
            {
                ObservedOnUtc = runtime.Snapshot.ObservedOnUtc.AddSeconds(1),
                Feeds = runtime.Snapshot.Feeds.Select(feed => feed.Dataset == request.Dataset
                    ? Feed(generation, feed.Criticality, true) with
                    {
                        Dataset = feed.Dataset,
                        FeedInstanceId = feed.FeedInstanceId,
                        ContractRoles = feed.ContractRoles
                    }
                    : feed).ToArray()
            };
            return Task.FromResult(new DatabentoDatasetResetResult(request.Dataset,
                request.ExpectedGenerationId, generation, true, "replaced"));
        }
    }

    sealed class ManualTimeProvider : TimeProvider
    {
        DateTimeOffset utcNow = new(2026, 9, 4, 12, 0, 0, TimeSpan.Zero);
        long timestamp;
        public override DateTimeOffset GetUtcNow() => utcNow;
        public override long GetTimestamp() => timestamp;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public void Advance(TimeSpan value)
        {
            utcNow += value;
            timestamp = checked(timestamp + value.Ticks);
        }
    }
}
