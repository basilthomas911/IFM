using TomasAI.IFM.Domain.MarketData.Securities.Persistence;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.Messaging.Nats;
using TomasAI.IFM.Domain.MarketData.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Shared.Events;
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

namespace TomasAI.IFM.Domain.MarketData.Securities.IntegrationTests;

/// <summary>
/// Provides integration tests for securities-related command operations, including adding, changing, and removing
/// futures contracts. Validates the behavior of command APIs and their event-driven workflows in a controlled 
/// test environment.
/// </summary>
/// <param name="factory">The web application factory used to create test HTTP clients for simulating API requests.</param>
/// <param name="dbFixture">The database fixture that provides access to test database instances and utilities for securities-related data setup and cleanup.</param>
public class FuturesContractFuturesContractCommandApiTests(TomasAI.IFM.IntegrationTesting.KestrelWebApplicationFactory<TomasAI.IFM.Application.Api.Server.ApiServerEntryPoint> factory, SecuritiesDatabaseFixture dbFixture)
    : IClassFixture<TomasAI.IFM.IntegrationTesting.KestrelWebApplicationFactory<TomasAI.IFM.Application.Api.Server.ApiServerEntryPoint>>, IClassFixture<SecuritiesDatabaseFixture>
{
    static readonly TimeSpan EventTimeout = TimeSpan.FromSeconds(30);
    readonly IActorProducer _actorProducer = factory.Services.GetRequiredService<IActorProducer>();
    readonly ILogger<NatsActorEventListener> _logger = Substitute.For<ILogger<NatsActorEventListener>>();

    [Fact]
    public async Task Rollover_commits_through_actor_and_rejects_a_stale_preparation()
    {
        using var timeout = new CancellationTokenSource(EventTimeout);
        var token = timeout.Token;
        var store = new ActorFuturesRolloverStore(dbFixture.Db, _actorProducer);
        var expiry = new DateOnly(2034, 3, 17);
        var contract = SampleData.NewFuturesContract with
        {
            ContractId = "RTY20340317", Symbol = "RTY", LocalSymbol = "RTYH4",
            LastTradeDate = expiry, OnTheRun = true, Rollover = true
        };
        var now = new DateTime(2033, 12, 1, 0, 0, 0, DateTimeKind.Utc);
        var pointer = new FuturesContractRolloverReadModel
        {
            Symbol = contract.Symbol, ContractId = contract.ContractId, NextRolloverDate = expiry,
            CreatedOn = now, CreatedBy = nameof(Rollover_commits_through_actor_and_rejects_a_stale_preparation),
            UpdatedOn = now, UpdatedBy = "test"
        };
        var before = await dbFixture.Db.ReadCatalogRevisionAsync(SecuritiesCatalogStreams.Futures, token);
        await store.ReplaceFuturesRolloverSetAsync(pointer, [contract], token);
        var committed = await dbFixture.Db.ReadCatalogRevisionAsync(SecuritiesCatalogStreams.Futures, token);
        Assert.True(committed > before);
        Assert.Equal(contract, Assert.Single(await store.GetFuturesRolloverSetAsync("RTY", token)));
        Assert.Equal(contract.ContractId, (await store.GetFuturesContractRolloverAsync("RTY", token))?.ContractId);
        Assert.Equal(contract.ContractId, (await store.GetFuturesContractRolloverAsync(" rty ", token))?.ContractId);
        await store.ReplaceFuturesRolloverSetAsync(pointer, [contract], token);
        Assert.Equal(committed, await dbFixture.Db.ReadCatalogRevisionAsync(SecuritiesCatalogStreams.Futures, token));

        var stale = new ReplaceFuturesRolloverSetCommand
        {
            CommandId = Guid.NewGuid(), EntityId = contract.Id, ExpectedCatalogRevision = before,
            Subject = new(ActorType.Command, AddFuturesContractCommand.Actor, ReplaceFuturesRolloverSetCommand.Verb, contract.Id.Format()),
            Rollover = pointer, ChangedContracts = [contract], PostEvents = true
        };
        var rejected = await _actorProducer.RequestAsync<ReplaceFuturesRolloverSetCommand, FuturesContractId, GuidResult>(
            stale.Subject, stale, stale.EntityId, token);
        Assert.False(rejected.Success);
        Assert.Equal(ReplaceFuturesRolloverSetCommand.CatalogChanged, rejected.ErrorCode);
        Assert.Equal(committed, await dbFixture.Db.ReadCatalogRevisionAsync(SecuritiesCatalogStreams.Futures, token));
    }

    [Fact]
    public async Task AddFuturesContract_Ok()
    {
        // arrange...
        var eventListener = new NatsActorEventListener(new NatsEventListenerOptions(), _logger);
        FuturesContractAddedEvent futuresContractAddedEvent = default!;
        FuturesContractAddedCompleteEvent futuresContractAddedCompleteEvent = default!;
        FuturesContractAddedFailEvent futuresContractAddedFailEvent = default!;
        var addCompleted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        await eventListener.StartAsync(
            "TestEventListener",
            new()
            {
                [new ActorMailboxId(ActorType.Event, FuturesContractAddedEvent.Actor)] =
                [
                    FuturesContractAddedEvent.Verb,
                    FuturesContractAddedCompleteEvent.Verb,
                    FuturesContractAddedFailEvent.Verb
                ]
            },
            EventHandlerAsync
        );

        var futuresContract = SampleData.NewFuturesContract;
        var maturity = futuresContract.LastTradeDate.AddDays(1);
        futuresContract = futuresContract with { ContractId = $"{futuresContract.Symbol}{maturity:yyyyMMdd}", LastTradeDate = maturity };

        // act...
        var marketDataApi = new MarketDataCommandApi(_actorProducer);
        var response = await marketDataApi.AddFuturesContractAsync(futuresContract, overwrite: false);

        response.Should().NotBeNull();
        response.Success.Should().BeTrue(response.ErrorMessage);
        response.Value.Should().NotBe(Guid.Empty);
        await addCompleted.Task.WaitAsync(EventTimeout);

        // assert...
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
        savedContract.OnTheRun.Should().Be(futuresContract.OnTheRun);

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
                        addCompleted.TrySetResult(true);
                        break;
                    case FuturesContractAddedFailEvent e:
                        futuresContractAddedFailEvent = e;
                        addCompleted.TrySetResult(true);
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
        var addCompleted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var changeCompleted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        await eventListener.StartAsync(
            "TestEventListener",
            new()
            {
                [new ActorMailboxId(ActorType.Event, FuturesContractAddedEvent.Actor)] =
                [
                    FuturesContractAddedEvent.Verb,
                            FuturesContractAddedCompleteEvent.Verb,
                            FuturesContractAddedFailEvent.Verb,
                            FuturesContractChangedEvent.Verb,
                            FuturesContractChangedCompleteEvent.Verb,
                            FuturesContractChangedFailEvent.Verb
                ]
            },
            EventHandlerAsync
        );

        var futuresContract = SampleData.NewFuturesContract;
        var changedContract = SampleData.ChangedFuturesContract;

        // act - add futures contract first...
        var marketDataApi = new MarketDataCommandApi(_actorProducer);
        var addResponse = await marketDataApi.AddFuturesContractAsync(futuresContract, overwrite: false);

        addResponse.Should().NotBeNull();
        addResponse.Success.Should().BeTrue(addResponse.ErrorMessage);
        addResponse.Value.Should().NotBe(Guid.Empty);
        await addCompleted.Task.WaitAsync(EventTimeout);

        // assert - verify add was successful...
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

        changeResponse.Should().NotBeNull();
        changeResponse.Success.Should().BeTrue(changeResponse.ErrorMessage);
        changeResponse.Value.Should().NotBe(Guid.Empty);
        await changeCompleted.Task.WaitAsync(EventTimeout);

        // assert - verify change was successful...
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
        updatedContract.OnTheRun.Should().Be(changedContract.OnTheRun);

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
                        addCompleted.TrySetResult(true);
                        break;
                    case FuturesContractAddedFailEvent e:
                        futuresContractAddedFailEvent = e;
                        addCompleted.TrySetResult(true);
                        break;
                    case FuturesContractChangedEvent e:
                        futuresContractChangedEvent = e;
                        break;
                    case FuturesContractChangedCompleteEvent e:
                        futuresContractChangedCompleteEvent = e;
                        changeCompleted.TrySetResult(true);
                        break;
                    case FuturesContractChangedFailEvent e:
                        futuresContractChangedFailEvent = e;
                        changeCompleted.TrySetResult(true);
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
        var addCompleted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var removeCompleted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        await eventListener.StartAsync(
            "TestEventListener",
            new()
            {
                [new ActorMailboxId(ActorType.Event, FuturesContractAddedEvent.Actor)] =
                [
                    FuturesContractAddedEvent.Verb,
                    FuturesContractAddedCompleteEvent.Verb,
                    FuturesContractAddedFailEvent.Verb,
                    FuturesContractRemovedEvent.Verb,
                    FuturesContractRemovedCompleteEvent.Verb,
                    FuturesContractRemovedFailEvent.Verb
                ],
            },
            EventHandlerAsync
        );

        var futuresContract = SampleData.NewFuturesContract;
        var maturity = futuresContract.LastTradeDate.AddDays(2);
        futuresContract = futuresContract with { ContractId = $"{futuresContract.Symbol}{maturity:yyyyMMdd}", LastTradeDate = maturity };

        // act - add futures contract first...
        var marketDataApi = new MarketDataCommandApi(_actorProducer);
        var addResponse = await marketDataApi.AddFuturesContractAsync(futuresContract, overwrite: false);

        addResponse.Should().NotBeNull();
        addResponse.Success.Should().BeTrue(addResponse.ErrorMessage);
        addResponse.Value.Should().NotBe(Guid.Empty);
        await addCompleted.Task.WaitAsync(EventTimeout);

        // assert - verify add was successful...
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

        removeResponse.Should().NotBeNull();
        removeResponse.Success.Should().BeTrue(removeResponse.ErrorMessage);
        removeResponse.Value.Should().NotBe(Guid.Empty);
        await removeCompleted.Task.WaitAsync(EventTimeout);

        // assert - verify remove was successful...
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
                        addCompleted.TrySetResult(true);
                        break;
                    case FuturesContractAddedFailEvent e:
                        futuresContractAddedFailEvent = e;
                        addCompleted.TrySetResult(true);
                        break;
                    case FuturesContractRemovedEvent e:
                        futuresContractRemovedEvent = e;
                        break;
                    case FuturesContractRemovedCompleteEvent e:
                        futuresContractRemovedCompleteEvent = e;
                        removeCompleted.TrySetResult(true);
                        break;
                    case FuturesContractRemovedFailEvent e:
                        futuresContractRemovedFailEvent = e;
                        removeCompleted.TrySetResult(true);
                        break;
                }
                return @event;
            }
        }
    }
}
