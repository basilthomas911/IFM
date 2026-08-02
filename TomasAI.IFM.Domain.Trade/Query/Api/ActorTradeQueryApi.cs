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

    public async Task<ServiceResult<TradeHistoryReadModel[]>> GetTradeHistoryAsync(int orderId)
    {
        try
        {
            TradeHistoryReadModel[] result = [.. await _dbFactory.TradeDb.GetTradeHistoryAsync(orderId)];
            return new ServiceOk<TradeHistoryReadModel[]>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<TradeHistoryReadModel[]>(GetTradeHistoryQuery.ErrorId, ex.Message);
        }
    }

    public async Task<ServiceResult<string[]>> GetOptionLegContractIdsAsync(int tradeId)
    {
        try
        {
            string[] result = [.. await _dbFactory.TradeDb.GetOptionLegContractIdsAsync(tradeId)];
            return new ServiceOk<string[]>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<string[]>(GetOptionLegContractIdsQuery.ErrorId, ex.Message);
        }
    }

    public async Task<ServiceResult<TradeLimitReadModel>> GetTradeLimitAsync(int tradeId)
    {
        try
        {
            TradeLimitReadModel result = (await _dbFactory.TradeDb.GetTradeLimitAsync(tradeId))!;
            return new ServiceOk<TradeLimitReadModel>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<TradeLimitReadModel>(GetTradeLimitQuery.ErrorId, ex.Message);
        }
    }

    public async Task<ServiceResult<TradeTypeLimitReadModel>> GetTradeTypeLimitAsync(
        int tradeId, TradeType tradeType)
    {
        try
        {
            TradeTypeLimitReadModel result =
                (await _dbFactory.TradeDb.GetTradeTypeLimitAsync(tradeId, tradeType))!;
            return new ServiceOk<TradeTypeLimitReadModel>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<TradeTypeLimitReadModel>(GetTradeTypeLimitQuery.ErrorId, ex.Message);
        }
    }

    public async Task<ServiceResult<ScalarReadModel<int>>> GetTradeQuantityAsync(int tradeId)
    {
        try
        {
            var result = new ScalarReadModel<int>(await _dbFactory.TradeDb.GetTradeQuantityAsync(tradeId));
            return new ServiceOk<ScalarReadModel<int>>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<ScalarReadModel<int>>(GetTradeQuantityQuery.ErrorId, ex.Message);
        }
    }

    public async Task<ServiceResult<OptionTradeReadModel>> GetOptionTradeAsync(int orderId, int tradeId)
    {
        try
        {
            OptionTradeReadModel result = (await _dbFactory.TradeDb.GetOptionTradeAsync(orderId, tradeId))!;
            return new ServiceOk<OptionTradeReadModel>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<OptionTradeReadModel>(GetOptionTradeQuery.ErrorId, ex.Message);
        }
    }

    public async Task<ServiceResult<OptionTradeSpreadsDataModel>> GetOptionTradeSpreadDataAsync(
        int orderId, int tradeId, TradeType tradeType, DateOnly valueDate)
    {
        try
        {
            OptionTradeSpreadsDataModel result =
                (await _dbFactory.TradeDb.GetOptionTradeSpreadDataAsync(
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

    public async Task<ServiceResult<OptionTradeSpreadBarsDataModel[]>> GetOptionTradeSpreadBarDataAsync(
        int orderId, int tradeId, TradeType tradeType, DateOnly valueDate,
        DateTime startDate, DateTime endDate)
    {
        try
        {
            OptionTradeSpreadBarsDataModel[] result =
                [.. await _dbFactory.TradeDb.GetOptionTradeSpreadBarDataAsync(
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

    public async Task<ServiceResult<OptionTradeReadModel[]>> GetOptionTradesAsync(int orderId)
    {
        try
        {
            OptionTradeReadModel[] result = [.. await _dbFactory.TradeDb.GetOptionTradesAsync(orderId)];
            return new ServiceOk<OptionTradeReadModel[]>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<OptionTradeReadModel[]>(GetOptionTradesQuery.ErrorId, ex.Message);
        }
    }

    public async Task<ServiceResult<TradePositionReadModel[]>> GetTradePositionsAsync(int orderId, int tradeId)
    {
        try
        {
            TradePositionReadModel[] result =
                [.. await _dbFactory.TradeDb.GetTradePositionsAsync(orderId, tradeId)];
            return new ServiceOk<TradePositionReadModel[]>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<TradePositionReadModel[]>(GetTradePositionsQuery.ErrorId, ex.Message);
        }
    }

    public async Task<ServiceResult<TradePositionReadModel>> GetTradePositionAsync(
        int orderId, int tradeId, TradeType tradeType, DateOnly valueDate,
        int daysToExpiry, TradeStatus tradeStatus)
    {
        try
        {
            TradePositionReadModel result =
                (await _dbFactory.TradeDb.GetTradePositionAsync(
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

    public async Task<ServiceResult<TradePriceReadModel>> GetIronCondorTradePriceAsync(
        int tradeId, DateOnly valueDate)
    {
        try
        {
            TradePriceReadModel result =
                (await _dbFactory.TradeDb.GetIronCondorTradePriceAsync(tradeId, valueDate))!;
            return new ServiceOk<TradePriceReadModel>(result);
        }
        catch (Exception ex)
        {
            return new ServiceFailed<TradePriceReadModel>(GetIronCondorTradePriceQuery.ErrorId, ex.Message);
        }
    }

    public Task<ServiceResult<TradePlanActionReadModel[]>> GetTradePlanSummaryAsync(
        int orderId, int tradeId, DateOnly valueDate)
        => throw new NotImplementedException(
            $"{nameof(GetTradePlanSummaryAsync)} is no longer supported and will be removed during the UI refactor.");

    public async Task<ServiceResult<string[]>> GetTradePositionTradeTypesAsync(
        int orderId, int tradeId, DateOnly valueDate, int daysToExpiry, TradeStatus tradeStatus)
    {
        try
        {
            string[] result = [.. await _dbFactory.TradeDb.GetTradePositionTradeTypesAsync(
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

    public Task<ServiceResult<IronCondorMDILimitDataModel>> GetIronCondorMDILimitAsync(
        int orderId, int tradeId, DateOnly valueDate)
    {
        try
        {
            IronCondorMDILimitDataModel result = _blackboardService.Trade.IronCondorMDILimit.Get(
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
