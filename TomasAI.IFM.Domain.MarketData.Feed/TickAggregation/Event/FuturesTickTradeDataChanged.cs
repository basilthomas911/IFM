using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Commands;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Domain.MarketData.Feed.TickAggregation.Event.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.StatusConsole;

namespace TomasAI.IFM.Domain.MarketData.Feed.TickAggregation.Event;

/// <summary>
/// Handles the event family initiated by <see cref="FuturesTickTradeDataChangedEvent"/>.
/// </summary>
public static class FuturesTickTradeDataChanged
{
    /// <summary>
    /// Initializes the event-family service identifier from its dedicated log source.
    /// </summary>
    static FuturesTickTradeDataChanged()
    {
        ServiceId = $"{LogSourceType.FuturesTickTradeDataChanged}";
    }

    /// <summary>
    /// Gets the structured logging service identifier for this event family.
    /// </summary>
    static string ServiceId { get; }

    /// <summary>
    /// Converts a changed trade event into its matching insert command and sends the command through
    /// the event actor context.
    /// </summary>
    /// <param name="event">The changed trade event containing the normalized trade payload.</param>
    /// <param name="context">The event actor context used to send the insert command.</param>
    /// <param name="logger">The actor logger used to record handler exceptions.</param>
    /// <returns><see langword="true"/> after the command has been accepted by the context send operation.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="event"/>, <paramref name="context"/>, or <paramref name="logger"/>
    /// is <see langword="null"/>.
    /// </exception>
    /// <remarks>
    /// Command publication is awaited. A publication failure propagates to the event actor so its normal
    /// error and retry behavior remains authoritative.
    /// </remarks>
    public static async ValueTask<bool> ExecuteAsync(
        this FuturesTickTradeDataChangedEvent @event,
        IEventActorContext context,
        ILogger<TickAggregationEventActor> logger)
    {
        IsArgumentNull.Check(@event);
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(logger);

        var source = $"{nameof(FuturesTickTradeDataChangedEvent)} for EntityId: {@event.EntityId}";
        try
        {
            await context.SendAsync(ToCommand(@event), @event.EntityId).ConfigureAwait(false);
            return true;
        }
        catch (Exception exception)
        {
            logger.LogErrorEvent(
                ServiceId,
                exception,
                "{Source}: trade insert command publication failed",
                source);
            throw;
        }
    }

    /// <summary>
    /// Creates the trade-insert command while preserving the source event's identity, schema, metadata,
    /// and normalized trade payload.
    /// </summary>
    /// <param name="event">The changed trade event to convert.</param>
    /// <returns>The command consumed by the Tick Aggregation command actor.</returns>
    static InsertFuturesTickTradeDataCommand ToCommand(FuturesTickTradeDataChangedEvent @event) => new()
    {
        CommandId = @event.CommandId,
        Subject = new ActorSubject(
            ActorType.Command,
            InsertFuturesTickTradeDataCommand.Actor,
            InsertFuturesTickTradeDataCommand.Verb,
            @event.EntityId.Format()),
        EntityId = @event.EntityId,
        SchemaVersion = @event.SchemaVersion,
        TickDataId = @event.TickDataId,
        AssetTypeId = @event.AssetTypeId,
        Dataset = @event.Dataset,
        DefinitionDate = @event.DefinitionDate,
        PublisherId = @event.PublisherId,
        InstrumentId = @event.InstrumentId,
        TradeData = @event.TradeData
    };
}
