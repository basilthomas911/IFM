using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Query.Model;

/// <summary>Applies query presentation rules to typed TradeDbContext reads.</summary>
internal static class FuturesOptionTradeQueryModel
{
    extension(IDbContextFactory dbFactory)
    {
        /// <summary>Orders history by value date and the established trade-status sequence.</summary>
        internal async ValueTask<ICollection<TradeHistoryReadModel>> GetTradeHistoryAsync(int orderId, CancellationToken cancellationToken = default)
        {
            var trades = await dbFactory.TradeDb.GetTradeHistoryAsync(orderId, cancellationToken).ConfigureAwait(false);
            return [.. trades.Where(trade => StatusOrder(trade.TradeStatus) >= 0)
                .OrderBy(trade => trade.ValueDate).ThenBy(trade => StatusOrder(trade.TradeStatus))];
        }

        /// <summary>Reads the effective trade limit through the provider-owned typed boundary.</summary>
        internal async ValueTask<TradeLimitReadModel> GetTradeLimitAsync(int tradeId, CancellationToken cancellationToken = default)
            => await dbFactory.TradeDb.GetTradeLimitAsync(tradeId, cancellationToken).ConfigureAwait(false) ?? new();

        /// <summary>Reads thresholds for the exact trade and trade type.</summary>
        internal async ValueTask<TradeTypeLimitReadModel?> GetTradeTypeLimitAsync(int tradeId, TradeType tradeType, CancellationToken cancellationToken = default)
            => await dbFactory.TradeDb.GetTradeTypeLimitAsync(tradeId, tradeType, cancellationToken).ConfigureAwait(false);

        /// <summary>Reads one position using its complete typed position key.</summary>
        internal async ValueTask<TradePositionReadModel> GetTradePositionAsync(int orderId, int tradeId, TradeType tradeType, DateOnly valueDate, int daysToExpiry, TradeStatus tradeStatus, CancellationToken cancellationToken = default)
            => await dbFactory.TradeDb.GetTradePositionAsync(orderId, tradeId, tradeType, valueDate, daysToExpiry, tradeStatus, cancellationToken).ConfigureAwait(false) ?? new();

        /// <summary>Reads average option-leg quantity without duplicating storage SQL or bindings.</summary>
        internal async ValueTask<ScalarReadModel<int>> GetTradeQuantityAsync(int tradeId, CancellationToken cancellationToken = default)
            => new(await dbFactory.TradeDb.GetTradeQuantityAsync(tradeId, cancellationToken).ConfigureAwait(false));
    }

    static int StatusOrder(TradeStatus status) => status switch
    {
        TradeStatus.Open => 0,
        TradeStatus.IntraDay => 1,
        TradeStatus.EndOfDay => 2,
        TradeStatus.Close => 3,
        _ => -1
    };
}
