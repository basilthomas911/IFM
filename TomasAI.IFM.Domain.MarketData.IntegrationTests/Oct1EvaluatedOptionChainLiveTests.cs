using System.Globalization;
using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Application.Api.Nats.Client;
using TomasAI.IFM.Domain.MarketData.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Shared.EventModelActor;
using Xunit.Abstractions;

namespace TomasAI.IFM.Domain.MarketData.IntegrationTests;

public sealed class Oct1EvaluatedOptionChainLiveTests(ITestOutputHelper output)
{
    [Oct1ChainLiveFact]
    [Trait("Category", "Live")]
    public async Task Observe_october_first_evaluated_chain_for_five_minutes()
    {
        var duration = int.TryParse(Environment.GetEnvironmentVariable("IFM_OCT1_CHAIN_DURATION_SECONDS"), out var durationSeconds)
            && durationSeconds > 0 ? TimeSpan.FromSeconds(durationSeconds) : TimeSpan.FromMinutes(5);
        var expiry = new DateOnly(2026, 10, 1);
        var connection = new NatsConnectionManager();
        var producer = new NatsActorProducer(new NatsProducerOptions(), NullLogger.Instance, connection);
        var api = new MarketDataQueryApi(producer);
        var reportPath = Path.Combine(Path.GetTempPath(), $"ifm-oct1-chain-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv");
        var stats = new Dictionary<(decimal Strike, bool IsCall), StrikeStats>();
        var failures = new Dictionary<string, int>(StringComparer.Ordinal);
        var methods = new Dictionary<string, int>(StringComparer.Ordinal);
        var start = DateTimeOffset.UtcNow;
        var samples = 0;
        var successful = 0;
        var maximumSnapshotStrikes = 0;
        var maximumSnapshotContracts = 0;
        decimal? lower = null, upper = null, underlying = null;
        string windowMethod = "";
        await producer.StartAsync(new ActorMailboxId(ActorType.Query, $"IFM.Oct1Chain.{Guid.NewGuid():N}"), CancellationToken.None);
        try
        {
            while (DateTimeOffset.UtcNow - start < duration)
            {
                var next = DateTimeOffset.UtcNow.AddSeconds(1);
                samples++;
                try
                {
                    var result = await api.GetEvaluatedOptionChainAsync(NewQuery(expiry));
                    if (!result.Success || result.Value is null)
                    {
                        var reason = result.ErrorMessage ?? result.ErrorCode.ToString(CultureInfo.InvariantCulture);
                        failures[reason] = failures.GetValueOrDefault(reason) + 1;
                        break;
                    }
                    else
                    {
                        successful++;
                        var chain = result.Value;
                        lower = chain.LowerStrikeBound;
                        upper = chain.UpperStrikeBound;
                        underlying = chain.UnderlyingPrice;
                        windowMethod = chain.WindowMethod;
                        methods[windowMethod] = methods.GetValueOrDefault(windowMethod) + 1;
                        if (windowMethod is not ("Bollinger2.5Sigma" or "ImpliedVolatility5Delta"))
                        {
                            var reason = "Unexpected strike window: " + windowMethod;
                            failures[reason] = failures.GetValueOrDefault(reason) + 1;
                            break;
                        }
                        maximumSnapshotStrikes = Math.Max(maximumSnapshotStrikes,
                            chain.Contracts.Select(x => x.Strike).Distinct().Count());
                        maximumSnapshotContracts = Math.Max(maximumSnapshotContracts, chain.Contracts.Count());
                        foreach (var contract in chain.Contracts)
                        {
                            var key = (contract.Strike, contract.IsCall);
                            if (!stats.TryGetValue(key, out var stat)) stats[key] = stat = new StrikeStats();
                            stat.Returned++;
                            if (contract.Bid is not null && contract.Ask is not null) stat.Quoted++;
                            if (contract.QuoteAtUtc >= start) stat.LiveQuote++;
                            if (contract.GreeksValid && contract.Delta is not null && contract.ImpliedVolatility is not null)
                                stat.Evaluated++;
                            if (contract.Delta is not null) stat.Delta++;
                            if (contract.Gamma is not null) stat.Gamma++;
                            if (contract.Vega is not null) stat.Vega++;
                            if (contract.Theta is not null) stat.Theta++;
                            if (contract.Rho is not null) stat.Rho++;
                            if (contract.ImpliedVolatility is not null) stat.Iv++;
                            if (contract.OpenInterest is not null) stat.OpenInterest++;
                            if (contract.Volume is not null) stat.Volume++;
                        }
                    }
                }
                catch (Exception exception)
                {
                    var reason = exception.GetType().Name + ": " + exception.Message;
                    failures[reason] = failures.GetValueOrDefault(reason) + 1;
                    break;
                }
                var remaining = next - DateTimeOffset.UtcNow;
                if (remaining > TimeSpan.Zero) await Task.Delay(remaining);
            }
        }
        finally
        {
            try { await api.GetEvaluatedOptionChainAsync(NewQuery(expiry) with { ReleaseOnly = true }); }
            catch { }
            await producer.StopAsync();
            await connection.DisposeAsync();
        }

        await using (var writer = new StreamWriter(reportPath))
        {
            await writer.WriteLineAsync("strike,right,returned,quoted,live_quote,evaluated,delta,gamma,vega,theta,rho,iv,volume,open_interest");
            foreach (var ((strike, isCall), s) in stats.OrderBy(x => x.Key.Strike).ThenBy(x => x.Key.IsCall))
                await writer.WriteLineAsync($"{strike.ToString(CultureInfo.InvariantCulture)},{(isCall ? "C" : "P")},{s.Returned},{s.Quoted},{s.LiveQuote},{s.Evaluated},{s.Delta},{s.Gamma},{s.Vega},{s.Theta},{s.Rho},{s.Iv},{s.Volume},{s.OpenInterest}");
        }
        output.WriteLine("Start={0:O}; elapsed={1}; samples={2}; successful={3}; underlying={4}; bounds={5}..{6}; method={7}; unique contracts={8}; unique strikes={9}; max snapshot contracts={10}; max snapshot strikes={11}; quoted contracts={12}; live-quoted contracts={13}; evaluated contracts={14}; report={15}",
            start, DateTimeOffset.UtcNow - start, samples, successful, underlying, lower, upper, windowMethod,
            stats.Count, stats.Keys.Select(x => x.Strike).Distinct().Count(), maximumSnapshotContracts, maximumSnapshotStrikes,
            stats.Count(x => x.Value.Quoted > 0), stats.Count(x => x.Value.LiveQuote > 0),
            stats.Count(x => x.Value.Evaluated > 0), reportPath);
        foreach (var failure in failures.OrderByDescending(x => x.Value))
            output.WriteLine("Failure {0}: {1}", failure.Value, failure.Key);
        foreach (var method in methods.OrderBy(x => x.Key))
            output.WriteLine("Window method {0}: {1} snapshots", method.Key, method.Value);
        Assert.Empty(failures);
        Assert.True(successful > 0, "No evaluated-chain snapshot succeeded; see reported failures.");
        if (duration >= TimeSpan.FromMinutes(5))
            Assert.True(methods.GetValueOrDefault("ImpliedVolatility5Delta") > 0,
                "No qualified near-ATM IV arrived to activate the implied-volatility window.");
        Assert.True(maximumSnapshotStrikes > 0);
    }

    static GetEvaluatedOptionChainQuery NewQuery(DateOnly expiry) => new()
    {
        UnderlyingContractId = "ES20261218", UnderlyingSymbol = "ES", ProviderRoots = ["E1D"],
        ExpiryDate = expiry, StandardDeviationMultiplier = 2.5
    };

    sealed class StrikeStats
    {
        public int Returned, Quoted, LiveQuote, Evaluated, Delta, Gamma, Vega, Theta, Rho, Iv, Volume, OpenInterest;
    }
}

public sealed class Oct1ChainLiveFactAttribute : FactAttribute
{
    public Oct1ChainLiveFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("IFM_OCT1_CHAIN_LIVE_TEST") != "true")
            Skip = "Requires explicit five-minute live-chain opt-in and an admitted GLBX.MDP3 worker.";
    }
}
