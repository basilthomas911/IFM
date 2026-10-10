using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Application.Api.Nats.Client;
using TomasAI.IFM.Domain.MarketData.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Shared.EventModelActor;
using Xunit.Abstractions;

namespace TomasAI.IFM.Domain.MarketData.IntegrationTests;

public sealed class OptionChainLatencyLiveTests(ITestOutputHelper output)
{
    [OptionChainLatencyLiveFact]
    [Trait("Category", "Live")]
    [Trait("Infrastructure", "SelfContained")]
    public async Task Four_live_legs_are_ready_and_owner_release_preserves_other_owner()
    {
        var connection = new NatsConnectionManager();
        var producer = new NatsActorProducer(new NatsProducerOptions(), NullLogger.Instance, connection);
        var api = new MarketDataQueryApi(producer);
        var owner = "latency-test-" + Guid.NewGuid().ToString("N");
        var query = new GetEvaluatedOptionChainQuery
        {
            UnderlyingContractId = Environment.GetEnvironmentVariable("IFM_CHAIN_UNDERLYING") ?? "ES20261218",
            UnderlyingSymbol = "ES", ProviderRoots = [Environment.GetEnvironmentVariable("IFM_CHAIN_ROOT") ?? "EW3"],
            ExpiryDate = DateOnly.Parse(Environment.GetEnvironmentVariable("IFM_CHAIN_EXPIRY") ?? "2026-11-20"),
            SubscriptionOwnerId = owner, SpreadWingWidth = 50
        };
        var sampleCount = Math.Clamp(int.TryParse(Environment.GetEnvironmentVariable("IFM_CHAIN_WARM_SAMPLES"), out var requestedSamples) ? requestedSamples : 30, 30, 300);
        var samples = new List<double>();
        var failures = new List<string>();
        double coldMs = 0;
        await producer.StartAsync(new ActorMailboxId(ActorType.Query, "IFM.ChainLatency." + Guid.NewGuid().ToString("N")), default);
        try
        {
            var cold = Stopwatch.StartNew();
            while (cold.Elapsed < TimeSpan.FromSeconds(30))
            {
                var result = await api.GetEvaluatedOptionChainAsync(query);
                if (!result.Success || result.Value is null) { failures.Add(result.ErrorMessage ?? "No chain"); break; }
                var chain = result.Value;
                if (query.RequiredContractIds.Length == 0) query.RequiredContractIds = Select(chain);
                if (Ready(chain, query.RequiredContractIds)) break;
                await Task.Delay(100);
            }
            coldMs = cold.Elapsed.TotalMilliseconds;
            if (query.RequiredContractIds.Length != 4) failures.Add("Four contracts could not be selected from live deltas and wings.");
            if (failures.Count == 0)
            {
                var secondOwner = query with { SubscriptionOwnerId = owner + "-second" };
                var second = await api.GetEvaluatedOptionChainAsync(secondOwner);
                Assert.True(second.Success, second.ErrorMessage);
                await api.GetEvaluatedOptionChainAsync(query with { ReleaseOnly = true });
                query = secondOwner;
                for (var i = 0; i < sampleCount; i++)
                {
                    var started = Stopwatch.StartNew();
                    var result = await api.GetEvaluatedOptionChainAsync(query);
                    while (started.Elapsed < TimeSpan.FromSeconds(2) && result.Success && result.Value is not null && !Ready(result.Value, query.RequiredContractIds))
                    {
                        await Task.Delay(100);
                        result = await api.GetEvaluatedOptionChainAsync(query);
                    }
                    samples.Add(started.Elapsed.TotalMilliseconds);
                    if (!result.Success || result.Value is null || !Ready(result.Value, query.RequiredContractIds))
                        failures.Add($"Sample {i}: success={result.Success}; error={result.ErrorMessage}; asOf={result.Value?.AsOfUtc:O}; legs=" + JsonSerializer.Serialize(result.Value?.Contracts.Where(x => query.RequiredContractIds.Contains(x.ContractId))));
                    await Task.Delay(100);
                }
            }
        }
        finally
        {
            try { await api.GetEvaluatedOptionChainAsync(query with { ReleaseOnly = true }); }
            finally { await producer.StopAsync(); await connection.DisposeAsync(); }
            var report = new { AtUtc = DateTimeOffset.UtcNow, query.UnderlyingContractId, query.ExpiryDate, query.RequiredContractIds,
                ColdFourLegMilliseconds = coldMs, WarmMilliseconds = samples, Failures = failures };
            var path = Environment.GetEnvironmentVariable("IFM_CHAIN_REPORT") ?? Path.Combine(Path.GetTempPath(), "ifm-option-chain-latency.json");
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
            output.WriteLine($"Cold={coldMs:F2}ms; warm samples={samples.Count}; failures={failures.Count}; report={path}");
        }
        Assert.Empty(failures);
        Assert.Equal(sampleCount, samples.Count);
        samples.Sort();
        var p50 = samples[(int)Math.Ceiling(samples.Count * .5) - 1];
        var p95 = samples[(int)Math.Ceiling(samples.Count * .95) - 1];
        output.WriteLine($"Warm p50={p50:F2}ms; p95={p95:F2}ms; max={samples[^1]:F2}ms");
        Assert.True(p95 <= 2000, "Warm p95 exceeds two seconds.");
        Assert.True(coldMs <= 5000, "Cold four-leg readiness exceeds five seconds.");
    }

    static bool Ready(EvaluatedOptionChainReadModel chain, string[] ids) => ids.Length == 4 && ids.All(id =>
        chain.Contracts.Any(x => x.ContractId == id && x.Bid is not null && x.Ask is not null && x.Delta is not null
            && !x.IsStale && (x.GreeksValid || x.SelectionValid)));

    static string[] Select(EvaluatedOptionChainReadModel chain)
    {
        var call = chain.Contracts.Where(x => x.IsCall && x.Delta is > 0 and < 0.5).MinBy(x => Math.Abs(x.Delta!.Value - 0.16));
        var put = chain.Contracts.Where(x => !x.IsCall && x.Delta is < 0 and > -0.5).MinBy(x => Math.Abs(x.Delta!.Value + 0.16));
        if (call is null || put is null) return [];
        var callWing = chain.Contracts.FirstOrDefault(x => x.IsCall && x.Strike == call.Strike + 50);
        var putWing = chain.Contracts.FirstOrDefault(x => !x.IsCall && x.Strike == put.Strike - 50);
        return callWing is null || putWing is null ? [] : [call.ContractId, callWing.ContractId, put.ContractId, putWing.ContractId];
    }
}

public sealed class OptionChainLatencyLiveFactAttribute : FactAttribute
{
    public OptionChainLatencyLiveFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("IFM_CHAIN_LATENCY_LIVE") != "true")
            Skip = "Requires a running development API and live Databento feed.";
    }
}
