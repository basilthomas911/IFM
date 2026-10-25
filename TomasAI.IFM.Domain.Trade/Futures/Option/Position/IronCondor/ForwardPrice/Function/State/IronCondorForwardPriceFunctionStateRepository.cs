using ForwardPriceSessionId = TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan.IronCondorTradePlanId;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.EventSourcing.Repository;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position.IronCondor.ForwardPrice;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.ForwardPrice.Function.State;
/// <summary>Persists successful calculations only; no job tables, history replay or synchronous projector.</summary>
public sealed class IronCondorForwardPriceFunctionStateRepository(IEventSourceActorStateFactory factory, IEventSourceActorDbContext events,
    IActorService actors, ILogger<IronCondorForwardPriceFunctionStateRepository> logger)
    : BaseEventSourceActorRepository(factory, events, actors, logger), IEventSourceFunctionStateRepository<IronCondorForwardPriceFunctionState, CalculateIronCondorForwardPriceCommand>
{
    /// <summary>Loads at most the latest completed calculation for this trade/session.</summary>
    public async ValueTask<IronCondorForwardPriceFunctionState> LoadStateAsync(CalculateIronCondorForwardPriceCommand request, CancellationToken cancellationToken = default)
    {
        var state = new IronCondorForwardPriceFunctionState { Id = request.Subject.ThreadId };
        var stream = await GetStreamId(request.StreamId, cancellationToken).ConfigureAwait(false);
        await EventSourceDb.MapReduceActorEventStreamAsync<IronCondorForwardPriceFunctionState, IronCondorForwardPriceCalculatedEvent>(stream, 1, state.ReplayEvents, cancellationToken).ConfigureAwait(false);
        return state.Prepare(request);
    }
    /// <summary>Commits only the completed event; no distribution persistence or event broadcast is introduced.</summary>
    public async ValueTask SaveCompletedStateAsync(IFunctionActorContext context, IronCondorForwardPriceFunctionState state, CalculateIronCondorForwardPriceCommand request, CancellationToken cancellationToken = default)
    {
        await SaveStateEventsAsync(state, request, state.CommittedStreamVersion, cancellationToken).ConfigureAwait(false);
        state.AcceptChanges();
    }
    /// <inheritdoc />
    protected override ValueTask DenormalizeEventsAsync(ICommandActorContext context, DomainEventCollection domainEvents) => ValueTask.CompletedTask;
}
