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
public sealed class ScheduledTaskCatalogStateRepository(IEventSourceActorStateFactory stateFactory, IEventSourceActorDbContext eventSource,
    IActorService actorService, IEventProjector<ScheduledTaskCatalogCommandActor> projector, ILogger<ScheduledTaskCatalogStateRepository> logger)
    : BaseEventSourceActorRepository(stateFactory, eventSource, actorService, logger), IEventSourceActorStateRepository<ScheduledTaskCatalogCommandState>
{
    /// <inheritdoc />
    public ValueTask<ScheduledTaskCatalogCommandState> LoadStateAsync(ICommand command) => LoadStateAsync(command, CancellationToken.None);
    /// <inheritdoc />
    public async ValueTask<ScheduledTaskCatalogCommandState> LoadStateAsync(ICommand command, CancellationToken cancellationToken) => await base.LoadStateAsync<ScheduledTaskCatalogCommandState>(command, cancellationToken).ConfigureAwait(false);
    /// <inheritdoc />
    public ValueTask SaveStateAsync(ICommandActorContext context, ScheduledTaskCatalogCommandState state, ICommand command) => SaveStateAsync(context, state, command, CancellationToken.None);
    /// <inheritdoc />
    public async ValueTask SaveStateAsync(ICommandActorContext context, ScheduledTaskCatalogCommandState state, ICommand command, CancellationToken cancellationToken) => await SaveStateAndDenormalizeEventsAsync(context, state, command, cancellationToken).ConfigureAwait(false);
    /// <inheritdoc />
    protected override ValueTask DenormalizeEventsAsync(ICommandActorContext context, DomainEventCollection events) => projector.DomainEventsProjectionAsync(events);
}
