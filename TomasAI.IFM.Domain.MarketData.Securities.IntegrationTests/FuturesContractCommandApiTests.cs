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
using TomasAI.IFM.Shared.MarketData.Commands;
using TomasAI.IFM.Shared.MarketData.Events;

namespace TomasAI.IFM.Domain.MarketData.Securities.IntegrationTests;

/// <summary>
/// Provides integration tests for securities-related command operations, including adding, changing, and removing
/// futures contracts. Validates the behavior of command APIs and their event-driven workflows in a controlled 
/// test environment.
/// </summary>
/// <param name="factory">The web application factory used to create test HTTP clients for simulating API requests.</param>
/// <param name="dbFixture">The database fixture that provides access to test database instances and utilities for securities-related data setup and cleanup.</param>
public class FuturesContractFuturesContractCommandApiTests(WebApplicationFactory<Program> factory, SecuritiesDatabaseFixture dbFixture)
    : IClassFixture<WebApplicationFactory<Program>>, IClassFixture<SecuritiesDatabaseFixture>
{
    readonly HttpClientTestFactory _httpClientFactory = new(factory);
    readonly IJsonSerializer _jsonSerializer = new NewtonSoftJsonSerializer();
    readonly ILogger<NatsActorEventListener> _logger = Substitute.For<ILogger<NatsActorEventListener>>();

    [Fact]
    public async Task AddFuturesContract_Ok()
    {
        // arrange...
        var eventListener = new NatsActorEventListener(new NatsEventListenerOptions(), _logger);
        FuturesContractAddedEvent futuresContractAddedEvent = default!;
        FuturesContractAddedCompleteEvent futuresContractAddedCompleteEvent = default!;
        FuturesContractAddedFailEvent futuresContractAddedFailEvent = default!;

        await eventListener.StartAsync(
            "TestEventListener",
            new()
            {
                [new ActorMailboxId(ActorType.Event, FuturesContractAddedEvent.Actor)] = [FuturesContractAddedEvent.Verb]
            },
            EventHandlerAsync
        );

        var futuresContract = SampleData.NewFuturesContract;
        var subject = new ActorSubject(ActorType.Command, AddFuturesContractCommand.Actor, AddFuturesContractCommand.Verb, futuresContract.Id.Format());
        dbFixture.BlackboardService.EventStreamId.Remove($"{subject.ThreadId}");
        var eventStreamId = await dbFixture.ActorEventSourceDb.GetEventStreamIdAsync($"{subject.ThreadId}");
        if (eventStreamId > 0)
            await dbFixture.ActorEventSourceDb.DeleteEventLogByStreamIdAsync(eventStreamId);
        await dbFixture.Db.DeleteFuturesContractAsync(futuresContract.Id);

        // act...
        _httpClientFactory.CreateClient();
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var marketDataApi = new MarketDataCommandApi(commandServiceApi);
        var response = await marketDataApi.AddFuturesContractAsync(futuresContract, overwrite: false);

        await Task.Delay(1000);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
        futuresContractAddedEvent.Should().NotBeNull();
        futuresContractAddedCompleteEvent.Should().NotBeNull();
        futuresContractAddedFailEvent.Should().BeNull();

        var savedContract = await dbFixture.Db.GetFuturesContractAsync(futuresContract.Id);
        savedContract.Should().NotBeNull();
        savedContract!.ContractId.Should().Be(futuresContract.ContractId);
        savedContract.Symbol.Should().Be(futuresContract.Symbol);
        savedContract.Description.Should().Be(futuresContract.Description);
        savedContract.LocalSymbol.Should().Be(futuresContract.LocalSymbol);
        savedContract.SecurityType.Should().Be(futuresContract.SecurityType);
        savedContract.Currency.Should().Be(futuresContract.Currency);
        savedContract.Exchange.Should().Be(futuresContract.Exchange);
        savedContract.Multiplier.Should().Be(futuresContract.Multiplier);
        savedContract.LastTradeDate.Should().Be(futuresContract.LastTradeDate);
        savedContract.CurrentlyTraded.Should().Be(futuresContract.CurrentlyTraded);
        
        await eventListener.StopAsync();

                async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
                {
                    IEvent receivedEvent = eventVerb switch
                    {
                        _ when eventVerb == FuturesContractAddedEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesContractAddedEvent>()!),
                        _ when eventVerb == FuturesContractAddedCompleteEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesContractAddedCompleteEvent>()!),
                        _ when eventVerb == FuturesContractAddedFailEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesContractAddedFailEvent>()!),
                        _ => default!
                    };
                    await ValueTask.CompletedTask;

                    IEvent SetEvent(IEvent @event)
                    {
                        switch (@event)
                        {
                            case FuturesContractAddedEvent e:
                                futuresContractAddedEvent = e;
                                break;
                            case FuturesContractAddedCompleteEvent e:
                                futuresContractAddedCompleteEvent = e;
                                break;
                            case FuturesContractAddedFailEvent e:
                                futuresContractAddedFailEvent = e;
                                break;
                        }
                        return @event;
                    }
                }
            }

            [Fact]
            public async Task ChangeFuturesContract_Ok()
            {
                // arrange...
                var eventListener = new NatsActorEventListener(new NatsEventListenerOptions(), _logger);
                FuturesContractAddedEvent futuresContractAddedEvent = default!;
                FuturesContractAddedCompleteEvent futuresContractAddedCompleteEvent = default!;
                FuturesContractAddedFailEvent futuresContractAddedFailEvent = default!;
                FuturesContractChangedEvent futuresContractChangedEvent = default!;
                FuturesContractChangedCompleteEvent futuresContractChangedCompleteEvent = default!;
                FuturesContractChangedFailEvent futuresContractChangedFailEvent = default!;

                await eventListener.StartAsync(
                    "TestEventListener",
                    new()
                    {
                        [new ActorMailboxId(ActorType.Event, FuturesContractAddedEvent.Actor)] = [FuturesContractAddedEvent.Verb],
                        [new ActorMailboxId(ActorType.Event, FuturesContractChangedEvent.Actor)] = [FuturesContractChangedEvent.Verb]
                    },
                    EventHandlerAsync
                );

                var futuresContract = SampleData.NewFuturesContract;
                var changedContract = SampleData.ChangedFuturesContract;
        
                // Clean up any existing data
                var addSubject = new ActorSubject(ActorType.Command, AddFuturesContractCommand.Actor, AddFuturesContractCommand.Verb, $"{futuresContract.Id.Format()}");
                dbFixture.BlackboardService.EventStreamId.Remove($"{addSubject.ThreadId}");
                var addEventStreamId = await dbFixture.ActorEventSourceDb.GetEventStreamIdAsync($"{addSubject.ThreadId}");
                if (addEventStreamId > 0)
                    await dbFixture.ActorEventSourceDb.DeleteEventLogByStreamIdAsync(addEventStreamId);
                await dbFixture.Db.DeleteFuturesContractAsync(futuresContract.Id);

                // act - add futures contract first...
                _httpClientFactory.CreateClient();
                var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
                var marketDataApi = new MarketDataCommandApi(commandServiceApi);
                var addResponse = await marketDataApi.AddFuturesContractAsync(futuresContract, overwrite: false);

                await Task.Delay(1000);

                // assert - verify add was successful...
                addResponse.Should().NotBeNull();
                addResponse.Success.Should().BeTrue();
                addResponse.Value.Should().NotBe(Guid.Empty);
                futuresContractAddedEvent.Should().NotBeNull();
                futuresContractAddedCompleteEvent.Should().NotBeNull();
                futuresContractAddedFailEvent.Should().BeNull();

                var savedContract = await dbFixture.Db.GetFuturesContractAsync(futuresContract.Id);
                savedContract.Should().NotBeNull();
                savedContract!.ContractId.Should().Be(futuresContract.ContractId);
                savedContract.Symbol.Should().Be(futuresContract.Symbol);
                savedContract.Description.Should().Be(futuresContract.Description);

                // act - change futures contract...
                var changeResponse = await marketDataApi.ChangeFuturesContractAsync(futuresContract.Id, changedContract, overwrite: true);

                await Task.Delay(1000);

                // assert - verify change was successful...
                changeResponse.Should().NotBeNull();
                changeResponse.Success.Should().BeTrue();
                changeResponse.Value.Should().NotBe(Guid.Empty);
                futuresContractChangedEvent.Should().NotBeNull();
                futuresContractChangedCompleteEvent.Should().NotBeNull();
                futuresContractChangedFailEvent.Should().BeNull();

                var updatedContract = await dbFixture.Db.GetFuturesContractAsync(changedContract.Id);
                updatedContract.Should().NotBeNull();
                updatedContract!.ContractId.Should().Be(changedContract.ContractId);
                updatedContract.Symbol.Should().Be(changedContract.Symbol);
                updatedContract.Description.Should().Be(changedContract.Description);
                updatedContract.LocalSymbol.Should().Be(changedContract.LocalSymbol);
                updatedContract.SecurityType.Should().Be(changedContract.SecurityType);
                updatedContract.Currency.Should().Be(changedContract.Currency);
                updatedContract.Exchange.Should().Be(changedContract.Exchange);
                updatedContract.Multiplier.Should().Be(changedContract.Multiplier);
                updatedContract.LastTradeDate.Should().Be(changedContract.LastTradeDate);
                updatedContract.CurrentlyTraded.Should().Be(changedContract.CurrentlyTraded);

                await eventListener.StopAsync();

                async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
                {
                    IEvent receivedEvent = eventVerb switch
                    {
                        _ when eventVerb == FuturesContractAddedEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesContractAddedEvent>()!),
                        _ when eventVerb == FuturesContractAddedCompleteEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesContractAddedCompleteEvent>()!),
                        _ when eventVerb == FuturesContractAddedFailEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesContractAddedFailEvent>()!),
                        _ when eventVerb == FuturesContractChangedEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesContractChangedEvent>()!),
                        _ when eventVerb == FuturesContractChangedCompleteEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesContractChangedCompleteEvent>()!),
                        _ when eventVerb == FuturesContractChangedFailEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesContractChangedFailEvent>()!),
                        _ => default!
                    };
                    await ValueTask.CompletedTask;

                    IEvent SetEvent(IEvent @event)
                    {
                        switch (@event)
                        {
                            case FuturesContractAddedEvent e:
                                futuresContractAddedEvent = e;
                                break;
                            case FuturesContractAddedCompleteEvent e:
                                futuresContractAddedCompleteEvent = e;
                                break;
                            case FuturesContractAddedFailEvent e:
                                futuresContractAddedFailEvent = e;
                                break;
                            case FuturesContractChangedEvent e:
                                futuresContractChangedEvent = e;
                                break;
                            case FuturesContractChangedCompleteEvent e:
                                futuresContractChangedCompleteEvent = e;
                                break;
                            case FuturesContractChangedFailEvent e:
                                futuresContractChangedFailEvent = e;
                                break;
                        }
                        return @event;
                    }
                }
            }

    [Fact]
    public async Task RemoveFuturesContract_Ok()
    {
        // arrange...
        var eventListener = new NatsActorEventListener(new NatsEventListenerOptions(), _logger);
        FuturesContractAddedEvent futuresContractAddedEvent = default!;
        FuturesContractAddedCompleteEvent futuresContractAddedCompleteEvent = default!;
        FuturesContractAddedFailEvent futuresContractAddedFailEvent = default!;
        FuturesContractRemovedEvent futuresContractRemovedEvent = default!;
        FuturesContractRemovedCompleteEvent futuresContractRemovedCompleteEvent = default!;
        FuturesContractRemovedFailEvent futuresContractRemovedFailEvent = default!;

        await eventListener.StartAsync(
            "TestEventListener",
            new()
            {
                [new ActorMailboxId(ActorType.Event, FuturesContractAddedEvent.Actor)] = [FuturesContractAddedEvent.Verb, FuturesContractRemovedEvent.Verb],
            },
            EventHandlerAsync
        );

        var futuresContract = SampleData.NewFuturesContract;

        // Clean up any existing data
        var addSubject = new ActorSubject(ActorType.Command, AddFuturesContractCommand.Actor, AddFuturesContractCommand.Verb, $"{futuresContract.Id.Format()}");
        dbFixture.BlackboardService.EventStreamId.Remove($"{addSubject.ThreadId}");
        var addEventStreamId = await dbFixture.ActorEventSourceDb.GetEventStreamIdAsync($"{addSubject.ThreadId}");
        if (addEventStreamId > 0)
            await dbFixture.ActorEventSourceDb.DeleteEventLogByStreamIdAsync(addEventStreamId);
        await dbFixture.Db.DeleteFuturesContractAsync(futuresContract.Id);

        // act - add futures contract first...
        _httpClientFactory.CreateClient();
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var marketDataApi = new MarketDataCommandApi(commandServiceApi);
        var addResponse = await marketDataApi.AddFuturesContractAsync(futuresContract, overwrite: false);

        await Task.Delay(1000);

        // assert - verify add was successful...
        addResponse.Should().NotBeNull();
        addResponse.Success.Should().BeTrue();
        addResponse.Value.Should().NotBe(Guid.Empty);
        futuresContractAddedEvent.Should().NotBeNull();
        futuresContractAddedCompleteEvent.Should().NotBeNull();
        futuresContractAddedFailEvent.Should().BeNull();

        var savedContract = await dbFixture.Db.GetFuturesContractAsync(futuresContract.Id);
        savedContract.Should().NotBeNull();
        savedContract!.ContractId.Should().Be(futuresContract.ContractId);
        savedContract.Symbol.Should().Be(futuresContract.Symbol);
        savedContract.Description.Should().Be(futuresContract.Description);

        // act - remove futures contract...
        var removeResponse = await marketDataApi.RemoveFuturesContractAsync(futuresContract.Id, overwrite: true);

        await Task.Delay(5000);

        // assert - verify remove was successful...
        removeResponse.Should().NotBeNull();
        removeResponse.Success.Should().BeTrue();
        removeResponse.Value.Should().NotBe(Guid.Empty);
        futuresContractRemovedEvent.Should().NotBeNull();
        futuresContractRemovedCompleteEvent.Should().NotBeNull();
        futuresContractRemovedFailEvent.Should().BeNull();

        var removedContract = await dbFixture.Db.GetFuturesContractAsync(futuresContract.Id);
        removedContract.Should().BeNull();

        await eventListener.StopAsync();

        async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            IEvent receivedEvent = eventVerb switch
            {
                _ when eventVerb == FuturesContractAddedEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesContractAddedEvent>()!),
                _ when eventVerb == FuturesContractAddedCompleteEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesContractAddedCompleteEvent>()!),
                _ when eventVerb == FuturesContractAddedFailEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesContractAddedFailEvent>()!),
                _ when eventVerb == FuturesContractRemovedEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesContractRemovedEvent>()!),
                _ when eventVerb == FuturesContractRemovedCompleteEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesContractRemovedCompleteEvent>()!),
                _ when eventVerb == FuturesContractRemovedFailEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesContractRemovedFailEvent>()!),
                _ => default!
            };
            await ValueTask.CompletedTask;

            IEvent SetEvent(IEvent @event)
            {
                switch (@event)
                {
                    case FuturesContractAddedEvent e:
                        futuresContractAddedEvent = e;
                        break;
                    case FuturesContractAddedCompleteEvent e:
                        futuresContractAddedCompleteEvent = e;
                        break;
                    case FuturesContractAddedFailEvent e:
                        futuresContractAddedFailEvent = e;
                        break;
                    case FuturesContractRemovedEvent e:
                        futuresContractRemovedEvent = e;
                        break;
                    case FuturesContractRemovedCompleteEvent e:
                        futuresContractRemovedCompleteEvent = e;
                        break;
                    case FuturesContractRemovedFailEvent e:
                        futuresContractRemovedFailEvent = e;
                        break;
                }
                return @event;
            }
        }
    }
}
