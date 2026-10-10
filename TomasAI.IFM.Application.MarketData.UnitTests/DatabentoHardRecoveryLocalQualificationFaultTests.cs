using TomasAI.IFM.Application.MarketData.Databento;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.DataBento;
using NSubstitute;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

public sealed class DatabentoHardRecoveryLocalQualificationFaultTests
{
    static readonly DateOnly ValueDate = new(2026, 9, 30);

    [Fact]
    public void Missing_required_dataset_fails_before_any_worker_is_started()
    {
        var desired = new DatasetDesiredSubscriptionRegistry();
        desired.Set("GLBX.MDP3", ValueDate, [Registration("GLBX.MDP3")]);

        var error = Assert.Throws<InvalidOperationException>(() =>
            desired.CaptureRecoverySnapshot(ValueDate, ["GLBX.MDP3", "OPRA.PILLAR"], TimeProvider.System));

        Assert.Contains("OPRA.PILLAR", error.Message);
    }

    [Fact]
    public void Wrong_value_date_cannot_be_captured_from_a_current_manifest()
    {
        var desired = new DatasetDesiredSubscriptionRegistry();
        desired.Set("GLBX.MDP3", ValueDate, [Registration("GLBX.MDP3")]);

        var error = Assert.Throws<InvalidOperationException>(() =>
            desired.CaptureRecoverySnapshot(ValueDate.AddDays(1), ["GLBX.MDP3"], TimeProvider.System));

        Assert.Contains("GLBX.MDP3", error.Message);
    }

    [Fact]
    public void Frozen_snapshot_rejects_manifests_from_another_value_date()
    {
        var desired = new DatasetDesiredSubscriptionRegistry();
        var manifest = desired.Set("GLBX.MDP3", ValueDate, [Registration("GLBX.MDP3")]);

        Assert.Throws<ArgumentException>(() => new DatasetRecoveryManifestSnapshot(
            ValueDate.AddDays(1), TimeProvider.System.GetUtcNow(), [manifest]));
    }

    [Fact]
    public async Task Session_value_date_mismatch_fails_before_worker_containment()
    {
        var desired = new DatasetDesiredSubscriptionRegistry();
        desired.Set("GLBX.MDP3", ValueDate, [Registration("GLBX.MDP3")]);
        var frozen = desired.CaptureRecoverySnapshot(ValueDate, ["GLBX.MDP3"], TimeProvider.System);
        var sessions = Substitute.For<IFuturesMarketSessionAuthority>();
        sessions.Current.Returns(Session(ValueDate.AddDays(1), FuturesMarketState.LiveTrading));
        await using var workers = new DatasetWorkerProcessRecoveryService(new(), new(),
            desiredSubscriptions: desired);
        var runtime = new SupervisedDatabentoHardRecoveryRuntime(workers, WorkerOptions(), TimeProvider.System);

        var result = await runtime.AttemptAsync(frozen, sessions, TimeSpan.FromMinutes(5),
            TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1),
            CancellationToken.None);

        Assert.Equal("MarketSession", result.Stage);
        Assert.False(result.Succeeded);
        Assert.Empty(workers.Current);
    }

    [Theory]
    [InlineData(FuturesMarketState.LiveTrading)]
    [InlineData(FuturesMarketState.OffTrading)]
    public async Task Authoritative_session_accepts_both_live_and_quiet_modes(
        FuturesMarketState state)
    {
        var desired = new DatasetDesiredSubscriptionRegistry();
        desired.Set("GLBX.MDP3", ValueDate, [Registration("GLBX.MDP3")]);
        var frozen = desired.CaptureRecoverySnapshot(ValueDate, ["GLBX.MDP3"], TimeProvider.System);
        var sessions = Substitute.For<IFuturesMarketSessionAuthority>();
        sessions.Current.Returns(Session(ValueDate, state));
        await using var workers = new DatasetWorkerProcessRecoveryService(new(), new(),
            desiredSubscriptions: desired);
        var runtime = new SupervisedDatabentoHardRecoveryRuntime(workers, WorkerOptions(), TimeProvider.System);

        var result = await runtime.AttemptAsync(frozen, sessions, TimeSpan.FromMinutes(5),
            TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(100),
            TimeSpan.FromSeconds(1), CancellationToken.None);

        Assert.NotEqual("MarketSession", result.Stage);
    }

    [Fact]
    public void Missing_required_worker_does_not_qualify()
    {
        var (evaluator, _) = Fixture(liveTrading: false);

        Assert.False(evaluator.IsQualified([]));
    }

    [Fact]
    public void Partial_subscription_acknowledgement_does_not_qualify()
    {
        var (evaluator, worker) = Fixture(liveTrading: false);
        var unacknowledged = worker with
        {
            Diagnostics = worker.Diagnostics! with { ReceivedSubscriptions = 0 }
        };

        Assert.False(evaluator.IsQualified([unacknowledged]));
    }

    [Fact]
    public void Old_generation_worker_and_diagnostic_cannot_qualify_new_candidate()
    {
        var (evaluator, worker) = Fixture(liveTrading: false);
        var oldGeneration = Guid.NewGuid();
        var stale = worker with
        {
            GenerationId = oldGeneration,
            Diagnostics = worker.Diagnostics! with { GenerationId = oldGeneration }
        };

        Assert.False(evaluator.IsQualified([stale]));
        Assert.True(evaluator.IsQualified([worker]));
    }

    [Fact]
    public void Old_generation_diagnostic_cannot_qualify_current_worker()
    {
        var (evaluator, worker) = Fixture(liveTrading: false);
        var stale = worker with
        {
            Diagnostics = worker.Diagnostics! with { GenerationId = Guid.NewGuid() }
        };

        Assert.False(evaluator.IsQualified([stale]));
    }

    [Fact]
    public void Live_session_accepts_fresh_gateway_heartbeat_without_market_records()
    {
        var (evaluator, worker) = Fixture(liveTrading: true);

        Assert.True(evaluator.IsQualified([worker]));
        var progressed = worker with
        {
            Diagnostics = worker.Diagnostics! with { RecordsProduced = 1, RecordsConsumed = 1 }
        };
        Assert.True(evaluator.IsQualified([progressed]));
    }

    [Fact]
    public void Quiet_session_accepts_current_heartbeat_without_new_records()
    {
        var (evaluator, worker) = Fixture(liveTrading: false);

        Assert.True(evaluator.IsQualified([worker]));
    }

    [Fact]
    public void Quiet_session_rejects_stale_heartbeat()
    {
        var (evaluator, worker) = Fixture(liveTrading: false);
        var stale = worker with
        {
            Diagnostics = worker.Diagnostics! with
            {
                LastHeartbeatAgeTicks = TimeSpan.FromMinutes(1).Ticks
            }
        };

        Assert.False(evaluator.IsQualified([stale]));
    }

    [Fact]
    public void Quiet_session_accepts_fresh_provider_messages_without_heartbeat()
    {
        var (evaluator, worker) = Fixture(liveTrading: false);
        var providerActive = worker with
        {
            Diagnostics = worker.Diagnostics! with
            {
                HeartbeatCount = 0,
                LastHeartbeatAgeTicks = long.MaxValue,
                ProviderMessageCount = 10,
                LastProviderMessageAgeTicks = TimeSpan.FromMilliseconds(100).Ticks,
                RecordsProduced = 10,
                RecordsConsumed = 10
            }
        };

        Assert.True(evaluator.IsQualified([providerActive]));
        Assert.False(evaluator.IsQualified([providerActive with
        {
            Diagnostics = providerActive.Diagnostics! with
            {
                LastProviderMessageAgeTicks = TimeSpan.FromMinutes(1).Ticks
            }
        }]));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Ready_session_does_not_require_empty_local_queues(bool liveTrading)
    {
        var (evaluator, worker) = Fixture(liveTrading);
        var backlogged = worker with
        {
            Diagnostics = worker.Diagnostics! with { RingUsed = 1, RecordsProduced = 1 }
        };

        Assert.True(evaluator.IsQualified([backlogged]));
    }

    [Fact]
    public void Live_session_rejects_stale_provider_message_despite_consumption_progress()
    {
        var (evaluator, worker) = Fixture(liveTrading: true);
        Assert.True(evaluator.IsQualified([worker]));
        var stale = worker with
        {
            Diagnostics = worker.Diagnostics! with
            {
                LastHeartbeatAgeTicks = TimeSpan.FromMinutes(1).Ticks,
                RecordsProduced = 1,
                RecordsConsumed = 1,
                LastProviderMessageAgeTicks = TimeSpan.FromMinutes(1).Ticks
            }
        };

        Assert.False(evaluator.IsQualified([stale]));
    }

    [Fact]
    public void Worker_alive_but_control_pipe_stalled_cannot_qualify()
    {
        var (evaluator, worker) = Fixture(liveTrading: false);

        Assert.True(worker.Running);
        Assert.False(evaluator.IsQualified([worker with { ControlResponsive = false }]));
    }

    [Fact]
    public void Database_unavailable_does_not_affect_local_qualification_evaluator()
    {
        // Neither the manifest registry nor the evaluator has a database dependency.
        var (evaluator, worker) = Fixture(liveTrading: false);

        Assert.True(evaluator.IsQualified([worker]));
    }

    static (DatabentoLocalQualificationEvaluator Evaluator, DatasetWorkerProcessSnapshot Worker)
        Fixture(bool liveTrading)
    {
        var desired = new DatasetDesiredSubscriptionRegistry();
        var manifest = desired.Set("GLBX.MDP3", ValueDate, [Registration("GLBX.MDP3")]);
        var generation = Guid.NewGuid();
        var evaluator = new DatabentoLocalQualificationEvaluator(
            [manifest], new Dictionary<string, Guid> { [manifest.Dataset] = generation },
            liveTrading, false, TimeSpan.FromSeconds(5), TimeProvider.System);
        var diagnostics = new DatasetWorkerDiagnostics
        {
            Dataset = manifest.Dataset,
            GenerationId = generation,
            ObservedOnUtc = DateTime.UtcNow,
            Complete = true,
            FeedInstanceId = 1,
            NativeMajorStatus = 1,
            NativeState = FeedState.Running,
            ProducerAlive = true,
            TransportReady = true,
            ExpectedSubscriptions = manifest.Contracts.Count,
            ReceivedSubscriptions = manifest.Contracts.Count,
            HeartbeatCount = 1,
            LastHeartbeatAgeTicks = TimeSpan.FromSeconds(1).Ticks,
            LastProviderMessageAgeTicks = TimeSpan.FromSeconds(1).Ticks,
            Drain = new DatasetWorkerDrainDiagnostics(default, 0, 0, 0, 0, 0, 0, string.Empty,
                0, 0, 0, false, 0, 0, 0)
        };
        var worker = new DatasetWorkerProcessSnapshot
        {
            Dataset = manifest.Dataset,
            WorkerInstanceId = Guid.NewGuid(),
            GenerationId = generation,
            ProcessId = 123,
            StartedOnUtc = DateTime.UtcNow,
            Running = true,
            Healthy = true,
            GracefulStopSucceeded = false,
            ForcedTermination = false,
            ControlResponsive = true,
            ManifestRevision = manifest.Revision,
            ManifestFingerprint = manifest.Fingerprint,
            Diagnostics = diagnostics
        };
        return (evaluator, worker);
    }

    static DatabentoContractRegistration Registration(string dataset) => new()
    {
        DomainContractId = "ES20261218",
        ProviderContractName = "ESZ6",
        AssetTypeId = AssetTypeId.Futures,
        Dataset = dataset,
        RootSymbol = "ES",
        OnTheRun = true,
        Rollover = true
    };

    static MarketSessionReadModel Session(DateOnly valueDate, FuturesMarketState state)
    {
        var now = DateTime.UtcNow;
        return new()
        {
            OperationalValueDate = valueDate,
            ActiveValueDate = state == FuturesMarketState.Closed ? null : valueDate,
            State = state,
            SessionStartUtc = now.AddHours(-1),
            SessionEndUtc = now.AddHours(1),
            NextTransitionUtc = now.AddHours(1),
            AsOfUtc = now,
            Revision = 1
        };
    }

    static DatabentoSupervisedWorkerOptions WorkerOptions() => new()
    {
        DotNetHostPath = "unused-for-fault-test",
        WorkerAssemblyPath = "unused-for-fault-test"
    };
}
