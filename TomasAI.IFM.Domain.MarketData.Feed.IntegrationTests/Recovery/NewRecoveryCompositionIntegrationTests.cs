using System.Diagnostics;
using Cassandra;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Actor.IntegrationTests;
using TomasAI.IFM.Application.Api.Nats.Client;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Framework.MarketData.DataBento;
using TomasAI.IFM.Application.Api.Server;
using TomasAI.IFM.Application.MarketData.Databento;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Application.MarketData.OperationsHealth;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Framework.MarketData.Contracts.TickAggregation;
using TomasAI.IFM.IntegrationTesting;

namespace TomasAI.IFM.Domain.MarketData.Feed.IntegrationTests.Recovery;

public sealed class NewRecoveryCompositionIntegrationTests
{
    [Fact]
    public async Task Isolated_kestrel_host_composes_only_the_opted_in_recovery_requester()
    {
        await using var root = new KestrelWebApplicationFactory<ApiServerEntryPoint>();
        await using var factory = root.WithWebHostBuilder(builder => builder
            .UseEnvironment("Development")
            .UseSetting("AppSettings:Databento:DeploymentProfile", "SyntheticCi")
            .UseSetting("AppSettings:Databento:DataSource", "Synthetic")
            .UseSetting("MarketDataRecovery:Stage3:Enabled", "true")
            .UseSetting("MarketDataRecovery:Stage3:WorkerAssemblyPath",
                "../../../../TomasAI.IFM.Application.MarketData.Worker/bin/Debug/net10.0/TomasAI.IFM.Application.MarketData.Worker.dll")
            .UseSetting("MarketDataRecovery:HardRecovery:Pipeline:Enabled", "true"));

        var services = factory.Services;
        Assert.IsType<ApiDatabentoRecoveryPipeline>(
            services.GetRequiredService<IDatabentoRecoveryRequester>());
        Assert.NotNull(services.GetRequiredService<IApiFatalRecoveryShutdown>());
        // The configured host must not restore the old 30-second aggregate startup deadline.
        Assert.Equal(new DatabentoStage3Options().WorkerStartTimeout,
            services.GetRequiredService<DatabentoStage3Options>().WorkerStartTimeout);
    }

    [Fact]
    public async Task Synthetic_replacement_generation_must_reach_real_durable_tick_storage_before_admission()
    {
        await using var root = new KestrelWebApplicationFactory<ApiServerEntryPoint>();
        await using var factory = root.WithWebHostBuilder(builder => builder
            .UseEnvironment("Development")
            .UseSetting("AppSettings:Databento:DeploymentProfile", "SyntheticCi")
            .UseSetting("AppSettings:Databento:DataSource", "Synthetic")
            .UseSetting("MarketDataRecovery:Stage3:Enabled", "true")
            .UseSetting("MarketDataRecovery:Stage3:WorkerAssemblyPath",
                "../../../../TomasAI.IFM.Application.MarketData.Worker/bin/Debug/net10.0/TomasAI.IFM.Application.MarketData.Worker.dll")
            .UseSetting("MarketDataRecovery:HardRecovery:Pipeline:Enabled", "true")
            .UseSetting("MarketDataRecovery:HardRecovery:Pipeline:OverallTimeout", "00:01:00")
            .UseSetting("MarketDataRecovery:HardRecovery:Runtime:QualificationTimeout", "00:00:12")
            .UseSetting("MarketDataRecovery:HardRecovery:CandidateProof:ProofTimeout", "00:00:12")
            .UseSetting("MarketDataRecovery:HardRecovery:Pipeline:DownstreamTimeout", "00:00:20")
            .ConfigureTestServices(services =>
            {
                services.RemoveAll<IApiFatalRecoveryShutdown>();
                services.AddSingleton<IApiFatalRecoveryShutdown, NonTerminatingFatalRecorder>();
            }));

        var services = factory.Services;
        var session = services.GetRequiredService<IFuturesMarketSessionAuthority>().Current;
        Assert.True(session.IsValid);
        var valueDate = session.OperationalValueDate;
        var manifest = services.GetRequiredService<DatasetDesiredSubscriptionRegistry>().Set("GLBX.MDP3", valueDate,
        [
            new DatabentoContractRegistration
            {
                DomainContractId = "ES20261218", ProviderContractName = "ESZ6",
                AssetTypeId = AssetTypeId.Futures, RootSymbol = "ES", Dataset = "GLBX.MDP3",
                OnTheRun = true, Rollover = true
            }
        ]);

        // Full watchdog qualification requires ES and both VX roles, not an ES-only fixture.
        var vxManifest = services.GetRequiredService<DatasetDesiredSubscriptionRegistry>().Set("XCBF.PITCH", valueDate,
        [
            new DatabentoContractRegistration
            {
                DomainContractId = "VX20261216", ProviderContractName = "VXZ6",
                AssetTypeId = AssetTypeId.Futures, RootSymbol = "VX", Dataset = "XCBF.PITCH",
                OnTheRun = true, Rollover = true
            },
            new DatabentoContractRegistration
            {
                DomainContractId = "VX20270120", ProviderContractName = "VXF7",
                AssetTypeId = AssetTypeId.Futures, RootSymbol = "VX", Dataset = "XCBF.PITCH",
                OnTheRun = false, Rollover = true
            }
        ]);
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        await services.GetRequiredService<ITickAggregationEventPublisher>().StartAsync(timeout.Token);
        var workers = services.GetRequiredService<DatasetWorkerProcessRecoveryService>();
        var original = await workers.StartOwnedAsync(
            services.GetRequiredService<DatabentoSupervisedWorkerOptions>().CreateStartRequest(manifest), timeout.Token);
        var originalVx = await workers.StartOwnedAsync(
            services.GetRequiredService<DatabentoSupervisedWorkerOptions>().CreateStartRequest(vxManifest), timeout.Token);
        var result = await services.GetRequiredService<IDatabentoRecoveryRequester>()
            .HardResetRecoveryAsync(new DatabentoHardRecoveryRequest(Guid.NewGuid(), valueDate,
                original.GenerationId, nameof(NewRecoveryCompositionIntegrationTests), "Synthetic running-generation replacement"),
                timeout.Token);

        Assert.NotNull(result);
        Assert.True(result.Outcome == DatabentoRecoveryRequestOutcome.FullyHealthy,
            $"{result.Outcome}: {result.Detail}; hard={result.HardResult?.FailedStage} "
            + $"{result.HardResult?.Detail}; attempts="
            + string.Join(" | ", result.HardResult?.AttemptEvidence.Select(item =>
                $"{item.Attempt}:{item.Stage}:{item.FailureType}:{item.Detail}"
                + $":cleanup={item.CleanupFailureType}:{item.CleanupDetail}") ?? []));
        Assert.Equal(2, workers.Current.Count);
        var replacement = Assert.Single(workers.Current, worker => worker.Dataset == manifest.Dataset);
        var replacementVx = Assert.Single(workers.Current, worker => worker.Dataset == vxManifest.Dataset);
        Assert.NotEqual(originalVx.ProcessId, replacementVx.ProcessId);
        Assert.NotEqual(originalVx.GenerationId, replacementVx.GenerationId);
        Assert.True(HasExited(originalVx.ProcessId));
        Assert.Equal(result.HardResult!.DatasetGenerations[vxManifest.Dataset], replacementVx.GenerationId);
        Assert.NotEqual(original.GenerationId, replacement.GenerationId);
        Assert.NotEqual(original.ProcessId, replacement.ProcessId);
        Assert.Equal(result.HardResult!.DatasetGenerations["GLBX.MDP3"], replacement.GenerationId);
        Assert.False(services.GetRequiredService<IApiFatalRecoveryShutdown>().IsRequested);
        Assert.True(HasExited(original.ProcessId), "The old worker must be gone before recovery succeeds.");
        var apiHealth = await services.GetRequiredService<MarketDataRuntimeHealthCheck>()
            .CheckHealthAsync(new HealthCheckContext(), timeout.Token);
        Assert.Equal(HealthStatus.Healthy, apiHealth.Status);
        var pipelineHealth = await services.GetRequiredService<ILivePipelineProbe>()
            .CheckAsync(timeout.Token);
        Assert.DoesNotContain(pipelineHealth.Checks, check =>
            (check.Component is "Databento feed" or "Native diagnostics")
            && (check.Status is "Unknown" or "Degraded" or "Unhealthy"));
        await VerifyContinuingDurableWritesAsync(valueDate, timeout.Token);
        await VerifyContinuingDurableWritesAsync(valueDate, timeout.Token, "VX20261216");
        await VerifyContinuingDurableWritesAsync(valueDate, timeout.Token, "VX20270120");
        Console.WriteLine($"SYNTHETIC_HARD_RESET_VERIFIED correlation={result.CorrelationId:D} oldPid={original.ProcessId} newPid={replacement.ProcessId} generation={replacement.GenerationId:D}");

        // Exercise the public actor command after proving the coordinator directly.
        await VerifyNatsCommandRecoveryAsync(services, valueDate, timeout.Token);
    }

    [Fact]
    public async Task Infrastructure_failure_requests_shutdown_and_rejects_later_recovery()
    {
        var probes = Enum.GetValues<RecoveryInfrastructureKind>()
            .Select(kind => new MutableRecoveryProbe(kind)).ToArray();
        var postgres = probes.Single(item => item.Kind == RecoveryInfrastructureKind.PostgreSql);
        postgres.Qualified = false;
        await using var root = new KestrelWebApplicationFactory<ApiServerEntryPoint>();
        await using var factory = root.WithWebHostBuilder(builder => builder
            .UseEnvironment("Development")
            .UseSetting("AppSettings:Databento:DeploymentProfile", "SyntheticCi")
            .UseSetting("AppSettings:Databento:DataSource", "Synthetic")
            .UseSetting("MarketDataRecovery:Stage3:Enabled", "true")
            .UseSetting("MarketDataRecovery:Stage3:WorkerAssemblyPath",
                "../../../../TomasAI.IFM.Application.MarketData.Worker/bin/Debug/net10.0/TomasAI.IFM.Application.MarketData.Worker.dll")
            .UseSetting("MarketDataRecovery:HardRecovery:Pipeline:Enabled", "true")
            .UseSetting("MarketDataRecovery:HardRecovery:SoftGate:MaximumRounds", "1")
            .UseSetting("MarketDataRecovery:HardRecovery:CandidateProof:ProofTimeout", "00:00:12")
            .UseSetting("MarketDataRecovery:HardRecovery:Pipeline:DownstreamTimeout", "00:00:20")
            .ConfigureTestServices(services =>
            {
                services.RemoveAll<IApiFatalRecoveryShutdown>();
                services.AddSingleton<IApiFatalRecoveryShutdown, NonTerminatingFatalRecorder>();
                services.RemoveAll<IRecoveryInfrastructureProbe>();
                foreach (var probe in probes)
                    services.AddSingleton<IRecoveryInfrastructureProbe>(probe);
            }));

        var services = factory.Services;
        var valueDate = services.GetRequiredService<IFuturesMarketSessionAuthority>()
            .Current.OperationalValueDate;
        services.GetRequiredService<DatasetDesiredSubscriptionRegistry>().Set("GLBX.MDP3", valueDate,
        [
            new DatabentoContractRegistration
            {
                DomainContractId = "ES20261218", ProviderContractName = "ESZ6",
                AssetTypeId = AssetTypeId.Futures, RootSymbol = "ES", Dataset = "GLBX.MDP3",
                OnTheRun = true, Rollover = true
            }
        ]);
        var requester = services.GetRequiredService<IDatabentoRecoveryRequester>();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        var first = await requester.HardResetRecoveryAsync(new DatabentoHardRecoveryRequest(
            Guid.NewGuid(), valueDate, Guid.Empty, nameof(NewRecoveryCompositionIntegrationTests),
            "Injected PostgreSQL probe outage"), timeout.Token);

        Assert.NotNull(first);
        Assert.Equal(DatabentoRecoveryRequestOutcome.Unrecoverable, first.Outcome);
        var worker = Assert.Single(services.GetRequiredService<DatasetWorkerProcessRecoveryService>().Current);
        Assert.Equal(worker.GenerationId, first.HardResult!.DatasetGenerations["GLBX.MDP3"]);
        postgres.Qualified = true;
        var recovered = await requester.HardResetRecoveryAsync(new DatabentoHardRecoveryRequest(
            Guid.NewGuid(), valueDate, Guid.NewGuid(), nameof(NewRecoveryCompositionIntegrationTests),
            "Infrastructure restored"), timeout.Token);

        Assert.NotNull(recovered);
        Assert.Same(first, recovered);
        Assert.Equal(worker.GenerationId,
            Assert.Single(services.GetRequiredService<DatasetWorkerProcessRecoveryService>().Current).GenerationId);
        Assert.True(services.GetRequiredService<IApiFatalRecoveryShutdown>().IsRequested);
    }

    [LiveDatabentoRecoveryFact]
    public async Task Isolated_active_ES_and_VX_generations_qualify_even_when_one_dataset_has_no_tick()
    {
        await using var root = new KestrelWebApplicationFactory<ApiServerEntryPoint>();
        await using var factory = root.WithWebHostBuilder(builder => builder
            .UseEnvironment("Development")
            .UseSetting("AppSettings:Databento:DeploymentProfile", "Development")
            .UseSetting("AppSettings:Databento:DataSource", "DatabentoLive")
            .UseSetting("MarketDataRecovery:Stage3:Enabled", "true")
            .UseSetting("MarketDataRecovery:Stage3:AllowDevelopmentLiveQualification", "true")
            .UseSetting("MarketDataRecovery:Stage3:WorkerAssemblyPath",
                "../../../../TomasAI.IFM.Application.MarketData.Worker/bin/Debug/net10.0/TomasAI.IFM.Application.MarketData.Worker.dll")
            .UseSetting("MarketDataRecovery:HardRecovery:Pipeline:Enabled", "true")
            .ConfigureTestServices(services =>
            {
                services.RemoveAll<IApiFatalRecoveryShutdown>();
                services.AddSingleton<IApiFatalRecoveryShutdown, NonTerminatingFatalRecorder>();
            }));
        var services = factory.Services;
        var workerOptions = services.GetRequiredService<DatabentoSupervisedWorkerOptions>();
        Assert.Equal(FeedDeploymentProfile.Development, workerOptions.DeploymentProfile);
        Assert.Equal(FeedDataSourceMode.DatabentoLive, workerOptions.DataSource);
        var session = services.GetRequiredService<IFuturesMarketSessionAuthority>().Current;
        Assert.NotNull(session.ActiveValueDate);
        var valueDate = session.OperationalValueDate;
        var manifest = services.GetRequiredService<DatasetDesiredSubscriptionRegistry>().Set("GLBX.MDP3", valueDate,
        [
            new DatabentoContractRegistration
            {
                DomainContractId = "ES20261218", ProviderContractName = "ESZ6",
                AssetTypeId = AssetTypeId.Futures, RootSymbol = "ES", Dataset = "GLBX.MDP3",
                OnTheRun = true, Rollover = true
            }
        ]);
        var vxManifest = services.GetRequiredService<DatasetDesiredSubscriptionRegistry>().Set("XCBF.PITCH", valueDate,
        [
            new DatabentoContractRegistration
            {
                DomainContractId = "VX20261021", ProviderContractName = "VX/V6",
                AssetTypeId = AssetTypeId.Futures, RootSymbol = "VX", Dataset = "XCBF.PITCH",
                OnTheRun = true, Rollover = true
            },
            new DatabentoContractRegistration
            {
                DomainContractId = "VX20261118", ProviderContractName = "VX/X6",
                AssetTypeId = AssetTypeId.Futures, RootSymbol = "VX", Dataset = "XCBF.PITCH",
                OnTheRun = false, Rollover = true
            }
        ]);
        // Allow the initial worker and replacement their production startup budgets plus soft proof.
        var startBudget = services.GetRequiredService<DatabentoStage3Options>().WorkerStartTimeout;
        using var timeout = new CancellationTokenSource(startBudget * 2 + TimeSpan.FromMinutes(2));
        await services.GetRequiredService<ITickAggregationEventPublisher>().StartAsync(timeout.Token);
        var workers = services.GetRequiredService<DatasetWorkerProcessRecoveryService>();
        var original = await workers.StartOwnedAsync(workerOptions.CreateStartRequest(manifest), timeout.Token);
        var originalVx = await workers.StartOwnedAsync(workerOptions.CreateStartRequest(vxManifest), timeout.Token);
        var result = await services.GetRequiredService<IDatabentoRecoveryRequester>()
            .HardResetRecoveryAsync(new DatabentoHardRecoveryRequest(Guid.NewGuid(), valueDate,
                original.GenerationId, nameof(NewRecoveryCompositionIntegrationTests), "Observed isolated live replacement"),
                timeout.Token);

        Assert.NotNull(result);
        Assert.True(result.Outcome == DatabentoRecoveryRequestOutcome.FullyHealthy,
            $"{result.Outcome}: {result.Detail}; hard={result.HardResult?.FailedStage} "
            + $"{result.HardResult?.Detail}; attempts="
            + string.Join(" | ", result.HardResult?.AttemptEvidence.Select(item =>
                $"{item.Attempt}:{item.Stage}:{item.FailureType}:{item.Detail}") ?? []));
        Assert.Equal(2, workers.Current.Count);
        var worker = Assert.Single(workers.Current, current => current.Dataset == manifest.Dataset);
        var vxWorker = Assert.Single(workers.Current, current => current.Dataset == vxManifest.Dataset);
        Assert.NotEqual(original.GenerationId, worker.GenerationId);
        Assert.NotEqual(original.ProcessId, worker.ProcessId);
        Assert.Equal(result.HardResult!.DatasetGenerations["GLBX.MDP3"], worker.GenerationId);
        Assert.NotEqual(originalVx.GenerationId, vxWorker.GenerationId);
        Assert.NotEqual(originalVx.ProcessId, vxWorker.ProcessId);
        Assert.Equal(result.HardResult.DatasetGenerations["XCBF.PITCH"], vxWorker.GenerationId);
        Assert.True(worker.ProcessId > 0);
        Assert.False(services.GetRequiredService<IApiFatalRecoveryShutdown>().IsRequested);
        Assert.True(HasExited(original.ProcessId));
        Assert.True(HasExited(originalVx.ProcessId));
        await VerifyContinuingDurableWritesAsync(valueDate, timeout.Token);
        Console.WriteLine($"ISOLATED_LIVE_HARD_RESET_OK correlation={result.CorrelationId:D} "
            + $"generation={worker.GenerationId:D} workerPid={worker.ProcessId} "
            + $"vxGeneration={vxWorker.GenerationId:D} vxWorkerPid={vxWorker.ProcessId} "
            + $"attempts={result.HardResult.Attempts} marketState={session.State}");
    }

    static async Task VerifyNatsCommandRecoveryAsync(IServiceProvider services, DateOnly valueDate, CancellationToken token)
    {
        var workers = services.GetRequiredService<DatasetWorkerProcessRecoveryService>();
        var originals = workers.Current.ToDictionary(worker => worker.Dataset, StringComparer.Ordinal);
        var terminal = new TaskCompletionSource<IEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        var notified = new TaskCompletionSource<MarketDataFeedResetStreamingEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        var listener = new NatsActorEventListener(new NatsEventListenerOptions
        {
            Url = Environment.GetEnvironmentVariable("IFM_CONFIGURED_TEST_NATS_URL")
                ?? throw new InvalidOperationException("An isolated actor broker is required.")
        }, services.GetRequiredService<ILogger<NatsActorEventListener>>());
        try
        {
            await listener.StartAsync($"hard-reset-verification-{Guid.NewGuid():N}", new()
            {
                [new ActorMailboxId(ActorType.Event, MarketDataFeedResetCompleteEvent.Actor)] =
                    [MarketDataFeedResetCompleteEvent.Verb, MarketDataFeedResetFailEvent.Verb, MarketDataFeedResetStreamingEvent.Verb]
            }, (verb, message) =>
            {
                if (verb == MarketDataFeedResetStreamingEvent.Verb)
                {
                    notified.TrySetResult(message.AsEvent<MarketDataFeedResetStreamingEvent>()!);
                    return ValueTask.CompletedTask;
                }
                IEvent completed = verb == MarketDataFeedResetCompleteEvent.Verb
                    ? message.AsEvent<MarketDataFeedResetCompleteEvent>()!
                    : message.AsEvent<MarketDataFeedResetFailEvent>()!;
                terminal.TrySetResult(completed);
                return ValueTask.CompletedTask;
            });
            var api = new MarketDataFeedCommandApi(services.GetRequiredService<IActorProducer>());
            var response = await api.ResetMarketDataFeedAsync([SampleData.FuturesContract], valueDate).WaitAsync(token);
            Assert.True(response.Success, response.ErrorMessage);
            var completed = await terminal.Task.WaitAsync(TimeSpan.FromSeconds(60), token);
            Assert.True(completed is MarketDataFeedResetCompleteEvent,
                (completed as MarketDataFeedResetFailEvent)?.ErrorData ?? completed.EventName);
            Assert.Equal(response.Value, ((MarketDataFeedResetCompleteEvent)completed).CommandId);
            Assert.Equal(response.Value, (await notified.Task.WaitAsync(TimeSpan.FromSeconds(15), token)).CommandId);
            var replacements = workers.Current;
            Assert.Equal(originals.Count, replacements.Count);
            foreach (var replacement in replacements)
            {
                var original = originals[replacement.Dataset];
                Assert.NotEqual(original.ProcessId, replacement.ProcessId);
                Assert.NotEqual(original.GenerationId, replacement.GenerationId);
                Assert.True(HasExited(original.ProcessId));
                Console.WriteLine($"NATS_COMMAND_WORKER_REPLACED dataset={replacement.Dataset} oldPid={original.ProcessId} newPid={replacement.ProcessId} generation={replacement.GenerationId:D}");
            }
            Assert.Equal(valueDate, services.GetRequiredService<IDatabentoLifecycleRuntime>().ActiveValueDate);
            await services.GetRequiredService<IMarketDataLifecycleRequests>().ProbeAsync(token);
            var health = services.GetRequiredService<IMarketDataLifecycleRequests>().Current;
            Assert.True(health.State == DatabentoLifecycleState.Healthy, $"{health.State}: {health.Reason}");
            Assert.False(services.GetRequiredService<IApiFatalRecoveryShutdown>().IsRequested);
            await VerifyContinuingDurableWritesAsync(valueDate, token);
            await VerifyContinuingDurableWritesAsync(valueDate, token, "VX20261216");
            await VerifyContinuingDurableWritesAsync(valueDate, token, "VX20270120");
            Console.WriteLine($"NATS_COMMAND_HARD_RESET_VERIFIED command={response.Value:D} datasets={replacements.Count} health={health.State}");
        }
        finally { await listener.StopAsync(); }
    }

    static bool HasExited(int processId)
    {
        try { using var process = Process.GetProcessById(processId); return process.HasExited; }
        catch (ArgumentException) { return true; }
    }

    static async Task VerifyContinuingDurableWritesAsync(DateOnly valueDate, CancellationToken token,
        string contractId = "ES20261218")
    {
        var connection = new CassandraConnectionStringBuilder(
            Environment.GetEnvironmentVariable("IFM_TEST_MARKET_DATA_CONNECTION")
            ?? throw new InvalidOperationException("An isolated market-data database is required."));
        Assert.StartsWith("ifm_synthetic_", connection.DefaultKeyspace);
        using var cluster = Cluster.Builder().AddContactPoints(connection.ContactPoints)
            .WithPort(connection.Port).Build();
        using var session = await cluster.ConnectAsync(connection.DefaultKeyspace).WaitAsync(token);
        var date = new LocalDate(valueDate.Year, valueDate.Month, valueDate.Day);
        async Task<long> LatestAsync()
        {
            long latest = 0;
            foreach (var table in new[] { "tick_trade_data", "tick_quote_data" })
            {
                var rows = await session.ExecuteAsync(new SimpleStatement(
                    $"SELECT aggregation_timestamp_utc_ticks FROM {table} WHERE asset_type_id=? AND contract_id=? AND value_date=? ORDER BY value_date DESC, aggregation_time DESC, sequence_id DESC LIMIT 1",
                    (sbyte)AssetTypeId.Futures, contractId, date)).WaitAsync(token);
                latest = Math.Max(latest, rows.FirstOrDefault()?.GetValue<long>("aggregation_timestamp_utc_ticks") ?? 0);
            }
            return latest;
        }
        var first = await LatestAsync();
        Assert.True(first > 0, "Candidate proof must leave an actual durable trade or quote tick row.");
        using var progress = CancellationTokenSource.CreateLinkedTokenSource(token);
        progress.CancelAfter(TimeSpan.FromSeconds(15));
        while (await LatestAsync() <= first)
            await Task.Delay(100, progress.Token);
        Console.WriteLine($"DURABLE_POST_ADMISSION_PROGRESS contract={contractId} valueDate={valueDate:yyyy-MM-dd} firstTick={first}");
    }

    sealed class MutableRecoveryProbe(RecoveryInfrastructureKind kind) : IRecoveryInfrastructureProbe
    {
        volatile bool qualified = true;
        public string Name => Kind.ToString();
        public RecoveryInfrastructureKind Kind { get; } = kind;
        public bool Qualified { get => qualified; set => qualified = value; }
        public Task<RecoveryProbeResult> CheckAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new RecoveryProbeResult(Name, Kind, Qualified,
                Qualified ? "Available" : "Injected outage"));
    }

    sealed class NonTerminatingFatalRecorder : IApiFatalRecoveryShutdown
    {
        public bool IsRequested { get; private set; }
        public Task RequestAsync(FatalRecoveryReport report)
        {
            IsRequested = true;
            return Task.CompletedTask;
        }
        public void MarkGracefulShutdownComplete() { }
        public void FailAndExit(Exception exception) => throw exception;
    }
}

sealed class LiveDatabentoRecoveryFactAttribute : FactAttribute
{
    public LiveDatabentoRecoveryFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("IFM_RUN_LIVE_DATABENTO_RECOVERY") != "1")
            Skip = "Requires an explicitly observed isolated live Databento qualification.";
    }
}
