using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.Api.Nats.Client;
using TomasAI.IFM.Domain.Portfolio.IntegrationTests.Persistence;
using TomasAI.IFM.Domain.Portfolio.Persistence;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Domain.Portfolio.Shared.Validation;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using Xunit.Abstractions;

namespace TomasAI.IFM.Domain.Portfolio.IntegrationTests.Messaging;

public sealed class EventLogQualificationLoadTests(ITestOutputHelper output)
{
    [Fact]
    [Trait("Category", "PortfolioLiveHostLoad")]
    public async Task Concurrent_creates_replays_and_conflicts_preserve_exact_authority_and_projections()
    {
        var url = Environment.GetEnvironmentVariable("IFM_NATS_URL");
        url.Should().Be("nats://127.0.0.1:24223");
        var connection = Environment.GetEnvironmentVariable("IFM_POSTGRES_EVENTSOURCE_TEST_CONNECTION") ?? "";
        Regex.IsMatch(connection, @"\AHost=127\.0\.0\.1;Port=25432;Database=ifm_eventlog_bench_[a-f0-9]{12}_synthetic_host\z")
            .Should().BeTrue("load qualification must never fall back to ordinary application stores");
        const int count = 256;
        const int concurrency = 8;
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        var token = deadline.Token;
        var producer = new NatsActorProducer(new NatsProducerOptions { Url = url! }, Substitute.For<ILogger<NatsActorProducer>>());
        await producer.StartAsync(new ActorMailboxId(ActorType.Command, $"QualificationLoad{Guid.NewGuid():N}"), token);
        try
        {
            var identities = new PortfolioIdentityApi(producer);
            var commands = new PortfolioCommandApi(producer);
            var queries = new PortfolioQueryApi(producer);
            var models = new PortfolioReadModel[count];
            for (var i = 0; i < count; i++)
            {
                var identity = await identities.AllocatePortfolioIdAsync(token);
                identity.Success.Should().BeTrue(identity.ErrorMessage);
                models[i] = new()
                {
                    PortfolioId = identity.Value!.Value, Name = $"Synthetic event-log load {i}", PortfolioVersion = 1,
                    OperatingState = PortfolioOperatingState.Draft, EffectiveFromUtc = DateTime.UtcNow,
                    CreatedOnUtc = DateTime.UtcNow, CreatedBy = "event-log-qualification"
                };
            }
            models.Select(x => x.PortfolioId).Distinct().Should().HaveCount(count);
            var latencies = new double[count];
            var keys = Enumerable.Range(0, count).Select(_ => Guid.NewGuid()).ToArray();
            var elapsed = Stopwatch.StartNew();
            await Parallel.ForEachAsync(Enumerable.Range(0, count), new ParallelOptions { MaxDegreeOfParallelism = concurrency, CancellationToken = token }, async (i, ct) =>
            {
                var model = models[i];
                var key = keys[i];
                var start = Stopwatch.GetTimestamp();
                var created = await commands.CreatePortfolioAsync(model, key, ct);
                latencies[i] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                created.Success.Should().BeTrue(created.ErrorMessage);
                var replays = await Task.WhenAll(commands.CreatePortfolioAsync(model, key, ct), commands.CreatePortfolioAsync(model, key, ct));
                foreach (var replay in replays) replay.Success.Should().BeTrue(replay.ErrorMessage);
                var conflict = await commands.CreatePortfolioAsync(model with { Name = "Conflicting replay" }, key, ct);
                conflict.Success.Should().BeFalse();
                conflict.ErrorCode.Should().Be(PortfolioErrorCodes.IdempotencyConflict, conflict.ErrorMessage);
            });
            elapsed.Stop();
            var authority = new PortfolioEventStore(new PortfolioEventStoreFixture(initializeSchema: false).EventSourceDb);
            foreach (var model in models)
            {
                var state = await authority.LoadPortfolioAsync(new PortfolioId(model.PortfolioId), token);
                state.Revision.Should().Be(1, "replays and conflicting payloads must not append another business event");
                state.Current.Should().BeEquivalentTo(model);
                for (var attempt = 0; ; attempt++)
                {
                    var projected = await queries.GetPortfolioAsync(model.PortfolioId, cancellationToken: token);
                    if (projected.Success && projected.Value is not null)
                    {
                        projected.Value.Should().BeEquivalentTo(state.Current);
                        break;
                    }
                    attempt.Should().BeLessThan(60, projected.ErrorMessage);
                    await Task.Delay(100, token);
                }
            }
            Array.Sort(latencies);
            var manifest = models.Select((model, i) => new RetryCase(model, keys[i])).ToArray();
            await File.WriteAllTextAsync(ManifestPath(), JsonSerializer.Serialize(manifest), token);
            output.WriteLine($"PASS: {count} committed creates, {count * 2} successful concurrent replays, {count} rejected conflicts, {count} exact authorities and projections; concurrency={concurrency}.");
            output.WriteLine($"Mixed command phase: {elapsed.Elapsed.TotalSeconds:F3}s; {count * 4 / elapsed.Elapsed.TotalSeconds:F3} requests/s (includes replay/conflict requests, excludes identity allocation and final verification).");
            output.WriteLine($"Create latency: p50={latencies[(int)Math.Ceiling(count * .50) - 1]:F2}ms, p95={latencies[(int)Math.Ceiling(count * .95) - 1]:F2}ms, p99={latencies[(int)Math.Ceiling(count * .99) - 1]:F2}ms.");
            output.WriteLine("Bounded live-host correctness/load smoke; not a sustained soak or before/after performance comparison.");
        }
        finally { await producer.StopAsync(CancellationToken.None); }
    }

    [Fact]
    [Trait("Category", "PortfolioLiveHostLoadRestart")]
    public async Task Retries_after_API_restart_preserve_one_business_event_per_portfolio()
    {
        Environment.GetEnvironmentVariable("IFM_NATS_URL").Should().Be("nats://127.0.0.1:24223");
        var cases = JsonSerializer.Deserialize<RetryCase[]>(await File.ReadAllTextAsync(ManifestPath()))!;
        cases.Should().HaveCount(256);
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        var token = deadline.Token;
        var producer = new NatsActorProducer(new NatsProducerOptions { Url = "nats://127.0.0.1:24223" }, Substitute.For<ILogger<NatsActorProducer>>());
        await producer.StartAsync(new ActorMailboxId(ActorType.Command, $"QualificationRetryRestart{Guid.NewGuid():N}"), token);
        try
        {
            var commands = new PortfolioCommandApi(producer);
            var authority = new PortfolioEventStore(new PortfolioEventStoreFixture(initializeSchema: false).EventSourceDb);
            foreach (var item in cases)
            {
                var replay = await commands.CreatePortfolioAsync(item.Model, item.Key, token);
                replay.Success.Should().BeTrue(replay.ErrorMessage);
                var state = await authority.LoadPortfolioAsync(new PortfolioId(item.Model.PortfolioId), token);
                state.Revision.Should().Be(1);
                state.Current.Should().BeEquivalentTo(item.Model);
            }
            output.WriteLine("PASS: 256 cold-cache API retries; every Portfolio remains at business revision 1.");
        }
        finally { await producer.StopAsync(CancellationToken.None); }
    }

    static string ManifestPath()
    {
        var connection = Environment.GetEnvironmentVariable("IFM_POSTGRES_EVENTSOURCE_TEST_CONNECTION") ?? "";
        var match = Regex.Match(connection, @"\AHost=127\.0\.0\.1;Port=25432;Database=ifm_eventlog_bench_([a-f0-9]{12})_synthetic_host\z");
        match.Success.Should().BeTrue();
        var directory = Path.GetFullPath(Environment.GetEnvironmentVariable("IFM_QUALIFICATION_ARTIFACT_DIRECTORY") ?? throw new InvalidOperationException("Qualification artifact directory required."));
        Path.GetFileName(directory).Should().Be("acceptance-" + match.Groups[1].Value);
        return Path.Combine(directory, "retry-load-manifest.json");
    }

    public sealed record RetryCase(PortfolioReadModel Model, Guid Key);
}
