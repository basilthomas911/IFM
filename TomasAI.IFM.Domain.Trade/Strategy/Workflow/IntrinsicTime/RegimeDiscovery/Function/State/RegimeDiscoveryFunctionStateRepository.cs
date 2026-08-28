using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Function.State;

/// <summary>Loads completed Function state and saves it without denormalization.</summary>
public sealed class RegimeDiscoveryFunctionStateRepository(
    IEventSourceActorStateFactory stateFactory,
    IEventSourceActorDbContext eventSource,
    IActorService actorService,
    ILogger<RegimeDiscoveryFunctionStateRepository> logger)
    : BaseEventSourceActorRepository(stateFactory, eventSource, actorService, logger),
      IEventSourceFunctionStateRepository<
          RegimeDiscoveryFunctionState,
          ExecuteRegimeDiscoveryPipelineCommand>
{
    public async ValueTask<RegimeDiscoveryFunctionState> LoadStateAsync(
        ExecuteRegimeDiscoveryPipelineCommand request,
        CancellationToken cancellationToken = default)
        => await LoadStateAsync<RegimeDiscoveryFunctionState>(request, cancellationToken)
            .ConfigureAwait(false);

    public async ValueTask SaveCompletedStateAsync(
        IFunctionActorContext context,
        RegimeDiscoveryFunctionState state,
        ExecuteRegimeDiscoveryPipelineCommand request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        await SaveStateEventsAsync(
            state,
            request,
            expectedStreamVersion: 0,
            cancellationToken).ConfigureAwait(false);
    }

    // Command repositories still use the denormalizing save path. This Function repository never calls it.
    protected override ValueTask DenormalizeEventsAsync(
        ICommandActorContext context,
        DomainEventCollection domainEvents)
        => ValueTask.CompletedTask;
}
