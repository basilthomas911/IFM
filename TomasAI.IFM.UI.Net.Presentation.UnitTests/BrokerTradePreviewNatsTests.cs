using Microsoft.Extensions.Logging.Abstractions;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Api.Nats.Client;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Order.Query.Actor;
using TomasAI.IFM.Domain.Trade.Shared.Order;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.UI.Net.Services.Trade;
using TomasAI.IFM.UI.Net.ViewModels.Trade;

public sealed class BrokerTradePreviewNatsTests
{
    /// <summary>Uses an explicitly provided disposable NATS server, never the running development actor system.</summary>
    [PreviewNatsFact]
    public async Task Actual_nats_client_actor_dispatch_and_reply_support_repeated_live_draft_updates()
    {
        var url = Environment.GetEnvironmentVariable("IFM_PREVIEW_NATS_TEST_URL")!;
        var endpoint = new Uri(url);
        Assert.Equal("127.0.0.1", endpoint.Host); Assert.NotEqual(4222, endpoint.Port);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var connection = new NatsConnection(new NatsOpts { Url = url });
        await using var subscription = await connection.SubscribeCoreAsync<byte[]>(
            $"Query.{GetTradeOrderPreviewQuery.Actor}.{GetTradeOrderPreviewQuery.Verb}.*",
            serializer: new NatsByteArrayMessageSerializer(), cancellationToken: timeout.Token);
        await connection.PingAsync(timeout.Token);
        var db = Substitute.For<IDbContextFactory>();
        var securities = Substitute.For<TomasAI.IFM.Application.Storage.SecuritiesDb.ISecuritiesDbContext>();
        db.SecuritiesDb.Returns(securities);
        securities.GetFuturesOptionContractAsync(Arg.Is<string>(id => id == "SC" || id == "LC"), Arg.Any<CancellationToken>()).Returns(
            new TomasAI.IFM.Domain.MarketData.Shared.ViewModels.FuturesOptionContractReadModel { UnderlyingContractId = "ES" });
        securities.GetFuturesContractAsync("ES", Arg.Any<CancellationToken>()).Returns(
            new TomasAI.IFM.Domain.MarketData.Shared.ViewModels.FuturesContractV3ReadModel { ContractId = "ES", Symbol = "ES", TickSize = .25m });
        var context = new TradeOrderQueryContext(Substitute.For<IActorSupervisor>(), db, NullLogger<TradeOrderQueryActor>.Instance);
        var actor = new TradeOrderQueryActor(context);
        const int updates = 20;
        var receiving = Task.Run(async () => {
            var count = 0;
            await foreach (var message in subscription.Msgs.ReadAllAsync(timeout.Token))
            {
                using var actorMessage = new NatsActorMessage(message);
                await actor.HandleMessageAsync(actorMessage);
                if (++count == updates + 2) return;
            }
        });
        await using var producer = new NatsActorProducer(new NatsProducerOptions { Url = url }, NullLogger.Instance);
        await producer.StartAsync(new ActorMailboxId(ActorType.Query, "PreviewTestClient"), timeout.Token);
        var owner = new BrokerTradePreviewQuery(new TradeQueryService(new OptionTradeQueryApi(producer)));
        var latencies = new List<double>();
        for (var quantity = 1; quantity <= updates; quantity++)
        {
            var started = System.Diagnostics.Stopwatch.GetTimestamp();
            var preview = await owner.ExecuteAsync(new(0,
                [new("SC", -1, quantity, 110, true, false, 2, 2.4m, .16, .2, 50),
                 new("LC", 1, quantity, 120, true, false, .5m, .7m, .10, .1, 50)], quantity, -1.55m, .25m, null), timeout.Token);
            latencies.Add(System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            Assert.Equal(77.5m * quantity, preview.MaximumProfit);
            Assert.Equal(422.5m * quantity, preview.MaximumLoss);
            Assert.Equal(.05m, preview.ResolvedTickIncrement);
            Assert.Equal(quantity, preview.InputRevision);
            Assert.Equal(0, context.PendingMessageCount);
        }
        await Assert.ThrowsAsync<InvalidOperationException>(() => owner.ExecuteAsync(new(0, [], 0, null, .05m, null), timeout.Token));
        var missing = await Assert.ThrowsAsync<InvalidOperationException>(() => owner.ExecuteAsync(new(0,
            [new("UNKNOWN", -1, 1, 110, true, false, 2, 2.4m, .16, .2, 50)], 1, -1.55m, .05m, null), timeout.Token));
        Assert.Contains("underlying reference is unavailable", missing.Message);
        await receiving;
        Assert.Equal(0, context.PendingMessageCount);
        Console.WriteLine($"Trade preview NATS round trips: {updates}; median={latencies.Order().ElementAt(updates / 2):F2}ms; maximum={latencies.Max():F2}ms");
    }
}

public sealed class PreviewNatsFactAttribute : FactAttribute
{
    public PreviewNatsFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("IFM_PREVIEW_NATS_TEST_URL")))
            Skip = "Requires IFM_PREVIEW_NATS_TEST_URL pointing to an isolated disposable NATS server on a nonstandard loopback port.";
    }
}
