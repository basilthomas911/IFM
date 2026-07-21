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
/// Provides integration tests for futures option contract command operations, including adding futures option contracts.
/// Validates the behavior of command APIs and their event-driven workflows in a controlled test environment.
/// </summary>
/// <param name="factory">The web application factory used to create test HTTP clients for simulating API requests.</param>
/// <param name="dbFixture">The database fixture that provides access to test database instances and utilities for securities-related data setup and cleanup.</param>
public class FuturesOptionContractCommandApiTests(WebApplicationFactory<Program> factory, SecuritiesDatabaseFixture dbFixture)
    : IClassFixture<WebApplicationFactory<Program>>, IClassFixture<SecuritiesDatabaseFixture>
{
    readonly HttpClientTestFactory _httpClientFactory = new(factory);
    readonly IJsonSerializer _jsonSerializer = new NewtonSoftJsonSerializer();
    readonly ILogger<NatsActorEventListener> _logger = Substitute.For<ILogger<NatsActorEventListener>>();

    [Fact]
    public async Task AddFuturesOptionContract_Ok()
    {
        // arrange...
        var eventListener = new NatsActorEventListener(new NatsEventListenerOptions(), _logger);
        FuturesOptionContractAddedEvent futuresOptionContractAddedEvent = default!;
        FuturesOptionContractAddedCompleteEvent futuresOptionContractAddedCompleteEvent = default!;
        FuturesOptionContractAddedFailEvent futuresOptionContractAddedFailEvent = default!;

        await eventListener.StartAsync(
            "TestEventListener",
            new()
            {
                [new ActorMailboxId(ActorType.Event, FuturesOptionContractAddedEvent.Actor)] = [FuturesOptionContractAddedEvent.Verb]
            },
            EventHandlerAsync
        );

        var futuresOptionContract = SampleData.NewFuturesOptionContract;
        var subject = new ActorSubject(ActorType.Command, AddFuturesOptionContractCommand.Actor, AddFuturesOptionContractCommand.Verb, futuresOptionContract.ContractId);
        dbFixture.BlackboardService.EventStreamId.Remove($"{subject.ThreadId}");
        var eventStreamId = await dbFixture.ActorEventSourceDb.GetEventStreamIdAsync($"{subject.ThreadId}");
        if (eventStreamId > 0)
            await dbFixture.ActorEventSourceDb.DeleteEventLogByStreamIdAsync(eventStreamId);
        await dbFixture.Db.DeleteFuturesOptionContractAsync(futuresOptionContract.ContractId);

        // act...
        _httpClientFactory.CreateClient();
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var marketDataApi = new MarketDataCommandApi(commandServiceApi);
        var response = await marketDataApi.AddFuturesOptionContractAsync(futuresOptionContract, overwrite: false);

        await Task.Delay(1000);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
        futuresOptionContractAddedEvent.Should().NotBeNull();
        futuresOptionContractAddedCompleteEvent.Should().NotBeNull();
        futuresOptionContractAddedFailEvent.Should().BeNull();

        var savedContract = await dbFixture.Db.GetFuturesOptionContractAsync(futuresOptionContract.ContractId);
        savedContract.Should().NotBeNull();
        savedContract!.ContractId.Should().Be(futuresOptionContract.ContractId);
        savedContract.Symbol.Should().Be(futuresOptionContract.Symbol);
        savedContract.Description.Should().Be(futuresOptionContract.Description);
        savedContract.LocalSymbol.Should().Be(futuresOptionContract.LocalSymbol);
        savedContract.SecurityType.Should().Be(futuresOptionContract.SecurityType);
        savedContract.Currency.Should().Be(futuresOptionContract.Currency);
        savedContract.Exchange.Should().Be(futuresOptionContract.Exchange);
        savedContract.Multiplier.Should().Be(futuresOptionContract.Multiplier);
        savedContract.ContractMonth.Should().Be(futuresOptionContract.ContractMonth);
        savedContract.StrikePrice.Should().Be(futuresOptionContract.StrikePrice);
        savedContract.OptionType.Should().Be(futuresOptionContract.OptionType);

        await eventListener.StopAsync();

        async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            IEvent receivedEvent = eventVerb switch
            {
                _ when eventVerb == FuturesOptionContractAddedEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesOptionContractAddedEvent>()!),
                _ when eventVerb == FuturesOptionContractAddedCompleteEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesOptionContractAddedCompleteEvent>()!),
                _ when eventVerb == FuturesOptionContractAddedFailEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesOptionContractAddedFailEvent>()!),
                _ => default!
            };
            await ValueTask.CompletedTask;

            IEvent SetEvent(IEvent @event)
            {
                switch (@event)
                {
                    case FuturesOptionContractAddedEvent e:
                        futuresOptionContractAddedEvent = e;
                        break;
                    case FuturesOptionContractAddedCompleteEvent e:
                        futuresOptionContractAddedCompleteEvent = e;
                        break;
                    case FuturesOptionContractAddedFailEvent e:
                        futuresOptionContractAddedFailEvent = e;
                        break;
                }
                return @event;
            }
        }
    }

    [Fact]
    public async Task AddFuturesOptionContracts_Ok()
    {
        // arrange...
        var eventListener = new NatsActorEventListener(new NatsEventListenerOptions(), _logger);
        FuturesOptionContractsAddedEvent futuresOptionContractsAddedEvent = default!;
        FuturesOptionContractsAddedCompleteEvent futuresOptionContractsAddedCompleteEvent = default!;
        FuturesOptionContractsAddedFailEvent futuresOptionContractsAddedFailEvent = default!;

        await eventListener.StartAsync(
            "TestEventListener",
            new()
            {
                [new ActorMailboxId(ActorType.Event, FuturesOptionContractsAddedEvent.Actor)] = [FuturesOptionContractsAddedEvent.Verb]
            },
            EventHandlerAsync
        );

        var futuresOptionContracts = SampleData.NewFuturesOptionContracts;
        
        // Clean up any existing data for all contracts
        foreach (var contract in futuresOptionContracts)
        {
            var subject = new ActorSubject(ActorType.Command, AddFuturesOptionContractsCommand.Actor, AddFuturesOptionContractsCommand.Verb, contract.ContractId);
            dbFixture.BlackboardService.EventStreamId.Remove($"{subject.ThreadId}");
            var eventStreamId = await dbFixture.ActorEventSourceDb.GetEventStreamIdAsync($"{subject.ThreadId}");
            if (eventStreamId > 0)
                await dbFixture.ActorEventSourceDb.DeleteEventLogByStreamIdAsync(eventStreamId);
            await dbFixture.Db.DeleteFuturesOptionContractAsync(contract.ContractId);
        }

        // act...
        _httpClientFactory.CreateClient();
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var marketDataApi = new MarketDataCommandApi(commandServiceApi);
        var response = await marketDataApi.AddFuturesOptionContractsAsync(DateTime.UtcNow.Year, futuresOptionContracts);

        await Task.Delay(2000);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
        futuresOptionContractsAddedEvent.Should().NotBeNull();
        futuresOptionContractsAddedCompleteEvent.Should().NotBeNull();
        futuresOptionContractsAddedFailEvent.Should().BeNull();

        // Verify all contracts were saved to database
        foreach (var expectedContract in futuresOptionContracts)
        {
            var savedContract = await dbFixture.Db.GetFuturesOptionContractAsync(expectedContract.ContractId);
            savedContract.Should().NotBeNull($"Contract {expectedContract.ContractId} should be saved");
            savedContract!.ContractId.Should().Be(expectedContract.ContractId);
            savedContract.Symbol.Should().Be(expectedContract.Symbol);
            savedContract.Description.Should().Be(expectedContract.Description);
            savedContract.LocalSymbol.Should().Be(expectedContract.LocalSymbol);
            savedContract.SecurityType.Should().Be(expectedContract.SecurityType);
            savedContract.Currency.Should().Be(expectedContract.Currency);
            savedContract.Exchange.Should().Be(expectedContract.Exchange);
            savedContract.Multiplier.Should().Be(expectedContract.Multiplier);
            savedContract.ContractMonth.Should().Be(expectedContract.ContractMonth);
            savedContract.StrikePrice.Should().Be(expectedContract.StrikePrice);
            savedContract.OptionType.Should().Be(expectedContract.OptionType);
        }

        await eventListener.StopAsync();

        async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            IEvent receivedEvent = eventVerb switch
            {
                _ when eventVerb == FuturesOptionContractsAddedEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesOptionContractsAddedEvent>()!),
                _ when eventVerb == FuturesOptionContractsAddedCompleteEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesOptionContractsAddedCompleteEvent>()!),
                _ when eventVerb == FuturesOptionContractsAddedFailEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesOptionContractsAddedFailEvent>()!),
                _ => default!
            };
            await ValueTask.CompletedTask;

            IEvent SetEvent(IEvent @event)
            {
                switch (@event)
                {
                    case FuturesOptionContractsAddedEvent e:
                        futuresOptionContractsAddedEvent = e;
                        break;
                    case FuturesOptionContractsAddedCompleteEvent e:
                        futuresOptionContractsAddedCompleteEvent = e;
                        break;
                    case FuturesOptionContractsAddedFailEvent e:
                        futuresOptionContractsAddedFailEvent = e;
                        break;
                }
                return @event;
            }
        }
    }

    [Fact]
    public async Task ChangeFuturesOptionContract_Ok()
    {
        // arrange...
        var eventListener = new NatsActorEventListener(new NatsEventListenerOptions(), _logger);
        FuturesOptionContractAddedEvent futuresOptionContractAddedEvent = default!;
        FuturesOptionContractAddedCompleteEvent futuresOptionContractAddedCompleteEvent = default!;
        FuturesOptionContractAddedFailEvent futuresOptionContractAddedFailEvent = default!;
        FuturesOptionContractChangedEvent futuresOptionContractChangedEvent = default!;
        FuturesOptionContractChangedCompleteEvent futuresOptionContractChangedCompleteEvent = default!;
        FuturesOptionContractChangedFailEvent futuresOptionContractChangedFailEvent = default!;

        await eventListener.StartAsync(
            "TestEventListener",
            new()
            {
                [new ActorMailboxId(ActorType.Event, FuturesOptionContractAddedEvent.Actor)] = [FuturesOptionContractAddedEvent.Verb],
                [new ActorMailboxId(ActorType.Event, FuturesOptionContractChangedEvent.Actor)] = [FuturesOptionContractChangedEvent.Verb]
            },
            EventHandlerAsync
        );

        var futuresOptionContract = SampleData.NewFuturesOptionContract;
        var changedContract = SampleData.ChangedFuturesOptionContract;

        // Clean up any existing data
        var addSubject = new ActorSubject(ActorType.Command, AddFuturesOptionContractCommand.Actor, AddFuturesOptionContractCommand.Verb, futuresOptionContract.ContractId);
        dbFixture.BlackboardService.EventStreamId.Remove($"{addSubject.ThreadId}");
        var addEventStreamId = await dbFixture.ActorEventSourceDb.GetEventStreamIdAsync($"{addSubject.ThreadId}");
        if (addEventStreamId > 0)
            await dbFixture.ActorEventSourceDb.DeleteEventLogByStreamIdAsync(addEventStreamId);
        await dbFixture.Db.DeleteFuturesOptionContractAsync(futuresOptionContract.ContractId);

        // act - add futures option contract first...
        _httpClientFactory.CreateClient();
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var marketDataApi = new MarketDataCommandApi(commandServiceApi);
        var addResponse = await marketDataApi.AddFuturesOptionContractAsync(futuresOptionContract, overwrite: false);

        await Task.Delay(1000);

        // assert - verify add was successful...
        addResponse.Should().NotBeNull();
        addResponse.Success.Should().BeTrue();
        addResponse.Value.Should().NotBe(Guid.Empty);
        futuresOptionContractAddedEvent.Should().NotBeNull();
        futuresOptionContractAddedCompleteEvent.Should().NotBeNull();
        futuresOptionContractAddedFailEvent.Should().BeNull();

        var savedContract = await dbFixture.Db.GetFuturesOptionContractAsync(futuresOptionContract.ContractId);
        savedContract.Should().NotBeNull();
        savedContract!.ContractId.Should().Be(futuresOptionContract.ContractId);
        savedContract.Symbol.Should().Be(futuresOptionContract.Symbol);
        savedContract.Description.Should().Be(futuresOptionContract.Description);

        // act - change futures option contract...
        var changeResponse = await marketDataApi.ChangeFuturesOptionContractAsync(futuresOptionContract.ContractId, changedContract, overwrite: true);

        await Task.Delay(1000);

        // assert - verify change was successful...
        changeResponse.Should().NotBeNull();
        changeResponse.Success.Should().BeTrue();
        changeResponse.Value.Should().NotBe(Guid.Empty);
        futuresOptionContractChangedEvent.Should().NotBeNull();
        futuresOptionContractChangedCompleteEvent.Should().NotBeNull();
        futuresOptionContractChangedFailEvent.Should().BeNull();

        var updatedContract = await dbFixture.Db.GetFuturesOptionContractAsync(changedContract.ContractId);
        updatedContract.Should().NotBeNull();
        updatedContract!.ContractId.Should().Be(changedContract.ContractId);
        updatedContract.Symbol.Should().Be(changedContract.Symbol);
        updatedContract.Description.Should().Be(changedContract.Description);
        updatedContract.LocalSymbol.Should().Be(changedContract.LocalSymbol);
        updatedContract.SecurityType.Should().Be(changedContract.SecurityType);
        updatedContract.Currency.Should().Be(changedContract.Currency);
        updatedContract.Exchange.Should().Be(changedContract.Exchange);
        updatedContract.Multiplier.Should().Be(changedContract.Multiplier);
        updatedContract.ContractMonth.Should().Be(changedContract.ContractMonth);
        updatedContract.StrikePrice.Should().Be(changedContract.StrikePrice);
        updatedContract.OptionType.Should().Be(changedContract.OptionType);

        await eventListener.StopAsync();

        async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            IEvent receivedEvent = eventVerb switch
            {
                _ when eventVerb == FuturesOptionContractAddedEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesOptionContractAddedEvent>()!),
                _ when eventVerb == FuturesOptionContractAddedCompleteEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesOptionContractAddedCompleteEvent>()!),
                _ when eventVerb == FuturesOptionContractAddedFailEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesOptionContractAddedFailEvent>()!),
                _ when eventVerb == FuturesOptionContractChangedEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesOptionContractChangedEvent>()!),
                _ when eventVerb == FuturesOptionContractChangedCompleteEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesOptionContractChangedCompleteEvent>()!),
                _ when eventVerb == FuturesOptionContractChangedFailEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesOptionContractChangedFailEvent>()!),
                _ => default!
            };
            await ValueTask.CompletedTask;

            IEvent SetEvent(IEvent @event)
            {
                switch (@event)
                {
                    case FuturesOptionContractAddedEvent e:
                        futuresOptionContractAddedEvent = e;
                        break;
                    case FuturesOptionContractAddedCompleteEvent e:
                        futuresOptionContractAddedCompleteEvent = e;
                        break;
                    case FuturesOptionContractAddedFailEvent e:
                        futuresOptionContractAddedFailEvent = e;
                        break;
                    case FuturesOptionContractChangedEvent e:
                        futuresOptionContractChangedEvent = e;
                        break;
                    case FuturesOptionContractChangedCompleteEvent e:
                        futuresOptionContractChangedCompleteEvent = e;
                        break;
                    case FuturesOptionContractChangedFailEvent e:
                        futuresOptionContractChangedFailEvent = e;
                        break;
                }
                return @event;
            }
        }
    }

    [Fact]
    public async Task RemoveFuturesOptionContract_Ok()
    {
        // arrange...
        var eventListener = new NatsActorEventListener(new NatsEventListenerOptions(), _logger);
        FuturesOptionContractAddedEvent futuresOptionContractAddedEvent = default!;
        FuturesOptionContractAddedCompleteEvent futuresOptionContractAddedCompleteEvent = default!;
        FuturesOptionContractAddedFailEvent futuresOptionContractAddedFailEvent = default!;
        FuturesOptionContractRemovedEvent futuresOptionContractRemovedEvent = default!;
        FuturesOptionContractRemovedCompleteEvent futuresOptionContractRemovedCompleteEvent = default!;
        FuturesOptionContractRemovedFailEvent futuresOptionContractRemovedFailEvent = default!;

        await eventListener.StartAsync(
            "TestEventListener",
            new()
            {
                [new ActorMailboxId(ActorType.Event, FuturesOptionContractAddedEvent.Actor)] = [FuturesOptionContractAddedEvent.Verb, FuturesOptionContractRemovedEvent.Verb],
            },
            EventHandlerAsync
        );

        var futuresOptionContract = SampleData.NewFuturesOptionContract;

        // Clean up any existing data
        var addSubject = new ActorSubject(ActorType.Command, AddFuturesOptionContractCommand.Actor, AddFuturesOptionContractCommand.Verb, futuresOptionContract.ContractId);
        dbFixture.BlackboardService.EventStreamId.Remove($"{addSubject.ThreadId}");
        var addEventStreamId = await dbFixture.ActorEventSourceDb.GetEventStreamIdAsync($"{addSubject.ThreadId}");
        if (addEventStreamId > 0)
            await dbFixture.ActorEventSourceDb.DeleteEventLogByStreamIdAsync(addEventStreamId);
        await dbFixture.Db.DeleteFuturesOptionContractAsync(futuresOptionContract.ContractId);

        // act - add futures option contract first...
        _httpClientFactory.CreateClient();
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var marketDataApi = new MarketDataCommandApi(commandServiceApi);
        var addResponse = await marketDataApi.AddFuturesOptionContractAsync(futuresOptionContract, overwrite: false);

        await Task.Delay(1000);

        // assert - verify add was successful...
        addResponse.Should().NotBeNull();
        addResponse.Success.Should().BeTrue();
        addResponse.Value.Should().NotBe(Guid.Empty);
        futuresOptionContractAddedEvent.Should().NotBeNull();
        futuresOptionContractAddedCompleteEvent.Should().NotBeNull();
        futuresOptionContractAddedFailEvent.Should().BeNull();

        var savedContract = await dbFixture.Db.GetFuturesOptionContractAsync(futuresOptionContract.ContractId);
        savedContract.Should().NotBeNull();
        savedContract!.ContractId.Should().Be(futuresOptionContract.ContractId);
        savedContract.Symbol.Should().Be(futuresOptionContract.Symbol);
        savedContract.Description.Should().Be(futuresOptionContract.Description);

        // act - remove futures option contract...
        var removeResponse = await marketDataApi.RemoveFuturesOptionContractAsync(futuresOptionContract.ContractId, overwrite: true);

        await Task.Delay(5000);

        // assert - verify remove was successful...
        removeResponse.Should().NotBeNull();
        removeResponse.Success.Should().BeTrue();
        removeResponse.Value.Should().NotBe(Guid.Empty);
        futuresOptionContractRemovedEvent.Should().NotBeNull();
        futuresOptionContractRemovedCompleteEvent.Should().NotBeNull();
        futuresOptionContractRemovedFailEvent.Should().BeNull();

        var removedContract = await dbFixture.Db.GetFuturesOptionContractAsync(futuresOptionContract.ContractId);
        removedContract.Should().BeNull();

        await eventListener.StopAsync();

        async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            IEvent receivedEvent = eventVerb switch
            {
                _ when eventVerb == FuturesOptionContractAddedEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesOptionContractAddedEvent>()!),
                _ when eventVerb == FuturesOptionContractAddedCompleteEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesOptionContractAddedCompleteEvent>()!),
                _ when eventVerb == FuturesOptionContractAddedFailEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesOptionContractAddedFailEvent>()!),
                _ when eventVerb == FuturesOptionContractRemovedEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesOptionContractRemovedEvent>()!),
                _ when eventVerb == FuturesOptionContractRemovedCompleteEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesOptionContractRemovedCompleteEvent>()!),
                _ when eventVerb == FuturesOptionContractRemovedFailEvent.Verb => SetEvent(eventMsg.AsEvent<FuturesOptionContractRemovedFailEvent>()!),
                _ => default!
            };
            await ValueTask.CompletedTask;

            IEvent SetEvent(IEvent @event)
            {
                switch (@event)
                {
                    case FuturesOptionContractAddedEvent e:
                        futuresOptionContractAddedEvent = e;
                        break;
                    case FuturesOptionContractAddedCompleteEvent e:
                        futuresOptionContractAddedCompleteEvent = e;
                        break;
                    case FuturesOptionContractAddedFailEvent e:
                        futuresOptionContractAddedFailEvent = e;
                        break;
                    case FuturesOptionContractRemovedEvent e:
                        futuresOptionContractRemovedEvent = e;
                        break;
                    case FuturesOptionContractRemovedCompleteEvent e:
                        futuresOptionContractRemovedCompleteEvent = e;
                        break;
                    case FuturesOptionContractRemovedFailEvent e:
                        futuresOptionContractRemovedFailEvent = e;
                        break;
                }
                return @event;
            }
        }
    }
}
