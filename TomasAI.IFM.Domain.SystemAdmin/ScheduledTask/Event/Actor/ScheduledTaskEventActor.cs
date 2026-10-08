using System.Collections.Frozen;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Events;
using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Event;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
namespace TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Event.Actor;
/// <summary>Routes committed projection completions to the owning Quartz host through durable actor messages.</summary>
public sealed class ScheduledTaskEventActor(IEventActorContext<ScheduledTaskEventActor> context)
    : BaseEventActor<ScheduledTaskEventActor>(context, ((ScheduledTaskEventContext)context).Logger)
{
    public const string Actor = "ScheduledTaskEvent";
    private readonly ScheduledTaskEventContext _owner = (ScheduledTaskEventContext)context;
    private static readonly IReadOnlyDictionary<string, Func<IActorMessage, IEvent>> _parseMap = new Dictionary<string, Func<IActorMessage, IEvent>>(StringComparer.Ordinal)
    {
        [ScheduledTaskCreatedCompleteEvent.Verb] = message => message.AsEvent<ScheduledTaskCreatedCompleteEvent>()!,
        [ScheduledTaskCreatedFailEvent.Verb] = message => message.AsEvent<ScheduledTaskCreatedFailEvent>()!,
        [ScheduledTaskScheduleChangedCompleteEvent.Verb] = message => message.AsEvent<ScheduledTaskScheduleChangedCompleteEvent>()!,
        [ScheduledTaskScheduleChangedFailEvent.Verb] = message => message.AsEvent<ScheduledTaskScheduleChangedFailEvent>()!,
        [ScheduledTaskEnabledCompleteEvent.Verb] = message => message.AsEvent<ScheduledTaskEnabledCompleteEvent>()!,
        [ScheduledTaskEnabledFailEvent.Verb] = message => message.AsEvent<ScheduledTaskEnabledFailEvent>()!,
        [ScheduledTaskDisabledCompleteEvent.Verb] = message => message.AsEvent<ScheduledTaskDisabledCompleteEvent>()!,
        [ScheduledTaskDisabledFailEvent.Verb] = message => message.AsEvent<ScheduledTaskDisabledFailEvent>()!,
        [ScheduledTaskRemovedCompleteEvent.Verb] = message => message.AsEvent<ScheduledTaskRemovedCompleteEvent>()!,
        [ScheduledTaskRemovedFailEvent.Verb] = message => message.AsEvent<ScheduledTaskRemovedFailEvent>()!,
        [ScheduledTaskInstallationRecordedCompleteEvent.Verb] = message => message.AsEvent<ScheduledTaskInstallationRecordedCompleteEvent>()!,
        [ScheduledTaskInstallationRecordedFailEvent.Verb] = message => message.AsEvent<ScheduledTaskInstallationRecordedFailEvent>()!,
        [ScheduledTaskInstallationFailureRecordedCompleteEvent.Verb] = message => message.AsEvent<ScheduledTaskInstallationFailureRecordedCompleteEvent>()!,
        [ScheduledTaskInstallationFailureRecordedFailEvent.Verb] = message => message.AsEvent<ScheduledTaskInstallationFailureRecordedFailEvent>()!,
        [ScheduledTaskRunAdmittedCompleteEvent.Verb] = message => message.AsEvent<ScheduledTaskRunAdmittedCompleteEvent>()!,
        [ScheduledTaskRunAdmittedFailEvent.Verb] = message => message.AsEvent<ScheduledTaskRunAdmittedFailEvent>()!,
        [ScheduledTaskRunReleasedCompleteEvent.Verb] = message => message.AsEvent<ScheduledTaskRunReleasedCompleteEvent>()!,
        [ScheduledTaskRunReleasedFailEvent.Verb] = message => message.AsEvent<ScheduledTaskRunReleasedFailEvent>()!,
        [ScheduledTaskProjectRegisteredCompleteEvent.Verb] = message => message.AsEvent<ScheduledTaskProjectRegisteredCompleteEvent>()!,
        [ScheduledTaskProjectRegisteredFailEvent.Verb] = message => message.AsEvent<ScheduledTaskProjectRegisteredFailEvent>()!,
        [ScheduledTaskHostCapabilityRecordedCompleteEvent.Verb] = message => message.AsEvent<ScheduledTaskHostCapabilityRecordedCompleteEvent>()!,
        [ScheduledTaskHostCapabilityRecordedFailEvent.Verb] = message => message.AsEvent<ScheduledTaskHostCapabilityRecordedFailEvent>()!,
        [ScheduledTaskRunRequestedCompleteEvent.Verb] = message => message.AsEvent<ScheduledTaskRunRequestedCompleteEvent>()!,
        [ScheduledTaskRunRequestedFailEvent.Verb] = message => message.AsEvent<ScheduledTaskRunRequestedFailEvent>()!,
        [ScheduledTaskRunAdmissionRecordedCompleteEvent.Verb] = message => message.AsEvent<ScheduledTaskRunAdmissionRecordedCompleteEvent>()!,
        [ScheduledTaskRunAdmissionRecordedFailEvent.Verb] = message => message.AsEvent<ScheduledTaskRunAdmissionRecordedFailEvent>()!,
        [ScheduledTaskRunStartedCompleteEvent.Verb] = message => message.AsEvent<ScheduledTaskRunStartedCompleteEvent>()!,
        [ScheduledTaskRunStageRecordedCompleteEvent.Verb] = message => message.AsEvent<ScheduledTaskRunStageRecordedCompleteEvent>()!,
        [ScheduledTaskRunStartedFailEvent.Verb] = message => message.AsEvent<ScheduledTaskRunStartedFailEvent>()!,
        [ScheduledTaskRunStageRecordedFailEvent.Verb] = message => message.AsEvent<ScheduledTaskRunStageRecordedFailEvent>()!,
        [ScheduledTaskRunCompletedCompleteEvent.Verb] = message => message.AsEvent<ScheduledTaskRunCompletedCompleteEvent>()!,
        [ScheduledTaskRunCompletedFailEvent.Verb] = message => message.AsEvent<ScheduledTaskRunCompletedFailEvent>()!,
        [ScheduledTaskRunFailedCompleteEvent.Verb] = message => message.AsEvent<ScheduledTaskRunFailedCompleteEvent>()!,
        [ScheduledTaskRunFailedFailEvent.Verb] = message => message.AsEvent<ScheduledTaskRunFailedFailEvent>()!,
        [ScheduledTaskRunUncertainRecordedCompleteEvent.Verb] = message => message.AsEvent<ScheduledTaskRunUncertainRecordedCompleteEvent>()!,
        [ScheduledTaskRunUncertainRecordedFailEvent.Verb] = message => message.AsEvent<ScheduledTaskRunUncertainRecordedFailEvent>()!,
    }.ToFrozenDictionary();
    private static readonly IReadOnlyDictionary<Type, Func<IEvent, ScheduledTaskEventContext, ValueTask>> _receiveMap = new Dictionary<Type, Func<IEvent, ScheduledTaskEventContext, ValueTask>>
    {
        [typeof(ScheduledTaskCreatedCompleteEvent)] = (domainEvent, owner) => ((ScheduledTaskCreatedCompleteEvent)domainEvent).ExecuteAsync(owner),
        [typeof(ScheduledTaskCreatedFailEvent)] = (domainEvent, owner) => ((ScheduledTaskCreatedFailEvent)domainEvent).ExecuteAsync(owner),
        [typeof(ScheduledTaskScheduleChangedCompleteEvent)] = (domainEvent, owner) => ((ScheduledTaskScheduleChangedCompleteEvent)domainEvent).ExecuteAsync(owner),
        [typeof(ScheduledTaskScheduleChangedFailEvent)] = (domainEvent, owner) => ((ScheduledTaskScheduleChangedFailEvent)domainEvent).ExecuteAsync(owner),
        [typeof(ScheduledTaskEnabledCompleteEvent)] = (domainEvent, owner) => ((ScheduledTaskEnabledCompleteEvent)domainEvent).ExecuteAsync(owner),
        [typeof(ScheduledTaskEnabledFailEvent)] = (domainEvent, owner) => ((ScheduledTaskEnabledFailEvent)domainEvent).ExecuteAsync(owner),
        [typeof(ScheduledTaskDisabledCompleteEvent)] = (domainEvent, owner) => ((ScheduledTaskDisabledCompleteEvent)domainEvent).ExecuteAsync(owner),
        [typeof(ScheduledTaskDisabledFailEvent)] = (domainEvent, owner) => ((ScheduledTaskDisabledFailEvent)domainEvent).ExecuteAsync(owner),
        [typeof(ScheduledTaskRemovedCompleteEvent)] = (domainEvent, owner) => ((ScheduledTaskRemovedCompleteEvent)domainEvent).ExecuteAsync(owner),
        [typeof(ScheduledTaskRemovedFailEvent)] = (domainEvent, owner) => ((ScheduledTaskRemovedFailEvent)domainEvent).ExecuteAsync(owner),
        [typeof(ScheduledTaskInstallationRecordedCompleteEvent)] = (domainEvent, owner) => ((ScheduledTaskInstallationRecordedCompleteEvent)domainEvent).ExecuteAsync(owner),
        [typeof(ScheduledTaskInstallationRecordedFailEvent)] = (domainEvent, owner) => ((ScheduledTaskInstallationRecordedFailEvent)domainEvent).ExecuteAsync(owner),
        [typeof(ScheduledTaskInstallationFailureRecordedCompleteEvent)] = (domainEvent, owner) => ((ScheduledTaskInstallationFailureRecordedCompleteEvent)domainEvent).ExecuteAsync(owner),
        [typeof(ScheduledTaskInstallationFailureRecordedFailEvent)] = (domainEvent, owner) => ((ScheduledTaskInstallationFailureRecordedFailEvent)domainEvent).ExecuteAsync(owner),
        [typeof(ScheduledTaskRunAdmittedCompleteEvent)] = (domainEvent, owner) => ((ScheduledTaskRunAdmittedCompleteEvent)domainEvent).ExecuteAsync(owner),
        [typeof(ScheduledTaskRunAdmittedFailEvent)] = (domainEvent, owner) => ((ScheduledTaskRunAdmittedFailEvent)domainEvent).ExecuteAsync(owner),
        [typeof(ScheduledTaskRunReleasedCompleteEvent)] = (domainEvent, owner) => ((ScheduledTaskRunReleasedCompleteEvent)domainEvent).ExecuteAsync(owner),
        [typeof(ScheduledTaskRunReleasedFailEvent)] = (domainEvent, owner) => ((ScheduledTaskRunReleasedFailEvent)domainEvent).ExecuteAsync(owner),
        [typeof(ScheduledTaskProjectRegisteredCompleteEvent)] = (domainEvent, owner) => ((ScheduledTaskProjectRegisteredCompleteEvent)domainEvent).ExecuteAsync(owner),
        [typeof(ScheduledTaskProjectRegisteredFailEvent)] = (domainEvent, owner) => ((ScheduledTaskProjectRegisteredFailEvent)domainEvent).ExecuteAsync(owner),
        [typeof(ScheduledTaskHostCapabilityRecordedCompleteEvent)] = (domainEvent, owner) => ((ScheduledTaskHostCapabilityRecordedCompleteEvent)domainEvent).ExecuteAsync(owner),
        [typeof(ScheduledTaskHostCapabilityRecordedFailEvent)] = (domainEvent, owner) => ((ScheduledTaskHostCapabilityRecordedFailEvent)domainEvent).ExecuteAsync(owner),
        [typeof(ScheduledTaskRunRequestedCompleteEvent)] = (domainEvent, owner) => ((ScheduledTaskRunRequestedCompleteEvent)domainEvent).ExecuteAsync(owner),
        [typeof(ScheduledTaskRunRequestedFailEvent)] = (domainEvent, owner) => ((ScheduledTaskRunRequestedFailEvent)domainEvent).ExecuteAsync(owner),
        [typeof(ScheduledTaskRunAdmissionRecordedCompleteEvent)] = (domainEvent, owner) => ((ScheduledTaskRunAdmissionRecordedCompleteEvent)domainEvent).ExecuteAsync(owner),
        [typeof(ScheduledTaskRunAdmissionRecordedFailEvent)] = (domainEvent, owner) => ((ScheduledTaskRunAdmissionRecordedFailEvent)domainEvent).ExecuteAsync(owner),
        [typeof(ScheduledTaskRunStartedCompleteEvent)] = (domainEvent, owner) => ((ScheduledTaskRunStartedCompleteEvent)domainEvent).ExecuteAsync(owner),
        [typeof(ScheduledTaskRunStageRecordedCompleteEvent)] = (domainEvent, owner) => ((ScheduledTaskRunStageRecordedCompleteEvent)domainEvent).ExecuteAsync(owner),
        [typeof(ScheduledTaskRunStartedFailEvent)] = (domainEvent, owner) => ((ScheduledTaskRunStartedFailEvent)domainEvent).ExecuteAsync(owner),
        [typeof(ScheduledTaskRunStageRecordedFailEvent)] = (domainEvent, owner) => ((ScheduledTaskRunStageRecordedFailEvent)domainEvent).ExecuteAsync(owner),
        [typeof(ScheduledTaskRunCompletedCompleteEvent)] = (domainEvent, owner) => ((ScheduledTaskRunCompletedCompleteEvent)domainEvent).ExecuteAsync(owner),
        [typeof(ScheduledTaskRunCompletedFailEvent)] = (domainEvent, owner) => ((ScheduledTaskRunCompletedFailEvent)domainEvent).ExecuteAsync(owner),
        [typeof(ScheduledTaskRunFailedCompleteEvent)] = (domainEvent, owner) => ((ScheduledTaskRunFailedCompleteEvent)domainEvent).ExecuteAsync(owner),
        [typeof(ScheduledTaskRunFailedFailEvent)] = (domainEvent, owner) => ((ScheduledTaskRunFailedFailEvent)domainEvent).ExecuteAsync(owner),
        [typeof(ScheduledTaskRunUncertainRecordedCompleteEvent)] = (domainEvent, owner) => ((ScheduledTaskRunUncertainRecordedCompleteEvent)domainEvent).ExecuteAsync(owner),
        [typeof(ScheduledTaskRunUncertainRecordedFailEvent)] = (domainEvent, owner) => ((ScheduledTaskRunUncertainRecordedFailEvent)domainEvent).ExecuteAsync(owner),
    }.ToFrozenDictionary();
    /// <inheritdoc />
    protected override IEvent ParseMessage(IEventActorContext<ScheduledTaskEventActor> context, IActorMessage message) => ParseMappedEvent(context, message, _parseMap);
    /// <inheritdoc />
    protected override ValueTask ReceiveAsync(IEventActorContext<ScheduledTaskEventActor> context, IEvent domainEvent) => ResolveMappedEventHandler(domainEvent, _receiveMap)(domainEvent, _owner);
    /// <inheritdoc />
    protected override ValueTask OnExceptionAsync(IEventActorContext<ScheduledTaskEventActor> context, ActorThreadId threadId, IEvent domainEvent, Exception exception)
    {
        _owner.Logger.LogError(exception, "{Component}.{Method} Runtime dispatch failed for {EventName} {ThreadId}", nameof(ScheduledTaskEventActor), nameof(OnExceptionAsync), domainEvent.EventName, threadId);
        return ValueTask.FromException(exception);
    }
}
