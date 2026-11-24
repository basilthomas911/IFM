using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using NSubstitute;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.OperationsHealth;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Realtime;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Shared.FuturesVwapSignal;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.TickAggregation;
using TomasAI.IFM.Shared.MarketData.Contracts.TickAggregation;

namespace TomasAI.IFM.Application.Api.Server.Benchmarks;

public partial class FuturesItiStorageBaselineBenchmarks
{
    public async Task VerifyRecoveredPrefixAsync(string file)
    {
        qualifying = true;
        var prefix = System.Text.Json.JsonDocument.Parse(await File.ReadAllTextAsync(file)).RootElement
            .EnumerateArray().Select(row => (Ticks: row.GetProperty("market_ticks").GetInt64(),
                Command: Domain.MarketData.Analytics.FuturesItiSignal.Command.State.FuturesItiJournalEnvelope.Decode(
                    Convert.FromBase64String(row.GetProperty("payload_base64").GetString()!), 0))).ToArray();
        Require(prefix.Length == 91 && prefix.Select((row, index) => row.Command.SourceCursor!.Ordinal == index + 1
            && row.Command.Timestamp.Ticks == row.Ticks && row.Command.SourceCursor.EventAtUtc.Ticks == row.Ticks
            && !string.IsNullOrEmpty(row.Command.InputUnavailableReason)).All(value => value),
            "Recovered source prefix changed original event times or continuity.");
        var predecessor = SourceCommand(5000);
        predecessor = predecessor with { SourceCursor = predecessor.SourceCursor! with { InstrumentId = 10252 } };
        await actor.ProcessPreparedAsync(await actor.PrepareInputsAsync([predecessor]));
        await actor.SealProcessedEpochsAsync();
        var stream = prefix[0].Command.StreamId;
        var before = await database.EventCountAsync(stream);
        await actor.ProcessPreparedAsync(await actor.PrepareInputsAsync(prefix.Select(row => row.Command).ToArray()));
        Require(await database.EventCountAsync(stream) == before, "Unavailable startup context generated a synthetic signal.");
        await actor.SealProcessedEpochsAsync();
        await actor.StopAsync();
        actor = CreateActor();
        await actor.StartAsync(database.Supervisor);
        Require(await database.EventCountAsync(stream) == before, "Recovered prefix duplicated signal history on restart.");
        Console.WriteLine("Verified authentic 91-record prefix: exact event timestamps, contiguous source ordinals, sealed predecessor bridge, unavailable-context suppression, restart and no duplicate signals.");
    }

    public async Task VerifyOrderedIngressAsync()
    {
        qualifying = true;
        var realtime = Substitute.For<IFuturesItiSignalRealtimeContext>();
        var market = Substitute.For<IMarketDataApi>();
        realtime.MarketDataApi.Returns(market);
        realtime.Telemetry.Returns(telemetry);
        realtime.HealthEvidence.Returns(new LivePipelineEvidence(TimeProvider.System));
        realtime.Logger.Returns(NullLogger<FuturesItiSignalRealtimeActor>.Instance);
        var contractReady = false;
        FuturesContractV3ReadModel ContractFor(string symbol, string id) => new(id, symbol, symbol, symbol,
            "FUT", "USD", "CME", "50", new DateOnly(2026, 12, 18), true);
        market.TryGetOnTheRunFuturesContract("ES", out Arg.Any<FuturesContractV3ReadModel>()!)
            .Returns(call => { call[1] = ContractFor("ES", Contract); return contractReady; });
        market.TryGetOnTheRunFuturesContract("VX", out Arg.Any<FuturesContractV3ReadModel>()!)
            .Returns(call => { call[1] = ContractFor("VX", "VX20261216"); return true; });
        FuturesMarketPriceSnapshot Price(string id, decimal price, long sequence) => new(id, 42, 1,
            AssetTypeId.Futures, DateOnly.FromDateTime(epoch), null,
            new(price, 1, sequence, new(epoch), new(epoch), NormalizedTradeAction.New,
                NormalizedTradeSide.Buy, NormalizedTradeConditionFlags.None, sourceEpoch, sequence));
        market.TryGetLastTickPrice("VX20261216", out Arg.Any<FuturesMarketPriceSnapshot>())
            .Returns(call => { call[1] = Price("VX20261216", 20, 1); return true; });
        FuturesMarketPriceUpdatedRealtimeEvent Tick(decimal price, long sequence) => new()
        {
            Id = Guid.NewGuid(), EntityId = new(Contract, DateOnly.FromDateTime(epoch), AssetTypeId.Futures),
            Price = Price(Contract, price, sequence), UpdateSource = FuturesMarketPriceUpdateSource.Trade,
            SourceDataset = "GLBX.MDP3", ReceivedOn = epoch
        };
        var publisher = new TickAggregationEventPublisher(database.Supervisor);
        await publisher.StartAsync();
        var runtime = await FuturesItiOrderedRuntime.StartAsync(realtime, fixtureJournalDirectory);
        try
        {
            await publisher.PublishAsync(Tick(5000, 0) with { VwapCheckpoint = new()
                { StreamEpochId = sourceEpoch, LastTradeOrdinal = 0, IsReplayComplete = true } });
            var first = Tick(5000, 1);
            await publisher.PublishAsync(first);
            contractReady = true;
            await publisher.PublishAsync(first); // Exact duplicate must not recapture changing context.
            await publisher.PublishAsync(Tick(6000, 2));
            await publisher.PublishAsync(Tick(4000, 3));
            await publisher.PublishAsync(Tick(7000, 4) with { UpdateSource = FuturesMarketPriceUpdateSource.Quote });
            await runtime.DisposeAsync(); // Drains queue and seals before the writer shuts down.
            Require(telemetry.GetSnapshot().OrderedProcessed == 3, "Ordered publisher lost or duplicated a trade, or included a quote.");
            var stream = Command(5000).StreamId;
            var count = await database.EventCountAsync(stream);
            Require(count == 3, "Ordered publisher did not run the unchanged ITI evaluator.");
            await using (var connection = await database.OpenAsync())
            {
                await using var command = new NpgsqlCommand("""
                    SELECT count(*) FROM event_projector_state
                    WHERE projectorname='FuturesItiSignalEventProjector' AND stage='ApplyProjection'
                    """, connection);
                Require((long)(await command.ExecuteScalarAsync())! == count,
                    "Signal commits did not atomically retain their durable projector work.");
            }
            await actor.StopAsync();
            actor = CreateActor();
            await actor.StartAsync(database.Supervisor);
            Require(await database.EventCountAsync(stream) == count, "Publisher restart duplicated a committed signal.");
            runtime = await FuturesItiOrderedRuntime.StartAsync(realtime, fixtureJournalDirectory);
            sourceEpoch = Guid.NewGuid();
            await publisher.PublishAsync(Tick(4000, 0) with { VwapCheckpoint = new()
                { StreamEpochId = sourceEpoch, LastTradeOrdinal = 0, IsReplayComplete = true } });
            await publisher.PublishAsync(Tick(7000, 1));
            await runtime.DisposeAsync();
            Require(await database.EventCountAsync(stream) == count + 1,
                "Publisher/native checkpoint epoch handover failed after restart.");
            Console.WriteLine("ORDERED publisher verified: pre-fanout durable intake; duplicates and quote exclusion; bounded drain; transactional projector markers; restart and native checkpoint epoch handover.");
        }
        finally { await runtime.DisposeAsync(); await publisher.StopAsync(); }
    }
}
