using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.ScheduledTaskDb;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Events.Domain;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Command.State;

/// <summary>Commits source events, required scheduler rows and durable notification markers before state acceptance.</summary>
internal static class ScheduledTaskAtomicPersistence
{
    internal static async Task<DomainEventCollection> Save<TState>(IEventSourceActorState<TState> state, ICommand command,
        IEventSourceActorDbContext source, IScheduledTaskTransactionalProjectionWriter writer,
        IPostgresEventTransaction transactions, CancellationToken token) where TState : IActorState
    {
        if (!string.Equals(source.ProviderName,"System.Data.Postgres",StringComparison.Ordinal) || PostgresDatabaseIdentity.Parse(source.ConnectionString) != PostgresDatabaseIdentity.Parse(writer.ConnectionString))
            throw new InvalidOperationException("ScheduledTaskDb and EventSourceDb must share the same PostgreSQL database/security identity.");
        var pending = new DomainEventCollection(state.Events);
        if (pending.Count == 0) return pending;
        await transactions.ExecuteAsync(async (transaction, cancellation) =>
        {
            var version = state.CommittedStreamVersion;
            foreach (var domainEvent in pending)
            {
                await transaction.AppendAsync(command.StreamId, command.CommandId, domainEvent, version++, cancellation).ConfigureAwait(false);
                await Project(transaction, writer, domainEvent, cancellation).ConfigureAwait(false);
            }
            return true;
        }, token).ConfigureAwait(false);
        state.AcceptChanges();
        return pending;
    }
    static Task Project(IEnlistedPostgresTransaction transaction, IScheduledTaskTransactionalProjectionWriter writer, IEvent domainEvent, CancellationToken token) =>
        domainEvent switch
        {
            ScheduledTaskCreatedEvent value => writer.ProjectAsync(transaction, value.ScheduledTaskDefinition ?? throw new InvalidDataException("Missing ScheduledTaskDefinition source payload."), token),
            ScheduledTaskDisabledEvent value => writer.ProjectAsync(transaction, value.ScheduledTaskDefinition ?? throw new InvalidDataException("Missing ScheduledTaskDefinition source payload."), token),
            ScheduledTaskEnabledEvent value => writer.ProjectAsync(transaction, value.ScheduledTaskDefinition ?? throw new InvalidDataException("Missing ScheduledTaskDefinition source payload."), token),
            ScheduledTaskHostCapabilityRecordedEvent value => writer.ProjectAsync(transaction, value.ScheduledTaskCatalog ?? throw new InvalidDataException("Missing ScheduledTaskCatalog source payload."), token),
            ScheduledTaskInstallationFailureRecordedEvent value => writer.ProjectAsync(transaction, value.ScheduledTaskDefinition ?? throw new InvalidDataException("Missing ScheduledTaskDefinition source payload."), token),
            ScheduledTaskInstallationRecordedEvent value => writer.ProjectAsync(transaction, value.ScheduledTaskDefinition ?? throw new InvalidDataException("Missing ScheduledTaskDefinition source payload."), token),
            ScheduledTaskProjectRegisteredEvent value => writer.ProjectAsync(transaction, value.ScheduledTaskCatalog ?? throw new InvalidDataException("Missing ScheduledTaskCatalog source payload."), token),
            ScheduledTaskRemovedEvent value => writer.ProjectAsync(transaction, value.ScheduledTaskDefinition ?? throw new InvalidDataException("Missing ScheduledTaskDefinition source payload."), token),
            ScheduledTaskRunAdmissionRecordedEvent value => writer.ProjectAsync(transaction, value.ScheduledTaskRun ?? throw new InvalidDataException("Missing ScheduledTaskRun source payload."), token),
            ScheduledTaskRunAdmittedEvent value => writer.ProjectAsync(transaction, value.ScheduledTaskDefinition ?? throw new InvalidDataException("Missing ScheduledTaskDefinition source payload."), token),
            ScheduledTaskRunCompletedEvent value => writer.ProjectAsync(transaction, value.ScheduledTaskRun ?? throw new InvalidDataException("Missing ScheduledTaskRun source payload."), token),
            ScheduledTaskRunFailedEvent value => writer.ProjectAsync(transaction, value.ScheduledTaskRun ?? throw new InvalidDataException("Missing ScheduledTaskRun source payload."), token),
            ScheduledTaskRunReleasedEvent value => writer.ProjectAsync(transaction, value.ScheduledTaskDefinition ?? throw new InvalidDataException("Missing ScheduledTaskDefinition source payload."), token),
            ScheduledTaskRunRequestedEvent value => writer.ProjectAsync(transaction, value.ScheduledTaskRun ?? throw new InvalidDataException("Missing ScheduledTaskRun source payload."), token),
            ScheduledTaskRunStageRecordedEvent value => writer.ProjectAsync(transaction, value.ScheduledTaskRun ?? throw new InvalidDataException("Missing ScheduledTaskRun source payload."), token),
            ScheduledTaskRunStartedEvent value => writer.ProjectAsync(transaction, value.ScheduledTaskRun ?? throw new InvalidDataException("Missing ScheduledTaskRun source payload."), token),
            ScheduledTaskRunUncertainRecordedEvent value => writer.ProjectAsync(transaction, value.ScheduledTaskRun ?? throw new InvalidDataException("Missing ScheduledTaskRun source payload."), token),
            ScheduledTaskScheduleChangedEvent value => writer.ProjectAsync(transaction, value.ScheduledTaskDefinition ?? throw new InvalidDataException("Missing ScheduledTaskDefinition source payload."), token),
            _ => throw new InvalidOperationException("No enlisted scheduled-task projection for " + domainEvent.GetType().Name)
        };
}
