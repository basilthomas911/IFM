using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.ViewModels;

namespace TomasAI.IFM.Domain.Trade.Actor.Queries.Model;

internal static class TradeDbContext
{
    /// <summary>
    /// Retrieves trade history for the specified order, ordered by value date and grouped by trade status.
    /// </summary>
    internal static async ValueTask<ICollection<TradeHistoryReadModel>> GetTradeHistoryAsync(this IDbContextFactory dbFactory, int orderId)
    {
        List<TradeHistoryReadModel> trades = [.. (await dbFactory.TradeDb
            .Use(TradeDbCql.GetTradeHistory)
            .SetParameters(new GetTradeHistory(orderId))
            .ExecuteQueryAsync(MapToTradeHistory!)).OrderBy(e => e.ValueDate)];

        var tradeHistory = new List<TradeHistoryReadModel>();
        foreach (var valueDate in trades.Select(e => e.ValueDate).Distinct())
        {
            tradeHistory.AddRange(trades.Where(e => e.TradeStatus == TradeStatus.Open && e.ValueDate == valueDate));
            tradeHistory.AddRange(trades.Where(e => e.TradeStatus == TradeStatus.IntraDay && e.ValueDate == valueDate));
            tradeHistory.AddRange(trades.Where(e => e.TradeStatus == TradeStatus.EndOfDay && e.ValueDate == valueDate));
            tradeHistory.AddRange(trades.Where(e => e.TradeStatus == TradeStatus.Close && e.ValueDate == valueDate));
        }
        return tradeHistory;
    }

    /// <summary>
    /// Retrieves the trade limit for the specified trade, enriched with trade type limit thresholds.
    /// </summary>
    internal static async ValueTask<TradeLimitReadModel> GetTradeLimitAsync(this IDbContextFactory dbFactory, int tradeId)
    {
        var tradeLimit = await dbFactory.TradeDb
            .Use(TradeDbCql.GetTradeLimit)
            .SetParameters(new GetTradeLimit(tradeId))
            .ExecuteSingleAsync(MapToTradeLimit!);

        if (tradeLimit is null)
            return new();

        var tradeTypeLimit = await dbFactory.GetTradeTypeLimitAsync(tradeLimit.TradeId, tradeLimit.TradeType);
        return tradeTypeLimit is not null
            ? tradeLimit with
            {
                MaxLossLimit = tradeTypeLimit.MaxLossLimit,
                MinProfitLimit = tradeTypeLimit.MinProfitLimit,
                MaxProfitLimit = tradeTypeLimit.MaxProfitLimit
            }
            : tradeLimit;
    }

    /// <summary>
    /// Retrieves the trade type limit for the specified trade and trade type.
    /// </summary>
    internal static async ValueTask<TradeTypeLimitReadModel?> GetTradeTypeLimitAsync(this IDbContextFactory dbFactory, int tradeId, TradeType tradeType)
        => await dbFactory.TradeDb
            .Use(TradeDbCql.GetTradeTypeLimit)
            .SetParameters(new GetTradeTypeLimit(tradeId, tradeType.ToStringFast()))
            .ExecuteSingleAsync(MapToTradeTypeLimit);

    /// <summary>
    /// Retrieves a single trade position for the specified order, trade, type, date, days to expiry, and status.
    /// </summary>
    internal static async ValueTask<TradePositionReadModel> GetTradePositionAsync(this IDbContextFactory dbFactory,
        int orderId, int tradeId, TradeType tradeType, DateOnly valueDate, int daysToExpiry, TradeStatus tradeStatus)
        => await dbFactory.TradeDb
            .Use(TradeDbCql.GetTradePosition)
            .SetParameters(new GetTradePosition(orderId, tradeId, tradeType.ToStringFast(), valueDate, daysToExpiry, tradeStatus.ToStringFast()))
            .ExecuteSingleAsync(MapToTradePosition!) ?? new();

    /// <summary>
    /// Retrieves all trade positions for the specified order and trade.
    /// </summary>
    internal static async ValueTask<ICollection<TradePositionReadModel>> GetTradePositionsAsync(this IDbContextFactory dbFactory, int orderId, int tradeId)
        => await dbFactory.TradeDb
            .Use(TradeDbCql.GetTradePositions)
            .SetParameters(new GetTradePositions(orderId, tradeId))
            .ExecuteQueryAsync(MapToTradePosition!);

    /// <summary>
    /// Retrieves the distinct trade types for positions matching the specified order, trade, date, days to expiry, and status.
    /// </summary>
    internal static async ValueTask<string[]> GetTradePositionTradeTypesAsync(this IDbContextFactory dbFactory,
        int orderId, int tradeId, DateOnly valueDate, TradeStatus tradeStatus, int daysToExpiry)
        => [.. (await dbFactory.TradeDb
            .Use(TradeDbCql.GetTradePositionsById)
            .SetParameters(new GetTradePositionsById(orderId, tradeId, valueDate, tradeStatus.ToStringFast(), daysToExpiry))
            .ExecuteQueryAsync(MapToTradePosition!)).Select(e => e.TradeType.ToStringFast())];

    /// <summary>
    /// Retrieves the trade quantity for the specified trade, computed as the average option leg quantity.
    /// </summary>
    internal static async ValueTask<ScalarReadModel<int>> GetTradeQuantityAsync(this IDbContextFactory dbFactory, int tradeId)
    {
        var optionLegs = await dbFactory.TradeDb
            .Use(TradeDbCql.GetOptionLegs)
            .SetParameters(new GetOptionLegs(tradeId))
            .ExecuteQueryAsync(MapToOptionLeg!);
        var quantity = optionLegs.Count > 0
            ? optionLegs.Sum(e => e.Quantity) / optionLegs.Count
            : 0;
        return new ScalarReadModel<int>(quantity);
    }

    // --- mappers ---

    static TradeHistoryReadModel MapToTradeHistory<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new(
            orderId: e.GetInt(0),
            tradeId: e.GetInt(1),
            valueDate: e.GetDateOnly(2),
            tradeStatus: e.GetEnum<TradeStatus>(3),
            daysToExpiry: e.GetInt(4),
            tradeType: e.GetEnum<TradeType>(5),
            commission: e.GetDecimal(6),
            netSpread: e.GetDecimal(7),
            tradePnl: e.GetDecimal(8)
        );

    static TradeLimitReadModel MapToTradeLimit<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new(
            tradeId: e.GetInt(0),
            tradeType: e.GetEnum<TradeType>(1),
            riskMargin: e.GetDecimal(2),
            maxProfit: e.GetDecimal(3),
            maxLoss: e.GetDecimal(4),
            maxReturn: e.GetDecimal(5),
            maxLossLimit: e.GetDecimal(6),
            minProfitLimit: e.GetDecimal(7),
            maxProfitLimit: e.GetDecimal(8),
            minProfitTarget: e.GetDecimal(9),
            dailyProfitTarget: e.GetDecimal(10),
            createdOn: e.GetDateTime(11),
            createdBy: e.GetString(12),
            updatedOn: e.GetDateTime(13),
            updatedBy: e.GetString(14)
        );

    static TradeTypeLimitReadModel MapToTradeTypeLimit<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new(
            tradeId: e.GetInt(0),
            tradeType: e.GetEnum<TradeType>(1),
            maxLossLimit: e.GetDecimal(2),
            minProfitLimit: e.GetDecimal(3),
            maxProfitLimit: e.GetDecimal(4)
        );

    static TradePositionReadModel MapToTradePosition<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new(
            orderId: e.GetInt(0),
            tradeId: e.GetInt(1),
            tradeType: e.GetEnum<TradeType>(2),
            valueDate: e.GetDateOnly(3),
            tradeStatus: e.GetEnum<TradeStatus>(4),
            daysToExpiry: e.GetInt(5),
            commission: e.GetDecimal(6),
            deltaHedge: e.GetInt(7),
            netSpread: e.GetDecimal(8),
            tradeValue: e.GetDecimal(9),
            tradePnl: e.GetDecimal(10),
            assetPrice: e.GetDecimal(11),
            otmProbability: e.GetDouble(12),
            forwardPrice: e.GetDecimal(13),
            forwardLossRatio: e.GetDouble(14),
            lossProbability: e.GetDouble(15),
            riskFreeRate: e.GetDouble(16),
            createdOn: e.GetDateTime(17),
            createdBy: e.GetString(18),
            updatedOn: e.GetDateTime(19),
            updatedBy: e.GetString(20)
        );

    static OptionTradeLegReadModel MapToOptionLeg<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new(
            orderId: e.GetInt(0),
            tradeId: e.GetInt(1),
            contractId: e.GetString(2),
            quantity: e.GetInt(3),
            strikePrice: e.GetDecimal(4),
            optionLegType: e.GetEnum<OptionType>(5),
            optionLegAction: e.GetEnum<OptionLegAction>(6),
            createdOn: e.GetDateTime(7),
            createdBy: e.GetString(8),
            updatedOn: e.GetDateTime(9),
            updatedBy: e.GetString(10)
        );

    // --- bind value structs ---

    internal readonly record struct GetTradeHistory(int orderId) : IBindValue
    {
        public object Bind() => new { orderId };
    }

    internal readonly record struct GetTradeLimit(int tradeId) : IBindValue
    {
        public object Bind() => new { tradeId };
    }

    internal readonly record struct GetTradeTypeLimit(int tradeId, string tradeType) : IBindValue
    {
        public object Bind() => new { tradeId, tradeType };
    }

    internal readonly record struct GetTradePosition(int orderId, int tradeId, string tradeType, DateOnly valueDate, int daysToExpiry, string tradeStatus) : IBindValue
    {
        public object Bind() => new { orderId, tradeId, tradeType, valueDate, daysToExpiry, tradeStatus };
    }

    internal readonly record struct GetTradePositions(int orderId, int tradeId) : IBindValue
    {
        public object Bind() => new { orderId, tradeId };
    }

    internal readonly record struct GetTradePositionsById(int orderId, int tradeId, DateOnly valueDate, string tradeStatus, int daysToExpiry) : IBindValue
    {
        public object Bind() => new { orderId, tradeId, valueDate, tradeStatus, daysToExpiry };
    }

    internal readonly record struct GetOptionLegs(int tradeId) : IBindValue
    {
        public object Bind() => new { tradeId };
    }
}
