using System.Collections.Immutable;
using System.Diagnostics;
using FluentAssertions;
using NSubstitute;
using NATS.Client.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Application.MarketData.Databento;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Remote;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;
namespace TomasAI.IFM.Application.MarketData.UnitTests;

/// <summary>Uses real NATS and actual named child processes on isolated subjects; never touches live feed subjects.</summary>
public sealed class MarketDataHostNatsRecoveryIntegrationTests
{
    [Fact(Timeout = 60000)]
    public async Task Hung_exact_option_repair_uses_the_callers_deadline_instead_of_the_five_second_worker_timeout()
    {
        await using var fixture = await Fixture.StartAsync(); await using var options = fixture.Worker("Options");
        var before = await options.StartAsync(Request());
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        var lease = OptionLease(before); await AcquireReadyAsync(options, lease, timeout.Token);
        await fixture.Connection.PostAsync<bool>($"workers/Options/{before.WorkerInstanceId:D}/hang", new { }, timeout.Token);
        IMarketDataApi api = new MarketDataApiClient(fixture.Connection, Substitute.For<IMarketDataApi>());
        var deadline = DateTimeOffset.UtcNow.AddSeconds(8); var elapsed = Stopwatch.StartNew();
        await Assert.ThrowsAnyAsync<Exception>(() => api.RepairOptionObservationsAsync(new(Guid.NewGuid(), ValueDate,
            before.GenerationId, [lease.Options[0].Pricing.Contract.ContractId], deadline), timeout.Token));
        Assert.InRange(elapsed.Elapsed.TotalSeconds, 7.5, 12);
        var recovered = await api.RecoverMarketDataAsync(new(Guid.NewGuid(), "Options", "GLBX.MDP3", before.WorkerInstanceId,
            before.GenerationId, ValueDate, "Clean up isolated hung-worker test", true, DateTimeOffset.UtcNow.AddSeconds(20)), timeout.Token);
        Assert.True(recovered.Recovered, recovered.Error);
    }

    [Fact(Timeout = 60000)]
    public async Task Retained_exact_option_observation_and_targeted_repair_cross_real_Host_NATS_without_restarting_futures()
    {
        await using var fixture = await Fixture.StartAsync();
        await using var options = fixture.Worker("Options"); await using var futures = fixture.Worker("Futures");
        var before = await options.StartAsync(Request());
        var future = await futures.StartAsync(Request("XCBF.PITCH", "VX"));
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(40));
        var lease = OptionLease(before); await AcquireReadyAsync(options, lease, deadline.Token);
        IMarketDataApi api = new MarketDataApiClient(fixture.Connection, Substitute.For<IMarketDataApi>());
        MarketDataReadSnapshot snapshot;
        do { snapshot = await api.GetBackendReadSnapshotAsync(deadline.Token);
            if (snapshot.OptionObservations?.Length > 0) break;
            await Task.Delay(50, deadline.Token);
        } while (true);
        var frame = Assert.Single(snapshot.OptionObservations!);
        var admissions = new DatasetWorkerAdmissionRegistry();
        var publisher = Substitute.For<TomasAI.IFM.Shared.MarketData.Contracts.TickAggregation.ITickAggregationEventPublisher>();
        var ingress = new DatasetPublicationIngress(admissions, publisher, Substitute.For<TomasAI.IFM.Application.MarketData.MarketOutlook.IMarketDataOperationsRecorder>());
        Assert.False(await ingress.AcceptAsync(frame));
        admissions.Admit(new(frame.Dataset, frame.ValueDate, frame.WorkerInstanceId, frame.GenerationId, frame.ManifestRevision));
        Assert.True(await ingress.AcceptAsync(frame)); Assert.False(await ingress.AcceptAsync(frame));
        using var optionCache = new TomasAI.IFM.Application.MarketData.OptionChainCache.OptionChainCache();
        var remoteAdmissions = new MarketDataRemoteAdmission();
        var supervisor = Substitute.For<TomasAI.IFM.Shared.EventModelActor.Contracts.IActorSupervisor>(); supervisor.IsReady.Returns(true);
        var received = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        publisher.PublishAsync(Arg.Any<TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events.FuturesTickTradeDataChangedEvent>(), Arg.Any<CancellationToken>())
            .Returns(call => { if (call.Arg<TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events.FuturesTickTradeDataChangedEvent>().OptionMarketPriceObservation is not null) received.TrySetResult(); return ValueTask.CompletedTask; });
        using (var receiver = new MarketDataClientReceiver(fixture.Connection, api, new(optionCache), remoteAdmissions, publisher,
            Substitute.For<TomasAI.IFM.Application.MarketData.MarketOutlook.IMarketDataOperationsRecorder>(), supervisor, NullLogger<MarketDataClientReceiver>.Instance))
        {
            await receiver.StartAsync(deadline.Token);
            try { await received.Task.WaitAsync(TimeSpan.FromSeconds(8), deadline.Token); }
            finally { await receiver.StopAsync(deadline.Token); }
        }
        Assert.True(remoteAdmissions.Options.TryGet(frame.Dataset, out var admitted)); Assert.Equal(frame.GenerationId, admitted.GenerationId);
        var operation = Guid.NewGuid(); var until = DateTimeOffset.UtcNow.AddSeconds(20);
        var contract = lease.Options[0].Pricing.Contract.ContractId;
        var repaired = await api.RepairOptionObservationsAsync(new(operation, ValueDate, before.GenerationId, [contract], until), deadline.Token);
        Assert.Equal([contract], repaired.Reconnected); Assert.Empty(repaired.Errors);
        var missing = await api.RepairOptionObservationsAsync(new(operation, ValueDate, before.GenerationId, ["missing-contract"], until), deadline.Token);
        Assert.Empty(missing.Reconnected); Assert.Contains("no active exact-contract subscription", Assert.Single(missing.Errors));
        Assert.Equal(before.ProcessId, (await options.GetHealthAsync(deadline.Token)).ProcessId);
        Assert.Equal(future.ProcessId, (await futures.GetHealthAsync(deadline.Token)).ProcessId);
        await options.ReleaseOptionChainAsync(new(lease.ScopeId, lease.LeaseId, before.GenerationId), deadline.Token);
        var released = await api.RepairOptionObservationsAsync(new(operation, ValueDate, before.GenerationId, [contract], until), deadline.Token);
        Assert.Empty(released.Reconnected); Assert.Single(released.Errors);
    }

    [Fact(Timeout = 60000)]
    public async Task Shared_scope_refresh_then_stale_owner_renewal_and_process_replacement_preserve_one_canonical_context()
    {
        await using var fixture = await Fixture.StartAsync();
        await using var options = fixture.Worker("Options");
        var process = await options.StartAsync(Request());
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        var first = OptionLease(process);
        await AcquireReadyAsync(options, first, deadline.Token);
        var original = await options.AcquireOptionChainAsync(first, deadline.Token);
        var second = first with { LeaseId = Guid.NewGuid() };
        (await options.AcquireOptionChainAsync(second, deadline.Token)).Active.Should().BeTrue();
        var refresh = first with { ExpectedContextDigest = original.ContextDigest,
            Options = first.Options.Select(option => option with { Pricing = option.Pricing with {
                ValidUntilUtc = option.Pricing.ValidUntilUtc.AddSeconds(-1) } }).ToImmutableArray() };
        var refreshed = await options.AcquireOptionChainAsync(refresh, deadline.Token);
        refreshed.Active.Should().BeTrue("{0}", refreshed.Failure);
        var renewed = await options.AcquireOptionChainAsync(second, deadline.Token);
        renewed.Active.Should().BeTrue("a stale owner's renewal must use the current shared context: {0}", renewed.Failure);
        renewed.ContextDigest.Should().Be(refreshed.ContextDigest);
        IMarketDataApi api = new MarketDataApiClient(fixture.Connection, Substitute.For<IMarketDataApi>());
        var recovered = await api.RecoverMarketDataAsync(new(Guid.NewGuid(), "Options", "GLBX.MDP3", process.WorkerInstanceId,
            process.GenerationId, ValueDate, "Shared scope recovery", true, DateTimeOffset.UtcNow.AddSeconds(30)), deadline.Token);
        recovered.Recovered.Should().BeTrue(recovered.Error);
        (await options.GetHealthAsync(deadline.Token)).Healthy.Should().BeTrue();
    }

    [Theory(Timeout = 165000)]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Real_options_children_retry_context_restoration_beyond_three_failures_until_success_or_full_deadline(bool permanent)
    {
        var output = Environment.GetEnvironmentVariable("IFM_MARKETDATA_TEST_HOST_DIRECTORY")
            ?? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.artifacts/market-data-host"));
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["MarketDataHost:WorkerDirectory"] = Path.Combine(output, "Workers"),
            ["MarketDataHost:DeploymentProfile"] = "SyntheticCi", ["MarketDataHost:DataSource"] = "Synthetic" }).Build();
        await using var manager = new NatsConnectionManager();
        var connection = new MarketDataHostConnection(manager, Environment.GetEnvironmentVariable("IFM_MARKETDATA_TEST_NATS_URL") ?? "nats://127.0.0.1:4222", "retrytest" + Guid.NewGuid().ToString("N"));
        var attempts = 0;
        var firstAttempt = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseAttempt = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var state = new TomasAI.IFM.Application.MarketData.Host.MarketDataHostState(new DatasetWorkerCurrentValues(), new DatasetWorkerAdmissionRegistry()) {
            RequalifyOptions = async (lease, token) => {
                var attempt = Interlocked.Increment(ref attempts);
                if (attempt == 1 && !permanent) { firstAttempt.TrySetResult(); await releaseAttempt.Task.WaitAsync(token); }
                if (attempt <= 4 || permanent) throw new InvalidOperationException("Injected context qualification failure");
                return lease;
            } };
        await using var host = new TomasAI.IFM.Application.MarketData.Host.MarketDataWorkerHost(config,
            NullLogger<TomasAI.IFM.Application.MarketData.Host.MarketDataWorkerHost>.Instance, connection, state);
        await host.StartAsync(default);
        var request = Request();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(150));
        var before = await host.StartWorkerAsync("Options", new(request.Manifest!, request.WorkerInstanceId, request.GenerationId), deadline.Token);
        var future = Request("XCBF.PITCH", "VX");
        var unchanged = await host.StartWorkerAsync("Futures", new(future.Manifest!, future.WorkerInstanceId, future.GenerationId), deadline.Token);
        var lease = OptionLease(before) with { LeaseExpiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(119) };
        WorkerOptionChainResult acquired;
        do { acquired = await host.AcquireOptionLeaseAsync("Options", before.WorkerInstanceId, lease, deadline.Token);
            if (acquired.Failure?.Code == "UnderlyingQuoteUnavailable") await Task.Delay(50, deadline.Token);
        } while (acquired.Failure?.Code == "UnderlyingQuoteUnavailable");
        acquired.Active.Should().BeTrue("{0}", acquired.Failure);
        var ownership = new WorkerOptionChainOwnership(1, "IFM", [new(Guid.NewGuid(), [lease.Options[0].Pricing.Contract.ContractId])],
            WorkerOptionChainRuntime.PhysicalDigest(lease.Options));
        (await host.ApplyCommittedOwnershipAsync("Options", before.WorkerInstanceId, lease,
            new(lease.ScopeId, Guid.Empty, before.GenerationId, ownership), deadline.Token)).Failure.Should().BeNull();
        var timer = Stopwatch.StartNew();
        var requestRecovery = new MarketDataRecoveryRequest(Guid.NewGuid(), "Options", "GLBX.MDP3", before.WorkerInstanceId,
            before.GenerationId, ValueDate, "Repeated context restoration failures", true, DateTimeOffset.UtcNow.AddMinutes(2));
        var recovering = host.RecoverAsync(requestRecovery, deadline.Token);
        Task<MarketDataRecoveryResult>? joined = null;
        if (!permanent)
        {
            await firstAttempt.Task.WaitAsync(TimeSpan.FromSeconds(15));
            joined = host.RecoverAsync(requestRecovery with { OperationId = Guid.NewGuid() }, deadline.Token);
            releaseAttempt.SetResult();
        }
        var result = await recovering;
        if (joined is not null) { var shared = await joined; shared.Recovered.Should().BeTrue(shared.Error); shared.GenerationId.Should().Be(result.GenerationId); }
        attempts.Should().BeGreaterThan(4);
        result.Recovered.Should().Be(!permanent, result.Error);
        if (permanent) timer.Elapsed.TotalSeconds.Should().BeInRange(119, 140);
        else timer.Elapsed.Should().BeLessThan(TimeSpan.FromMinutes(2));
        (await host.RequireWorker("Futures", unchanged.WorkerInstanceId).GetHealthAsync(deadline.Token)).Healthy.Should().BeTrue();
        host.GetBackendStatus().Datasets.Single(dataset => dataset.Role == "Futures").ProcessId.Should().Be(unchanged.ProcessId);
    }
    [Theory(Timeout = 45000)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [InlineData(true, false)]
    public async Task Development_fault_contract_is_gated_and_targets_only_current_Options_generation(bool enabled, bool development)
    {
        await using var fixture = await Fixture.StartAsync(developmentFaults: enabled, developmentEnvironment: development);
        await using var options = fixture.Worker("Options");
        await using var futures = fixture.Worker("Futures");
        var before = await options.StartAsync(Request());
        var other = await futures.StartAsync(Request("XCBF.PITCH", "VX"));
        IMarketDataApi api = new MarketDataApiClient(fixture.Connection, Substitute.For<IMarketDataApi>());
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var request = new MarketDataDevelopmentFaultRequest(Guid.NewGuid(), before.WorkerInstanceId, before.GenerationId, ValueDate);
        await Assert.ThrowsAnyAsync<Exception>(() => api.InjectDevelopmentOptionWorkerFaultAsync(request with { GenerationId = Guid.NewGuid() }, deadline.Token));
        await Assert.ThrowsAnyAsync<Exception>(() => api.InjectDevelopmentOptionWorkerFaultAsync(request with { WorkerInstanceId = other.WorkerInstanceId, GenerationId = other.GenerationId }, deadline.Token));
        if (!enabled || !development)
        {
            var error = await Assert.ThrowsAnyAsync<Exception>(() => api.InjectDevelopmentOptionWorkerFaultAsync(request, deadline.Token));
            error.Message.Should().Contain("Development verification");
            (await options.GetHealthAsync(deadline.Token)).Healthy.Should().BeTrue();
        }
        else
        {
            (await api.InjectDevelopmentOptionWorkerFaultAsync(request, deadline.Token)).Should().BeTrue();
            var reset = await api.RecoverMarketDataAsync(new(Guid.NewGuid(), "Options", "GLBX.MDP3", before.WorkerInstanceId,
                before.GenerationId, ValueDate, "Armed Development injection integration test", true, DateTimeOffset.UtcNow.AddSeconds(25)), deadline.Token);
            reset.Recovered.Should().BeTrue(reset.Error);
            (await api.GetBackendStatusAsync(deadline.Token)).Datasets.Single(dataset => dataset.Role == "Options").ProcessId.Should().NotBe(before.ProcessId);
        }
        (await futures.GetHealthAsync(deadline.Token)).Healthy.Should().BeTrue();
        futures.Current.ProcessId.Should().Be(other.ProcessId);
    }
    [Fact(Timeout = 60000)]
    public async Task Emergency_options_replacement_discards_hung_control_restores_lease_and_preserves_futures_process()
    {
        await using var fixture = await Fixture.StartAsync();
        await using var futures = fixture.Worker("Futures");
        await using var options = fixture.Worker("Options");
        var unchanged = await futures.StartAsync(Request("XCBF.PITCH", "VX"));
        var before = await options.StartAsync(Request());
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(40));
        var lease = OptionLease(before); await AcquireReadyAsync(options, lease, deadline.Token);
        await fixture.Connection.PostAsync<bool>($"workers/Options/{before.WorkerInstanceId:D}/hang", new { }, deadline.Token);
        IMarketDataApi api = new MarketDataApiClient(fixture.Connection, Substitute.For<IMarketDataApi>());
        var timer = Stopwatch.StartNew();
        var result = await api.RecoverMarketDataAsync(new(Guid.NewGuid(), "Options", "GLBX.MDP3", before.WorkerInstanceId,
            before.GenerationId, ValueDate, "Emergency latest-state recovery", ReplaceProcessImmediately: true,
            DeadlineUtc: DateTimeOffset.UtcNow.AddSeconds(30)), deadline.Token);
        result.Recovered.Should().BeTrue(result.Error);
        timer.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(30));
        var replaced = (await api.GetBackendStatusAsync(deadline.Token)).Datasets.Single(dataset => dataset.Role == "Options");
        replaced.ProcessId.Should().NotBe(before.ProcessId); replaced.GenerationId.Should().NotBe(before.GenerationId);
        var snapshot = await options.CaptureCompositionSnapshotAsync(new(Guid.NewGuid(), lease.ScopeId, "Daily",
            result.GenerationId, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddSeconds(2), true)
            { SelectionOnly = true, AllowMissingOptionQuotes = true }, deadline.Token);
        snapshot.Snapshot.Should().NotBeNull("the desired option lease must survive the process kill: {0}", snapshot.Failure);
        (await futures.GetHealthAsync(deadline.Token)).Healthy.Should().BeTrue();
        futures.Current.ProcessId.Should().Be(unchanged.ProcessId);
        futures.Current.GenerationId.Should().Be(unchanged.GenerationId);
    }

    static readonly DateOnly ValueDate = TomasAI.IFM.Domain.MarketData.Shared.FuturesTradingValueDate.TryGet(DateTimeOffset.UtcNow, out var date)
        ? date : DateOnly.FromDateTime(DateTime.UtcNow);
    [Fact(Timeout = 30000)]
    public async Task Full_backend_public_API_starts_without_an_API_server_and_serializes_bootstrap_and_failures()
    {
        await using var fixture = await Fixture.StartAsync(fullBackend: true);
        using var cache = new TomasAI.IFM.Application.MarketData.OptionChainCache.OptionChainCache();
        var mirror = new MarketDataReadMirror(cache);
        IMarketDataApi api = new MarketDataApiClient(fixture.Connection, mirror);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var snapshot = await api.GetBackendReadSnapshotAsync(deadline.Token);
        snapshot.Status.Ready.Should().BeTrue();
        snapshot.Status.HostInstanceId.Should().NotBeEmpty();
        snapshot.Status.Datasets.Should().BeEmpty("control startup does not subscribe to live providers");
        mirror.Apply(snapshot).Should().BeTrue();
        api.GetRuntimeStatus().IsValid.Should().BeTrue();
        var unsupported = await api.GetTradeStrategySymbolsAsync((TomasAI.IFM.Domain.Reference.Shared.ViewModels.TradeStrategyFamilyType)999, deadline.Token);
        unsupported.Success.Should().BeFalse();
        unsupported.ErrorCode.Should().Be(400);
    }

    [Fact(Timeout = 45000)]
    public async Task Option_acquisition_through_Host_NATS_and_child_uses_current_UTC_underlying_quotes()
    {
        await using var fixture = await Fixture.StartAsync();
        await using var options = fixture.Worker("Options");
        var process = await options.StartAsync(Request());
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var lease = OptionLease(process);
        await AcquireReadyAsync(options, lease, deadline.Token);
        var snapshot = await options.CaptureCompositionSnapshotAsync(new(Guid.NewGuid(), lease.ScopeId, "Daily",
            process.GenerationId, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddSeconds(2), true)
            { SelectionOnly = true, AllowMissingOptionQuotes = true }, deadline.Token);
        snapshot.Snapshot.Should().NotBeNull("option acquisition must yield a readable scope: {0}", snapshot.Failure);
        var underlying = snapshot.Snapshot!.Instruments.Single().Instrument.Underlying;
        underlying.Should().NotBeNull();
        (DateTimeOffset.UtcNow - underlying!.EventAtUtc).TotalSeconds.Should().BeInRange(-2, 10);
        (DateTimeOffset.UtcNow - underlying.ReceivedAtUtc).TotalSeconds.Should().BeInRange(-2, 10);
        underlying.GenerationId.Should().Be(process.GenerationId);
        (await options.ReleaseOptionChainAsync(new(lease.ScopeId, lease.LeaseId, process.GenerationId), deadline.Token))
            .Active.Should().BeFalse();
    }

    [Fact(Timeout = 60000)]
    public async Task Repeated_chain_captures_through_real_child_pipe_and_NATS_preserve_complete_pricing_evidence()
    {
        await using var fixture = await Fixture.StartAsync();
        await using var options = fixture.Worker("Options");
        var process = await options.StartAsync(Request());
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(40));
        var lease = OptionLease(process);
        var template = lease.Options[0];
        lease = lease with
        {
            SeparateContractConnection = false,
            Options = Enumerable.Range(0, 128).Select(i => template with
            {
                Pricing = template.Pricing with { Contract = template.Pricing.Contract with
                    { ContractId = $"ES-option-{i}", InstrumentId = (uint)(10000 + i), RawSymbol = $"ES-test-{i}" } },
                Strike = 5000 + i
            }).ToImmutableArray()
        };
        await AcquireReadyAsync(options, lease, deadline.Token);
        for (var i = 0; i < 8; i++)
        {
            var snapshot = await options.CaptureCompositionSnapshotAsync(new(Guid.NewGuid(), lease.ScopeId, "Daily",
                process.GenerationId, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddSeconds(2), true)
                { SelectionOnly = true, AllowMissingOptionQuotes = true }, deadline.Token);
            snapshot.Snapshot.Should().NotBeNull("repeated capture {0}: {1}", i, snapshot.Failure);
            var rows = snapshot.Snapshot!.Instruments;
            rows.Length.Should().Be(128);
            foreach (var row in rows)
            {
                var expected = lease.Options.Single(x => x.Pricing.Contract.ContractId == row.Instrument.ContractId);
                PricingSemanticHash.Compute(row.Instrument.Pricing).Should().Be(PricingSemanticHash.Compute(expected.Pricing));
                row.Instrument.Strike.Should().Be(expected.Strike);
            }
        }
        (await options.GetHealthAsync(deadline.Token)).Healthy.Should().BeTrue();
        (await options.ReleaseOptionChainAsync(new(lease.ScopeId, lease.LeaseId, process.GenerationId), deadline.Token)).Active.Should().BeFalse();
    }

    [Fact(Timeout = 45000)]
    public async Task Option_soft_reset_keeps_futures_alive_and_changes_only_option_generation()
    {
        await using var fixture = await Fixture.StartAsync();
        await using var futures = fixture.Worker("Futures");
        await using var options = fixture.Worker("Options");
        var f = await futures.StartAsync(Request());
        var o = await options.StartAsync(Request());
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var reset = await options.ResetAsync(Request().Manifest!, deadline.Token);
        reset.Healthy.Should().BeTrue(reset.Detail);
        reset.GenerationId.Should().NotBe(o.GenerationId);
        options.Current.ProcessId.Should().Be(o.ProcessId);
        (await futures.GetHealthAsync(deadline.Token)).Healthy.Should().BeTrue();
        futures.Current.ProcessId.Should().Be(f.ProcessId);
        futures.Current.GenerationId.Should().Be(f.GenerationId);
    }

    [Fact(Timeout = 45000)]
    public async Task Hung_option_command_cannot_block_emergency_stop_or_fresh_option_restart()
    {
        await using var fixture = await Fixture.StartAsync();
        await using var futures = fixture.Worker("Futures");
        await using var options = fixture.Worker("Options");
        var f = await futures.StartAsync(Request());
        var o = await options.StartAsync(Request());
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await fixture.Connection.PostAsync<bool>($"workers/Options/{o.WorkerInstanceId:D}/hang", new { }, timeout.Token);
        using var commandTimeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(250));
        var blocked = options.GetHealthAsync(commandTimeout.Token);
        await Task.Delay(50);
        var elapsed = Stopwatch.StartNew();
        await options.TerminateAsync().WaitAsync(TimeSpan.FromSeconds(8));
        elapsed.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(8));
        options.Current.Running.Should().BeFalse();
        try { using var old = Process.GetProcessById(o.ProcessId); old.HasExited.Should().BeTrue(); }
        catch (ArgumentException) { /* An absent PID confirms process termination. */ }
        await Assert.ThrowsAnyAsync<Exception>(() => blocked);
        using var healthDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        (await futures.GetHealthAsync(healthDeadline.Token)).Healthy.Should().BeTrue();
        futures.Current.ProcessId.Should().Be(f.ProcessId);
        await using var replacement = fixture.Worker("Options");
        var next = await replacement.StartAsync(Request());
        next.WorkerInstanceId.Should().NotBe(o.WorkerInstanceId);
        next.GenerationId.Should().NotBe(o.GenerationId);
        next.Healthy.Should().BeTrue(next.Detail);
        // Replayed old stop must not kill the replacement.
        await options.TerminateAsync();
        (await replacement.GetHealthAsync(healthDeadline.Token)).Healthy.Should().BeTrue();
    }

    [Fact(Timeout = 45000)]
    public async Task Saturated_command_handlers_and_duplicate_stops_cannot_block_containment()
    {
        await using var fixture = await Fixture.StartAsync();
        await using var options = fixture.Worker("Options");
        var current = await options.StartAsync(Request());
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await fixture.Connection.PostAsync<bool>($"workers/Options/{current.WorkerInstanceId:D}/hang", new { }, timeout.Token);
        var requests = Enumerable.Range(0, 8).Select(_ => fixture.Connection.PostAsync<DatasetWorkerControlFrame>(
            $"workers/Options/{current.WorkerInstanceId:D}/health", new { }, timeout.Token)).ToArray();
        await Task.Delay(100, timeout.Token);
        IMarketDataApi api = new MarketDataApiClient(fixture.Connection, Substitute.For<IMarketDataApi>());
        using var observationDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        (await api.GetBackendStatusAsync(observationDeadline.Token)).Ready.Should().BeTrue("ordinary commands cannot starve health observation");
        await Task.WhenAll(options.TerminateAsync(), options.TerminateAsync()).WaitAsync(TimeSpan.FromSeconds(8));
        options.Current.Running.Should().BeFalse();
        foreach (var request in requests)
            await Assert.ThrowsAnyAsync<Exception>(() => request);
        await using var replacement = fixture.Worker("Options");
        (await replacement.StartAsync(Request())).Healthy.Should().BeTrue();
    }

    [Fact(Timeout = 20000)]
    public async Task Stop_before_start_retires_the_lifetime_without_launching_a_child()
    {
        await using var fixture = await Fixture.StartAsync();
        var request = Request();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await fixture.Connection.PostAsync<bool>($"workers/Options/{request.WorkerInstanceId:D}/stop", new { }, timeout.Token);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Connection.PostAsync<DatasetWorkerProcessSnapshot>(
            "workers/Options/start", new MarketDataWorkerStart(request.Manifest!, request.WorkerInstanceId, request.GenerationId), timeout.Token));
    }

    [Fact(Timeout = 45000)]
    public async Task Both_futures_datasets_and_options_have_independent_processes_and_reset_boundaries()
    {
        await using var fixture = await Fixture.StartAsync();
        await using var es = fixture.Worker("Futures");
        await using var vx = fixture.Worker("Futures");
        await using var options = fixture.Worker("Options");
        var esState = await es.StartAsync(Request());
        var vxState = await vx.StartAsync(Request("XCBF.PITCH", "VX"));
        var optionState = await options.StartAsync(Request());
        new[] { esState.ProcessId, vxState.ProcessId, optionState.ProcessId }.Distinct().Should().HaveCount(3);
        using (var process = Process.GetProcessById(esState.ProcessId)) process.ProcessName.Should().EndWith("Futures-GLBX.MDP3");
        using (var process = Process.GetProcessById(vxState.ProcessId)) process.ProcessName.Should().EndWith("Futures-XCBF.PITCH");
        using (var process = Process.GetProcessById(optionState.ProcessId)) process.ProcessName.Should().EndWith("Options-GLBX.MDP3");
        await vx.TerminateAsync();
        IMarketDataApi api = new MarketDataApiClient(fixture.Connection, Substitute.For<IMarketDataApi>());
        var stopped = await api.GetBackendStatusAsync();
        stopped.Ready.Should().BeTrue("Host control readiness is independent of an individual stopped dataset");
        stopped.Datasets.Single(dataset => dataset.Dataset == "XCBF.PITCH").Lifecycle.Should().Be("Stopped");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        (await es.GetHealthAsync(timeout.Token)).Healthy.Should().BeTrue();
        (await options.GetHealthAsync(timeout.Token)).Healthy.Should().BeTrue();
        es.Current.GenerationId.Should().Be(esState.GenerationId);
        options.Current.GenerationId.Should().Be(optionState.GenerationId);
    }

    [Fact(Timeout = 30000)]
    public async Task Market_data_contract_queries_execute_in_the_dataset_child_and_cannot_bypass_its_lifecycle_owner()
    {
        await using var fixture = await Fixture.StartAsync();
        await using var worker = fixture.Worker("Futures");
        await worker.StartAsync(Request());
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var method = typeof(IMarketDataApi).GetMethod(nameof(IMarketDataApi.GetFuturesContractAsync))!;
        var result = await worker.InvokeMarketDataApiAsync(new(MarketDataApiClient.Signature(method),
            [MarketDataHostConnection.Encode("ES20261218")]), timeout.Token);
        MarketDataHostConnection.Decode<FuturesContractV3ReadModel>(result).ContractId.Should().Be("ES20261218");
        var stop = typeof(IMarketDataApi).GetMethod(nameof(IMarketDataApi.StopAsync))!;
        await Assert.ThrowsAsync<InvalidOperationException>(() => worker.InvokeMarketDataApiAsync(
            new(MarketDataApiClient.Signature(stop), [MarketDataHostConnection.Encode(ValueDate)]), timeout.Token));
        (await worker.GetHealthAsync(timeout.Token)).Healthy.Should().BeTrue();
    }

    [Fact(Timeout = 45000)]
    public async Task Public_API_soft_recovery_is_idempotent_and_reports_the_new_generation()
    {
        await using var fixture = await Fixture.StartAsync();
        await using var options = fixture.Worker("Options");
        var before = await options.StartAsync(Request());
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var lease = OptionLease(before);
        await AcquireReadyAsync(options, lease, deadline.Token);
        IMarketDataApi api = new MarketDataApiClient(fixture.Connection, Substitute.For<IMarketDataApi>());
        var request = new MarketDataRecoveryRequest(Guid.NewGuid(), "Options", "GLBX.MDP3", before.WorkerInstanceId,
            before.GenerationId, ValueDate, "Injected recoverable failure");
        var result = await api.RecoverMarketDataAsync(request);
        result.Recovered.Should().BeTrue(result.Error);
        result.GenerationId.Should().NotBe(before.GenerationId);
        (await api.RecoverMarketDataAsync(request)).GenerationId.Should().Be(result.GenerationId);
        (await api.GetBackendStatusAsync()).Datasets.Single().Lifecycle.Should().Be("Healthy");
        var snapshot = await options.CaptureCompositionSnapshotAsync(new(Guid.NewGuid(), lease.ScopeId, "Daily",
            result.GenerationId, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddSeconds(2), true)
            { SelectionOnly = true, AllowMissingOptionQuotes = true }, deadline.Token);
        snapshot.Snapshot.Should().NotBeNull("the restored option scope must be readable: {0}", snapshot.Failure);
        snapshot.Snapshot!.Instruments.Should().Contain(instrument => instrument.Instrument.ContractId == lease.Options[0].Pricing.Contract.ContractId);
    }

    [Fact(Timeout = 45000)]
    public async Task Public_API_timeout_stops_all_option_leases_emits_terminal_error_and_allows_fresh_start()
    {
        await using var fixture = await Fixture.StartAsync(TimeSpan.FromMilliseconds(300));
        await using var futures = fixture.Worker("Futures");
        await using var options = fixture.Worker("Options");
        var f = await futures.StartAsync(Request());
        var before = await options.StartAsync(Request());
        var lease = OptionLease(before);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await AcquireReadyAsync(options, lease, deadline.Token);
        (await options.AcquireOptionChainAsync(lease with { LeaseId = Guid.NewGuid() }, deadline.Token)).Active.Should().BeTrue();
        var nats = await fixture.Connection.GetClientAsync(deadline.Token);
        await using var states = await nats.Connection.SubscribeCoreAsync<MarketDataDatasetStatus>(
            fixture.Connection.Scope + ".state.Options.GLBX.MDP3",
            serializer: NatsMessagePackSerializer<MarketDataDatasetStatus>.Default, cancellationToken: deadline.Token);
        await nats.PingAsync(deadline.Token);
        await fixture.Connection.PostAsync<bool>($"workers/Options/{before.WorkerInstanceId:D}/hang", new { }, deadline.Token);
        IMarketDataApi api = new MarketDataApiClient(fixture.Connection, Substitute.For<IMarketDataApi>());
        var result = await api.RecoverMarketDataAsync(new(Guid.NewGuid(), "Options", "GLBX.MDP3", before.WorkerInstanceId,
            before.GenerationId, ValueDate, "Injected timeout"), deadline.Token);
        result.Recovered.Should().BeFalse();
        result.CleanupConfirmed.Should().BeTrue(result.Error);
        result.LiveFeedEnabled.Should().BeFalse();
        result.Error.Should().Contain("Recovery deadline").And.Contain("reset/readiness");
        await foreach (var state in states.Msgs.ReadAllAsync(deadline.Token))
        {
            if (state.Data!.Lifecycle != "StoppedError") continue;
            state.Data.Running.Should().BeFalse();
            state.Data.Error.Should().NotBeNullOrWhiteSpace();
            break;
        }
        (await futures.GetHealthAsync(deadline.Token)).Healthy.Should().BeTrue();
        futures.Current.ProcessId.Should().Be(f.ProcessId);
        await using var replacement = fixture.Worker("Options");
        var next = await replacement.StartAsync(Request());
        next.WorkerInstanceId.Should().NotBe(before.WorkerInstanceId);
        next.GenerationId.Should().NotBe(before.GenerationId);
        // Releasing a former lease cannot attach it to the new process.
        (await replacement.ReleaseOptionChainAsync(new(lease.ScopeId, lease.LeaseId, before.GenerationId), deadline.Token)).Active.Should().BeFalse();
    }

    [Fact(Timeout = 165000)]
    public async Task Default_recovery_uses_full_two_minutes_then_reports_error_without_stopping_other_datasets()
    {
        await using var fixture = await Fixture.StartAsync();
        await using var es = fixture.Worker("Futures");
        await using var vx = fixture.Worker("Futures");
        var before = await es.StartAsync(Request());
        var other = await vx.StartAsync(Request("XCBF.PITCH", "VX"));
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(150));
        var nats = await fixture.Connection.GetClientAsync(deadline.Token);
        await using var states = await nats.Connection.SubscribeCoreAsync<MarketDataDatasetStatus>(
            fixture.Connection.Scope + ".state.Futures.GLBX.MDP3",
            serializer: NatsMessagePackSerializer<MarketDataDatasetStatus>.Default, cancellationToken: deadline.Token);
        await nats.PingAsync(deadline.Token);
        await fixture.Connection.PostAsync<bool>($"workers/Futures/{before.WorkerInstanceId:D}/hang", new { }, deadline.Token);
        IMarketDataApi api = new MarketDataApiClient(fixture.Connection, Substitute.For<IMarketDataApi>());
        var timer = Stopwatch.StartNew();
        var recovery = api.RecoverMarketDataAsync(new(Guid.NewGuid(), "Futures", "GLBX.MDP3", before.WorkerInstanceId,
            before.GenerationId, ValueDate, "Injected two-minute recovery timeout"), deadline.Token);
        var notified = false;
        await foreach (var state in states.Msgs.ReadAllAsync(deadline.Token))
        {
            if (state.Data!.Lifecycle != "CleanupUnconfirmed") continue;
            state.Data.Error.Should().Contain("120.000s").And.Contain("reset/readiness");
            timer.Elapsed.TotalSeconds.Should().BeInRange(119, 132);
            notified = true;
            break;
        }
        notified.Should().BeTrue("failure must be published at expiry before waiting for cleanup");
        var result = await recovery;
        result.Recovered.Should().BeFalse();
        result.CleanupConfirmed.Should().BeTrue(result.Error);
        result.Error.Should().Contain("120.000s");
        (await vx.GetHealthAsync(deadline.Token)).Healthy.Should().BeTrue();
        vx.Current.ProcessId.Should().Be(other.ProcessId);
    }

    [Theory(Timeout = 60000)]
    [InlineData("Options")]
    [InlineData("Futures")]
    public async Task Broken_control_pipe_replaces_only_affected_process_and_restores_desired_subscriptions(string role)
    {
        await using var fixture = await Fixture.StartAsync();
        await using var affected = fixture.Worker(role);
        await using var other = fixture.Worker("Futures");
        var before = await affected.StartAsync(Request());
        var unaffected = await other.StartAsync(Request("XCBF.PITCH", "VX"));
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        var retained = OptionLease(before);
        var released = retained with { ScopeId = "released", LeaseId = Guid.NewGuid() };
        if (role == "Options")
        {
            await AcquireReadyAsync(affected, released, deadline.Token);
            await affected.ReleaseOptionChainAsync(new(released.ScopeId, released.LeaseId, before.GenerationId), deadline.Token);
            await AcquireReadyAsync(affected, retained, deadline.Token);
            var expiring = retained with { LeaseId = Guid.NewGuid(), LeaseExpiresAtUtc = DateTimeOffset.UtcNow.AddMilliseconds(300) };
            await AcquireReadyAsync(affected, expiring, deadline.Token);
            await Task.Delay(350, deadline.Token);
        }
        await fixture.Connection.PostAsync<bool>($"workers/{role}/{before.WorkerInstanceId:D}/hang", new { }, deadline.Token);
        // Slow/hung business commands cannot consume or poison the separate health connection.
        for (var i = 0; i < 3; i++)
        {
            var health = await fixture.Connection.PostAsync<DatasetWorkerControlFrame>(
                $"workers/{role}/{before.WorkerInstanceId:D}/probe", new { }, deadline.Token);
            health.Healthy.Should().BeTrue();
        }
        await Assert.ThrowsAnyAsync<Exception>(() => affected.GetHealthAsync(deadline.Token));
        var unusable = await fixture.Connection.PostAsync<DatasetWorkerControlFrame>(
            $"workers/{role}/{before.WorkerInstanceId:D}/probe", new { }, deadline.Token);
        unusable.Healthy.Should().BeFalse("the monitor must detect broken control framing even if the runtime still produces quotes");
        IMarketDataApi api = new MarketDataApiClient(fixture.Connection, Substitute.For<IMarketDataApi>());
        var recovered = await api.RecoverMarketDataAsync(new(Guid.NewGuid(), role, "GLBX.MDP3",
            before.WorkerInstanceId, before.GenerationId, ValueDate, "Injected partial/cancelled control read"), deadline.Token);
        recovered.Recovered.Should().BeTrue(recovered.Error);
        var after = (await api.GetBackendStatusAsync(deadline.Token)).Datasets.Single(d => d.Role == role && d.Dataset == "GLBX.MDP3");
        after.ProcessId.Should().NotBe(before.ProcessId);
        after.GenerationId.Should().NotBe(before.GenerationId);
        after.Lifecycle.Should().Be("Healthy");
        (await other.GetHealthAsync(deadline.Token)).Healthy.Should().BeTrue();
        other.Current.ProcessId.Should().Be(unaffected.ProcessId);
        if (role == "Options")
        {
            var captured = await affected.CaptureCompositionSnapshotAsync(new(Guid.NewGuid(), retained.ScopeId, "Daily",
                after.GenerationId, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddSeconds(2), true)
                { SelectionOnly = true, AllowMissingOptionQuotes = true }, deadline.Token);
            captured.Snapshot.Should().NotBeNull("active desired leases survive process replacement: {0}", captured.Failure);
            var absent = await affected.CaptureCompositionSnapshotAsync(new(Guid.NewGuid(), released.ScopeId, "Daily",
                after.GenerationId, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddSeconds(2), true)
                { SelectionOnly = true, AllowMissingOptionQuotes = true }, deadline.Token);
            absent.Snapshot.Should().BeNull("released scopes must not be resurrected");
            (await affected.ReleaseOptionChainAsync(new(retained.ScopeId, retained.LeaseId, before.GenerationId), deadline.Token)).Active.Should().BeFalse();
        }
    }

    [Fact(Timeout = 165000)]
    public async Task Independent_health_reports_a_two_minute_stalled_command_then_replaces_the_process()
    {
        await using var fixture = await Fixture.StartAsync();
        await using var options = fixture.Worker("Options");
        var before = await options.StartAsync(Request());
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(150));
        await fixture.Connection.PostAsync<bool>($"workers/Options/{before.WorkerInstanceId:D}/hang", new { }, deadline.Token);
        var first = await fixture.Connection.PostAsync<DatasetWorkerControlFrame>($"workers/Options/{before.WorkerInstanceId:D}/probe", new { }, deadline.Token);
        first.Healthy.Should().BeTrue("short business work must not cause false health failures");
        await Task.Delay(TimeSpan.FromSeconds(121), deadline.Token);
        var stalled = await fixture.Connection.PostAsync<DatasetWorkerControlFrame>($"workers/Options/{before.WorkerInstanceId:D}/probe", new { }, deadline.Token);
        stalled.Healthy.Should().BeFalse();
        stalled.Detail.Should().Be("Business control stalled");
        IMarketDataApi api = new MarketDataApiClient(fixture.Connection, Substitute.For<IMarketDataApi>());
        var result = await api.RecoverMarketDataAsync(new(Guid.NewGuid(), "Options", "GLBX.MDP3", before.WorkerInstanceId,
            before.GenerationId, ValueDate, "Independent control watchdog"), deadline.Token);
        result.Recovered.Should().BeTrue(result.Error);
        (await api.GetBackendStatusAsync(deadline.Token)).Datasets.Single().ProcessId.Should().NotBe(before.ProcessId);
    }

    [Theory(Timeout = 45000)]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Committed_ownership_survives_recovery_after_the_temporary_lease_is_released(bool forceProcessReplacement)
    {
        var output = Environment.GetEnvironmentVariable("IFM_MARKETDATA_TEST_HOST_DIRECTORY")
            ?? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.artifacts/market-data-host"));
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["MarketDataHost:WorkerDirectory"] = Path.Combine(output, "Workers"),
            ["MarketDataHost:DeploymentProfile"] = "SyntheticCi", ["MarketDataHost:DataSource"] = "Synthetic"
        }).Build();
        await using var manager = new NatsConnectionManager();
        var connection = new MarketDataHostConnection(manager, Environment.GetEnvironmentVariable("IFM_MARKETDATA_TEST_NATS_URL") ?? "nats://127.0.0.1:4222", "ownershiptest" + Guid.NewGuid().ToString("N"));
        await using var host = new TomasAI.IFM.Application.MarketData.Host.MarketDataWorkerHost(configuration,
            NullLogger<TomasAI.IFM.Application.MarketData.Host.MarketDataWorkerHost>.Instance, connection);
        await host.StartAsync(default);
        var request = Request();
        var before = await host.StartWorkerAsync("Options", new(request.Manifest!, request.WorkerInstanceId, request.GenerationId), default);
        var lease = OptionLease(before);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        WorkerOptionChainResult acquired;
        do
        {
            acquired = await host.AcquireOptionLeaseAsync("Options", before.WorkerInstanceId, lease, deadline.Token);
            if (acquired.Failure?.Code == "UnderlyingQuoteUnavailable") await Task.Delay(50, deadline.Token);
        } while (acquired.Failure?.Code == "UnderlyingQuoteUnavailable");
        acquired.Active.Should().BeTrue("{0}", acquired.Failure);
        var ownership = new WorkerOptionChainOwnership(1, "IFM", [new(Guid.NewGuid(), [lease.Options[0].Pricing.Contract.ContractId])],
            WorkerOptionChainRuntime.PhysicalDigest(lease.Options));
        (await host.ApplyCommittedOwnershipAsync("Options", before.WorkerInstanceId, lease,
            new(lease.ScopeId, Guid.Empty, before.GenerationId, ownership), deadline.Token)).Failure.Should().BeNull();
        await host.ReleaseOptionLeaseAsync("Options", before.WorkerInstanceId,
            new(lease.ScopeId, lease.LeaseId, before.GenerationId), deadline.Token);
        var recovered = await host.RecoverAsync(new(Guid.NewGuid(), "Options", "GLBX.MDP3", before.WorkerInstanceId,
            before.GenerationId, ValueDate, "Committed ownership recovery", ReplaceProcessImmediately: forceProcessReplacement,
            DeadlineUtc: DateTimeOffset.UtcNow.AddSeconds(20)), deadline.Token);
        recovered.Recovered.Should().BeTrue(recovered.Error);
        if (forceProcessReplacement) host.GetBackendStatus().Datasets.Single().ProcessId.Should().NotBe(before.ProcessId);
        TomasAI.IFM.Application.MarketData.Pricing.CompositionSnapshotResult captured;
        do
        {
            captured = await host.RequireWorker("Options", before.WorkerInstanceId).CaptureCompositionSnapshotAsync(
                new(Guid.NewGuid(), lease.ScopeId, "Daily", recovered.GenerationId, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddSeconds(2), true)
                { SelectionOnly = true, AllowMissingOptionQuotes = true }, deadline.Token);
            if (captured.Snapshot is null) await Task.Delay(50, deadline.Token);
        } while (captured.Snapshot is null);
        captured.Snapshot.Should().NotBeNull("committed ownership must survive without a temporary monitoring lease: {0}", captured.Failure);
    }

    static async Task AcquireReadyAsync(RemoteDatasetWorkerProcess worker, WorkerOptionChainRequest lease, CancellationToken token)
    {
        var readiness = Stopwatch.StartNew();
        var acquired = await worker.AcquireOptionChainAsync(lease, token);
        while (acquired.Failure?.Code == "UnderlyingQuoteUnavailable" && readiness.Elapsed < TimeSpan.FromSeconds(8))
        {
            await Task.Delay(50, token);
            acquired = await worker.AcquireOptionChainAsync(lease, token);
        }
        acquired.Active.Should().BeTrue("option acquisition must succeed before injecting a fault: {0}", acquired.Failure);
    }

    static WorkerOptionChainRequest OptionLease(DatasetWorkerProcessSnapshot process)
    {
        var now = DateTimeOffset.UtcNow;
        var until = new DateTimeOffset(now.UtcDateTime.Date.AddDays(24).AddHours(16), TimeSpan.Zero);
        while (until.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) until = until.AddDays(1);
        var template = OrderCompositionPricingPrerequisiteTests.Context();
        var contract = template.Contract with { UnderlyingContractId = "ES20261218", ExpirationUtc = until,
            LastTradingUtc = until, EffectiveFromUtc = now.AddDays(-10), EffectiveUntilUtc = now.AddDays(40) };
        var coverageStart = DateOnly.FromDateTime(now.UtcDateTime).AddDays(-10);
        var calendar = template.Calendar with { CoverageFrom = coverageStart, CoverageUntil = coverageStart.AddDays(90),
            TradingDates = System.Collections.Immutable.ImmutableArray.CreateRange(Enumerable.Range(0, 91)
                .Select(offset => coverageStart.AddDays(offset))
                .Where(date => date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))) };
        var days = TomasAI.IFM.Shared.MarketData.Pricing.OptionPricingQualification.CountTradingDays(calendar, contract, now);
        var tenor = TomasAI.IFM.Framework.MarketData.ReferenceData.TreasuryRateConversion.SelectTenor(days)!.Value;
        var context = template with { Contract = contract, Calendar = calendar, GenerationId = process.GenerationId, ValidUntilUtc = now.AddHours(1),
            Rate = template.Rate with { Tenor = tenor, ObservedAtUtc = now.AddMinutes(-1), ValueDate = DateOnly.FromDateTime(now.UtcDateTime) } };
        return new("recovery-test", Guid.NewGuid(), process.GenerationId, ValueDate,
            DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(until, TimeZoneInfo.FindSystemTimeZoneById(calendar.TimeZoneId)).DateTime),
            now.AddSeconds(90), [new(context, 5000, true)], SeparateContractConnection: true);
    }

    static DatasetWorkerStartRequest Request(string dataset = "GLBX.MDP3", string root = "ES") => new()
    {
        ExecutablePath = Environment.ProcessPath!, Dataset = dataset, ValueDate = ValueDate,
        WorkerInstanceId = Guid.NewGuid(), GenerationId = Guid.NewGuid(),
        Manifest = new DatasetDesiredSubscriptionRegistry().Set(dataset, ValueDate,
            [new DatabentoContractRegistration { DomainContractId = root + "20261218", ProviderContractName = root + "20261218",
                AssetTypeId = Domain.MarketData.Feed.Shared.TickAggregation.AssetTypeId.Futures,
                RootSymbol = root, Dataset = dataset, OnTheRun = true, Rollover = true }])
    };
    sealed class Fixture : IAsyncDisposable
    {
        readonly NatsConnectionManager connections = new();
        Process host = null!;
        bool fullBackend;
        readonly System.Text.StringBuilder diagnostics = new();
        public MarketDataHostConnection Connection { get; private set; } = null!;
        public static async Task<Fixture> StartAsync(TimeSpan? softTimeout = null, bool fullBackend = false, bool developmentFaults = false, bool developmentEnvironment = true)
        {
            var value = new Fixture { fullBackend = fullBackend };
            var root = new DirectoryInfo(AppContext.BaseDirectory);
            while (root is not null && !File.Exists(Path.Combine(root.FullName, "TomasAI.IFM.sln"))) root = root.Parent;
            var output = Environment.GetEnvironmentVariable("IFM_MARKETDATA_TEST_HOST_DIRECTORY")
                ?? Path.Combine(root!.FullName, ".artifacts", "market-data-host");
            var scope = "recoverytest" + Guid.NewGuid().ToString("N");
            var natsUrl = Environment.GetEnvironmentVariable("IFM_MARKETDATA_TEST_NATS_URL") ?? "nats://127.0.0.1:4222";
            value.Connection = new(value.connections, natsUrl, scope);
            var start = new ProcessStartInfo(Path.Combine(output, "TomasAI.IFM.Application.MarketData.Host.exe"))
            {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = true
            };
            start.Environment["MarketDataHost__Scope"] = scope;
            start.Environment["MarketDataHost__NatsUrl"] = natsUrl;
            start.Environment["MarketDataHost__DeploymentProfile"] = "SyntheticCi";
            start.Environment["MarketDataHost__DataSource"] = "Synthetic";
            start.Environment["MarketDataHost__ControlOnly"] = "true";
            start.Environment["Telemetry__Logs__Enabled"] = "false";
            start.Environment["DOTNET_ENVIRONMENT"] = developmentEnvironment ? "Development" : "Production";
            start.Environment["MarketDataRecovery__DevelopmentVerification__Enabled"] = developmentFaults.ToString();
            if (fullBackend)
            {
                start.Environment["DOTNET_ENVIRONMENT"] = "Development";
                start.Environment["MarketDataHost__DeploymentProfile"] = "Development";
                start.Environment["MarketDataHost__DataSource"] = "DatabentoLive";
                start.Environment["MarketDataHost__ControlOnly"] = "false";
                start.Environment["MarketDataHost__StdinShutdown"] = "true";
                start.Environment["MarketDataHost__ApiConfigurationDirectory"] = Path.Combine(root.FullName, "TomasAI.IFM.Application.Api.Server");
            }
            if (softTimeout is { } duration) start.Environment["MarketDataHost__SoftRecoveryTimeout"] = duration.ToString("c");
            start.Environment["ASPNETCORE_URLS"] = "http://127.0.0.1:0";
            value.host = Process.Start(start)!;
            value.host.OutputDataReceived += (_, line) => { lock (value.diagnostics) { if (value.diagnostics.Length < 12000) value.diagnostics.AppendLine(line.Data); } };
            value.host.ErrorDataReceived += (_, line) => { lock (value.diagnostics) { if (value.diagnostics.Length < 12000) value.diagnostics.AppendLine(line.Data); } };
            value.host.BeginOutputReadLine();
            value.host.BeginErrorReadLine();
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            while (true)
            {
                try
                {
                    using var probe = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
                    probe.CancelAfter(TimeSpan.FromMilliseconds(300));
                    if (await value.Connection.PostAsync<bool>("ready", new { }, probe.Token)) return value;
                }
                catch (Exception) when (!deadline.IsCancellationRequested) { }
                if (value.host.HasExited) throw new InvalidOperationException("Qualification Host exited: " + value.diagnostics);
                await Task.Delay(50, deadline.Token);
            }
        }
        public RemoteDatasetWorkerProcess Worker(string role) => new(Connection, role, (_, _) => ValueTask.CompletedTask);
        public async ValueTask DisposeAsync()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            if (!host.HasExited)
            {
                if (fullBackend) { await host.StandardInput.WriteLineAsync("shutdown"); await host.StandardInput.FlushAsync(); }
                else await Connection.PostAsync<bool>("qualification/shutdown", new { }, timeout.Token);
            }
            await host.WaitForExitAsync(timeout.Token);
            host.Dispose();
            await connections.DisposeAsync();
        }
    }
}
