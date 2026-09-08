using System.Text.Json;
using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.Subscriptions;
using TomasAI.IFM.Application.MarketData.Worker;
using TomasAI.IFM.Application.Storage.ReferenceDb;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Events;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Framework.MarketData.ReferenceData;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Framework.MarketData.DataBento;

namespace TomasAI.IFM.Domain.Trade.IntegratedTests.Strategy.Workflow.IntrinsicTime;

public sealed partial class CompositionBusinessProjectionTests
{
    [CompositionLiveFact]
    public async Task Published_reference_live_worker_pricing_durable_handoff_replacement_and_close()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(45));
        var token = deadline.Token;
        var directory = Path.GetFullPath(Environment.GetEnvironmentVariable("IFM_OCP_EVIDENCE_DIRECTORY")!);
        var evidence = new List<object>();
        void Record(object value)
        {
            evidence.Add(value);
            File.WriteAllText(Path.Combine(directory, "live-pricing-recovery.json"), JsonSerializer.Serialize(evidence, new JsonSerializerOptions { WriteIndented = true }));
        }
        var settings = new DbConnectionSettings()
            .Add("reference", "Contact Points=localhost;Port=9042;Default Keyspace=reference_test_db", "System.Data.ScyllaDb")
            .Add("market", "Contact Points=localhost;Port=9042;Default Keyspace=market_data_test_db", "System.Data.ScyllaDb");
        var referenceDb = new LiveRepository(settings["reference"]);
        var bundle = await new OptionPricingReferenceBundleStore(referenceDb).ReadAsync(Environment.GetEnvironmentVariable("IFM_OCP_BUNDLE_ID")!, token);
        Assert.NotNull(bundle);
        var native = new DatabentoFeedFactory();
        var options = DatabentoFeedOptions.ForProfile(FeedDeploymentProfile.Production, "GLBX.MDP3") with { DataSource = FeedDataSourceMode.DatabentoLive };
        var forward = native.CreateLatestPriceClient(options).GetLatestPrice(new()
        {
            Dataset = "GLBX.MDP3", Symbol = bundle.Underlying.ProviderContractName,
            PricePolicy = LatestPricePolicy.QuoteMidpoint, FreshnessPolicy = LatestPriceFreshnessPolicy.NextObserved
        }, TimeSpan.FromSeconds(15));
        var mid = forward.SelectedPrice / 1_000_000_000m;
        var strikes = bundle.Definitions.Select(x => x.Definition.StrikePrice).Distinct().OrderBy(x => Math.Abs(x - mid)).Take(2).Order().ToArray();
        var plan = bundle.CreatePlan(strikes[0], strikes[1]); Assert.Equal(4, plan.Options.Length);
        await using var fixture = await Fixture.Create();
        await fixture.SavePlan(plan);
        var marketDb = new LiveRepository(settings["market"]);
        await marketDb.Use("Live.OcpSchema", CompositionPreparationStore.CreateTable).ExecuteCommandAsync(token);
        var savedCaptures = new CompositionPreparationStore(marketDb);
        var desired = new DatasetDesiredSubscriptionRegistry(); var admissions = new DatasetWorkerAdmissionRegistry();
        var valueDate = FuturesTradingValueDate.GetOperational(DateTimeOffset.UtcNow);
        var manifest = desired.Set("GLBX.MDP3", valueDate, [bundle.Underlying.ToRegistration() with { OnTheRun = true, Rollover = true }]);
        var limits = new DatabentoStage3Options
        {
            WorkerHandshakeTimeout = TimeSpan.FromSeconds(15), WorkerStartTimeout = TimeSpan.FromSeconds(30),
            WorkerCommandTimeout = TimeSpan.FromSeconds(20), WorkerGracefulStopTimeout = TimeSpan.FromSeconds(2), WorkerForceKillTimeout = TimeSpan.FromSeconds(10)
        };
        await using var workers = new DatasetWorkerProcessRecoveryService(limits, admissions, desiredSubscriptions: desired, durableIntent: fixture.Store);
        var started = await workers.StartOwnedAsync(new()
        {
            ExecutablePath = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet", "dotnet.exe"),
            PrefixArguments = [Environment.GetEnvironmentVariable("IFM_OCP_WORKER_ASSEMBLY") ?? throw new InvalidOperationException("The standalone worker output is required; a web test directory omits shared framework dependencies."), "--deployment-profile", "Production", "--data-source", "DatabentoLive"],
            Dataset = manifest.Dataset, ValueDate = valueDate, GenerationId = Guid.NewGuid(), WorkerInstanceId = Guid.NewGuid(), Manifest = manifest, ManifestRevision = manifest.Revision
        }, token);
        Record(new { Stage = "WorkerStarted", started.ProcessId, started.GenerationId, bundle.BundleId, plan.PlanId, mid, strikes, At = DateTimeOffset.UtcNow });
        using var http = new HttpClient(); using var treasury = new UsTreasuryCurve(http);
        var pricing = new TreasuryPricingProvider(treasury);
        await using var discovery = new QualifiedCompositionDiscovery(new(new OptionPricingConventionStore(referenceDb)),
            new OptionPricingContextProvider(pricing), workers, routePlans: fixture.Plans, desired: desired);
        var acquired = await discovery.AcquireAsync(new(Guid.NewGuid(), started.GenerationId, valueDate, plan.MaturityDate,
            DateTimeOffset.UtcNow.AddSeconds(120), plan.Options, true, plan.Calendar!, plan.Publication!, plan.Conversion!), token, refreshAutomatically: false);
        Assert.Null(acquired.Failure); var discoveryLease = acquired.Lease!;
        Record(new { Stage = "DiscoveryAcquired", discoveryLease.ScopeId, Options = discoveryLease.Options.Select(x => new { x.Pricing.Contract.ContractId, x.Pricing.Contract.InstrumentId, x.Pricing.Rate }).ToArray() });
        async Task<CompositionPreparation> Capture(string horizon)
        {
            var until = DateTimeOffset.UtcNow.AddSeconds(30); string? last = null;
            while (DateTimeOffset.UtcNow < until)
            {
                var generation = workers.Current.Single().GenerationId;
                var request = new CompositionSnapshotRequest(Guid.NewGuid(), plan.PlanId, horizon, generation, default, DateTimeOffset.UtcNow.AddSeconds(5), true);
                var result = await new CompositionPreparationService(workers, savedCaptures).PrepareAsync(
                    new(Guid.NewGuid(), 1, plan.PlanId), "GLBX.MDP3", request, token);
                if (result.Preparation is { } ready)
                {
                    Assert.Equal(4, ready.Snapshot.Instruments.Length);
                    Assert.All(ready.Snapshot.Instruments, x => Assert.NotNull(x.Valuation));
                    var persisted = await new CompositionPreparationStore(new LiveRepository(settings["market"])).ReadAsync(ready.Key, token);
                    Assert.Equal(ready.Digest, persisted!.Digest);
                    Record(new { Stage = "PricedSnapshot", horizon, generation, ready.Digest, ready.Snapshot, At = DateTimeOffset.UtcNow });
                    return ready;
                }
                last = result.Failure?.Code;
                await Task.Delay(50, token);
            }
            throw new InvalidOperationException("Live capture did not qualify: " + last);
        }
        foreach (var horizon in new[] { "Daily", "Weekly", "Monthly" }) await Capture(horizon);
        var selection = new CompositionContractSelection(plan.PlanId, plan.Options.Select(x => x.ContractId).ToImmutableArray());
        var trade = new OptionTradeReadModel { OrderId = 897231, TradeId = 1, TradeState = TradeState.OrderPlaced,
            CompositionContracts = selection, UnderlyingContractId = bundle.Underlying.DomainContractId }
            .AddOptionLegs(selection.ContractIds.Select(x => new OptionTradeLegReadModel { ContractId = x, Quantity = 1 }).ToArray());
        var placed = new OptionTradeOrderPlacedEvent { Id = Guid.NewGuid(), EntityId = trade.EntityId, OptionTrade = trade,
            Subject = new(ActorType.Event, OptionTradeOrderPlacedEvent.Actor, OptionTradeOrderPlacedEvent.Verb, trade.EntityId.Format()) };
        await fixture.Append(placed, 1); await fixture.Projector().ProjectPendingAsync(token);
        using var runtime = new DurableCompositionRuntime(fixture.Store, fixture.Plans, new(fixture.Store), admissions, desired, workers, discovery,
            NullLogger<DurableCompositionRuntime>.Instance, authorityScope: fixture.Scope);
        async Task Reconcile()
        {
            var until = DateTimeOffset.UtcNow.AddSeconds(40); Exception? last = null;
            while (DateTimeOffset.UtcNow < until)
            {
                try
                {
                    // Production probes health independently of business reconciliation. A transient
                    // unhealthy reply must be re-observed rather than becoming a permanent test latch.
                    if (!workers.Current.Single().Healthy)
                    {
                        Record(new { Stage = "HealthReprobe", workers.Current.Single().Diagnostics, At = DateTimeOffset.UtcNow });
                        await workers.GetHealthAsync(TimeSpan.FromSeconds(5), token);
                    }
                    if ((await runtime.ReconcileOnceAsync(token))?.AllRoutesReady == true) return;
                }
                catch (Exception e) { last = e; }
                await Task.Delay(200, token);
            }
            Record(new { Stage = "ReconciliationFailed", Workers = workers.Current, At = DateTimeOffset.UtcNow });
            throw new InvalidOperationException("Durable live reconciliation failed: " + last?.GetType().Name);
        }
        await Reconcile();
        await discovery.ReleaseAsync(discoveryLease, token);
        await Capture("Daily");
        Record(new { Stage = "CommittedHandoff", Owners = (await fixture.Store.ReadAsync(fixture.Scope, "GLBX.MDP3", token)).Authorities.Select(x => new { x.Owner, Count = x.Leases.Count }).ToArray() });
        var old = workers.Current.Single();
        using (var child = Process.GetProcessById(old.ProcessId)) { child.Kill(entireProcessTree: true); await child.WaitForExitAsync(token); }
        var replaced = await workers.ReplaceProcessAsync(new(old.Dataset, old.GenerationId, valueDate, DatabentoDatasetFailureReason.NativeDrainStalled,
            TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(30), Guid.NewGuid()), token);
        Assert.True(replaced.Succeeded, replaced.Detail);
        await Reconcile();
        Assert.NotEqual(old.ProcessId, workers.Current.Single().ProcessId);
        Assert.NotEqual(old.GenerationId, workers.Current.Single().GenerationId);
        await Assert.ThrowsAsync<CompositionMarketSourceException>(() => workers.CaptureAsync("GLBX.MDP3",
            new(Guid.NewGuid(), plan.PlanId, "Daily", old.GenerationId, default, DateTimeOffset.UtcNow.AddSeconds(2), true), token));
        await Capture("Weekly");
        Record(new { Stage = "WorkerReplacementRecovered", Old = old.ProcessId, New = workers.Current.Single().ProcessId, At = DateTimeOffset.UtcNow });
        var seconds = int.TryParse(Environment.GetEnvironmentVariable("IFM_OCP_SOAK_SECONDS"), out var configured) ? configured : 120;
        Assert.InRange(seconds, 30, 1800);
        var soak = Stopwatch.StartNew(); var latencies = new List<double>(); var rss = new List<long>(); var failures = new Dictionary<string, int>();
        var nextSample = TimeSpan.Zero;
        while (soak.Elapsed < TimeSpan.FromSeconds(seconds))
        {
            await Reconcile();
            var timer = Stopwatch.StartNew();
            CompositionSnapshotResult reply;
            try
            {
                reply = await workers.CaptureAsync("GLBX.MDP3", new(Guid.NewGuid(), plan.PlanId, "Daily", workers.Current.Single().GenerationId,
                    default, DateTimeOffset.UtcNow.AddSeconds(3), true), token);
            }
            catch (CompositionMarketSourceException ex) when (ex.Code == "Recovering")
            {
                reply = new(null, new("Recovering", "Worker", "", "Current health requires a fresh observation."));
            }
            latencies.Add(timer.Elapsed.TotalMilliseconds);
            if (reply.Failure is { } failure) failures[failure.Code] = failures.GetValueOrDefault(failure.Code) + 1;
            using var process = Process.GetProcessById(workers.Current.Single().ProcessId); rss.Add(process.WorkingSet64);
            if (soak.Elapsed >= nextSample)
            {
                var health = (await workers.GetHealthAsync(TimeSpan.FromSeconds(5), token)).Single();
                Record(new { Stage = "LiveSoakProgress", Seconds = soak.Elapsed.TotalSeconds, Samples = latencies.Count,
                    Qualified = latencies.Count - failures.Values.Sum(), Rss = process.WorkingSet64,
                    health.Diagnostics, At = DateTimeOffset.UtcNow });
                nextSample = soak.Elapsed + TimeSpan.FromSeconds(30);
            }
            await Task.Delay(250, token);
        }
        latencies.Sort();
        Record(new { Stage = "LiveSoak", Seconds = soak.Elapsed.TotalSeconds, Samples = latencies.Count, failures,
            P95Milliseconds = latencies[(int)(latencies.Count * .95)], P99Milliseconds = latencies[(int)(latencies.Count * .99)],
            MinRss = rss.Min(), MaxRss = rss.Max(), FirstRss = rss[0], LastRss = rss[^1], At = DateTimeOffset.UtcNow });
        Assert.True(latencies.Count > failures.Values.Sum(), "No qualified live snapshot during soak.");
        await fixture.Append(new OptionTradePositionOpenedEvent { Id = Guid.NewGuid(), EntityId = trade.EntityId, OptionTradeId = trade.EntityId,
            TradePositionState = TradePositionState.Opened, Subject = new(ActorType.Event, OptionTradePositionOpenedEvent.Actor,
                OptionTradePositionOpenedEvent.Verb, trade.EntityId.Format()) }, 2);
        await fixture.Projector().ProjectPendingAsync(token); await Reconcile(); await Capture("Monthly");
        await fixture.Append(new OptionTradePositionClosedEvent { Id = Guid.NewGuid(), EntityId = trade.EntityId, OptionTradeId = trade.EntityId,
            TradePositionState = TradePositionState.Closed, Subject = new(ActorType.Event, OptionTradePositionClosedEvent.Actor,
                OptionTradePositionClosedEvent.Verb, trade.EntityId.Format()) }, 3);
        await fixture.Projector().ProjectPendingAsync(token); await Reconcile();
        Assert.All((await fixture.Store.ReadAsync(fixture.Scope, "GLBX.MDP3", token)).Authorities, x => Assert.Empty(x.Leases));
        var afterClose = await workers.CaptureAsync("GLBX.MDP3", new(Guid.NewGuid(), plan.PlanId, "Daily", workers.Current.Single().GenerationId,
            default, DateTimeOffset.UtcNow.AddSeconds(3), true), token);
        Assert.Null(afterClose.Snapshot); Assert.NotNull(afterClose.Failure);
        await runtime.StopAsync(token); await workers.StopAllAsync(token);
        Record(new { Stage = "ClosedAndDrained", AfterClose = afterClose.Failure.Code, At = DateTimeOffset.UtcNow });
    }

    sealed class LiveRepository(IDbConnectionSetting setting) : ObjectDataRepository<LiveRepository>(setting, NullLogger<DbProvider>.Instance)
    { public override IObjectRepository Database => this; }
    [CompositionLiveFact]
    public void Inspect_live_reference_definitions()
    {
        var factory = new DatabentoFeedFactory();
        var options = DatabentoFeedOptions.ForProfile(FeedDeploymentProfile.Production, "GLBX.MDP3") with { DataSource = FeedDataSourceMode.DatabentoLive };
        var queries = factory.CreateMarketDataQueries(options);
        var underlying = queries.GetContractDetail("ESU6", TimeSpan.FromSeconds(30));
        var definitions = queries.GetContractDetails("E2D", TimeSpan.FromSeconds(45));
        var root = Path.GetFullPath(Environment.GetEnvironmentVariable("IFM_OCP_EVIDENCE_DIRECTORY")!);
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "live-reference-definitions.json"), JsonSerializer.Serialize(new { underlying, definitions }, new JsonSerializerOptions { WriteIndented = true }));
        Assert.NotNull(underlying); Assert.NotEmpty(definitions);
    }
}

public sealed class CompositionLiveFactAttribute : FactAttribute
{
    public CompositionLiveFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("IFM_OCP_LIVE") != "1") Skip = "Requires IFM_OCP_LIVE=1, live credentials and an evidence directory.";
    }
}
