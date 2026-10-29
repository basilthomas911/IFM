using TomasAI.IFM.Domain.MarketData.Feed.Shared.Recovery;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventSourcing.Repository;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.MarketData.Feed.Recovery.Command.Actor;
namespace TomasAI.IFM.Domain.MarketData.Feed.Recovery.Command.State;
/// <summary>Persists the incident before enqueueing independently replayable projections.</summary>
public sealed class MarketDataFeedRecoveryStateRepository(IEventSourceActorStateFactory factory,
    IEventSourceActorDbContext events, IActorService actors,
    IEventProjector<MarketDataFeedRecoveryCommandActor> projector,
    ILogger<MarketDataFeedRecoveryStateRepository> logger)
    : BaseEventSourceActorRepository(factory, events, actors, logger), IEventSourceActorStateRepository<MarketDataFeedRecoveryState>
{
    public ValueTask<MarketDataFeedRecoveryState> LoadStateAsync(ICommand command) => LoadStateAsync(command,CancellationToken.None);
    public async ValueTask<MarketDataFeedRecoveryState> LoadStateAsync(ICommand command, CancellationToken token)
    {
        using var budget = Budget((AppendMarketDataFeedRecoveryCommand)command,token);
        return await LoadStateFromSnapshotAsync<MarketDataFeedRecoveryState, MarketDataFeedRecoveryStartedEvent>(command,budget.Token).ConfigureAwait(false);
    }
    public ValueTask SaveStateAsync(ICommandActorContext context, MarketDataFeedRecoveryState state, ICommand command) => SaveStateAsync(context,state,command,CancellationToken.None);
    public async ValueTask SaveStateAsync(ICommandActorContext context, MarketDataFeedRecoveryState state, ICommand command, CancellationToken token)
    {
        using var budget = Budget((AppendMarketDataFeedRecoveryCommand)command,token);
        DomainEventCollection committed;
        if (state.Events.Count>0)
        {
            committed=await SaveStateEventsAsync(state,command,state.CommittedStreamVersion,budget.Token).ConfigureAwait(false);
            state.AcceptChanges();
            foreach(var fact in committed.OfType<MarketDataFeedRecoveryFact>())
            {
                var record=RecoveryRecord.Parse(fact.Payload,fact.Digest);
                logger.LogInformation("Recovery fact committed {RecordId} {OperationId} {Sequence} {Transition} {OccurredAtUtc} {ReceivedAtUtc} {ElapsedMilliseconds} {OldOwner} {NewOwner} {CompletionScope} {MonitoringReady} {LatestError}",
                    record.RecordId,record.OperationId,record.Sequence,record.Kind,record.OccurredAtUtc,fact.ReceivedOn,record.ElapsedMilliseconds,record.OldOwner,record.NewOwner,record.CompletionScope,record.MonitoringReady,record.LatestError);
            }
        }
        else
        {
            // Repairs commit-before-enqueue/reply crashes without adding another source fact.
            var record=RecoveryRecord.Parse(((AppendMarketDataFeedRecoveryCommand)command).Payload,((AppendMarketDataFeedRecoveryCommand)command).Digest);
            var fact=state.FindFact(record.RecordId) ?? throw new InvalidDataException("Committed audit fact missing.");
            committed=new DomainEventCollection { fact };
        }
        await projector.DomainEventsProjectionAsync(committed).AsTask().WaitAsync(budget.Token).ConfigureAwait(false);
    }
    static CancellationTokenSource Budget(AppendMarketDataFeedRecoveryCommand request,CancellationToken token)
    {
        var remaining = request.RequestDeadlineUtc-DateTime.UtcNow;
        if (remaining<=TimeSpan.Zero) throw new TimeoutException("Recovery audit persistence deadline expired.");
        var budget = CancellationTokenSource.CreateLinkedTokenSource(token);
        budget.CancelAfter(remaining);
        return budget;
    }
    protected override ValueTask DenormalizeEventsAsync(ICommandActorContext context, DomainEventCollection events) =>
        projector.DomainEventsProjectionAsync(events);
}
