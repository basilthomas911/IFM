using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Events;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
namespace TomasAI.IFM.UI.EventConsumer;
/// <summary>Defines an independently owned scheduled-task public notification consumer.</summary>
public interface IScheduledTaskUIEventConsumer
{
    /// <summary>Starts concrete completion/failure observation without sharing another view's listener.</summary>
    ValueTask StartScheduledTasksAsync(Func<Guid, ValueTask> handler, CancellationToken cancellationToken = default);
    /// <summary>Stops only this listener's subscription.</summary>
    ValueTask StopAsync();
}
/// <summary>Observes scheduled-task notifications; query actors supply the displayed persisted models.</summary>
public sealed class ScheduledTaskUIEventConsumer(INatsEventListenerOptions options, ILogger logger)
    : NatsActorEventListener(options, logger), IScheduledTaskUIEventConsumer
{
    /// <inheritdoc />
    public async ValueTask StartScheduledTasksAsync(Func<Guid, ValueTask> handler, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(handler);
        cancellationToken.ThrowIfCancellationRequested();
        await StartAsync("ScheduledTaskUIEventConsumer", new Dictionary<ActorMailboxId, List<string>>
        {
            [new(ActorType.Event, "ScheduledTaskEvent")] = [
ScheduledTaskCreatedCompleteEvent.Verb, ScheduledTaskCreatedFailEvent.Verb, ScheduledTaskScheduleChangedCompleteEvent.Verb, ScheduledTaskScheduleChangedFailEvent.Verb, ScheduledTaskEnabledCompleteEvent.Verb, ScheduledTaskEnabledFailEvent.Verb, ScheduledTaskDisabledCompleteEvent.Verb, ScheduledTaskDisabledFailEvent.Verb, ScheduledTaskRemovedCompleteEvent.Verb, ScheduledTaskRemovedFailEvent.Verb, ScheduledTaskInstallationRecordedCompleteEvent.Verb, ScheduledTaskInstallationRecordedFailEvent.Verb, ScheduledTaskInstallationFailureRecordedCompleteEvent.Verb, ScheduledTaskInstallationFailureRecordedFailEvent.Verb, ScheduledTaskRunAdmittedCompleteEvent.Verb, ScheduledTaskRunAdmittedFailEvent.Verb, ScheduledTaskRunReleasedCompleteEvent.Verb, ScheduledTaskRunReleasedFailEvent.Verb, ScheduledTaskProjectRegisteredCompleteEvent.Verb, ScheduledTaskProjectRegisteredFailEvent.Verb, ScheduledTaskHostCapabilityRecordedCompleteEvent.Verb, ScheduledTaskHostCapabilityRecordedFailEvent.Verb, ScheduledTaskRunRequestedCompleteEvent.Verb, ScheduledTaskRunRequestedFailEvent.Verb, ScheduledTaskRunAdmissionRecordedCompleteEvent.Verb, ScheduledTaskRunAdmissionRecordedFailEvent.Verb, ScheduledTaskRunStageRecordedCompleteEvent.Verb, ScheduledTaskRunStageRecordedFailEvent.Verb, ScheduledTaskRunStartedCompleteEvent.Verb, ScheduledTaskRunStartedFailEvent.Verb, ScheduledTaskRunCompletedCompleteEvent.Verb, ScheduledTaskRunCompletedFailEvent.Verb, ScheduledTaskRunFailedCompleteEvent.Verb, ScheduledTaskRunFailedFailEvent.Verb, ScheduledTaskRunUncertainRecordedCompleteEvent.Verb, ScheduledTaskRunUncertainRecordedFailEvent.Verb
            ]
        }, async (verb, message) =>
        {
            var identity = verb switch
            {
                ScheduledTaskCreatedCompleteEvent.Verb => message.AsEvent<ScheduledTaskCreatedCompleteEvent>()?.EntityId.Value,
                ScheduledTaskCreatedFailEvent.Verb => message.AsEvent<ScheduledTaskCreatedFailEvent>()?.EntityId.Value,
                ScheduledTaskScheduleChangedCompleteEvent.Verb => message.AsEvent<ScheduledTaskScheduleChangedCompleteEvent>()?.EntityId.Value,
                ScheduledTaskScheduleChangedFailEvent.Verb => message.AsEvent<ScheduledTaskScheduleChangedFailEvent>()?.EntityId.Value,
                ScheduledTaskEnabledCompleteEvent.Verb => message.AsEvent<ScheduledTaskEnabledCompleteEvent>()?.EntityId.Value,
                ScheduledTaskEnabledFailEvent.Verb => message.AsEvent<ScheduledTaskEnabledFailEvent>()?.EntityId.Value,
                ScheduledTaskDisabledCompleteEvent.Verb => message.AsEvent<ScheduledTaskDisabledCompleteEvent>()?.EntityId.Value,
                ScheduledTaskDisabledFailEvent.Verb => message.AsEvent<ScheduledTaskDisabledFailEvent>()?.EntityId.Value,
                ScheduledTaskRemovedCompleteEvent.Verb => message.AsEvent<ScheduledTaskRemovedCompleteEvent>()?.EntityId.Value,
                ScheduledTaskRemovedFailEvent.Verb => message.AsEvent<ScheduledTaskRemovedFailEvent>()?.EntityId.Value,
                ScheduledTaskInstallationRecordedCompleteEvent.Verb => message.AsEvent<ScheduledTaskInstallationRecordedCompleteEvent>()?.EntityId.Value,
                ScheduledTaskInstallationRecordedFailEvent.Verb => message.AsEvent<ScheduledTaskInstallationRecordedFailEvent>()?.EntityId.Value,
                ScheduledTaskInstallationFailureRecordedCompleteEvent.Verb => message.AsEvent<ScheduledTaskInstallationFailureRecordedCompleteEvent>()?.EntityId.Value,
                ScheduledTaskInstallationFailureRecordedFailEvent.Verb => message.AsEvent<ScheduledTaskInstallationFailureRecordedFailEvent>()?.EntityId.Value,
                ScheduledTaskRunAdmittedCompleteEvent.Verb => message.AsEvent<ScheduledTaskRunAdmittedCompleteEvent>()?.EntityId.Value,
                ScheduledTaskRunAdmittedFailEvent.Verb => message.AsEvent<ScheduledTaskRunAdmittedFailEvent>()?.EntityId.Value,
                ScheduledTaskRunReleasedCompleteEvent.Verb => message.AsEvent<ScheduledTaskRunReleasedCompleteEvent>()?.EntityId.Value,
                ScheduledTaskRunReleasedFailEvent.Verb => message.AsEvent<ScheduledTaskRunReleasedFailEvent>()?.EntityId.Value,
                ScheduledTaskProjectRegisteredCompleteEvent.Verb => message.AsEvent<ScheduledTaskProjectRegisteredCompleteEvent>()?.EntityId.Value,
                ScheduledTaskProjectRegisteredFailEvent.Verb => message.AsEvent<ScheduledTaskProjectRegisteredFailEvent>()?.EntityId.Value,
                ScheduledTaskHostCapabilityRecordedCompleteEvent.Verb => message.AsEvent<ScheduledTaskHostCapabilityRecordedCompleteEvent>()?.EntityId.Value,
                ScheduledTaskHostCapabilityRecordedFailEvent.Verb => message.AsEvent<ScheduledTaskHostCapabilityRecordedFailEvent>()?.EntityId.Value,
                ScheduledTaskRunRequestedCompleteEvent.Verb => message.AsEvent<ScheduledTaskRunRequestedCompleteEvent>()?.EntityId.Value,
                ScheduledTaskRunRequestedFailEvent.Verb => message.AsEvent<ScheduledTaskRunRequestedFailEvent>()?.EntityId.Value,
                ScheduledTaskRunAdmissionRecordedCompleteEvent.Verb => message.AsEvent<ScheduledTaskRunAdmissionRecordedCompleteEvent>()?.EntityId.Value,
                ScheduledTaskRunAdmissionRecordedFailEvent.Verb => message.AsEvent<ScheduledTaskRunAdmissionRecordedFailEvent>()?.EntityId.Value,
                ScheduledTaskRunStageRecordedCompleteEvent.Verb => message.AsEvent<ScheduledTaskRunStageRecordedCompleteEvent>()?.EntityId.Value,
                ScheduledTaskRunStageRecordedFailEvent.Verb => message.AsEvent<ScheduledTaskRunStageRecordedFailEvent>()?.EntityId.Value,
                ScheduledTaskRunStartedCompleteEvent.Verb => message.AsEvent<ScheduledTaskRunStartedCompleteEvent>()?.EntityId.Value,
                ScheduledTaskRunStartedFailEvent.Verb => message.AsEvent<ScheduledTaskRunStartedFailEvent>()?.EntityId.Value,
                ScheduledTaskRunCompletedCompleteEvent.Verb => message.AsEvent<ScheduledTaskRunCompletedCompleteEvent>()?.EntityId.Value,
                ScheduledTaskRunCompletedFailEvent.Verb => message.AsEvent<ScheduledTaskRunCompletedFailEvent>()?.EntityId.Value,
                ScheduledTaskRunFailedCompleteEvent.Verb => message.AsEvent<ScheduledTaskRunFailedCompleteEvent>()?.EntityId.Value,
                ScheduledTaskRunFailedFailEvent.Verb => message.AsEvent<ScheduledTaskRunFailedFailEvent>()?.EntityId.Value,
                ScheduledTaskRunUncertainRecordedCompleteEvent.Verb => message.AsEvent<ScheduledTaskRunUncertainRecordedCompleteEvent>()?.EntityId.Value,
                ScheduledTaskRunUncertainRecordedFailEvent.Verb => message.AsEvent<ScheduledTaskRunUncertainRecordedFailEvent>()?.EntityId.Value,
                _ => null
            };
            if (identity is { } id) await handler(id).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }
}
