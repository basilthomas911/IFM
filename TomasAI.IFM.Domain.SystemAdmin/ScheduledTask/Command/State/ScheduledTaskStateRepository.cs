using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Command.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Command.State;
/// <summary>Loads administrative source events and commits changes before submitting projections.</summary>
public sealed class ScheduledTaskStateRepository(IEventSourceActorStateFactory stateFactory, IEventSourceActorDbContext eventSource,
    IActorService actorService, IEventProjector<ScheduledTaskCommandActor> projector, ILogger<ScheduledTaskStateRepository> logger)
    : BaseEventSourceActorRepository(stateFactory, eventSource, actorService, logger), IEventSourceActorStateRepository<ScheduledTaskCommandState>
{
    /// <inheritdoc />
    public ValueTask<ScheduledTaskCommandState> LoadStateAsync(ICommand command) => LoadStateAsync(command, CancellationToken.None);
    /// <inheritdoc />
    public async ValueTask<ScheduledTaskCommandState> LoadStateAsync(ICommand command, CancellationToken cancellationToken) => await base.LoadStateAsync<ScheduledTaskCommandState>(command, cancellationToken).ConfigureAwait(false);
    /// <inheritdoc />
    public ValueTask SaveStateAsync(ICommandActorContext context, ScheduledTaskCommandState state, ICommand command) => SaveStateAsync(context, state, command, CancellationToken.None);
    /// <inheritdoc />
    public async ValueTask SaveStateAsync(ICommandActorContext context, ScheduledTaskCommandState state, ICommand command, CancellationToken cancellationToken) => await SaveStateAndDenormalizeEventsAsync(context, state, command, cancellationToken).ConfigureAwait(false);
    /// <inheritdoc />
    protected override ValueTask DenormalizeEventsAsync(ICommandActorContext context, DomainEventCollection events) => projector.DomainEventsProjectionAsync(events);
}
