using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Catalog.Event;
using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Run.Event;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Catalog.Events;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Run.Events;
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
public sealed class ScheduledTaskEventActor(IEventActorContext<ScheduledTaskEventActor> context, ILogger<ScheduledTaskEventActor> logger)
    : BaseEventActor<ScheduledTaskEventActor>(context, logger)
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
        [ScheduledTaskCatalogProjectRegisteredCompleteEvent.Verb] = message => message.AsEvent<ScheduledTaskCatalogProjectRegisteredCompleteEvent>()!,
        [ScheduledTaskCatalogProjectRegisteredFailEvent.Verb] = message => message.AsEvent<ScheduledTaskCatalogProjectRegisteredFailEvent>()!,
        [ScheduledTaskCatalogHostCapabilityRecordedCompleteEvent.Verb] = message => message.AsEvent<ScheduledTaskCatalogHostCapabilityRecordedCompleteEvent>()!,
        [ScheduledTaskCatalogHostCapabilityRecordedFailEvent.Verb] = message => message.AsEvent<ScheduledTaskCatalogHostCapabilityRecordedFailEvent>()!,
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
    private static readonly IReadOnlyDictionary<Type, Func<IEvent, ScheduledTaskEventContext, ILogger<ScheduledTaskEventActor>, ValueTask>> _receiveMap = new Dictionary<Type, Func<IEvent, ScheduledTaskEventContext, ILogger<ScheduledTaskEventActor>, ValueTask>>
    {
        [typeof(ScheduledTaskCreatedCompleteEvent)] = static (domainEvent, owner, logger) => ((ScheduledTaskCreatedCompleteEvent)domainEvent).ExecuteAsync(owner, logger),
        [typeof(ScheduledTaskCreatedFailEvent)] = static (domainEvent, owner, logger) => ((ScheduledTaskCreatedFailEvent)domainEvent).ExecuteAsync(owner, logger),
        [typeof(ScheduledTaskScheduleChangedCompleteEvent)] = static (domainEvent, owner, logger) => ((ScheduledTaskScheduleChangedCompleteEvent)domainEvent).ExecuteAsync(owner, logger),
        [typeof(ScheduledTaskScheduleChangedFailEvent)] = static (domainEvent, owner, logger) => ((ScheduledTaskScheduleChangedFailEvent)domainEvent).ExecuteAsync(owner, logger),
        [typeof(ScheduledTaskEnabledCompleteEvent)] = static (domainEvent, owner, logger) => ((ScheduledTaskEnabledCompleteEvent)domainEvent).ExecuteAsync(owner, logger),
        [typeof(ScheduledTaskEnabledFailEvent)] = static (domainEvent, owner, logger) => ((ScheduledTaskEnabledFailEvent)domainEvent).ExecuteAsync(owner, logger),
        [typeof(ScheduledTaskDisabledCompleteEvent)] = static (domainEvent, owner, logger) => ((ScheduledTaskDisabledCompleteEvent)domainEvent).ExecuteAsync(owner, logger),
        [typeof(ScheduledTaskDisabledFailEvent)] = static (domainEvent, owner, logger) => ((ScheduledTaskDisabledFailEvent)domainEvent).ExecuteAsync(owner, logger),
        [typeof(ScheduledTaskRemovedCompleteEvent)] = static (domainEvent, owner, logger) => ((ScheduledTaskRemovedCompleteEvent)domainEvent).ExecuteAsync(owner, logger),
        [typeof(ScheduledTaskRemovedFailEvent)] = static (domainEvent, owner, logger) => ((ScheduledTaskRemovedFailEvent)domainEvent).ExecuteAsync(owner, logger),
        [typeof(ScheduledTaskInstallationRecordedCompleteEvent)] = static (domainEvent, owner, logger) => ((ScheduledTaskInstallationRecordedCompleteEvent)domainEvent).ExecuteAsync(owner, logger),
        [typeof(ScheduledTaskInstallationRecordedFailEvent)] = static (domainEvent, owner, logger) => ((ScheduledTaskInstallationRecordedFailEvent)domainEvent).ExecuteAsync(owner, logger),
        [typeof(ScheduledTaskInstallationFailureRecordedCompleteEvent)] = static (domainEvent, owner, logger) => ((ScheduledTaskInstallationFailureRecordedCompleteEvent)domainEvent).ExecuteAsync(owner, logger),
        [typeof(ScheduledTaskInstallationFailureRecordedFailEvent)] = static (domainEvent, owner, logger) => ((ScheduledTaskInstallationFailureRecordedFailEvent)domainEvent).ExecuteAsync(owner, logger),
        [typeof(ScheduledTaskRunAdmittedCompleteEvent)] = static (domainEvent, owner, logger) => ((ScheduledTaskRunAdmittedCompleteEvent)domainEvent).ExecuteAsync(owner, logger),
        [typeof(ScheduledTaskRunAdmittedFailEvent)] = static (domainEvent, owner, logger) => ((ScheduledTaskRunAdmittedFailEvent)domainEvent).ExecuteAsync(owner, logger),
        [typeof(ScheduledTaskRunReleasedCompleteEvent)] = static (domainEvent, owner, logger) => ((ScheduledTaskRunReleasedCompleteEvent)domainEvent).ExecuteAsync(owner, logger),
        [typeof(ScheduledTaskRunReleasedFailEvent)] = static (domainEvent, owner, logger) => ((ScheduledTaskRunReleasedFailEvent)domainEvent).ExecuteAsync(owner, logger),
        [typeof(ScheduledTaskCatalogProjectRegisteredCompleteEvent)] = static (domainEvent, owner, logger) => ((ScheduledTaskCatalogProjectRegisteredCompleteEvent)domainEvent).ExecuteAsync(owner, logger),
        [typeof(ScheduledTaskCatalogProjectRegisteredFailEvent)] = static (domainEvent, owner, logger) => ((ScheduledTaskCatalogProjectRegisteredFailEvent)domainEvent).ExecuteAsync(owner, logger),
        [typeof(ScheduledTaskCatalogHostCapabilityRecordedCompleteEvent)] = static (domainEvent, owner, logger) => ((ScheduledTaskCatalogHostCapabilityRecordedCompleteEvent)domainEvent).ExecuteAsync(owner, logger),
        [typeof(ScheduledTaskCatalogHostCapabilityRecordedFailEvent)] = static (domainEvent, owner, logger) => ((ScheduledTaskCatalogHostCapabilityRecordedFailEvent)domainEvent).ExecuteAsync(owner, logger),
        [typeof(ScheduledTaskRunRequestedCompleteEvent)] = static (domainEvent, owner, logger) => ((ScheduledTaskRunRequestedCompleteEvent)domainEvent).ExecuteAsync(owner, logger),
        [typeof(ScheduledTaskRunRequestedFailEvent)] = static (domainEvent, owner, logger) => ((ScheduledTaskRunRequestedFailEvent)domainEvent).ExecuteAsync(owner, logger),
        [typeof(ScheduledTaskRunAdmissionRecordedCompleteEvent)] = static (domainEvent, owner, logger) => ((ScheduledTaskRunAdmissionRecordedCompleteEvent)domainEvent).ExecuteAsync(owner, logger),
        [typeof(ScheduledTaskRunAdmissionRecordedFailEvent)] = static (domainEvent, owner, logger) => ((ScheduledTaskRunAdmissionRecordedFailEvent)domainEvent).ExecuteAsync(owner, logger),
        [typeof(ScheduledTaskRunStartedCompleteEvent)] = static (domainEvent, owner, logger) => ((ScheduledTaskRunStartedCompleteEvent)domainEvent).ExecuteAsync(owner, logger),
        [typeof(ScheduledTaskRunStageRecordedCompleteEvent)] = static (domainEvent, owner, logger) => ((ScheduledTaskRunStageRecordedCompleteEvent)domainEvent).ExecuteAsync(owner, logger),
        [typeof(ScheduledTaskRunStartedFailEvent)] = static (domainEvent, owner, logger) => ((ScheduledTaskRunStartedFailEvent)domainEvent).ExecuteAsync(owner, logger),
        [typeof(ScheduledTaskRunStageRecordedFailEvent)] = static (domainEvent, owner, logger) => ((ScheduledTaskRunStageRecordedFailEvent)domainEvent).ExecuteAsync(owner, logger),
        [typeof(ScheduledTaskRunCompletedCompleteEvent)] = static (domainEvent, owner, logger) => ((ScheduledTaskRunCompletedCompleteEvent)domainEvent).ExecuteAsync(owner, logger),
        [typeof(ScheduledTaskRunCompletedFailEvent)] = static (domainEvent, owner, logger) => ((ScheduledTaskRunCompletedFailEvent)domainEvent).ExecuteAsync(owner, logger),
        [typeof(ScheduledTaskRunFailedCompleteEvent)] = static (domainEvent, owner, logger) => ((ScheduledTaskRunFailedCompleteEvent)domainEvent).ExecuteAsync(owner, logger),
        [typeof(ScheduledTaskRunFailedFailEvent)] = static (domainEvent, owner, logger) => ((ScheduledTaskRunFailedFailEvent)domainEvent).ExecuteAsync(owner, logger),
        [typeof(ScheduledTaskRunUncertainRecordedCompleteEvent)] = static (domainEvent, owner, logger) => ((ScheduledTaskRunUncertainRecordedCompleteEvent)domainEvent).ExecuteAsync(owner, logger),
        [typeof(ScheduledTaskRunUncertainRecordedFailEvent)] = static (domainEvent, owner, logger) => ((ScheduledTaskRunUncertainRecordedFailEvent)domainEvent).ExecuteAsync(owner, logger),
    }.ToFrozenDictionary();
    /// <inheritdoc />
    protected override IEvent ParseMessage(IEventActorContext<ScheduledTaskEventActor> context, IActorMessage message) => ParseMappedEvent(context, message, _parseMap);
    /// <inheritdoc />
    protected override ValueTask ReceiveAsync(IEventActorContext<ScheduledTaskEventActor> context, IEvent @event) => ResolveMappedEventHandler(@event, _receiveMap)(@event, _owner, logger);
    /// <inheritdoc />
    protected override ValueTask OnExceptionAsync(IEventActorContext<ScheduledTaskEventActor> context, ActorThreadId threadId, IEvent @event, Exception ex)
    {
        ArgumentNullException.ThrowIfNull(@event);
        global::TomasAI.IFM.Domain.SystemAdmin.ActorLifecycleDiagnosticLog.Write15(logger, ex, nameof(ScheduledTaskEventActor), nameof(OnExceptionAsync), @event.EventName, threadId);
        return ValueTask.FromException(ex);
    }
}
