using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Queries;
using TomasAI.IFM.Domain.Trade.Shared.ServiceApi;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Trade.Query.Api;

/// <summary>Provides direct, in-process Trade queries without actor messaging.</summary>
public sealed class ActorTradeQueryApi(
    IDbContextFactory dbFactory,
    IBlackboardService blackboardService) : IActorTradeQueryApi
{
    readonly IDbContextFactory _dbFactory = IsArgumentNull.Set(dbFactory);
    readonly IBlackboardService _blackboardService = IsArgumentNull.Set(blackboardService);

    public Task<ServiceResult<TradeHistoryReadModel[]>> GetTradeHistoryAsync(int orderId)
        => ExecuteAsync<TradeHistoryReadModel[]>(GetTradeHistoryQuery.ErrorId,
            async () => [.. await _dbFactory.TradeDb.GetTradeHistoryAsync(orderId)]);

    public Task<ServiceResult<string[]>> GetOptionLegContractIdsAsync(int tradeId)
        => ExecuteAsync<string[]>(GetOptionLegContractIdsQuery.ErrorId,
            async () => [.. await _dbFactory.TradeDb.GetOptionLegContractIdsAsync(tradeId)]);

    public Task<ServiceResult<TradeLimitReadModel>> GetTradeLimitAsync(int tradeId)
        => ExecuteAsync(GetTradeLimitQuery.ErrorId,
            async () => (await _dbFactory.TradeDb.GetTradeLimitAsync(tradeId))!);

    public Task<ServiceResult<TradeTypeLimitReadModel>> GetTradeTypeLimitAsync(
        int tradeId, TradeType tradeType)
        => ExecuteAsync(GetTradeTypeLimitQuery.ErrorId,
            async () => (await _dbFactory.TradeDb.GetTradeTypeLimitAsync(tradeId, tradeType))!);

    public Task<ServiceResult<ScalarReadModel<int>>> GetTradeQuantityAsync(int tradeId)
        => ExecuteAsync(GetTradeQuantityQuery.ErrorId,
            async () => new ScalarReadModel<int>(await _dbFactory.TradeDb.GetTradeQuantityAsync(tradeId)));

    public Task<ServiceResult<OptionTradeReadModel>> GetOptionTradeAsync(int orderId, int tradeId)
        => ExecuteAsync(GetOptionTradeQuery.ErrorId,
            async () => (await _dbFactory.TradeDb.GetOptionTradeAsync(orderId, tradeId))!);

    public Task<ServiceResult<OptionTradeSpreadsDataModel>> GetOptionTradeSpreadDataAsync(
        int orderId, int tradeId, TradeType tradeType, DateOnly valueDate)
        => ExecuteAsync(GetOptionTradeSpreadDataQuery.ErrorId,
            async () => (await _dbFactory.TradeDb.GetOptionTradeSpreadDataAsync(
                orderId, tradeId, valueDate, tradeType))!);

    public Task<ServiceResult<OptionTradeSpreadBarsDataModel[]>> GetOptionTradeSpreadBarDataAsync(
        int orderId, int tradeId, TradeType tradeType, DateOnly valueDate,
        DateTime startDate, DateTime endDate)
        => ExecuteAsync<OptionTradeSpreadBarsDataModel[]>(GetOptionTradeSpreadBarDataQuery.ErrorId,
            async () => [.. await _dbFactory.TradeDb.GetOptionTradeSpreadBarDataAsync(
                orderId, tradeId, valueDate, tradeType, startDate, endDate)]);

    public Task<ServiceResult<OptionTradeReadModel[]>> GetOptionTradesAsync(int orderId)
        => ExecuteAsync<OptionTradeReadModel[]>(GetOptionTradesQuery.ErrorId,
            async () => [.. await _dbFactory.TradeDb.GetOptionTradesAsync(orderId)]);

    public Task<ServiceResult<TradePositionReadModel[]>> GetTradePositionsAsync(int orderId, int tradeId)
        => ExecuteAsync<TradePositionReadModel[]>(GetTradePositionsQuery.ErrorId,
            async () => [.. await _dbFactory.TradeDb.GetTradePositionsAsync(orderId, tradeId)]);

    public Task<ServiceResult<TradePositionReadModel>> GetTradePositionAsync(
        int orderId, int tradeId, TradeType tradeType, DateOnly valueDate,
        int daysToExpiry, TradeStatus tradeStatus)
        => ExecuteAsync(GetTradePositionQuery.ErrorId,
            async () => (await _dbFactory.TradeDb.GetTradePositionAsync(
                orderId, tradeId, tradeType, valueDate, daysToExpiry, tradeStatus))!);

    public Task<ServiceResult<TradePriceReadModel>> GetIronCondorTradePriceAsync(
        int tradeId, DateOnly valueDate)
        => ExecuteAsync(GetIronCondorTradePriceQuery.ErrorId,
            async () => (await _dbFactory.TradeDb.GetIronCondorTradePriceAsync(tradeId, valueDate))!);

    public Task<ServiceResult<TradePlanActionReadModel[]>> GetTradePlanSummaryAsync(
        int orderId, int tradeId, DateOnly valueDate)
        => throw new NotImplementedException(
            $"{nameof(GetTradePlanSummaryAsync)} is no longer supported and will be removed during the UI refactor.");

    public Task<ServiceResult<string[]>> GetTradePositionTradeTypesAsync(
        int orderId, int tradeId, DateOnly valueDate, int daysToExpiry, TradeStatus tradeStatus)
        => ExecuteAsync<string[]>(GetTradePositionTradeTypesQuery.ErrorId,
            async () => [.. await _dbFactory.TradeDb.GetTradePositionTradeTypesAsync(
                orderId, tradeId, valueDate, tradeStatus, daysToExpiry)]);

    public Task<ServiceResult<IronCondorMDILimitDataModel>> GetIronCondorMDILimitAsync(
        int orderId, int tradeId, DateOnly valueDate)
        => ExecuteAsync(GetIronCondorMDILimitQuery.ErrorId, () => Task.FromResult(
            _blackboardService.Trade.IronCondorMDILimit.Get(
                new OptionTradeEntityId(orderId, tradeId), valueDate)!));

    static async Task<ServiceResult<T>> ExecuteAsync<T>(int errorId, Func<Task<T>> query)
    {
        try
        {
            return new ServiceOk<T>(await query());
        }
        catch (Exception ex)
        {
            return new ServiceFailed<T>(errorId, ex.Message);
        }
    }
}
