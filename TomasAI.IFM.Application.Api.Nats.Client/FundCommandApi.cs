using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Domain.Fund.Shared;
using TomasAI.IFM.Domain.Fund.Shared.Commands;
using TomasAI.IFM.Domain.Fund.Shared.ServiceApi;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;

namespace TomasAI.IFM.Application.Api.Nats.Client;

/// <summary>
/// 
/// </summary>
/// <param name="actorProducer"></param>
public class FundCommandApi(IActorProducer actorProducer) 
    : NatsCommandApi(actorProducer), IFundCommandApi
{
    /// <summary>
    /// create fund
    /// </summary>
    /// <param name="newFund"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> CreateFundAsync(FundReadModel newFund)
    { 
        Guid cmdId = Guid.NewGuid();
        ServiceResult<Guid> serviceResult;
        try
        {
            var entityId = new FundId(newFund.FundId);
            CreateFundCommand cmd = new(newFund)
            {
                CommandId = cmdId,
                Subject = new ActorSubject(ActorType.Command, CreateFundCommand.Actor, CreateFundCommand.Verb, entityId.Format()),
                ErrorCode = CreateFundCommand.ErrorId
            };
            serviceResult = await SendAsync(cmd!, entityId);
        }
        catch (Exception ex)
        {
            serviceResult = OnError(ex, cmdId, CreateFundCommand.ErrorId);
        }
        return serviceResult;
    }


    /// <summary>
    /// add order to fund
    /// </summary>
    /// <param name="fundOrder"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> AddOrderToFundAsync(FundOrderReadModel fundOrder)
    {
        Guid cmdId = Guid.NewGuid();
        ServiceResult<Guid> serviceResult;
        try
        {
            var entityId = new FundId(fundOrder.FundId);
            AddOrderToFundCommand cmd = new(fundOrder)
            {
                CommandId = cmdId,
                Subject = new ActorSubject(ActorType.Command, AddOrderToFundCommand.Actor, AddOrderToFundCommand.Verb, entityId.Format()),
                ErrorCode = AddOrderToFundCommand.ErrorId
            };
            serviceResult = await SendAsync(cmd!, entityId);
        }
        catch (Exception ex)
        {
            serviceResult = OnError(ex, cmdId, AddOrderToFundCommand.ErrorId);
        }
        return serviceResult;
    }

    /// <summary>
    /// remove order from fund
    /// </summary>
    /// <param name="fundOrderId"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> RemoveOrderFromFundAsync(FundOrderId fundOrderId)
    { 
        Guid cmdId = Guid.NewGuid();
        ServiceResult<Guid> serviceResult;
        try
        {
            var entityId = new FundId(fundOrderId.FundId);
            RemoveOrderFromFundCommand cmd = new(fundOrderId)
            {
                CommandId = cmdId,
                Subject = new ActorSubject(ActorType.Command, RemoveOrderFromFundCommand.Actor, RemoveOrderFromFundCommand.Verb, entityId.Format()),
                ErrorCode = RemoveOrderFromFundCommand.ErrorId
            };
            serviceResult = await SendAsync(cmd!, entityId);
        }
        catch (Exception ex)
        {
            serviceResult = OnError(ex, cmdId, RemoveOrderFromFundCommand.ErrorId);
        }
        return serviceResult;
    }

    /// <summary>
    /// add trade to fund order
    /// </summary>
    /// <param name="fundOrderTrade"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> AddTradeToFundOrderAsync(FundOrderTradeReadModel fundOrderTrade)
    {
        Guid cmdId = Guid.NewGuid();
        ServiceResult<Guid> serviceResult;
        try
        {
            var entityId = new FundId(fundOrderTrade.FundId);
            AddTradeToFundOrderCommand cmd = new(fundOrderTrade)
            {
                CommandId = cmdId,
                Subject = new ActorSubject(ActorType.Command, AddTradeToFundOrderCommand.Actor, AddTradeToFundOrderCommand.Verb, entityId.Format()),
                ErrorCode = AddTradeToFundOrderCommand.ErrorId
            };
            serviceResult = await SendAsync(cmd!, entityId);
        }
        catch (Exception ex)
        {
            serviceResult = OnError(ex, cmdId, AddTradeToFundOrderCommand.ErrorId);
        }
        return serviceResult;
    }

    /// <summary>
    /// remove trade from fund order
    /// </summary>
    /// <param name="fundOrderTradeId"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> RemoveTradeFromFundOrderAsync(FundOrderTradeId fundOrderTradeId)
    {
        Guid cmdId = Guid.NewGuid();
        ServiceResult<Guid> serviceResult;
        try
        {
            var entityId = new FundId(fundOrderTradeId.FundId);
            RemoveTradeFromFundOrderCommand cmd = new(fundOrderTradeId)
            {
                CommandId = cmdId,
                Subject = new ActorSubject(ActorType.Command, RemoveTradeFromFundOrderCommand.Actor, RemoveTradeFromFundOrderCommand.Verb, entityId.Format()),
                ErrorCode = RemoveTradeFromFundOrderCommand.ErrorId
            };
            serviceResult = await SendAsync(cmd!, entityId);
        }
        catch (Exception ex)
        {
            serviceResult = OnError(ex, cmdId, RemoveTradeFromFundOrderCommand.ErrorId);
        }
        return serviceResult;
    }

    /// <summary>
    /// close fund order from any more changes
    /// </summary>
    /// <param name="fundOrderId"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> CloseFundOrderAsync(FundOrderId fundOrderId)
    {
        Guid cmdId = Guid.NewGuid();
        ServiceResult<Guid> serviceResult;
        try
        {
            var entityId = new FundId(fundOrderId.FundId);
            CloseFundOrderCommand cmd = new(fundOrderId)
            {
                CommandId = cmdId,
                Subject = new ActorSubject(ActorType.Command, CloseFundOrderCommand.Actor, CloseFundOrderCommand.Verb, entityId.Format()),
                ErrorCode = CloseFundOrderCommand.ErrorId
            };
            serviceResult = await SendAsync(cmd!, entityId);
        }
        catch (Exception ex)
        {
            serviceResult = OnError(ex, cmdId, CloseFundOrderCommand.ErrorId);
        }
        return serviceResult;
    }

    /// <summary>
    /// change fund order trade state
    /// </summary>
    /// <param name="fundOrderTradeId"></param>
    /// <param name="tradeState"></param>
    /// <param name="correlationId"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> ChangeFundOrderTradeStateAsync(FundOrderTradeId fundOrderTradeId, TradeState tradeState, Guid correlationId)
    {
        Guid cmdId = Guid.NewGuid();
        ServiceResult<Guid> serviceResult;
        try
        {
            var entityId = new FundId(fundOrderTradeId.FundId);
            ChangeFundOrderTradeStateCommand cmd = new(fundOrderTradeId, tradeState)
            {
                CommandId = cmdId,
                Subject = new ActorSubject(ActorType.Command, ChangeFundOrderTradeStateCommand.Actor, ChangeFundOrderTradeStateCommand.Verb, entityId.Format()),
                ErrorCode = ChangeFundOrderTradeStateCommand.ErrorId
            };
            serviceResult = await SendAsync(cmd!, entityId);
        }
        catch (Exception ex)
        {
            serviceResult = OnError(ex, cmdId, ChangeFundOrderTradeStateCommand.ErrorId);
        }
        return serviceResult;
    }

    /// <summary>
    /// change fund order trade state
    /// </summary>
    /// <param name="fundOrderTradeId"></param>
    /// <param name="tradeState"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> ChangeFundOrderTradeStateAsync(FundOrderTradeId fundOrderTradeId, TradeState tradeState)
    {
        Guid cmdId = Guid.NewGuid();
        ServiceResult<Guid> serviceResult;
        try
        {
            var entityId = new FundId(fundOrderTradeId.FundId);
            ChangeFundOrderTradeStateCommand cmd = new(fundOrderTradeId, tradeState)
            {
                CommandId = cmdId,
                Subject = new ActorSubject(ActorType.Command, ChangeFundOrderTradeStateCommand.Actor, ChangeFundOrderTradeStateCommand.Verb, entityId.Format()),
                ErrorCode = ChangeFundOrderTradeStateCommand.ErrorId
            };
            serviceResult = await SendAsync(cmd!, entityId);
        }
        catch (Exception ex)
        {
            serviceResult = OnError(ex, cmdId, ChangeFundOrderTradeStateCommand.ErrorId);
        }
        return serviceResult;
    }

    /// <summary>
    /// generate fund max profit
    /// </summary>
    /// <param name="fundOrder"></param>
    /// <param name="timePeriod"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> GenerateFundMaxProfitAsync(FundOrderReadModel fundOrder, TradeTimePeriodType timePeriod)
    {
        Guid cmdId = Guid.NewGuid();
        ServiceResult<Guid> serviceResult;
        try
        {
            var entityId = new FundId(fundOrder.FundId);
            GenerateFundMaxProfitCommand cmd = new(fundOrder, timePeriod)
            {
                CommandId = cmdId,
                Subject = new ActorSubject(ActorType.Command, GenerateFundMaxProfitCommand.Actor, GenerateFundMaxProfitCommand.Verb, entityId.Format()),
                ErrorCode = GenerateFundMaxProfitCommand.ErrorId
            };
            serviceResult = await SendAsync(cmd!, entityId);
        }
        catch (Exception ex)
        {
            serviceResult = OnError(ex, cmdId, GenerateFundMaxProfitCommand.ErrorId);
        }
        return serviceResult;
    }

    /// <summary>
    /// create fund transaction
    /// </summary>
    /// <param name="fundTransaction"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> CreateFundTransactionAsync(FundTransactionReadModel fundTransaction)
    {
        Guid cmdId = Guid.NewGuid();
        ServiceResult<Guid> serviceResult;
        try
        {
            var entityId = fundTransaction.EntityId;
            CreateFundTransactionCommand cmd = new(fundTransaction)
            {
                CommandId = cmdId,
                Subject = new ActorSubject(ActorType.Command, CreateFundTransactionCommand.Actor, CreateFundTransactionCommand.Verb, entityId.Format()),
                ErrorCode = CreateFundTransactionCommand.ErrorId
            };
            serviceResult = await SendAsync(cmd!, entityId);
        }
        catch (Exception ex)
        {
            serviceResult = OnError(ex, cmdId, CreateFundTransactionCommand.ErrorId);
        }
        return serviceResult;
    }

    /// <summary>
    /// create fund transactions
    /// </summary>
    /// <param name="transactionsId"></param>
    /// <param name="fundTransactions"></param>
    /// <param name="correlationId"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> CreateFundTransactionsAsync(FundTransactionEntityId transactionsId, FundTransactionReadModel[] fundTransactions, Guid correlationId)
    {
        Guid cmdId = Guid.NewGuid();
        ServiceResult<Guid> serviceResult;
        try
        {
            var entityId = transactionsId;
            CreateFundTransactionsCommand cmd = new(transactionsId, fundTransactions)
            {
                CommandId = cmdId,
                Subject = new ActorSubject(ActorType.Command, CreateFundTransactionsCommand.Actor, CreateFundTransactionsCommand.Verb, entityId.Format()),
                ErrorCode = CreateFundTransactionsCommand.ErrorId
            };
            serviceResult = await SendAsync(cmd!, entityId);
        }
        catch (Exception ex)
        {
            serviceResult = OnError(ex, cmdId, CreateFundTransactionsCommand.ErrorId);
        }
        return serviceResult;
    }

   
    /// <summary>
    /// process end of day fund transaction command
    /// </summary>
    /// <param name="correlationId"></param>
    /// <param name="fundTransaction"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> ProcessEndOfDayFundTransactionAsync(Guid correlationId, FundTransactionReadModel fundTransaction)
    {
        Guid cmdId = Guid.NewGuid();
        ServiceResult<Guid> serviceResult;
        try
        {
            var entityId = fundTransaction.EntityId;
            ProcessEndOfDayFundTransactionCommand cmd = new(fundTransaction)
            {
                CommandId = cmdId,
                Subject = new ActorSubject(ActorType.Command, ProcessEndOfDayFundTransactionCommand.Actor, ProcessEndOfDayFundTransactionCommand.Verb, entityId.Format()),
                ErrorCode = ProcessEndOfDayFundTransactionCommand.ErrorId
            };
            serviceResult = await SendAsync(cmd!, entityId);
        }
        catch (Exception ex)
        {
            serviceResult = OnError(ex, cmdId, ProcessEndOfDayFundTransactionCommand.ErrorId);
        }
        return serviceResult;
    }
}
