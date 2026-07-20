using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Actor.Queries.Model;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Trade.Queries;
using TomasAI.IFM.Shared.Trade.ViewModels;

namespace TomasAI.IFM.Domain.Trade.Actor.Queries.Handlers;

/// <summary>
/// Provides internal extension methods that handle trade queries within the actor messaging pipeline.
/// Each method executes its query logic and publishes the result back to the caller via a NATS reply.
/// Handles trade history, trade limit, trade position, trade positions, trade position trade types,
/// trade quantity, and trade type limit retrieval.
/// </summary>
internal static class TradeQueryHandlers
{
    /// <summary>
    /// Handles a <see cref="GetTradeHistoryQuery"/> by retrieving trade history for the specified order,
    /// ordered by value date and grouped by trade status. The result is published back to the caller via a NATS reply.
    /// </summary>
    /// <param name="q">The query containing the order identifier for which to retrieve trade history.</param>
    /// <param name="dbFactory">The database context factory used to access trade data.</param>
    /// <param name="msgInfo">Actor message context used to send the NATS reply to the caller.</param>
    /// <returns>A <see cref="ValueTask"/> that completes after the reply has been sent.</returns>
    internal static async ValueTask GetTradeHistoryAsync(this GetTradeHistoryQuery q, IDbContextFactory dbFactory, ActorMessageInfo msgInfo)
    {
        var result = await dbFactory.GetTradeHistoryAsync(q.OrderId);
        await msgInfo.ActorMessage.NatsReplyAsync(new ServiceResult<TradeHistoryReadModel[]>([.. result]));
    }

    /// <summary>
    /// Handles a <see cref="GetTradeLimitQuery"/> by retrieving the trade limit for the specified trade,
    /// enriched with trade type limit thresholds. The result is published back to the caller via a NATS reply.
    /// </summary>
    /// <param name="q">The query containing the trade identifier for which to retrieve the trade limit.</param>
    /// <param name="dbFactory">The database context factory used to access trade data.</param>
    /// <param name="msgInfo">Actor message context used to send the NATS reply to the caller.</param>
    /// <returns>A <see cref="ValueTask"/> that completes after the reply has been sent.</returns>
    internal static async ValueTask GetTradeLimitAsync(this GetTradeLimitQuery q, IDbContextFactory dbFactory, ActorMessageInfo msgInfo)
    {
        var result = await dbFactory.GetTradeLimitAsync(q.TradeId);
        await msgInfo.ActorMessage.NatsReplyAsync(new ServiceResult<TradeLimitReadModel>(result));
    }

    /// <summary>
    /// Handles a <see cref="GetTradePositionQuery"/> by retrieving the trade position for the specified
    /// order, trade, trade type, value date, days to expiry, and trade status.
    /// The result is published back to the caller via a NATS reply.
    /// </summary>
    /// <param name="q">The query containing the order ID, trade ID, trade type, value date, days to expiry, and trade status.</param>
    /// <param name="dbFactory">The database context factory used to access trade data.</param>
    /// <param name="msgInfo">Actor message context used to send the NATS reply to the caller.</param>
    /// <returns>A <see cref="ValueTask"/> that completes after the reply has been sent.</returns>
    internal static async ValueTask GetTradePositionAsync(this GetTradePositionQuery q, IDbContextFactory dbFactory, ActorMessageInfo msgInfo)
    {
        var result = await dbFactory.GetTradePositionAsync(q.OrderId, q.TradeId, q.TradeType, q.ValueDate, q.DaysToExpiry, q.TradeStatus);
        await msgInfo.ActorMessage.NatsReplyAsync(new ServiceResult<TradePositionReadModel>(result));
    }

    /// <summary>
    /// Handles a <see cref="GetTradeQuantityQuery"/> by retrieving the quantity for the specified trade,
    /// computed as the average option leg quantity. The result is published back to the caller via a NATS reply.
    /// </summary>
    /// <param name="q">The query containing the trade identifier for which to retrieve the trade quantity.</param>
    /// <param name="dbFactory">The database context factory used to access trade data.</param>
    /// <param name="msgInfo">Actor message context used to send the NATS reply to the caller.</param>
    /// <returns>A <see cref="ValueTask"/> that completes after the reply has been sent.</returns>
    internal static async ValueTask GetTradeQuantityAsync(this GetTradeQuantityQuery q, IDbContextFactory dbFactory, ActorMessageInfo msgInfo)
    {
        var result = await dbFactory.GetTradeQuantityAsync(q.TradeId);
        await msgInfo.ActorMessage.NatsReplyAsync(new ServiceResult<ScalarReadModel<int>>(result));
    }

    /// <summary>
    /// Handles a <see cref="GetTradeTypeLimitQuery"/> by retrieving the trade type limit for the specified
    /// trade and trade type. The result is published back to the caller via a NATS reply.
    /// </summary>
    /// <param name="q">The query containing the trade identifier and trade type for which to retrieve the trade type limit.</param>
    /// <param name="dbFactory">The database context factory used to access trade data.</param>
    /// <param name="msgInfo">Actor message context used to send the NATS reply to the caller.</param>
    /// <returns>A <see cref="ValueTask"/> that completes after the reply has been sent.</returns>
    internal static async ValueTask GetTradeTypeLimitAsync(this GetTradeTypeLimitQuery q, IDbContextFactory dbFactory, ActorMessageInfo msgInfo)
    {
        var result = await dbFactory.GetTradeTypeLimitAsync(q.TradeId, q.TradeType);
        await msgInfo.ActorMessage.NatsReplyAsync(new ServiceResult<TradeTypeLimitReadModel>(result ?? new()));
    }
}
