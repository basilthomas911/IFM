using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Commands;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Feed.TickAggregation.Event.Actor;

public sealed class TickAggregationEventActor(
    IActorSupervisor supervisor,
    IDbContextFactory dbFactory,
    ILogger<TickAggregationEventActor> logger)
    : BaseEventActor<TickAggregationEventActor>(
        supervisor, logger, new ActorMailboxId(ActorType.Event, ActorName))
{
    public const string ActorName = "TickAggregationEvent";

    protected override IEvent ParseMessage(IEventActorContext context, IActorMessage message)
    {
        var subject = message.Subject;
        IEvent? result = subject switch
        {
            { ActorType: ActorType.Event, Name: ActorName, Verb: FuturesTickTradeDataChangedEvent.Verb } => message.AsEvent<FuturesTickTradeDataChangedEvent>(),
            { ActorType: ActorType.Event, Name: ActorName, Verb: FuturesTickQuoteDataChangedEvent.Verb } => message.AsEvent<FuturesTickQuoteDataChangedEvent>(),
            { ActorType: ActorType.Event, Name: ActorName, Verb: FuturesTickTradeDataInsertedEvent.Verb } => message.AsEvent<FuturesTickTradeDataInsertedEvent>(),
            { ActorType: ActorType.Event, Name: ActorName, Verb: FuturesTickQuoteDataInsertedEvent.Verb } => message.AsEvent<FuturesTickQuoteDataInsertedEvent>(),
            { ActorType: ActorType.Event, Name: ActorName, Verb: FuturesTickTradeDataInsertedCompleteEvent.Verb } => message.AsEvent<FuturesTickTradeDataInsertedCompleteEvent>(),
            { ActorType: ActorType.Event, Name: ActorName, Verb: FuturesTickQuoteDataInsertedCompleteEvent.Verb } => message.AsEvent<FuturesTickQuoteDataInsertedCompleteEvent>(),
            { ActorType: ActorType.Event, Name: ActorName, Verb: FuturesTickTradeDataInsertedFailEvent.Verb } => message.AsEvent<FuturesTickTradeDataInsertedFailEvent>(),
            { ActorType: ActorType.Event, Name: ActorName, Verb: FuturesTickQuoteDataInsertedFailEvent.Verb } => message.AsEvent<FuturesTickQuoteDataInsertedFailEvent>(),
            _ => null
        };
        return result!;
    }

    protected override async ValueTask ReceiveAsync(IEventActorContext context, IEvent @event)
    {
        switch (@event)
        {
            case FuturesTickTradeDataChangedEvent changed:
                await context.SendAsync(ToCommand(changed), changed.EntityId).ConfigureAwait(false);
                break;
            case FuturesTickQuoteDataChangedEvent changed:
                await context.SendAsync(ToCommand(changed), changed.EntityId).ConfigureAwait(false);
                break;
            case FuturesTickTradeDataInsertedEvent inserted:
                await ProjectTradeAsync(context, inserted).ConfigureAwait(false);
                break;
            case FuturesTickQuoteDataInsertedEvent inserted:
                await ProjectQuoteAsync(context, inserted).ConfigureAwait(false);
                break;
            case TickAggregationCompleteEvent:
            case TickAggregationFailEvent:
                break;
            default:
                throw new InvalidOperationException($"Unsupported tick aggregation event {@event.EventName}.");
        }
    }

    private static InsertFuturesTickTradeDataCommand ToCommand(FuturesTickTradeDataChangedEvent e) => new()
    {
        CommandId = e.CommandId, Subject = new ActorSubject(ActorType.Command, InsertFuturesTickTradeDataCommand.Actor,
            InsertFuturesTickTradeDataCommand.Verb, e.EntityId.Format()), EntityId = e.EntityId,
        SchemaVersion = e.SchemaVersion, TickDataId = e.TickDataId, AssetTypeId = e.AssetTypeId,
        Dataset = e.Dataset, DefinitionDate = e.DefinitionDate, PublisherId = e.PublisherId,
        InstrumentId = e.InstrumentId, TradeData = e.TradeData
    };

    private static InsertFuturesTickQuoteDataCommand ToCommand(FuturesTickQuoteDataChangedEvent e) => new()
    {
        CommandId = e.CommandId, Subject = new ActorSubject(ActorType.Command, InsertFuturesTickQuoteDataCommand.Actor,
            InsertFuturesTickQuoteDataCommand.Verb, e.EntityId.Format()), EntityId = e.EntityId,
        SchemaVersion = e.SchemaVersion, TickDataId = e.TickDataId, AssetTypeId = e.AssetTypeId,
        Dataset = e.Dataset, DefinitionDate = e.DefinitionDate, PublisherId = e.PublisherId,
        InstrumentId = e.InstrumentId, EmissionReason = e.EmissionReason,
        QuoteCount = e.QuoteCount, QuoteData = e.QuoteData
    };

    private async ValueTask ProjectTradeAsync(IEventActorContext context, FuturesTickTradeDataInsertedEvent e)
    {
        try
        {
            await dbFactory.MarketDataDb.InsertTickTradeDataAsync(e).ConfigureAwait(false);
            var complete = e.ToCompleteEvent<FuturesTickTradeDataInsertedCompleteEvent, TickDataEntityId>();
            await context.SendAsync<FuturesTickTradeDataInsertedCompleteEvent, TickDataEntityId>(
                (FuturesTickTradeDataInsertedCompleteEvent)complete).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            var failed = e.ToFailEvent<FuturesTickTradeDataInsertedFailEvent, TickDataEntityId>(exception);
            await context.SendAsync<FuturesTickTradeDataInsertedFailEvent, TickDataEntityId>(
                (FuturesTickTradeDataInsertedFailEvent)failed).ConfigureAwait(false);
            throw;
        }
    }

    private async ValueTask ProjectQuoteAsync(IEventActorContext context, FuturesTickQuoteDataInsertedEvent e)
    {
        try
        {
            await dbFactory.MarketDataDb.InsertTickQuoteDataAsync(e).ConfigureAwait(false);
            var complete = e.ToCompleteEvent<FuturesTickQuoteDataInsertedCompleteEvent, TickDataEntityId>();
            await context.SendAsync<FuturesTickQuoteDataInsertedCompleteEvent, TickDataEntityId>(
                (FuturesTickQuoteDataInsertedCompleteEvent)complete).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            var failed = e.ToFailEvent<FuturesTickQuoteDataInsertedFailEvent, TickDataEntityId>(exception);
            await context.SendAsync<FuturesTickQuoteDataInsertedFailEvent, TickDataEntityId>(
                (FuturesTickQuoteDataInsertedFailEvent)failed).ConfigureAwait(false);
            throw;
        }
    }

    protected override async ValueTask OnExceptionAsync(
        IEventActorContext context, ActorThreadId threadId, IEvent @event, Exception exception) =>
        await exception.SendErrorEventAsync<
            TomasAI.IFM.Shared.EventModelActor.Events.EventExceptionEvent,
            ActorEntityId>(ErrorType.EventService, context).ConfigureAwait(false);
}
