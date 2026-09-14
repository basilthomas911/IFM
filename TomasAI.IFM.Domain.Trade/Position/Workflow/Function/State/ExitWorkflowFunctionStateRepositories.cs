using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Position.Workflow.Function.State;

public sealed class ExitOrderCompositionFunctionStateRepository(
    IEventSourceActorStateFactory states, IEventSourceActorDbContext eventSource,
    IActorService actors, ILogger<ExitOrderCompositionFunctionStateRepository> logger)
    : BaseEventSourceActorRepository(states, eventSource, actors, logger),
        IEventSourceFunctionStateRepository<ExitOrderCompositionFunctionState, ComposeExitOrderCommand>
{
    public async ValueTask<ExitOrderCompositionFunctionState> LoadStateAsync(
        ComposeExitOrderCommand request, CancellationToken cancellationToken = default) =>
        (await LoadStateFromSnapshotAsync<ExitOrderCompositionFunctionState,
            ExitOrderCompositionCompletedEvent>(request, cancellationToken).ConfigureAwait(false)).Prepare(request);
    public async ValueTask SaveCompletedStateAsync(IFunctionActorContext context,
        ExitOrderCompositionFunctionState state, ComposeExitOrderCommand request,
        CancellationToken cancellationToken = default) =>
        _ = await SaveStateEventsAsync(state, request, state.LastPersistedEventId, cancellationToken)
            .ConfigureAwait(false);
    protected override ValueTask DenormalizeEventsAsync(ICommandActorContext context,
        DomainEventCollection events) => ValueTask.CompletedTask;
}

public sealed class PositionExitRiskFunctionStateRepository(
    IEventSourceActorStateFactory states, IEventSourceActorDbContext eventSource,
    IActorService actors, ILogger<PositionExitRiskFunctionStateRepository> logger)
    : BaseEventSourceActorRepository(states, eventSource, actors, logger),
        IEventSourceFunctionStateRepository<PositionExitRiskFunctionState, EvaluatePositionExitRiskCommand>
{
    public async ValueTask<PositionExitRiskFunctionState> LoadStateAsync(
        EvaluatePositionExitRiskCommand request, CancellationToken cancellationToken = default) =>
        (await LoadStateFromSnapshotAsync<PositionExitRiskFunctionState,
            PositionExitRiskCompletedEvent>(request, cancellationToken).ConfigureAwait(false)).Prepare(request);
    public async ValueTask SaveCompletedStateAsync(IFunctionActorContext context,
        PositionExitRiskFunctionState state, EvaluatePositionExitRiskCommand request,
        CancellationToken cancellationToken = default) =>
        _ = await SaveStateEventsAsync(state, request, state.LastPersistedEventId, cancellationToken)
            .ConfigureAwait(false);
    protected override ValueTask DenormalizeEventsAsync(ICommandActorContext context,
        DomainEventCollection events) => ValueTask.CompletedTask;
}
