using FluentAssertions;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Application.MarketData.Remote;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.MarketData.Contracts.Ticker;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Application.MarketData.UnitTests;

public sealed class MarketDataApiNatsContractTests
{
    [Fact(Timeout = 15000)]
    public async Task Existing_API_supports_nullable_owner_and_polymorphic_result_over_real_NATS()
    {
        await using var connections = new NatsConnectionManager();
        var connection = new MarketDataHostConnection(connections, "nats://127.0.0.1:4222", "apitest" + Guid.NewGuid().ToString("N"));
        var backend = Substitute.For<IMarketDataApi>();
        var owner = new TickerStreamOwner("Test", "1", "put");
        backend.StartStreamingFuturesTickDataAsync("ES", owner).Returns(true);
        backend.GetTradeStrategySymbolsAsync(default, Arg.Any<CancellationToken>())
            .Returns(new ServiceOk<TradeStrategySymbolReadModel[]>([]));
        var dispatcher = new MarketDataApiDispatcher(backend);
        var nats = await connection.GetClientAsync(CancellationToken.None);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var subscription = await nats.Connection.SubscribeCoreAsync<MarketDataHostRequest>(connection.Scope + ".control",
            serializer: NatsMessagePackSerializer<MarketDataHostRequest>.Default, cancellationToken: deadline.Token);
        var server = Task.Run(async () =>
        {
            var handled = 0;
            await foreach (var message in subscription.Msgs.ReadAllAsync(deadline.Token))
            {
                var payload = await dispatcher.InvokeAsync(MarketDataHostConnection.Decode<MarketDataApiCall>(message.Data!.Payload), deadline.Token);
                await message.ReplyAsync(new MarketDataHostReply(payload, null),
                    serializer: NatsMessagePackSerializer<MarketDataHostReply>.Default, cancellationToken: deadline.Token);
                if (++handled == 2) break;
            }
        }, deadline.Token);
        await nats.PingAsync(deadline.Token);
        var client = new MarketDataApiClient(connection, Substitute.For<IMarketDataApi>());
        (await client.StartStreamingFuturesTickDataAsync("ES", owner)).Should().BeTrue();
        var symbols = await client.GetTradeStrategySymbolsAsync(default, deadline.Token);
        symbols.Success.Should().BeTrue();
        await backend.Received(1).StartStreamingFuturesTickDataAsync("ES", owner);
        await server.WaitAsync(deadline.Token);
    }

    [Fact(Timeout = 5000)]
    public async Task API_dispatch_deadline_does_not_wait_for_a_noncooperative_implementation()
    {
        var backend = Substitute.For<IMarketDataApi>();
        backend.StopAsync(new DateOnly(2026, 10, 21)).Returns(new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously).Task);
        var method = typeof(IMarketDataApi).GetMethod(nameof(IMarketDataApi.StopAsync))!;
        var call = new MarketDataApiCall(MarketDataApiClient.Signature(method), [MarketDataHostConnection.Encode(new DateOnly(2026, 10, 21))]);
        using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new MarketDataApiDispatcher(backend).InvokeAsync(call, timeout.Token));
    }

    [Fact]
    public async Task Dispatcher_rejects_undeclared_operations()
    {
        var dispatcher = new MarketDataApiDispatcher(Substitute.For<IMarketDataApi>());
        await Assert.ThrowsAsync<InvalidDataException>(() => dispatcher.InvokeAsync(new("System.Environment.Exit", []), CancellationToken.None));
    }
}
