using TomasAI.IFM.Domain.Trade.Queries;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Queries;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Query.Extensions;

/// <summary>Provides the TradeQueryExtensions implementation.</summary>
public static partial class TradeQueryExtensions
{
    /// <summary>Executes the GetTradeHistoryAsync operation.</summary>
    public static Task<ServiceResult<TradeHistoryReadModel[]>> GetTradeHistoryAsync(this ITradeQueryContext context,
        int orderId,
        CancellationToken cancellationToken)
        => ExecuteAsync<TradeHistoryReadModel[]>(
            GetTradeHistoryQuery.ErrorId,
            cancellationToken,
            async () => [.. await context.DbFactory.TradeDb
                .GetTradeHistoryAsync(orderId, cancellationToken)]);

    /// <summary>Executes the GetOptionLegContractIdsAsync operation.</summary>
    public static Task<ServiceResult<string[]>> GetOptionLegContractIdsAsync(this ITradeQueryContext context,
        int tradeId,
        CancellationToken cancellationToken)
        => ExecuteAsync<string[]>(
            GetOptionLegContractIdsQuery.ErrorId,
            cancellationToken,
            async () => [.. await context.DbFactory.TradeDb
                .GetOptionLegContractIdsAsync(tradeId, cancellationToken)]);

    /// <summary>Executes the GetTradeLimitAsync operation.</summary>
    public static Task<ServiceResult<TradeLimitReadModel>> GetTradeLimitAsync(this ITradeQueryContext context,
        int tradeId,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            GetTradeLimitQuery.ErrorId,
            cancellationToken,
            async () => (await context.DbFactory.TradeDb
                .GetTradeLimitAsync(tradeId, cancellationToken))!);

    /// <summary>Executes the GetTradeTypeLimitAsync operation.</summary>
    public static Task<ServiceResult<TradeTypeLimitReadModel>> GetTradeTypeLimitAsync(this ITradeQueryContext context,
        int tradeId,
        TradeType tradeType,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            GetTradeTypeLimitQuery.ErrorId,
            cancellationToken,
            async () => (await context.DbFactory.TradeDb
                .GetTradeTypeLimitAsync(tradeId, tradeType, cancellationToken))!);

    /// <summary>Executes the GetTradeQuantityAsync operation.</summary>
    public static Task<ServiceResult<ScalarReadModel<int>>> GetTradeQuantityAsync(this ITradeQueryContext context,
        int tradeId,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            GetTradeQuantityQuery.ErrorId,
            cancellationToken,
            async () => new ScalarReadModel<int>(await context.DbFactory.TradeDb
                .GetTradeQuantityAsync(tradeId, cancellationToken)));

    /// <summary>Executes the GetOptionTradeAsync operation.</summary>
    public static Task<ServiceResult<OptionTradeReadModel>> GetOptionTradeAsync(this ITradeQueryContext context,
        int orderId,
        int tradeId,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            GetOptionTradeQuery.ErrorId,
            cancellationToken,
            async () => (await context.DbFactory.TradeDb
                .GetOptionTradeAsync(orderId, tradeId, cancellationToken))!);

    /// <summary>Executes the GetOptionTradeSpreadDataAsync operation.</summary>
    public static Task<ServiceResult<OptionTradeSpreadsDataModel>> GetOptionTradeSpreadDataAsync(this ITradeQueryContext context,
        int orderId,
        int tradeId,
        TradeType tradeType,
        DateOnly valueDate,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            GetOptionTradeSpreadDataQuery.ErrorId,
            cancellationToken,
            async () => (await context.DbFactory.TradeDb.GetOptionTradeSpreadDataAsync(
                orderId,
                tradeId,
                valueDate,
                tradeType,
                cancellationToken))!);

    /// <summary>Executes the GetOptionTradeSpreadBarDataAsync operation.</summary>
    public static Task<ServiceResult<OptionTradeSpreadBarsDataModel[]>> GetOptionTradeSpreadBarDataAsync(this ITradeQueryContext context,
        int orderId,
        int tradeId,
        TradeType tradeType,
        DateOnly valueDate,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken)
        => ExecuteAsync<OptionTradeSpreadBarsDataModel[]>(
            GetOptionTradeSpreadBarDataQuery.ErrorId,
            cancellationToken,
            async () => [.. await context.DbFactory.TradeDb.GetOptionTradeSpreadBarDataAsync(
                orderId,
                tradeId,
                valueDate,
                tradeType,
                startDate,
                endDate,
                cancellationToken)]);

    /// <summary>Executes the GetOptionTradesAsync operation.</summary>
    public static Task<ServiceResult<OptionTradeReadModel[]>> GetOptionTradesAsync(this ITradeQueryContext context,
        int orderId,
        CancellationToken cancellationToken)
        => ExecuteAsync<OptionTradeReadModel[]>(
            GetOptionTradesQuery.ErrorId,
            cancellationToken,
            async () => [.. await context.DbFactory.TradeDb
                .GetOptionTradesAsync(orderId, cancellationToken)]);

    /// <summary>Executes the GetTradePositionsAsync operation.</summary>
    public static Task<ServiceResult<TradePositionReadModel[]>> GetTradePositionsAsync(this ITradeQueryContext context,
        int orderId,
        int tradeId,
        CancellationToken cancellationToken)
        => ExecuteAsync<TradePositionReadModel[]>(
            GetTradePositionsQuery.ErrorId,
            cancellationToken,
            async () => [.. await context.DbFactory.TradeDb
                .GetTradePositionsAsync(orderId, tradeId, cancellationToken)]);

    /// <summary>Executes the GetTradePositionAsync operation.</summary>
    public static Task<ServiceResult<TradePositionReadModel>> GetTradePositionAsync(this ITradeQueryContext context,
        int orderId,
        int tradeId,
        TradeType tradeType,
        DateOnly valueDate,
        int daysToExpiry,
        TradeStatus tradeStatus,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            GetTradePositionQuery.ErrorId,
            cancellationToken,
            async () => (await context.DbFactory.TradeDb.GetTradePositionAsync(
                orderId,
                tradeId,
                tradeType,
                valueDate,
                daysToExpiry,
                tradeStatus,
                cancellationToken))!);

    /// <summary>Executes the GetIronCondorTradePriceAsync operation.</summary>
    public static Task<ServiceResult<TradePriceReadModel>> GetIronCondorTradePriceAsync(this ITradeQueryContext context,
        int tradeId,
        DateOnly valueDate,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            GetIronCondorTradePriceQuery.ErrorId,
            cancellationToken,
            async () => (await context.DbFactory.TradeDb
                .GetIronCondorTradePriceAsync(tradeId, valueDate, cancellationToken))!);

    /// <summary>Executes the GetTradePlanSummaryAsync operation.</summary>
    public static Task<ServiceResult<TradePlanActionReadModel[]>> GetTradePlanSummaryAsync(this ITradeQueryContext context,
        int orderId,
        int tradeId,
        DateOnly valueDate,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        throw new NotImplementedException(
            $"{nameof(GetTradePlanSummaryAsync)} is no longer supported and will be removed during the UI refactor.");
    }

    /// <summary>Executes the GetTradePositionTradeTypesAsync operation.</summary>
    public static Task<ServiceResult<string[]>> GetTradePositionTradeTypesAsync(this ITradeQueryContext context,
        int orderId,
        int tradeId,
        DateOnly valueDate,
        int daysToExpiry,
        TradeStatus tradeStatus,
        CancellationToken cancellationToken)
        => ExecuteAsync<string[]>(
            GetTradePositionTradeTypesQuery.ErrorId,
            cancellationToken,
            async () => [.. await context.DbFactory.TradeDb.GetTradePositionTradeTypesAsync(
                orderId,
                tradeId,
                valueDate,
                tradeStatus,
                daysToExpiry,
                cancellationToken)]);

    /// <summary>Executes the GetIronCondorMDILimitAsync operation.</summary>
    public static Task<ServiceResult<IronCondorMDILimitDataModel>> GetIronCondorMDILimitAsync(this ITradeQueryContext context,
        int orderId,
        int tradeId,
        DateOnly valueDate,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            GetIronCondorMDILimitQuery.ErrorId,
            cancellationToken,
            () => Task.FromResult(context.BlackboardService.Trade.IronCondorMDILimit.Get(
                new OptionTradeEntityId(orderId, tradeId),
                valueDate)!));

    static async Task<ServiceResult<T>> ExecuteAsync<T>(
        int errorId,
        CancellationToken cancellationToken,
        Func<Task<T>> operation)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await operation().ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return new ServiceOk<T>(result);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new ServiceFailed<T>(errorId, ex.Message);
        }
    }
}
