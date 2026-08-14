using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Domain.MarketData.Feed.TickAggregation.Event.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.StatusConsole;

namespace TomasAI.IFM.Domain.MarketData.Feed.TickAggregation.Event;

/// <summary>
/// Handles the main, complete, and fail events in the futures tick trade-data insertion family.
/// </summary>
public static class FuturesTickTradeDataInserted
{
    /// <summary>
    /// Initializes the event-family service identifier from its dedicated log source.
    /// </summary>
    static FuturesTickTradeDataInserted()
    {
        ServiceId = $"{LogSourceType.FuturesTickTradeDataInserted}";
    }

    /// <summary>
    /// Gets the structured logging service identifier for this event family.
    /// </summary>
    static string ServiceId { get; }

    /// <summary>
    /// Projects an inserted trade event to the market-data database and publishes the matching complete
    /// or fail lifecycle event.
    /// </summary>
    /// <param name="event">The durable trade-inserted event containing the trade projection payload.</param>
    /// <param name="context">The event actor context used to publish lifecycle events.</param>
    /// <param name="dbFactory">The database factory providing the market-data projection context.</param>
    /// <param name="logger">The actor logger used to record handler exceptions.</param>
    /// <returns><see langword="true"/> when projection and complete-event publication succeed.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="event"/>, <paramref name="context"/>, <paramref name="dbFactory"/>,
    /// or <paramref name="logger"/> is <see langword="null"/>.
    /// </exception>
    /// <remarks>
    /// If projection fails, the typed fail event is published and the exception is rethrown so durable
    /// event processing retains its existing retry behavior.
    /// </remarks>
    public static async ValueTask<bool> ExecuteAsync(
        this FuturesTickTradeDataInsertedEvent @event,
        IEventActorContext context,
        IDbContextFactory dbFactory,
        ILogger<TickAggregationEventActor> logger)
    {
        IsArgumentNull.Check(@event);
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(dbFactory);
        IsArgumentNull.Check(logger);

        var source = $"{nameof(FuturesTickTradeDataInsertedEvent)} for EntityId: {@event.EntityId}";
        try
        {
            await dbFactory.MarketDataDb.InsertTickTradeDataAsync(@event).ConfigureAwait(false);
            var complete = @event.ToCompleteEvent<
                FuturesTickTradeDataInsertedCompleteEvent,
                TickDataEntityId>();
            await context.SendAsync<FuturesTickTradeDataInsertedCompleteEvent, TickDataEntityId>(
                (FuturesTickTradeDataInsertedCompleteEvent)complete).ConfigureAwait(false);
            return true;
        }
        catch (Exception exception)
        {
            logger.LogErrorEvent(
                ServiceId,
                exception,
                "{Source}: trade-data projection failed",
                source);
            var failed = @event.ToFailEvent<
                FuturesTickTradeDataInsertedFailEvent,
                TickDataEntityId>(exception);
            await context.SendAsync<FuturesTickTradeDataInsertedFailEvent, TickDataEntityId>(
                (FuturesTickTradeDataInsertedFailEvent)failed).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// Records successful completion of a futures tick trade-data projection.
    /// </summary>
    /// <param name="event">The terminal event describing the completed trade projection.</param>
    /// <param name="context">The event actor context associated with lifecycle processing.</param>
    /// <param name="logger">The mandatory actor logger reserved for handler exception or future domain logging.</param>
    /// <returns>A completed result containing <see langword="true"/>.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="event"/>, <paramref name="context"/>, or <paramref name="logger"/>
    /// is <see langword="null"/>.
    /// </exception>
    /// <remarks>
    /// This is a quiet terminal handler by default and does not create another command, projection, event,
    /// or informational log entry.
    /// </remarks>
    public static ValueTask<bool> ExecuteAsync(
        this FuturesTickTradeDataInsertedCompleteEvent @event,
        IEventActorContext context,
        ILogger<TickAggregationEventActor> logger)
    {
        IsArgumentNull.Check(@event);
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(logger);

        return ValueTask.FromResult(true);
    }

    /// <summary>
    /// Records failure of a futures tick trade-data projection.
    /// </summary>
    /// <param name="event">The terminal event describing the failed trade projection.</param>
    /// <param name="context">The event actor context associated with lifecycle processing.</param>
    /// <param name="logger">The actor logger used to record structured failure details.</param>
    /// <returns>A completed result containing <see langword="true"/> after the failure is logged.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="event"/>, <paramref name="context"/>, or <paramref name="logger"/>
    /// is <see langword="null"/>.
    /// </exception>
    /// <remarks>This is a terminal handler and does not retry or republish the failed domain operation.</remarks>
    public static ValueTask<bool> ExecuteAsync(
        this FuturesTickTradeDataInsertedFailEvent @event,
        IEventActorContext context,
        ILogger<TickAggregationEventActor> logger)
    {
        IsArgumentNull.Check(@event);
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(logger);

        logger.LogErrorEvent(
            ServiceId,
            "{EventName} for {EntityId} and command {CommandId}: error {ErrorCode} ({ErrorType}) - {ErrorMessage}",
            @event.EventName,
            @event.EntityId,
            @event.CommandId,
            @event.ErrorCode,
            @event.ErrorType,
            @event.ErrorMessage);
        return ValueTask.FromResult(true);
    }
}
