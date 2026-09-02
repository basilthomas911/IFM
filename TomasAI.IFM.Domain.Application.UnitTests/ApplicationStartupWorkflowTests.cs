using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Domain.Application.Actor.Event;
using TomasAI.IFM.Domain.Application.Actor.Event.Actor;
using TomasAI.IFM.Domain.Application.Shared;
using TomasAI.IFM.Domain.Application.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;

namespace TomasAI.IFM.Domain.Application.Actor.UnitTests;

public sealed class ApplicationStartupWorkflowTests
{
    static readonly DateOnly ValueDate = new(2026, 9, 2);

    [Fact]
    public async Task Startup_executes_every_activity_in_strict_order_and_completes()
    {
        var activities = new RecordingActivities();
        var context = new TestContext(activities);

        await Event().ExecuteAsync(context, CancellationToken.None);

        Assert.Equal(Enum.GetValues<ApplicationStartupActivity>(), activities.Executed);
        Assert.Equal(ApplicationLifecycleState.Running, context.StartupStatusStore.Current.State);
        Assert.Equal(7, context.StartupStatusStore.Current.Activities.Length);
        Assert.Single(context.SentEvents, value => value is ApplicationStartupCompleteEvent);
    }

    [Fact]
    public async Task Required_failure_skips_dependents_but_later_independent_qualification_runs()
    {
        var activities = new RecordingActivities
        {
            Failure = ApplicationStartupActivity.ReconcileCurrentContracts
        };
        var context = new TestContext(activities);

        await Event().ExecuteAsync(context, CancellationToken.None);

        Assert.Equal(
            [
                ApplicationStartupActivity.ResolveAuthority,
                ApplicationStartupActivity.ReconcileReferenceData,
                ApplicationStartupActivity.ReconcileCurrentContracts,
                ApplicationStartupActivity.QualifyOperationalState
            ],
            activities.Executed);
        var status = context.StartupStatusStore.Current;
        Assert.Equal(ApplicationLifecycleState.Failed, status.State);
        Assert.Equal(
            ApplicationStartupActivityOutcome.SkippedDependency,
            status.Activities.Single(value => value.Activity == ApplicationStartupActivity.StartMarketData).Outcome);
        Assert.Equal(
            ApplicationStartupActivityOutcome.AlreadySatisfied,
            status.Activities.Single(value => value.Activity == ApplicationStartupActivity.QualifyOperationalState).Outcome);
        Assert.Single(context.SentEvents, value => value is ApplicationStartupFailEvent);
    }

    [Fact]
    public async Task Optional_failure_degrades_and_does_not_stop_required_work()
    {
        var activities = new RecordingActivities
        {
            Failure = ApplicationStartupActivity.ReconcileReferenceData
        };
        var context = new TestContext(activities);

        await Event().ExecuteAsync(context, CancellationToken.None);

        Assert.Equal(Enum.GetValues<ApplicationStartupActivity>(), activities.Executed);
        Assert.Equal(ApplicationLifecycleState.Degraded, context.StartupStatusStore.Current.State);
        Assert.Single(context.SentEvents, value => value is ApplicationStartupDegradedEvent);
    }

    [Fact]
    public async Task Repeated_same_date_command_does_not_repeat_side_effects()
    {
        var activities = new RecordingActivities();
        var context = new TestContext(activities);

        await Event().ExecuteAsync(context, CancellationToken.None);
        await Event().ExecuteAsync(context, CancellationToken.None);

        Assert.Equal(7, activities.Executed.Count);
        Assert.Equal(2, context.SentEvents.Count(value => value is ApplicationStartupCompleteEvent));
    }

    static ApplicationStartupEvent Event() => new()
    {
        Subject = new ActorSubject(
            ActorType.Event,
            ApplicationStartupEvent.Actor,
            ApplicationStartupEvent.Verb,
            ValueDate.ToString("yyyy-MM-dd")),
        EntityId = new(ValueDate),
        Id = Guid.NewGuid(),
        CommandId = Guid.NewGuid(),
        EventId = 1,
        AggregateId = "application-test",
        ReceivedOn = DateTime.UtcNow,
        CreatedOn = DateTime.UtcNow,
        CreatedBy = "test"
    };

    sealed class RecordingActivities : IApplicationStartupActivities
    {
        public List<ApplicationStartupActivity> Executed { get; } = [];
        public ApplicationStartupActivity? Failure { get; init; }

        ValueTask<ApplicationStartupActivityOutcome> Execute(
            ApplicationStartupActivity activity,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Executed.Add(activity);
            if (Failure == activity)
                throw new TestActivityException(activity.ToString());
            return ValueTask.FromResult(ApplicationStartupActivityOutcome.AlreadySatisfied);
        }

        public ValueTask<ApplicationStartupActivityOutcome> ResolveAuthorityAsync(ApplicationStartupContext context, CancellationToken cancellationToken) => Execute(ApplicationStartupActivity.ResolveAuthority, cancellationToken);
        public ValueTask<ApplicationStartupActivityOutcome> ReconcileReferenceDataAsync(ApplicationStartupContext context, CancellationToken cancellationToken) => Execute(ApplicationStartupActivity.ReconcileReferenceData, cancellationToken);
        public ValueTask<ApplicationStartupActivityOutcome> ReconcileCurrentContractsAsync(ApplicationStartupContext context, CancellationToken cancellationToken) => Execute(ApplicationStartupActivity.ReconcileCurrentContracts, cancellationToken);
        public ValueTask<ApplicationStartupActivityOutcome> StartMarketDataAsync(ApplicationStartupContext context, CancellationToken cancellationToken) => Execute(ApplicationStartupActivity.StartMarketData, cancellationToken);
        public ValueTask<ApplicationStartupActivityOutcome> WarmHistoricalAnalyticsAsync(ApplicationStartupContext context, CancellationToken cancellationToken) => Execute(ApplicationStartupActivity.WarmHistoricalAnalytics, cancellationToken);
        public ValueTask<ApplicationStartupActivityOutcome> StartRealtimeAnalyticsAsync(ApplicationStartupContext context, CancellationToken cancellationToken) => Execute(ApplicationStartupActivity.StartRealtimeAnalytics, cancellationToken);
        public ValueTask<ApplicationStartupActivityOutcome> QualifyOperationalStateAsync(ApplicationStartupContext context, CancellationToken cancellationToken) => Execute(ApplicationStartupActivity.QualifyOperationalState, cancellationToken);
    }

    sealed class TestActivityException(string message) : Exception(message);

    sealed class RecordingConsole : IStatusConsoleWriter
    {
        public List<string> Messages { get; } = [];
        public Task WriteConsoleAsync(LogSourceType logSourceType, string statusMsg)
        {
            Messages.Add(statusMsg);
            return Task.CompletedTask;
        }

        public Task WriteConsoleAsync(LogSourceType logSourceType, int errorCode, string errorMsg, string dataType = "", string data = "")
        {
            Messages.Add(errorMsg);
            return Task.CompletedTask;
        }
    }

    sealed class TestContext(IApplicationStartupActivities activities) : IApplicationEventContext
    {
        readonly Dictionary<ActorThreadId, ActorMessageInfo> messageInfo = [];
        public List<object> SentEvents { get; } = [];
        public ActorMailboxId ActorId { get; } = new(ActorType.Event, ApplicationEventActor.Actor);
        public IContainerInstance Container => null!;
        public bool IsReady => true;
        public IActorSupervisor Supervisor => null!;
        public ILogger<ApplicationEventActor> Logger { get; } = NullLogger<ApplicationEventActor>.Instance;
        public IApplicationStartupActivities StartupActivities { get; } = activities;
        public IApplicationStartupStatusStore StartupStatusStore { get; } = new ApplicationStartupStatusStore();
        public IStatusConsoleWriter StatusConsoleWriter { get; } = new RecordingConsole();
        public TimeProvider TimeProvider { get; } = TimeProvider.System;

        public bool SetMessageInfo(ActorThreadId threadId, ActorMessageInfo info)
        {
            messageInfo[threadId] = info;
            return true;
        }

        public ActorMessageInfo? GetMessageInfo(ActorThreadId threadId) =>
            messageInfo.TryGetValue(threadId, out var value) ? value : null;

        public ValueTask SendAsync<TEvent, TEntityId>(TEvent @event)
            where TEvent : class, IEvent<TEntityId>
            where TEntityId : IActorEntityId
        {
            SentEvents.Add(@event);
            return ValueTask.CompletedTask;
        }

        public ValueTask SendAsync<TCommand, TEntityId>(TCommand command, TEntityId entityId)
            where TCommand : class, ICommand<TEntityId>
            where TEntityId : IActorEntityId => throw new NotSupportedException();

        public ValueTask<ServiceResult<TResult>> RequestAsync<TResult, TQuery>(TQuery query)
            where TQuery : class, IQuery<TResult>
            where TResult : class => throw new NotSupportedException();

        public ValueTask<ServiceResult<GuidResult>> RequestAsync<TCommand, TEntityId>(TCommand command)
            where TCommand : class, ICommand<TEntityId>
            where TEntityId : IActorEntityId => throw new NotSupportedException();

        public ValueTask<ServiceResult<TResult>> RequestFunctionAsync<TCommand, TEntityId, TResult>(TCommand command, CancellationToken cancellationToken = default)
            where TCommand : class, ICommand<TEntityId>
            where TEntityId : IActorEntityId
            where TResult : class => throw new NotSupportedException();

        public void AddEventRouter(ActorTypeId fromActorTypeId, ActorMailboxId toMailboxId) { }
        public void RemoveEventRouter(ActorTypeId fromActorTypeId, ActorMailboxId toMailboxId) { }
        public void AddRealtimeRouter(ActorTypeId fromActorTypeId, ActorMailboxId toMailboxId) { }
        public void RemoveRealtimeRouter(ActorTypeId fromActorTypeId, ActorMailboxId toMailboxId) { }
    }
}
