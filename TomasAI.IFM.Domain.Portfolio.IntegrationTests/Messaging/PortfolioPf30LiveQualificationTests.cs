using System.Collections.Concurrent;
using System.Diagnostics;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.Api.Nats.Client;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Domain.Portfolio.Shared.Validation;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using Xunit.Abstractions;

namespace TomasAI.IFM.Domain.Portfolio.IntegrationTests.Messaging;

public sealed class PortfolioPf30LiveQualificationTests(ITestOutputHelper output)
{
    [Fact]
    [Trait("Gate", "PF-30")]
    [Trait("Category", "PortfolioLiveHostPF30")]
    public async Task Production_NATS_enforces_reader_admin_and_anonymous_personas_without_mutating_authority()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var producer = await ProducerAsync(ActorType.Command, timeout.Token);
        try
        {
            var identities = new PortfolioIdentityApi(producer);
            var allocation = await identities.AllocatePortfolioIdAsync(timeout.Token);
            allocation.Success.Should().BeTrue(allocation.ErrorMessage);
            var id = allocation.Value!.Value;
            var now = DateTime.UtcNow;
            var commands = new PortfolioCommandApi(producer);
            var queries = new PortfolioQueryApi(producer);
            var created = await commands.CreatePortfolioAsync(new()
            {
                PortfolioId = id, PortfolioVersion = 1, Name = "PF-30 authorization",
                OperatingState = PortfolioOperatingState.Draft, EffectiveFromUtc = now,
                CreatedOnUtc = now, CreatedBy = "ignored-client-value",
            }, Guid.NewGuid(), timeout.Token);
            created.Success.Should().BeTrue(created.ErrorMessage);
            await WaitForPortfolioAsync(queries, id, timeout.Token);
            output.WriteLine($"PF-30 restart portfolioId={id}.");
            Console.WriteLine($"PF-30 restart portfolioId={id}.");

            using (PortfolioAccessScope.Push(PortfolioAccessContext.Reader("pf30-auditor")))
            {
                var read = await queries.GetPortfolioAsync(id, cancellationToken: timeout.Token);
                read.Success.Should().BeTrue(read.ErrorMessage);
                var denied = await commands.ChangePortfolioStateAsync(new PortfolioId(id), 1,
                    PortfolioOperatingState.Active, "must be denied", timeout.Token);
                denied.Success.Should().BeFalse();
                denied.ErrorCode.Should().Be(PortfolioErrorCodes.Unauthorized);
            }
            using (PortfolioAccessScope.Push(new PortfolioAccessContext()))
            {
                var denied = await queries.GetPortfolioAsync(id, cancellationToken: timeout.Token);
                denied.Success.Should().BeFalse();
                denied.ErrorCode.Should().Be(PortfolioErrorCodes.Unauthorized);
            }

            var unchanged = await queries.GetPortfolioRevisionAsync(id, timeout.Token);
            unchanged.Success.Should().BeTrue(unchanged.ErrorMessage);
            unchanged.Value!.Revision.Should().Be(1);
        }
        finally { await producer.StopAsync(CancellationToken.None); }
    }

    [Fact]
    [Trait("Gate", "PF-30")]
    [Trait("Category", "PortfolioLiveHostPF30Restart")]
    public async Task Production_restart_recovers_the_pre_restart_authority_and_projection()
    {
        var id = int.Parse(Environment.GetEnvironmentVariable("IFM_PORTFOLIO_PF30_ID")
            ?? throw new InvalidOperationException("IFM_PORTFOLIO_PF30_ID must identify the pre-restart Portfolio."));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var producer = await ProducerAsync(ActorType.Query, timeout.Token);
        try
        {
            var queries = new PortfolioQueryApi(producer);
            var read = await queries.GetPortfolioAsync(id, cancellationToken: timeout.Token);
            read.Success.Should().BeTrue(read.ErrorMessage);
            read.Value!.PortfolioId.Should().Be(id);
            var revision = await queries.GetPortfolioRevisionAsync(id, timeout.Token);
            revision.Success.Should().BeTrue(revision.ErrorMessage);
            revision.Value!.Revision.Should().Be(1);
        }
        finally { await producer.StopAsync(CancellationToken.None); }
    }

    [Fact]
    [Trait("Gate", "PF-30")]
    [Trait("Category", "PortfolioLiveHostPF30")]
    public async Task Production_Portfolio_query_meets_bounded_concurrent_load_baseline()
    {
        const int workers = 8;
        const int requestsPerWorker = 16;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        var setup = await ProducerAsync(ActorType.Command, timeout.Token);
        int id;
        try
        {
            var allocation = await new PortfolioIdentityApi(setup).AllocatePortfolioIdAsync(timeout.Token);
            allocation.Success.Should().BeTrue(allocation.ErrorMessage);
            id = allocation.Value!.Value;
            var now = DateTime.UtcNow;
            var created = await new PortfolioCommandApi(setup).CreatePortfolioAsync(new()
            {
                PortfolioId = id, PortfolioVersion = 1, Name = "PF-30 load",
                OperatingState = PortfolioOperatingState.Draft, EffectiveFromUtc = now,
                CreatedOnUtc = now, CreatedBy = "pf30",
            }, Guid.NewGuid(), timeout.Token);
            created.Success.Should().BeTrue(created.ErrorMessage);
            await WaitForPortfolioAsync(new PortfolioQueryApi(setup), id, timeout.Token);
        }
        finally { await setup.StopAsync(CancellationToken.None); }

        var latencies = new ConcurrentBag<double>();
        var elapsed = Stopwatch.StartNew();
        var results = await Task.WhenAll(Enumerable.Range(0, workers).Select(async _ =>
        {
            var producer = await ProducerAsync(ActorType.Query, timeout.Token);
            try
            {
                var api = new PortfolioQueryApi(producer);
                var workerResults = new bool[requestsPerWorker];
                using (PortfolioAccessScope.Push(PortfolioAccessContext.Reader("pf30-load-reader")))
                for (var index = 0; index < requestsPerWorker; index++)
                {
                    var started = Stopwatch.GetTimestamp();
                    var result = await api.GetPortfolioAsync(id, cancellationToken: timeout.Token);
                    latencies.Add(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
                    workerResults[index] = result.Success && result.Value?.PortfolioId == id;
                }
                return workerResults;
            }
            finally { await producer.StopAsync(CancellationToken.None); }
        }));
        elapsed.Stop();
        var ordered = latencies.Order().ToArray();
        var p95 = ordered[(int)Math.Ceiling(ordered.Length * 0.95) - 1];
        var evidence = $"PF-30 Portfolio load: requests={ordered.Length}, workers={workers}, elapsedMs={elapsed.Elapsed.TotalMilliseconds:F1}, p95Ms={p95:F1}.";
        output.WriteLine(evidence);
        Console.WriteLine(evidence);

        results.SelectMany(static x => x).Should().OnlyContain(static success => success);
        ordered.Should().HaveCount(workers * requestsPerWorker);
        p95.Should().BeLessThan(1_000);
        elapsed.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(30));
    }

    [Fact]
    [Trait("Gate", "PF-30")]
    [Trait("Category", "PortfolioLiveHostPF30Rollback")]
    public async Task Production_rollback_mode_rejects_mutations_but_keeps_queries_available()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var producer = await ProducerAsync(ActorType.Command, timeout.Token);
        try
        {
            var id = Random.Shared.Next(1_000_000, 2_000_000_000);
            var now = DateTime.UtcNow;
            var denied = await new PortfolioCommandApi(producer).CreatePortfolioAsync(new()
            {
                PortfolioId = id, PortfolioVersion = 1, Name = "must not commit",
                OperatingState = PortfolioOperatingState.Draft, EffectiveFromUtc = now,
                CreatedOnUtc = now, CreatedBy = "pf30",
            }, Guid.NewGuid(), timeout.Token);
            denied.Success.Should().BeFalse();
            denied.ErrorCode.Should().Be(PortfolioErrorCodes.OperationallyDisabled);

            var read = await new PortfolioQueryApi(producer).GetPortfoliosAsync(PortfolioOperatingState.Draft, 1, cancellationToken: timeout.Token);
            read.Success.Should().BeTrue(read.ErrorMessage);
        }
        finally { await producer.StopAsync(CancellationToken.None); }
    }

    static async Task<NatsActorProducer> ProducerAsync(ActorType type, CancellationToken cancellationToken)
    {
        var url = Environment.GetEnvironmentVariable("IFM_NATS_URL") ?? "nats://localhost:4222";
        var producer = new NatsActorProducer(new NatsProducerOptions { Url = url }, Substitute.For<ILogger<NatsActorProducer>>());
        await producer.StartAsync(new ActorMailboxId(type, $"PortfolioPf30{Guid.NewGuid():N}"), cancellationToken);
        return producer;
    }

    static async Task WaitForPortfolioAsync(PortfolioQueryApi queries, int id, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 60; attempt++)
        {
            var result = await queries.GetPortfolioAsync(id, cancellationToken: cancellationToken);
            if (result.Success) return;
            await Task.Delay(50, cancellationToken);
        }
        throw new TimeoutException($"Portfolio {id} projection was not visible.");
    }
}
