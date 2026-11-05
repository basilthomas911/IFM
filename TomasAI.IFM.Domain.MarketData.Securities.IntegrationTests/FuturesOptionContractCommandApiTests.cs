using TomasAI.IFM.Framework.Messaging.Nats;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.MarketData.Shared;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Actor.IntegrationTests;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Application.Api.Nats.Client;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Securities.IntegrationTests;

/// <summary>
/// Provides integration tests for futures option contract command operations, including adding futures option contracts.
/// Validates the behavior of command APIs and their event-driven workflows in a controlled test environment.
/// </summary>
/// <param name="factory">The web application factory used to create test HTTP clients for simulating API requests.</param>
/// <param name="dbFixture">The database fixture that provides access to test database instances and utilities for securities-related data setup and cleanup.</param>
public class FuturesOptionContractCommandApiTests(TomasAI.IFM.IntegrationTesting.KestrelWebApplicationFactory<TomasAI.IFM.Application.Api.Server.ApiServerEntryPoint> factory, SecuritiesDatabaseFixture dbFixture)
    : IClassFixture<TomasAI.IFM.IntegrationTesting.KestrelWebApplicationFactory<TomasAI.IFM.Application.Api.Server.ApiServerEntryPoint>>, IClassFixture<SecuritiesDatabaseFixture>
{
    static readonly TimeSpan EventTimeout = TimeSpan.FromSeconds(30);
    readonly IActorProducer _actorProducer = factory.Services.GetRequiredService<IActorProducer>();
    readonly ILogger<NatsActorEventListener> _logger = Substitute.For<ILogger<NatsActorEventListener>>();

    [Fact]
    public async Task AddFuturesOptionContract_Ok()
    {
        // arrange...
        var eventListener = new NatsActorEventListener(new NatsEventListenerOptions(), _logger);
        FuturesOptionContractAddedEvent futuresOptionContractAddedEvent = default!;
        FuturesOptionContractAddedCompleteEvent futuresOptionContractAddedCompleteEvent = default!;
        FuturesOptionContractAddedFailEvent futuresOptionContractAddedFailEvent = default!;
        var addCompleted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        await eventListener.StartAsync(
            "TestEventListener",
            new()
            {
                [new ActorMailboxId(ActorType.Event, FuturesOptionContractAddedEvent.Actor)] =
                [
                    FuturesOptionContractAddedEvent.Verb,
                    FuturesOptionContractAddedCompleteEvent.Verb,
                    FuturesOptionContractAddedFailEvent.Verb
                ]
            },
            EventHandlerAsync
        );

        var sample = SampleData.NewFuturesOptionContract;
        var futuresOptionContract = new FuturesOptionContractReadModel(
            $"{sample.Symbol}{sample.ContractMonth:yyyyMMdd}C5005", sample.Description, sample.Symbol,
            FuturesOptionContractReadModel.GetContractLocalSymbol(
                FuturesOptionContractReadModel.GetLocalSymbol(sample.Symbol, sample.ContractMonth), "Call", 5005),
            sample.SecurityType, sample.Currency, sample.Exchange, sample.Multiplier, sample.ContractMonth, 5005, "Call");

        // act...
        var marketDataApi = new MarketDataCommandApi(_actorProducer);
        var response = await marketDataApi.AddFuturesOptionContractAsync(futuresOptionContract, overwrite: false);

        response.Should().NotBeNull();
        response.Success.Should().BeTrue(response.ErrorMessage);
        response.Value.Should().NotBe(Guid.Empty);
        await addCompleted.Task.WaitAsync(EventTimeout);
        var preparationStream = await dbFixture.ActorEventSourceDb.GetEventStreamIdFromDbAsync(
            $"Function.OptionContractPreparationFunction.{response.Value:N}");
        preparationStream.Should().NotBeNull();

        // assert...

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
                        addCompleted.TrySetResult(true);
                        break;
                    case FuturesOptionContractAddedFailEvent e:
                        futuresOptionContractAddedFailEvent = e;
                        addCompleted.TrySetResult(true);
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
        var addCompleted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        await eventListener.StartAsync(
            "TestEventListener",
            new()
            {
                [new ActorMailboxId(ActorType.Event, FuturesOptionContractsAddedEvent.Actor)] =
                [
                    FuturesOptionContractsAddedEvent.Verb,
                    FuturesOptionContractsAddedCompleteEvent.Verb,
                    FuturesOptionContractsAddedFailEvent.Verb
                ]
            },
            EventHandlerAsync
        );

        var futuresOptionContracts = SampleData.NewFuturesOptionContracts;

        var year = futuresOptionContracts[0].ContractMonth.Year;
        var entityId = new FuturesOptionContractsEntityId(year);
        var subject = new ActorSubject(ActorType.Command, AddFuturesOptionContractsCommand.Actor, AddFuturesOptionContractsCommand.Verb, entityId.Format());
        dbFixture.BlackboardService.EventSourcing.EventStreamId.Remove($"{subject.ThreadId}");
        var eventStreamId = await dbFixture.ActorEventSourceDb.GetEventStreamIdAsync($"{subject.ThreadId}");
        if (eventStreamId > 0)
            await dbFixture.ActorEventSourceDb.DeleteEventLogByStreamIdAsync(eventStreamId);

        // Clean up any existing read-model data for all contracts
        foreach (var contract in futuresOptionContracts)
        {
            await dbFixture.Db.DeleteFuturesOptionContractAsync(contract.ContractId);
        }

        // act...
        var marketDataApi = new MarketDataCommandApi(_actorProducer);
        var response = await marketDataApi.AddFuturesOptionContractsAsync(year, futuresOptionContracts);

        response.Should().NotBeNull();
        response.Success.Should().BeTrue(response.ErrorMessage);
        response.Value.Should().NotBe(Guid.Empty);
        await addCompleted.Task.WaitAsync(EventTimeout);

        // assert...

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

        // The singular route must load membership written by the bulk route.
        var removed = await marketDataApi.RemoveFuturesOptionContractAsync(futuresOptionContracts[0].ContractId, overwrite: false);
        removed.Success.Should().BeTrue(removed.ErrorMessage);
        (await dbFixture.Db.GetFuturesOptionContractAsync(futuresOptionContracts[0].ContractId)).Should().BeNull();
        (await dbFixture.Db.GetFuturesOptionContractAsync(futuresOptionContracts[1].ContractId)).Should().NotBeNull();

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
                        addCompleted.TrySetResult(true);
                        break;
                    case FuturesOptionContractsAddedFailEvent e:
                        futuresOptionContractsAddedFailEvent = e;
                        addCompleted.TrySetResult(true);
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
        var addCompleted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var changeCompleted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        await eventListener.StartAsync(
            "TestEventListener",
            new()
            {
                [new ActorMailboxId(ActorType.Event, FuturesOptionContractAddedEvent.Actor)] =
                [
                    FuturesOptionContractAddedEvent.Verb,
                    FuturesOptionContractAddedCompleteEvent.Verb,
                    FuturesOptionContractAddedFailEvent.Verb,
                    FuturesOptionContractChangedEvent.Verb,
                    FuturesOptionContractChangedCompleteEvent.Verb,
                    FuturesOptionContractChangedFailEvent.Verb
                ]
            },
            EventHandlerAsync
        );

        var futuresOptionContract = SampleData.NewFuturesOptionContract;
        var changedContract = SampleData.ChangedFuturesOptionContract;

        // act - add futures option contract first...
        var marketDataApi = new MarketDataCommandApi(_actorProducer);
        var addResponse = await marketDataApi.AddFuturesOptionContractAsync(futuresOptionContract, overwrite: false);

        addResponse.Should().NotBeNull();
        addResponse.Success.Should().BeTrue(addResponse.ErrorMessage);
        addResponse.Value.Should().NotBe(Guid.Empty);
        await addCompleted.Task.WaitAsync(EventTimeout);

        // assert - verify add was successful...

        var savedContract = await dbFixture.Db.GetFuturesOptionContractAsync(futuresOptionContract.ContractId);
        savedContract.Should().NotBeNull();
        savedContract!.ContractId.Should().Be(futuresOptionContract.ContractId);
        savedContract.Symbol.Should().Be(futuresOptionContract.Symbol);
        savedContract.Description.Should().Be(futuresOptionContract.Description);

        // act - change futures option contract...
        var changeResponse = await marketDataApi.ChangeFuturesOptionContractAsync(futuresOptionContract.ContractId, changedContract, overwrite: true);

        changeResponse.Should().NotBeNull();
        changeResponse.Success.Should().BeTrue(changeResponse.ErrorMessage);
        changeResponse.Value.Should().NotBe(Guid.Empty);
        await changeCompleted.Task.WaitAsync(EventTimeout);

        // assert - verify change was successful...

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
                        addCompleted.TrySetResult(true);
                        break;
                    case FuturesOptionContractAddedFailEvent e:
                        futuresOptionContractAddedFailEvent = e;
                        addCompleted.TrySetResult(true);
                        break;
                    case FuturesOptionContractChangedEvent e:
                        futuresOptionContractChangedEvent = e;
                        break;
                    case FuturesOptionContractChangedCompleteEvent e:
                        futuresOptionContractChangedCompleteEvent = e;
                        changeCompleted.TrySetResult(true);
                        break;
                    case FuturesOptionContractChangedFailEvent e:
                        futuresOptionContractChangedFailEvent = e;
                        changeCompleted.TrySetResult(true);
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
        var addCompleted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var removeCompleted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        await eventListener.StartAsync(
            "TestEventListener",
            new()
            {
                [new ActorMailboxId(ActorType.Event, FuturesOptionContractAddedEvent.Actor)] =
                [
                    FuturesOptionContractAddedEvent.Verb,
                    FuturesOptionContractAddedCompleteEvent.Verb,
                    FuturesOptionContractAddedFailEvent.Verb,
                    FuturesOptionContractRemovedEvent.Verb,
                    FuturesOptionContractRemovedCompleteEvent.Verb,
                    FuturesOptionContractRemovedFailEvent.Verb
                ],
            },
            EventHandlerAsync
        );

        var futuresOptionContract = SampleData.NewFuturesOptionContract;
        futuresOptionContract = futuresOptionContract with
        {
            ContractId = $"{futuresOptionContract.Symbol}{futuresOptionContract.ContractMonth:yyyyMMdd}C5010",
            InstrumentId = 5010,
            StrikePrice = 5010,
            LocalSymbol = FuturesOptionContractReadModel.GetContractLocalSymbol(
                FuturesOptionContractReadModel.GetLocalSymbol(futuresOptionContract.Symbol, futuresOptionContract.ContractMonth),
                "Call", 5010)
        };

        // act - add futures option contract first...
        var marketDataApi = new MarketDataCommandApi(_actorProducer);
        var addResponse = await marketDataApi.AddFuturesOptionContractAsync(futuresOptionContract, overwrite: false);

        addResponse.Should().NotBeNull();
        addResponse.Success.Should().BeTrue(addResponse.ErrorMessage);
        addResponse.Value.Should().NotBe(Guid.Empty);
        await addCompleted.Task.WaitAsync(EventTimeout);

        // assert - verify add was successful...

        var savedContract = await dbFixture.Db.GetFuturesOptionContractAsync(futuresOptionContract.ContractId);
        savedContract.Should().NotBeNull();
        savedContract!.ContractId.Should().Be(futuresOptionContract.ContractId);
        savedContract.Symbol.Should().Be(futuresOptionContract.Symbol);
        savedContract.Description.Should().Be(futuresOptionContract.Description);

        // act - remove futures option contract...
        var removeResponse = await marketDataApi.RemoveFuturesOptionContractAsync(futuresOptionContract.ContractId, overwrite: true);

        removeResponse.Should().NotBeNull();
        removeResponse.Success.Should().BeTrue(removeResponse.ErrorMessage);
        removeResponse.Value.Should().NotBe(Guid.Empty);
        await removeCompleted.Task.WaitAsync(EventTimeout);

        // assert - verify remove was successful...

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
                        addCompleted.TrySetResult(true);
                        break;
                    case FuturesOptionContractAddedFailEvent e:
                        futuresOptionContractAddedFailEvent = e;
                        addCompleted.TrySetResult(true);
                        break;
                    case FuturesOptionContractRemovedEvent e:
                        futuresOptionContractRemovedEvent = e;
                        break;
                    case FuturesOptionContractRemovedCompleteEvent e:
                        futuresOptionContractRemovedCompleteEvent = e;
                        removeCompleted.TrySetResult(true);
                        break;
                    case FuturesOptionContractRemovedFailEvent e:
                        futuresOptionContractRemovedFailEvent = e;
                        removeCompleted.TrySetResult(true);
                        break;
                }
                return @event;
            }
        }
    }
}
