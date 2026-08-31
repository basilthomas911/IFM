using System.Collections.Concurrent;
using System.Diagnostics;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.Api.Nats.Client;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using Xunit.Abstractions;

namespace TomasAI.IFM.Domain.Portfolio.IntegrationTests.Messaging;

/// <summary>Bounded production-host concurrency qualification; performance baselining remains PF-30.</summary>
public sealed class PortfolioPf29LiveQualificationTests(ITestOutputHelper output)
{
    [Fact]
    [Trait("Gate", "PF-29")]
    [Trait("Category", "PortfolioLiveHostPF29")]
    public async Task Production_Reference_query_remains_exact_under_bounded_concurrent_NATS_load()
    {
        const int workers = 8;
        const int queriesPerWorker = 8;
        var url = Environment.GetEnvironmentVariable("IFM_NATS_URL") ?? "nats://localhost:4222";
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        var latencies = new ConcurrentBag<double>();
        var total = Stopwatch.StartNew();

        var results = await Task.WhenAll(Enumerable.Range(0, workers).Select(async worker =>
        {
            var producer = new NatsActorProducer(
                new NatsProducerOptions { Url = url },
                Substitute.For<ILogger<NatsActorProducer>>());
            await producer.StartAsync(
                new ActorMailboxId(ActorType.Query, $"PortfolioPf29Load{worker}{Guid.NewGuid():N}"),
                timeout.Token);
            try
            {
                var api = new ReferenceQueryApi(producer);
                var workerResults = new List<(bool Success, TradeStrategyFamilyReadModel[]? Value, string? Error)>(queriesPerWorker);
                for (var query = 0; query < queriesPerWorker; query++)
                {
                    var started = Stopwatch.GetTimestamp();
                    var result = await api.GetTradeStrategyFamiliesAsync(timeout.Token);
                    latencies.Add(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
                    workerResults.Add((result.Success, result.Value, result.ErrorMessage));
                }
                return workerResults;
            }
            finally
            {
                await producer.StopAsync(CancellationToken.None);
            }
        }));
        total.Stop();

        var flattened = results.SelectMany(x => x).ToArray();
        var orderedLatency = latencies.Order().ToArray();
        var p95 = orderedLatency[(int)Math.Ceiling(orderedLatency.Length * 0.95) - 1];
        var evidence = $"PF-29 bounded load: requests={flattened.Length}, workers={workers}, elapsedMs={total.Elapsed.TotalMilliseconds:F1}, p95Ms={p95:F1}.";
        output.WriteLine(evidence);
        Console.WriteLine(evidence);

        flattened.Should().HaveCount(workers * queriesPerWorker).And.OnlyContain(x => x.Success,
            string.Join("; ", flattened.Where(x => !x.Success).Select(x => x.Error)));
        flattened.All(x => x.Value != null &&
            x.Value.Select(family => $"{family.SystemKey}:{family.State}").SequenceEqual(new[]
            {
                "FUTURES:Active", "VERTICAL_SPREAD:Active", "IRON_CONDOR:Active",
            })).Should().BeTrue("every concurrent response must retain the exact read-only three-family catalog");
        total.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(30), "the bounded load must complete before its 45-second safety timeout");
    }
}
