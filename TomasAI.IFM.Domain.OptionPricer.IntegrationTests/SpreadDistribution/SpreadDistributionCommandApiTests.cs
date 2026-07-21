using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Actor.IntegrationTests;
using TomasAI.IFM.Application.Api.Client;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.RestApi;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Application.Commands;
using TomasAI.IFM.Shared.OptionPricer;
using TomasAI.IFM.Shared.OptionPricer.CommandParameters;
using TomasAI.IFM.Shared.OptionPricer.Commands;
using TomasAI.IFM.Shared.OptionPricer.Events;

namespace TomasAI.IFM.Domain.OptionPricer.IntegrationTests.SpreadDistribution;

public class SpreadDistributionCommandApiTests(WebApplicationFactory<Program> factory, OptionPricerFixture dbFixture)
    : IClassFixture<WebApplicationFactory<Program>>, IClassFixture<OptionPricerFixture>
{
    readonly HttpClientTestFactory _httpClientFactory = new(factory);
    readonly IJsonSerializer _jsonSerializer = new NewtonSoftJsonSerializer();
    readonly ILogger<NatsActorEventListener> _logger = Substitute.For<ILogger<NatsActorEventListener>>();

    [Fact]
    public async Task InsertSpreadDistribution_Ok()
    {
        // arrange...
        var eventListener = new NatsActorEventListener(new NatsEventListenerOptions(), _logger);
        SpreadDistributionInsertedEvent insertedEvent = default!;
        SpreadDistributionInsertedCompleteEvent insertedCompleteEvent = default!;
        SpreadDistributionInsertedFailEvent insertedFailEvent = default!;

        await eventListener.StartAsync(
            "TestEventListener",
            new()
            {
                [new ActorMailboxId(ActorType.Event, SpreadDistributionInsertedEvent.Actor)] = [
                    SpreadDistributionInsertedEvent.Verb,
                    SpreadDistributionInsertedCompleteEvent.Verb,
                    SpreadDistributionInsertedFailEvent.Verb]
            },
            EventHandlerAsync
        );

        var putSpread = SampleData.PutSpreadDistribution;
        var callSpread = SampleData.CallSpreadDistribution;
        var entityId = new SpreadDistributionEntityId(SampleData.TradeId, SampleData.ValueDate);
        var subject = new ActorSubject(ActorType.Command, InsertSpreadDistributionCommand.Actor, InsertSpreadDistributionCommand.Verb, entityId.Format());
        var eventStreamId = await dbFixture.ActorEventSourceDb.GetEventStreamIdAsync($"{subject.ThreadId}");
        if (eventStreamId > 0)
            await dbFixture.ActorEventSourceDb.DeleteEventLogByStreamIdAsync(eventStreamId);
        await dbFixture.OptionPricerDb.DeleteSpreadDistributionAsync(SampleData.TradeId, SampleData.ValueDate);

        // act...
        _httpClientFactory.CreateClient();
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var optionPricerApi = new OptionPricerCommandApi(commandServiceApi);
        var response = await optionPricerApi.InsertSpreadDistributionsAsync(putSpread, callSpread);

        await Task.Delay(1000);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
        insertedEvent.Should().NotBeNull();
        insertedCompleteEvent.Should().NotBeNull();
        insertedFailEvent.Should().BeNull();

        var savedPut = await dbFixture.OptionPricerDb.GetSpreadDistributionAsync(
            SampleData.TradeId, SampleData.PutTradeType, SampleData.TradeStatus, SampleData.ValueDate, SampleData.DaysToExpiry);
        savedPut.Should().NotBeNull();
        savedPut!.TradeId.Should().Be(SampleData.TradeId);
        savedPut.TradeType.Should().Be(SampleData.PutTradeType);

        var savedCall = await dbFixture.OptionPricerDb.GetSpreadDistributionAsync(
            SampleData.TradeId, SampleData.CallTradeType, SampleData.TradeStatus, SampleData.ValueDate, SampleData.DaysToExpiry);
        savedCall.Should().NotBeNull();
        savedCall!.TradeId.Should().Be(SampleData.TradeId);
        savedCall.TradeType.Should().Be(SampleData.CallTradeType);

        await eventListener.StopAsync();

        async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            IEvent receivedEvent = eventVerb switch
            {
                _ when eventVerb == SpreadDistributionInsertedEvent.Verb => SetEvent(eventMsg.AsEvent<SpreadDistributionInsertedEvent>()!),
                _ when eventVerb == SpreadDistributionInsertedCompleteEvent.Verb => SetEvent(eventMsg.AsEvent<SpreadDistributionInsertedCompleteEvent>()!),
                _ when eventVerb == SpreadDistributionInsertedFailEvent.Verb => SetEvent(eventMsg.AsEvent<SpreadDistributionInsertedFailEvent>()!),
                _ => default!
            };
            await ValueTask.CompletedTask;

            IEvent SetEvent(IEvent @event)
            {
                if (@event is SpreadDistributionInsertedEvent inserted)
                    insertedEvent = inserted;
                if (@event is SpreadDistributionInsertedCompleteEvent insertedComplete)
                    insertedCompleteEvent = insertedComplete;
                if (@event is SpreadDistributionInsertedFailEvent insertedFail)
                    insertedFailEvent = insertedFail;
                return @event;
            }
        }
    }

    [Fact]
    public async Task DeleteSpreadDistribution_Ok()
    {
        // arrange...
        var eventListener = new NatsActorEventListener(new NatsEventListenerOptions(), _logger);
        SpreadDistributionDeletedEvent deletedEvent = default!;
        SpreadDistributionDeletedCompleteEvent deletedCompleteEvent = default!;
        SpreadDistributionDeletedFailEvent deletedFailEvent = default!;

        await eventListener.StartAsync(
            "TestEventListener",
            new()
            {
                [new ActorMailboxId(ActorType.Event, SpreadDistributionDeletedEvent.Actor)] = [
                    SpreadDistributionDeletedEvent.Verb,
                    SpreadDistributionDeletedCompleteEvent.Verb,
                    SpreadDistributionDeletedFailEvent.Verb]
            },
            EventHandlerAsync
        );

        var putSpread = SampleData.PutSpreadDistribution;
        var callSpread = SampleData.CallSpreadDistribution;
        var entityId = new SpreadDistributionEntityId(SampleData.TradeId, SampleData.ValueDate);

        // clean up event stream for insert command...
        var insertSubject = new ActorSubject(ActorType.Command, InsertSpreadDistributionCommand.Actor, InsertSpreadDistributionCommand.Verb, entityId.Format());
        var insertStreamId = await dbFixture.ActorEventSourceDb.GetEventStreamIdAsync($"{insertSubject.ThreadId}");
        if (insertStreamId > 0)
            await dbFixture.ActorEventSourceDb.DeleteEventLogByStreamIdAsync(insertStreamId);

        // clean up event stream for delete command...
        var deleteKey = new SpreadDistributionKey(SampleData.TradeId, SampleData.TradeStatus, SampleData.ValueDate, SampleData.DaysToExpiry);
        var deleteSubject = new ActorSubject(ActorType.Command, DeleteSpreadDistributionCommand.Actor, DeleteSpreadDistributionCommand.Verb, deleteKey.Format());
        var deleteStreamId = await dbFixture.ActorEventSourceDb.GetEventStreamIdAsync($"{deleteSubject.ThreadId}");
        if (deleteStreamId > 0)
            await dbFixture.ActorEventSourceDb.DeleteEventLogByStreamIdAsync(deleteStreamId);

        await dbFixture.OptionPricerDb.DeleteSpreadDistributionAsync(SampleData.TradeId, SampleData.ValueDate);

        // seed data by inserting first...
        _httpClientFactory.CreateClient();
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var optionPricerApi = new OptionPricerCommandApi(commandServiceApi);
        var insertResponse = await optionPricerApi.InsertSpreadDistributionsAsync(putSpread, callSpread);
        await Task.Delay(1000);
        insertResponse.Success.Should().BeTrue();

        // act...
        var response = await optionPricerApi.DeleteSpreadDistributionAsync(entityId, Shared.Trade.TradeStatus.IntraDay,SampleData.DaysToExpiry);

        await Task.Delay(1000);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
        deletedEvent.Should().NotBeNull();
        deletedCompleteEvent.Should().NotBeNull();
        deletedFailEvent.Should().BeNull();

        var deletedPut = await dbFixture.OptionPricerDb.GetSpreadDistributionAsync(
            SampleData.TradeId, SampleData.PutTradeType, SampleData.TradeStatus, SampleData.ValueDate, SampleData.DaysToExpiry);
        deletedPut.Should().BeNull();

        var deletedCall = await dbFixture.OptionPricerDb.GetSpreadDistributionAsync(
            SampleData.TradeId, SampleData.CallTradeType, SampleData.TradeStatus, SampleData.ValueDate, SampleData.DaysToExpiry);
        deletedCall.Should().BeNull();

        await eventListener.StopAsync();

        async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            IEvent receivedEvent = eventVerb switch
            {
                _ when eventVerb == SpreadDistributionDeletedEvent.Verb => SetEvent(eventMsg.AsEvent<SpreadDistributionDeletedEvent>()!),
                _ when eventVerb == SpreadDistributionDeletedCompleteEvent.Verb => SetEvent(eventMsg.AsEvent<SpreadDistributionDeletedCompleteEvent>()!),
                _ when eventVerb == SpreadDistributionDeletedFailEvent.Verb => SetEvent(eventMsg.AsEvent<SpreadDistributionDeletedFailEvent>()!),
                _ => default!
            };
            await ValueTask.CompletedTask;

            IEvent SetEvent(IEvent @event)
            {
                if (@event is SpreadDistributionDeletedEvent deleted)
                    deletedEvent = deleted;
                if (@event is SpreadDistributionDeletedCompleteEvent deletedComplete)
                    deletedCompleteEvent = deletedComplete;
                if (@event is SpreadDistributionDeletedFailEvent deletedFail)
                    deletedFailEvent = deletedFail;
                return @event;
            }
        }
    }
}
