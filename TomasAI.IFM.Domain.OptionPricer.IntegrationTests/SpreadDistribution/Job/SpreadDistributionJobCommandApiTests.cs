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
using TomasAI.IFM.Shared.OptionPricer;
using TomasAI.IFM.Shared.OptionPricer.Commands;
using TomasAI.IFM.Shared.OptionPricer.Events;

namespace TomasAI.IFM.Domain.OptionPricer.IntegrationTests.SpreadDistribution.Job;

/// <summary>
/// Provides integration tests for spread distribution job command operations, including submitting,
/// completing, failing, clearing, and deleting jobs. Each test verifies the command API response,
/// the emitted domain events via NATS, and the resulting database state.
/// </summary>
/// <param name="factory">The web application factory used to create test HTTP clients for simulating API requests.</param>
/// <param name="dbFixture">The database fixture that provides access to test database instances for option pricer data setup and cleanup.</param>
public class SpreadDistributionJobCommandApiTests(WebApplicationFactory<Program> factory, OptionPricerFixture dbFixture)
    : IClassFixture<WebApplicationFactory<Program>>, IClassFixture<OptionPricerFixture>
{
    readonly HttpClientTestFactory _httpClientFactory = new(factory);
    readonly IJsonSerializer _jsonSerializer = new NewtonSoftJsonSerializer();
    readonly ILogger<NatsActorEventListener> _logger = Substitute.For<ILogger<NatsActorEventListener>>();

    [Fact]
    public async Task SubmitSpreadDistributionJob_Ok()
    {
        // arrange...
        var eventListener = new NatsActorEventListener(new NatsEventListenerOptions(), _logger);
        SpreadDistributionJobSubmittedEvent submittedEvent = default!;
        SpreadDistributionJobSubmittedCompleteEvent submittedCompleteEvent = default!;
        SpreadDistributionJobSubmittedFailEvent submittedFailEvent = default!;

        await eventListener.StartAsync(
            "TestEventListener",
            new()
            {
                [new ActorMailboxId(ActorType.Event, SpreadDistributionJobSubmittedEvent.Actor)] = [
                    SpreadDistributionJobSubmittedEvent.Verb,
                    SpreadDistributionJobSubmittedCompleteEvent.Verb,
                    SpreadDistributionJobSubmittedFailEvent.Verb]
            },
            EventHandlerAsync
        );

        var job = SampleData.SpreadDistributionJob;
        var entityId = SampleData.SpreadDistributionJobEntityId;
        var subject = new ActorSubject(ActorType.Command, SubmitSpreadDistributionJobCommand.Actor, SubmitSpreadDistributionJobCommand.Verb, entityId.Format());
        var eventStreamId = await dbFixture.ActorEventSourceDb.GetEventStreamIdAsync($"{subject.ThreadId}");
        if (eventStreamId > 0)
            await dbFixture.ActorEventSourceDb.DeleteEventLogByStreamIdAsync(eventStreamId);
        await dbFixture.OptionPricerDb.DeleteSpreadDistributionJobsAsync(SampleData.OrderId, SampleData.TradeId);

        // act...
        _httpClientFactory.CreateClient();
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var optionPricerApi = new OptionPricerCommandApi(commandServiceApi);
        var response = await optionPricerApi.SubmitSpreadDistributionJobAsync(job);

        await Task.Delay(1000);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
        submittedEvent.Should().NotBeNull();
        submittedCompleteEvent.Should().NotBeNull();
        submittedFailEvent.Should().BeNull();

        var inProgressCount = await dbFixture.OptionPricerDb.GetSpreadDistributionJobInProgressCountAsync(SampleData.OrderId, SampleData.TradeId);
        inProgressCount.Should().BeGreaterThan(0);

        await eventListener.StopAsync();

        async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            IEvent receivedEvent = eventVerb switch
            {
                _ when eventVerb == SpreadDistributionJobSubmittedEvent.Verb => SetEvent(eventMsg.AsEvent<SpreadDistributionJobSubmittedEvent>()!),
                _ when eventVerb == SpreadDistributionJobSubmittedCompleteEvent.Verb => SetEvent(eventMsg.AsEvent<SpreadDistributionJobSubmittedCompleteEvent>()!),
                _ when eventVerb == SpreadDistributionJobSubmittedFailEvent.Verb => SetEvent(eventMsg.AsEvent<SpreadDistributionJobSubmittedFailEvent>()!),
                _ => default!
            };
            await ValueTask.CompletedTask;

            IEvent SetEvent(IEvent @event)
            {
                if (@event is SpreadDistributionJobSubmittedEvent submitted)
                    submittedEvent = submitted;
                if (@event is SpreadDistributionJobSubmittedCompleteEvent submittedComplete)
                    submittedCompleteEvent = submittedComplete;
                if (@event is SpreadDistributionJobSubmittedFailEvent submittedFail)
                    submittedFailEvent = submittedFail;
                return @event;
            }
        }
    }

    [Fact]
    public async Task CompleteSpreadDistributionJob_Ok()
    {
        // arrange...
        var eventListener = new NatsActorEventListener(new NatsEventListenerOptions(), _logger);
        SpreadDistributionJobStatusUpdatedEvent statusUpdatedEvent = default!;
        SpreadDistributionJobStatusUpdatedCompleteEvent statusUpdatedCompleteEvent = default!;
        SpreadDistributionJobStatusUpdatedFailEvent statusUpdatedFailEvent = default!;

        var job = SampleData.SpreadDistributionJob;
        var entityId = SampleData.SpreadDistributionJobEntityId;

        // clean up event streams...
        var submitSubject = new ActorSubject(ActorType.Command, SubmitSpreadDistributionJobCommand.Actor, SubmitSpreadDistributionJobCommand.Verb, entityId.Format());
        var submitStreamId = await dbFixture.ActorEventSourceDb.GetEventStreamIdAsync($"{submitSubject.ThreadId}");
        if (submitStreamId > 0)
            await dbFixture.ActorEventSourceDb.DeleteEventLogByStreamIdAsync(submitStreamId);

        var completeSubject = new ActorSubject(ActorType.Command, CompleteSpreadDistributionJobCommand.Actor, CompleteSpreadDistributionJobCommand.Verb, entityId.Format());
        var completeStreamId = await dbFixture.ActorEventSourceDb.GetEventStreamIdAsync($"{completeSubject.ThreadId}");
        if (completeStreamId > 0)
            await dbFixture.ActorEventSourceDb.DeleteEventLogByStreamIdAsync(completeStreamId);

        await dbFixture.OptionPricerDb.DeleteSpreadDistributionJobsAsync(SampleData.OrderId, SampleData.TradeId);

        // submit job first...
        _httpClientFactory.CreateClient();
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var optionPricerApi = new OptionPricerCommandApi(commandServiceApi);
        var submitResponse = await optionPricerApi.SubmitSpreadDistributionJobAsync(job);
        await Task.Delay(1000);
        submitResponse.Success.Should().BeTrue();

        await eventListener.StartAsync(
            "TestEventListener",
            new()
            {
                [new ActorMailboxId(ActorType.Event, SpreadDistributionJobStatusUpdatedEvent.Actor)] = [
                    SpreadDistributionJobStatusUpdatedEvent.Verb,
                    SpreadDistributionJobStatusUpdatedCompleteEvent.Verb,
                    SpreadDistributionJobStatusUpdatedFailEvent.Verb]
            },
            EventHandlerAsync
        );

        // act...
        var response = await optionPricerApi.CompleteSpreadDistributionJobAsync(
            entityId, DateTime.UtcNow, SpreadDistributionJobStatus.Completed);

        await Task.Delay(1000);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
        statusUpdatedEvent.Should().NotBeNull();
        statusUpdatedEvent.JobStatus.Should().Be(SpreadDistributionJobStatus.Completed);
        statusUpdatedCompleteEvent.Should().NotBeNull();
        statusUpdatedFailEvent.Should().BeNull();

        var inProgressCount = await dbFixture.OptionPricerDb.GetSpreadDistributionJobInProgressCountAsync(SampleData.OrderId, SampleData.TradeId);
        inProgressCount.Should().Be(0);

        await eventListener.StopAsync();

        async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            IEvent receivedEvent = eventVerb switch
            {
                _ when eventVerb == SpreadDistributionJobStatusUpdatedEvent.Verb => SetEvent(eventMsg.AsEvent<SpreadDistributionJobStatusUpdatedEvent>()!),
                _ when eventVerb == SpreadDistributionJobStatusUpdatedCompleteEvent.Verb => SetEvent(eventMsg.AsEvent<SpreadDistributionJobStatusUpdatedCompleteEvent>()!),
                _ when eventVerb == SpreadDistributionJobStatusUpdatedFailEvent.Verb => SetEvent(eventMsg.AsEvent<SpreadDistributionJobStatusUpdatedFailEvent>()!),
                _ => default!
            };
            await ValueTask.CompletedTask;

            IEvent SetEvent(IEvent @event)
            {
                if (@event is SpreadDistributionJobStatusUpdatedEvent updated)
                    statusUpdatedEvent = updated;
                if (@event is SpreadDistributionJobStatusUpdatedCompleteEvent updatedComplete)
                    statusUpdatedCompleteEvent = updatedComplete;
                if (@event is SpreadDistributionJobStatusUpdatedFailEvent updatedFail)
                    statusUpdatedFailEvent = updatedFail;
                return @event;
            }
        }
    }

    [Fact]
    public async Task FailSpreadDistributionJob_Ok()
    {
        // arrange...
        var eventListener = new NatsActorEventListener(new NatsEventListenerOptions(), _logger);
        SpreadDistributionJobStatusUpdatedEvent statusUpdatedEvent = default!;
        SpreadDistributionJobStatusUpdatedCompleteEvent statusUpdatedCompleteEvent = default!;
        SpreadDistributionJobStatusUpdatedFailEvent statusUpdatedFailEvent = default!;

        var job = SampleData.SpreadDistributionJob;
        var entityId = SampleData.SpreadDistributionJobEntityId;

        // clean up event streams...
        var submitSubject = new ActorSubject(ActorType.Command, SubmitSpreadDistributionJobCommand.Actor, SubmitSpreadDistributionJobCommand.Verb, entityId.Format());
        var submitStreamId = await dbFixture.ActorEventSourceDb.GetEventStreamIdAsync($"{submitSubject.ThreadId}");
        if (submitStreamId > 0)
            await dbFixture.ActorEventSourceDb.DeleteEventLogByStreamIdAsync(submitStreamId);

        var failSubject = new ActorSubject(ActorType.Command, FailSpreadDistributionJobCommand.Actor, FailSpreadDistributionJobCommand.Verb, entityId.Format());
        var failStreamId = await dbFixture.ActorEventSourceDb.GetEventStreamIdAsync($"{failSubject.ThreadId}");
        if (failStreamId > 0)
            await dbFixture.ActorEventSourceDb.DeleteEventLogByStreamIdAsync(failStreamId);

        await dbFixture.OptionPricerDb.DeleteSpreadDistributionJobsAsync(SampleData.OrderId, SampleData.TradeId);

        // submit job first...
        _httpClientFactory.CreateClient();
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var optionPricerApi = new OptionPricerCommandApi(commandServiceApi);
        var submitResponse = await optionPricerApi.SubmitSpreadDistributionJobAsync(job);
        await Task.Delay(1000);
        submitResponse.Success.Should().BeTrue();

        await eventListener.StartAsync(
            "TestEventListener",
            new()
            {
                [new ActorMailboxId(ActorType.Event, SpreadDistributionJobStatusUpdatedEvent.Actor)] = [
                    SpreadDistributionJobStatusUpdatedEvent.Verb,
                    SpreadDistributionJobStatusUpdatedCompleteEvent.Verb,
                    SpreadDistributionJobStatusUpdatedFailEvent.Verb]
            },
            EventHandlerAsync
        );

        // act...
        var response = await optionPricerApi.FailSpreadDistributionJobAsync(
            entityId, DateTime.UtcNow, SpreadDistributionJobStatus.Failed, "Test failure reason");

        await Task.Delay(1000);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
        statusUpdatedEvent.Should().NotBeNull();
        statusUpdatedEvent.JobStatus.Should().Be(SpreadDistributionJobStatus.Failed);
        statusUpdatedCompleteEvent.Should().NotBeNull();
        statusUpdatedFailEvent.Should().BeNull();

        var inProgressCount = await dbFixture.OptionPricerDb.GetSpreadDistributionJobInProgressCountAsync(SampleData.OrderId, SampleData.TradeId);
        inProgressCount.Should().Be(0);

        await eventListener.StopAsync();

        async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            IEvent receivedEvent = eventVerb switch
            {
                _ when eventVerb == SpreadDistributionJobStatusUpdatedEvent.Verb => SetEvent(eventMsg.AsEvent<SpreadDistributionJobStatusUpdatedEvent>()!),
                _ when eventVerb == SpreadDistributionJobStatusUpdatedCompleteEvent.Verb => SetEvent(eventMsg.AsEvent<SpreadDistributionJobStatusUpdatedCompleteEvent>()!),
                _ when eventVerb == SpreadDistributionJobStatusUpdatedFailEvent.Verb => SetEvent(eventMsg.AsEvent<SpreadDistributionJobStatusUpdatedFailEvent>()!),
                _ => default!
            };
            await ValueTask.CompletedTask;

            IEvent SetEvent(IEvent @event)
            {
                if (@event is SpreadDistributionJobStatusUpdatedEvent updated)
                    statusUpdatedEvent = updated;
                if (@event is SpreadDistributionJobStatusUpdatedCompleteEvent updatedComplete)
                    statusUpdatedCompleteEvent = updatedComplete;
                if (@event is SpreadDistributionJobStatusUpdatedFailEvent updatedFail)
                    statusUpdatedFailEvent = updatedFail;
                return @event;
            }
        }
    }

    [Fact]
    public async Task ClearSpreadDistributionJob_Ok()
    {
        // arrange...
        var eventListener = new NatsActorEventListener(new NatsEventListenerOptions(), _logger);
        SpreadDistributionJobStatusUpdatedEvent statusUpdatedEvent = default!;
        SpreadDistributionJobStatusUpdatedCompleteEvent statusUpdatedCompleteEvent = default!;
        SpreadDistributionJobStatusUpdatedFailEvent statusUpdatedFailEvent = default!;

        var job = SampleData.SpreadDistributionJob;
        var entityId = SampleData.SpreadDistributionJobEntityId;

        // clean up event streams...
        var submitSubject = new ActorSubject(ActorType.Command, SubmitSpreadDistributionJobCommand.Actor, SubmitSpreadDistributionJobCommand.Verb, entityId.Format());
        var submitStreamId = await dbFixture.ActorEventSourceDb.GetEventStreamIdAsync($"{submitSubject.ThreadId}");
        if (submitStreamId > 0)
            await dbFixture.ActorEventSourceDb.DeleteEventLogByStreamIdAsync(submitStreamId);

        var clearSubject = new ActorSubject(ActorType.Command, ClearSpreadDistributionJobCommand.Actor, ClearSpreadDistributionJobCommand.Verb, entityId.Format());
        var clearStreamId = await dbFixture.ActorEventSourceDb.GetEventStreamIdAsync($"{clearSubject.ThreadId}");
        if (clearStreamId > 0)
            await dbFixture.ActorEventSourceDb.DeleteEventLogByStreamIdAsync(clearStreamId);

        await dbFixture.OptionPricerDb.DeleteSpreadDistributionJobsAsync(SampleData.OrderId, SampleData.TradeId);

        // submit job first...
        _httpClientFactory.CreateClient();
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var optionPricerApi = new OptionPricerCommandApi(commandServiceApi);
        var submitResponse = await optionPricerApi.SubmitSpreadDistributionJobAsync(job);
        await Task.Delay(1000);
        submitResponse.Success.Should().BeTrue();

        await eventListener.StartAsync(
            "TestEventListener",
            new()
            {
                [new ActorMailboxId(ActorType.Event, SpreadDistributionJobStatusUpdatedEvent.Actor)] = [
                    SpreadDistributionJobStatusUpdatedEvent.Verb,
                    SpreadDistributionJobStatusUpdatedCompleteEvent.Verb,
                    SpreadDistributionJobStatusUpdatedFailEvent.Verb]
            },
            EventHandlerAsync
        );

        // act...
        var response = await optionPricerApi.ClearSpreadDistributionJobAsync(entityId);

        await Task.Delay(1000);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
        statusUpdatedEvent.Should().NotBeNull();
        statusUpdatedEvent.JobStatus.Should().Be(SpreadDistributionJobStatus.Cleared);
        statusUpdatedCompleteEvent.Should().NotBeNull();
        statusUpdatedFailEvent.Should().BeNull();

        var inProgressCount = await dbFixture.OptionPricerDb.GetSpreadDistributionJobInProgressCountAsync(SampleData.OrderId, SampleData.TradeId);
        inProgressCount.Should().Be(0);

        await eventListener.StopAsync();

        async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            IEvent receivedEvent = eventVerb switch
            {
                _ when eventVerb == SpreadDistributionJobStatusUpdatedEvent.Verb => SetEvent(eventMsg.AsEvent<SpreadDistributionJobStatusUpdatedEvent>()!),
                _ when eventVerb == SpreadDistributionJobStatusUpdatedCompleteEvent.Verb => SetEvent(eventMsg.AsEvent<SpreadDistributionJobStatusUpdatedCompleteEvent>()!),
                _ when eventVerb == SpreadDistributionJobStatusUpdatedFailEvent.Verb => SetEvent(eventMsg.AsEvent<SpreadDistributionJobStatusUpdatedFailEvent>()!),
                _ => default!
            };
            await ValueTask.CompletedTask;

            IEvent SetEvent(IEvent @event)
            {
                if (@event is SpreadDistributionJobStatusUpdatedEvent updated)
                    statusUpdatedEvent = updated;
                if (@event is SpreadDistributionJobStatusUpdatedCompleteEvent updatedComplete)
                    statusUpdatedCompleteEvent = updatedComplete;
                if (@event is SpreadDistributionJobStatusUpdatedFailEvent updatedFail)
                    statusUpdatedFailEvent = updatedFail;
                return @event;
            }
        }
    }

    [Fact]
    public async Task DeleteSpreadDistributionJobsInProgress_Ok()
    {
        // arrange...
        var eventListener = new NatsActorEventListener(new NatsEventListenerOptions(), _logger);
        SpreadDistributionJobsInProgressDeletedEvent deletedEvent = default!;
        SpreadDistributionJobsInProgressDeletedCompleteEvent deletedCompleteEvent = default!;
        SpreadDistributionJobsInProgressDeletedFailEvent deletedFailEvent = default!;

        var job = SampleData.SpreadDistributionJob;
        var entityId = SampleData.SpreadDistributionJobEntityId;

        // clean up event streams...
        var submitSubject = new ActorSubject(ActorType.Command, SubmitSpreadDistributionJobCommand.Actor, SubmitSpreadDistributionJobCommand.Verb, entityId.Format());
        var submitStreamId = await dbFixture.ActorEventSourceDb.GetEventStreamIdAsync($"{submitSubject.ThreadId}");
        if (submitStreamId > 0)
            await dbFixture.ActorEventSourceDb.DeleteEventLogByStreamIdAsync(submitStreamId);

        var deleteSubject = new ActorSubject(ActorType.Command, DeleteSpreadDistributionJobsInProgressCommand.Actor, DeleteSpreadDistributionJobsInProgressCommand.Verb, entityId.Format());
        var deleteStreamId = await dbFixture.ActorEventSourceDb.GetEventStreamIdAsync($"{deleteSubject.ThreadId}");
        if (deleteStreamId > 0)
            await dbFixture.ActorEventSourceDb.DeleteEventLogByStreamIdAsync(deleteStreamId);

        await dbFixture.OptionPricerDb.DeleteSpreadDistributionJobsAsync(SampleData.OrderId, SampleData.TradeId);

        // submit job first...
        _httpClientFactory.CreateClient();
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var optionPricerApi = new OptionPricerCommandApi(commandServiceApi);
        var submitResponse = await optionPricerApi.SubmitSpreadDistributionJobAsync(job);
        await Task.Delay(1000);
        submitResponse.Success.Should().BeTrue();

        await eventListener.StartAsync(
            "TestEventListener",
            new()
            {
                [new ActorMailboxId(ActorType.Event, SpreadDistributionJobsInProgressDeletedEvent.Actor)] = [
                    SpreadDistributionJobsInProgressDeletedEvent.Verb,
                    SpreadDistributionJobsInProgressDeletedCompleteEvent.Verb,
                    SpreadDistributionJobsInProgressDeletedFailEvent.Verb]
            },
            EventHandlerAsync
        );

        // act...
        var response = await optionPricerApi.DeleteSpreadDistributionJobsInProgressAsync(entityId);

        await Task.Delay(1000);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
        deletedEvent.Should().NotBeNull();
        deletedCompleteEvent.Should().NotBeNull();
        deletedFailEvent.Should().BeNull();

        var inProgressCount = await dbFixture.OptionPricerDb.GetSpreadDistributionJobInProgressCountAsync(SampleData.OrderId, SampleData.TradeId);
        inProgressCount.Should().Be(0);

        await eventListener.StopAsync();

        async ValueTask EventHandlerAsync(string eventVerb, NatsMsg<byte[]> eventMsg)
        {
            IEvent receivedEvent = eventVerb switch
            {
                _ when eventVerb == SpreadDistributionJobsInProgressDeletedEvent.Verb => SetEvent(eventMsg.AsEvent<SpreadDistributionJobsInProgressDeletedEvent>()!),
                _ when eventVerb == SpreadDistributionJobsInProgressDeletedCompleteEvent.Verb => SetEvent(eventMsg.AsEvent<SpreadDistributionJobsInProgressDeletedCompleteEvent>()!),
                _ when eventVerb == SpreadDistributionJobsInProgressDeletedFailEvent.Verb => SetEvent(eventMsg.AsEvent<SpreadDistributionJobsInProgressDeletedFailEvent>()!),
                _ => default!
            };
            await ValueTask.CompletedTask;

            IEvent SetEvent(IEvent @event)
            {
                if (@event is SpreadDistributionJobsInProgressDeletedEvent deleted)
                    deletedEvent = deleted;
                if (@event is SpreadDistributionJobsInProgressDeletedCompleteEvent deletedComplete)
                    deletedCompleteEvent = deletedComplete;
                if (@event is SpreadDistributionJobsInProgressDeletedFailEvent deletedFail)
                    deletedFailEvent = deletedFail;
                return @event;
            }
        }
    }
}
