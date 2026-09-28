using TomasAI.IFM.Domain.MarketData.Shared;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Actor.IntegrationTests;
using TomasAI.IFM.Application.Api.Nats.Client;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using Microsoft.Extensions.DependencyInjection;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;

namespace TomasAI.IFM.Domain.MarketData.Feed.IntegrationTests.FuturesClosingPrice;

public class FuturesClosingPriceCommandApiTests(TomasAI.IFM.IntegrationTesting.KestrelWebApplicationFactory<TomasAI.IFM.Application.Api.Server.ApiServerEntryPoint> factory, MarketDataFeedFixture dbFixture)
    : IClassFixture<TomasAI.IFM.IntegrationTesting.KestrelWebApplicationFactory<TomasAI.IFM.Application.Api.Server.ApiServerEntryPoint>>, IClassFixture<MarketDataFeedFixture>
{
    readonly IActorProducer _actorProducer = factory.Services.GetRequiredService<IActorProducer>();
    readonly ILogger<NatsActorEventListener> _logger = Substitute.For<ILogger<NatsActorEventListener>>();

    [Fact]
    public async Task InsertFuturesClosingPrice_Ok()
    {
        // arrange...
        var eventListener = new NatsActorEventListener(new NatsEventListenerOptions(), _logger);
        FuturesClosingPriceInsertedEvent futuresClosingPriceInsertedEvent = default!;
        FuturesClosingPriceInsertedCompleteEvent futuresClosingPriceInsertedCompleteEvent = default!;
        FuturesClosingPriceInsertedFailEvent futuresClosingPriceInsertedFailEvent = default!;
        var terminalEventReceived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await eventListener.StartAsync(
            "TestEventListener",
            new()
            {
                [new ActorMailboxId(ActorType.Event, FuturesClosingPriceInsertedEvent.Actor)] =
                [
                    FuturesClosingPriceInsertedEvent.Verb,
                    FuturesClosingPriceInsertedCompleteEvent.Verb,
                    FuturesClosingPriceInsertedFailEvent.Verb
                ]
            },
            EventHandlerAsync
        );

        var contractId = $"ES{Guid.NewGuid():N}";
        var valueDate = SampleData.ValueDate;
        var closingPrice = 5450.25m;
        var futuresDataId = FuturesDataId.Create(contractId, valueDate);

        await dbFixture.MarketDataDb.DeleteFuturesClosingPriceAsync(contractId, valueDate);

        // act...
        var marketDataFeedApi = new MarketDataFeedCommandApi(_actorProducer);
        var response = await marketDataFeedApi.InsertFuturesClosingPriceAsync(futuresDataId, closingPrice);

        await terminalEventReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue(response.ErrorMessage);
        response.Value.Should().NotBe(Guid.Empty);
        futuresClosingPriceInsertedEvent.Should().NotBeNull();
        futuresClosingPriceInsertedCompleteEvent.Should().NotBeNull();
        futuresClosingPriceInsertedFailEvent.Should().BeNull();

        var insertedClosingPrice = await dbFixture.MarketDataDb.GetFuturesClosingPriceAsync(futuresDataId);
        insertedClosingPrice.Should().NotBeNull();
        insertedClosingPrice!.ContractId.Should().Be(contractId);
        insertedClosingPrice.ValueDate.Should().Be(valueDate);
        insertedClosingPrice.ClosingPrice.Should().Be(closingPrice);

        await dbFixture.MarketDataDb.DeleteFuturesClosingPriceAsync(contractId, valueDate);
        await eventListener.StopAsync();

        async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            IEvent receivedEvent = eventVerb switch
            {
                _ when eventVerb == FuturesClosingPriceInsertedEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesClosingPriceInsertedEvent>()!),
                _ when eventVerb == FuturesClosingPriceInsertedCompleteEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesClosingPriceInsertedCompleteEvent>()!),
                _ when eventVerb == FuturesClosingPriceInsertedFailEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesClosingPriceInsertedFailEvent>()!),
                _ => default!
            };
            await ValueTask.CompletedTask;

            IEvent SetEvent(IEvent @event)
            {
                if (@event is FuturesClosingPriceInsertedEvent inserted)
                    futuresClosingPriceInsertedEvent = inserted;
                if (@event is FuturesClosingPriceInsertedCompleteEvent insertedComplete)
                {
                    futuresClosingPriceInsertedCompleteEvent = insertedComplete;
                    terminalEventReceived.TrySetResult();
                }
                if (@event is FuturesClosingPriceInsertedFailEvent insertedFail)
                {
                    futuresClosingPriceInsertedFailEvent = insertedFail;
                    terminalEventReceived.TrySetResult();
                }
                return @event;
            }
        }
    }
}
