using TomasAI.IFM.Domain.Trade.Queries;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Queries;
using TomasAI.IFM.Domain.Trade.Shared.ServiceApi;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Trade.Query.Extensions;

/// <summary>
/// Provides direct, in-process Trade queries without actor messaging.
/// </summary>
/// <remarks>
/// Trade data is read through <see cref="IDbContextFactory.TradeDb"/> and the iron-condor MDI limit is read
/// from <see cref="IBlackboardService"/>. Every supported public query owns its typed success/failure mapping.
/// <c>GetTradePlanSummaryAsync</c> is intentionally unsupported pending removal of its obsolete UI contract.
/// </remarks>
public static partial class TradeQueryExtensions
{

    /// <summary>
    /// Gets trade history.
    /// </summary>
    /// <param name="orderId">The order identifier.</param>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
    public static async Task<ServiceResult<TradeHistoryReadModel[]>> GetTradeHistoryAsync(this ITradeQueryContext context, int orderId)
    {
        try
        {
            TradeHistoryReadModel[] result = [.. await context.DbFactory.TradeDb.GetTradeHistoryAsync(orderId)];
            return new ServiceOk<TradeHistoryReadModel[]>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<TradeHistoryReadModel[]>(GetTradeHistoryQuery.ErrorId, ex.Message);
        }
    }

    /// <summary>
    /// Gets option leg contract IDs.
    /// </summary>
    /// <param name="tradeId">The trade identifier.</param>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
    public static async Task<ServiceResult<string[]>> GetOptionLegContractIdsAsync(this ITradeQueryContext context, int tradeId)
    {
        try
        {
            string[] result = [.. await context.DbFactory.TradeDb.GetOptionLegContractIdsAsync(tradeId)];
            return new ServiceOk<string[]>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<string[]>(GetOptionLegContractIdsQuery.ErrorId, ex.Message);
        }
    }

    /// <summary>
    /// Gets trade limit.
    /// </summary>
    /// <param name="tradeId">The trade identifier.</param>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
    public static async Task<ServiceResult<TradeLimitReadModel>> GetTradeLimitAsync(this ITradeQueryContext context, int tradeId)
    {
        try
        {
            TradeLimitReadModel result = (await context.DbFactory.TradeDb.GetTradeLimitAsync(tradeId))!;
            return new ServiceOk<TradeLimitReadModel>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<TradeLimitReadModel>(GetTradeLimitQuery.ErrorId, ex.Message);
        }
    }

    /// <summary>
    /// Gets trade type limit.
    /// </summary>
    /// <param name="tradeId">The trade identifier.</param>
    /// <param name="tradeType">The trade strategy type.</param>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
    public static async Task<ServiceResult<TradeTypeLimitReadModel>> GetTradeTypeLimitAsync(this ITradeQueryContext context,
        int tradeId, TradeType tradeType)
    {
        try
        {
            TradeTypeLimitReadModel result =
                (await context.DbFactory.TradeDb.GetTradeTypeLimitAsync(tradeId, tradeType))!;
            return new ServiceOk<TradeTypeLimitReadModel>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<TradeTypeLimitReadModel>(GetTradeTypeLimitQuery.ErrorId, ex.Message);
        }
    }

    /// <summary>
    /// Gets trade quantity.
    /// </summary>
    /// <param name="tradeId">The trade identifier.</param>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
    public static async Task<ServiceResult<ScalarReadModel<int>>> GetTradeQuantityAsync(this ITradeQueryContext context, int tradeId)
    {
        try
        {
            var result = new ScalarReadModel<int>(await context.DbFactory.TradeDb.GetTradeQuantityAsync(tradeId));
            return new ServiceOk<ScalarReadModel<int>>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<ScalarReadModel<int>>(GetTradeQuantityQuery.ErrorId, ex.Message);
        }
    }

    /// <summary>
    /// Gets option trade.
    /// </summary>
    /// <param name="orderId">The order identifier.</param>
    /// <param name="tradeId">The trade identifier.</param>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
    public static async Task<ServiceResult<OptionTradeReadModel>> GetOptionTradeAsync(this ITradeQueryContext context, int orderId, int tradeId)
    {
        try
        {
            OptionTradeReadModel result = (await context.DbFactory.TradeDb.GetOptionTradeAsync(orderId, tradeId))!;
            return new ServiceOk<OptionTradeReadModel>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<OptionTradeReadModel>(GetOptionTradeQuery.ErrorId, ex.Message);
        }
    }

    /// <summary>
    /// Gets option trade spread data.
    /// </summary>
    /// <param name="orderId">The order identifier.</param>
    /// <param name="tradeId">The trade identifier.</param>
    /// <param name="tradeType">The trade strategy type.</param>
    /// <param name="valueDate">The applicable market value date.</param>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
    public static async Task<ServiceResult<OptionTradeSpreadsDataModel>> GetOptionTradeSpreadDataAsync(this ITradeQueryContext context,
        int orderId, int tradeId, TradeType tradeType, DateOnly valueDate)
    {
        try
        {
            OptionTradeSpreadsDataModel result =
                (await context.DbFactory.TradeDb.GetOptionTradeSpreadDataAsync(
                    orderId,
                    tradeId,
                    valueDate,
                    tradeType))!;
            return new ServiceOk<OptionTradeSpreadsDataModel>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<OptionTradeSpreadsDataModel>(
                GetOptionTradeSpreadDataQuery.ErrorId,
                ex.Message);
        }
    }

    /// <summary>
    /// Gets option trade spread bar data.
    /// </summary>
    /// <param name="orderId">The order identifier.</param>
    /// <param name="tradeId">The trade identifier.</param>
    /// <param name="tradeType">The trade strategy type.</param>
    /// <param name="valueDate">The applicable market value date.</param>
    /// <param name="startDate">The inclusive start date or timestamp.</param>
    /// <param name="endDate">The inclusive end date or timestamp.</param>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
    public static async Task<ServiceResult<OptionTradeSpreadBarsDataModel[]>> GetOptionTradeSpreadBarDataAsync(this ITradeQueryContext context,
        int orderId, int tradeId, TradeType tradeType, DateOnly valueDate,
        DateTime startDate, DateTime endDate)
    {
        try
        {
            OptionTradeSpreadBarsDataModel[] result =
                [.. await context.DbFactory.TradeDb.GetOptionTradeSpreadBarDataAsync(
                    orderId,
                    tradeId,
                    valueDate,
                    tradeType,
                    startDate,
                    endDate)];
            return new ServiceOk<OptionTradeSpreadBarsDataModel[]>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<OptionTradeSpreadBarsDataModel[]>(
                GetOptionTradeSpreadBarDataQuery.ErrorId,
                ex.Message);
        }
    }

    /// <summary>
    /// Gets option trades.
    /// </summary>
    /// <param name="orderId">The order identifier.</param>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
    public static async Task<ServiceResult<OptionTradeReadModel[]>> GetOptionTradesAsync(this ITradeQueryContext context, int orderId)
    {
        try
        {
            OptionTradeReadModel[] result = [.. await context.DbFactory.TradeDb.GetOptionTradesAsync(orderId)];
            return new ServiceOk<OptionTradeReadModel[]>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<OptionTradeReadModel[]>(GetOptionTradesQuery.ErrorId, ex.Message);
        }
    }

    /// <summary>
    /// Gets trade positions.
    /// </summary>
    /// <param name="orderId">The order identifier.</param>
    /// <param name="tradeId">The trade identifier.</param>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
    public static async Task<ServiceResult<TradePositionReadModel[]>> GetTradePositionsAsync(this ITradeQueryContext context, int orderId, int tradeId)
    {
        try
        {
            TradePositionReadModel[] result =
                [.. await context.DbFactory.TradeDb.GetTradePositionsAsync(orderId, tradeId)];
            return new ServiceOk<TradePositionReadModel[]>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<TradePositionReadModel[]>(GetTradePositionsQuery.ErrorId, ex.Message);
        }
    }

    /// <summary>
    /// Gets trade position.
    /// </summary>
    /// <param name="orderId">The order identifier.</param>
    /// <param name="tradeId">The trade identifier.</param>
    /// <param name="tradeType">The trade strategy type.</param>
    /// <param name="valueDate">The applicable market value date.</param>
    /// <param name="daysToExpiry">The number of days remaining until expiry.</param>
    /// <param name="tradeStatus">The trade lifecycle status.</param>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
    public static async Task<ServiceResult<TradePositionReadModel>> GetTradePositionAsync(this ITradeQueryContext context,
        int orderId, int tradeId, TradeType tradeType, DateOnly valueDate,
        int daysToExpiry, TradeStatus tradeStatus)
    {
        try
        {
            TradePositionReadModel result =
                (await context.DbFactory.TradeDb.GetTradePositionAsync(
                    orderId,
                    tradeId,
                    tradeType,
                    valueDate,
                    daysToExpiry,
                    tradeStatus))!;
            return new ServiceOk<TradePositionReadModel>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<TradePositionReadModel>(GetTradePositionQuery.ErrorId, ex.Message);
        }
    }

    /// <summary>
    /// Gets iron condor trade price.
    /// </summary>
    /// <param name="tradeId">The trade identifier.</param>
    /// <param name="valueDate">The applicable market value date.</param>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
    public static async Task<ServiceResult<TradePriceReadModel>> GetIronCondorTradePriceAsync(this ITradeQueryContext context,
        int tradeId, DateOnly valueDate)
    {
        try
        {
            TradePriceReadModel result =
                (await context.DbFactory.TradeDb.GetIronCondorTradePriceAsync(tradeId, valueDate))!;
            return new ServiceOk<TradePriceReadModel>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<TradePriceReadModel>(GetIronCondorTradePriceQuery.ErrorId, ex.Message);
        }
    }

    /// <summary>
    /// Gets trade plan summary.
    /// </summary>
    /// <param name="orderId">The order identifier.</param>
    /// <param name="tradeId">The trade identifier.</param>
    /// <param name="valueDate">The applicable market value date.</param>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
    public static Task<ServiceResult<TradePlanActionReadModel[]>> GetTradePlanSummaryAsync(this ITradeQueryContext context,
        int orderId, int tradeId, DateOnly valueDate)
        => throw new NotImplementedException(
            $"{nameof(GetTradePlanSummaryAsync)} is no longer supported and will be removed during the UI refactor.");

    /// <summary>
    /// Gets trade position trade types.
    /// </summary>
    /// <param name="orderId">The order identifier.</param>
    /// <param name="tradeId">The trade identifier.</param>
    /// <param name="valueDate">The applicable market value date.</param>
    /// <param name="daysToExpiry">The number of days remaining until expiry.</param>
    /// <param name="tradeStatus">The trade lifecycle status.</param>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
    public static async Task<ServiceResult<string[]>> GetTradePositionTradeTypesAsync(this ITradeQueryContext context,
        int orderId, int tradeId, DateOnly valueDate, int daysToExpiry, TradeStatus tradeStatus)
    {
        try
        {
            string[] result = [.. await context.DbFactory.TradeDb.GetTradePositionTradeTypesAsync(
                orderId,
                tradeId,
                valueDate,
                tradeStatus,
                daysToExpiry)];
            return new ServiceOk<string[]>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<string[]>(GetTradePositionTradeTypesQuery.ErrorId, ex.Message);
        }
    }

    /// <summary>
    /// Gets iron condor MDI limit.
    /// </summary>
    /// <param name="orderId">The order identifier.</param>
    /// <param name="tradeId">The trade identifier.</param>
    /// <param name="valueDate">The applicable market value date.</param>
    /// <returns>A task containing the typed success result or the operation-specific failure result.</returns>
    public static Task<ServiceResult<IronCondorMDILimitDataModel>> GetIronCondorMDILimitAsync(this ITradeQueryContext context,
        int orderId, int tradeId, DateOnly valueDate)
    {
        try
        {
            IronCondorMDILimitDataModel result = context.BlackboardService.Trade.IronCondorMDILimit.Get(
                new OptionTradeEntityId(orderId, tradeId),
                valueDate)!;
            return Task.FromResult<ServiceResult<IronCondorMDILimitDataModel>>(
                new ServiceOk<IronCondorMDILimitDataModel>(result));
        }
        catch (Exception ex)
        {
            return Task.FromResult<ServiceResult<IronCondorMDILimitDataModel>>(
                new ServiceFailed<IronCondorMDILimitDataModel>(
                    GetIronCondorMDILimitQuery.ErrorId,
                    ex.Message));
        }
    }
}
