using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Queries;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Query.Api;

public sealed partial class ActorTradeQueryApi
{
    public Task<ServiceResult<TradeHistoryReadModel[]>> GetTradeHistoryAsync(
        int orderId,
        CancellationToken cancellationToken)
        => ExecuteAsync<TradeHistoryReadModel[]>(
            GetTradeHistoryQuery.ErrorId,
            cancellationToken,
            async () => [.. await _dbFactory.TradeDb
                .GetTradeHistoryAsync(orderId, cancellationToken)]);

    public Task<ServiceResult<string[]>> GetOptionLegContractIdsAsync(
        int tradeId,
        CancellationToken cancellationToken)
        => ExecuteAsync<string[]>(
            GetOptionLegContractIdsQuery.ErrorId,
            cancellationToken,
            async () => [.. await _dbFactory.TradeDb
                .GetOptionLegContractIdsAsync(tradeId, cancellationToken)]);

    public Task<ServiceResult<TradeLimitReadModel>> GetTradeLimitAsync(
        int tradeId,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            GetTradeLimitQuery.ErrorId,
            cancellationToken,
            async () => (await _dbFactory.TradeDb
                .GetTradeLimitAsync(tradeId, cancellationToken))!);

    public Task<ServiceResult<TradeTypeLimitReadModel>> GetTradeTypeLimitAsync(
        int tradeId,
        TradeType tradeType,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            GetTradeTypeLimitQuery.ErrorId,
            cancellationToken,
            async () => (await _dbFactory.TradeDb
                .GetTradeTypeLimitAsync(tradeId, tradeType, cancellationToken))!);

    public Task<ServiceResult<ScalarReadModel<int>>> GetTradeQuantityAsync(
        int tradeId,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            GetTradeQuantityQuery.ErrorId,
            cancellationToken,
            async () => new ScalarReadModel<int>(await _dbFactory.TradeDb
                .GetTradeQuantityAsync(tradeId, cancellationToken)));

    public Task<ServiceResult<OptionTradeReadModel>> GetOptionTradeAsync(
        int orderId,
        int tradeId,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            GetOptionTradeQuery.ErrorId,
            cancellationToken,
            async () => (await _dbFactory.TradeDb
                .GetOptionTradeAsync(orderId, tradeId, cancellationToken))!);

    public Task<ServiceResult<OptionTradeSpreadsDataModel>> GetOptionTradeSpreadDataAsync(
        int orderId,
        int tradeId,
        TradeType tradeType,
        DateOnly valueDate,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            GetOptionTradeSpreadDataQuery.ErrorId,
            cancellationToken,
            async () => (await _dbFactory.TradeDb.GetOptionTradeSpreadDataAsync(
                orderId,
                tradeId,
                valueDate,
                tradeType,
                cancellationToken))!);

    public Task<ServiceResult<OptionTradeSpreadBarsDataModel[]>> GetOptionTradeSpreadBarDataAsync(
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
            async () => [.. await _dbFactory.TradeDb.GetOptionTradeSpreadBarDataAsync(
                orderId,
                tradeId,
                valueDate,
                tradeType,
                startDate,
                endDate,
                cancellationToken)]);

    public Task<ServiceResult<OptionTradeReadModel[]>> GetOptionTradesAsync(
        int orderId,
        CancellationToken cancellationToken)
        => ExecuteAsync<OptionTradeReadModel[]>(
            GetOptionTradesQuery.ErrorId,
            cancellationToken,
            async () => [.. await _dbFactory.TradeDb
                .GetOptionTradesAsync(orderId, cancellationToken)]);

    public Task<ServiceResult<TradePositionReadModel[]>> GetTradePositionsAsync(
        int orderId,
        int tradeId,
        CancellationToken cancellationToken)
        => ExecuteAsync<TradePositionReadModel[]>(
            GetTradePositionsQuery.ErrorId,
            cancellationToken,
            async () => [.. await _dbFactory.TradeDb
                .GetTradePositionsAsync(orderId, tradeId, cancellationToken)]);

    public Task<ServiceResult<TradePositionReadModel>> GetTradePositionAsync(
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
            async () => (await _dbFactory.TradeDb.GetTradePositionAsync(
                orderId,
                tradeId,
                tradeType,
                valueDate,
                daysToExpiry,
                tradeStatus,
                cancellationToken))!);

    public Task<ServiceResult<TradePriceReadModel>> GetIronCondorTradePriceAsync(
        int tradeId,
        DateOnly valueDate,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            GetIronCondorTradePriceQuery.ErrorId,
            cancellationToken,
            async () => (await _dbFactory.TradeDb
                .GetIronCondorTradePriceAsync(tradeId, valueDate, cancellationToken))!);

    public Task<ServiceResult<TradePlanActionReadModel[]>> GetTradePlanSummaryAsync(
        int orderId,
        int tradeId,
        DateOnly valueDate,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        throw new NotImplementedException(
            $"{nameof(GetTradePlanSummaryAsync)} is no longer supported and will be removed during the UI refactor.");
    }

    public Task<ServiceResult<string[]>> GetTradePositionTradeTypesAsync(
        int orderId,
        int tradeId,
        DateOnly valueDate,
        int daysToExpiry,
        TradeStatus tradeStatus,
        CancellationToken cancellationToken)
        => ExecuteAsync<string[]>(
            GetTradePositionTradeTypesQuery.ErrorId,
            cancellationToken,
            async () => [.. await _dbFactory.TradeDb.GetTradePositionTradeTypesAsync(
                orderId,
                tradeId,
                valueDate,
                tradeStatus,
                daysToExpiry,
                cancellationToken)]);

    public Task<ServiceResult<IronCondorMDILimitDataModel>> GetIronCondorMDILimitAsync(
        int orderId,
        int tradeId,
        DateOnly valueDate,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            GetIronCondorMDILimitQuery.ErrorId,
            cancellationToken,
            () => Task.FromResult(_blackboardService.Trade.IronCondorMDILimit.Get(
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
