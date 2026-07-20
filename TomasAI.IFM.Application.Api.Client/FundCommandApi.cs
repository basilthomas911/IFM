using TomasAI.IFM.Shared.Application.Commands;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Framework.Messaging;
using TomasAI.IFM.Domain.Fund.Shared;
using TomasAI.IFM.Domain.Fund.Shared.Commands;
using TomasAI.IFM.Domain.Fund.Shared.CommandParameters;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Domain.Fund.Shared.ServiceApi;

namespace TomasAI.IFM.Application.Api.Client;

/// <summary>
/// create fund command api
/// </summary>
/// <param name="commandSvc"></param>
/// <exception cref="ArgumentNullException"></exception>
public class FundCommandApi(ICommandServiceApi commandSvc) : IFundCommandApi
{
    readonly ICommandServiceApi _commandSvc = IsArgumentNull.Set(commandSvc);

    /// <summary>
    /// create fund
    /// </summary>
    /// <param name="newFund"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> CreateFundAsync(FundReadModel newFund)
        => await new CreateFundParameter(IsArgumentNull.Set(newFund), CreateFundCommand.ErrorId)
        .ExecuteAsync(e => _commandSvc.ExecuteCommandAsync(FundUriPath.Create, e));
    

    /// <summary>
    /// add order to fund
    /// </summary>
    /// <param name="fundOrder"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> AddOrderToFundAsync(FundOrderReadModel fundOrder)
        => await new AddOrderToFundParameter(IsArgumentNull.Set(fundOrder), AddOrderToFundCommand.ErrorId)
        .ExecuteAsync(e => _commandSvc.ExecuteCommandAsync(FundUriPath.AddOrderToFund, e));

    /// <summary>
    /// remove order from fund
    /// </summary>
    /// <param name="fundOrderId"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> RemoveOrderFromFundAsync(FundOrderId fundOrderId)
        => await new RemoveOrderFromFundParameter(IsArgumentNull.Set(fundOrderId), RemoveOrderFromFundCommand.ErrorId)
        .ExecuteAsync(e => _commandSvc.ExecuteCommandAsync(FundUriPath.RemoveOrderFromFund, e));

    /// <summary>
    /// add trade to fund order
    /// </summary>
    /// <param name="fundOrderTrade"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> AddTradeToFundOrderAsync(FundOrderTradeReadModel fundOrderTrade )
        => await new AddTradeToFundOrderParameter(IsArgumentNull.Set(fundOrderTrade), AddTradeToFundOrderCommand.ErrorId)
        .ExecuteAsync(e => _commandSvc.ExecuteCommandAsync(FundUriPath.AddTradeToFundOrder, e));

    /// <summary>
    /// remove trade from fund order
    /// </summary>
    /// <param name="fundOrderTradeId"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> RemoveTradeFromFundOrderAsync(FundOrderTradeId fundOrderTradeId)
        => await new RemoveTradeFromFundOrderParameter(IsArgumentNull.Set(fundOrderTradeId), RemoveTradeFromFundOrderCommand.ErrorId)
        .ExecuteAsync(e => _commandSvc.ExecuteCommandAsync(FundUriPath.RemoveTradeFromFundOrder, e));

    /// <summary>
    /// close fund order from any more changes
    /// </summary>
    /// <param name="fundOrderId"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> CloseFundOrderAsync(FundOrderId fundOrderId)
        => await new CloseFundOrderParameter(IsArgumentNull.Set(fundOrderId), CloseFundOrderCommand.ErrorId)
        .ExecuteAsync(e => _commandSvc.ExecuteCommandAsync(FundUriPath.CloseFundOrder, e));

    /// <summary>
    /// change fund order trade state
    /// </summary>
    /// <param name="fundOrderTradeId"></param>
    /// <param name="tradeState"></param>
    /// <param name="correlationId"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> ChangeFundOrderTradeStateAsync(FundOrderTradeId fundOrderTradeId, TradeState tradeState, Guid correlationId)
        => await new ChangeFundOrderTradeStateParameter(IsArgumentNull.Set(fundOrderTradeId), tradeState, ChangeFundOrderTradeStateCommand.ErrorId)
        .ExecuteAsync(e => _commandSvc.ExecuteCommandAsync(FundUriPath.ChangeFundOrderTradeState, e));

    /// <summary>
    /// change fund order trade state
    /// </summary>
    /// <param name="fundOrderTradeId"></param>
    /// <param name="tradeState"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> ChangeFundOrderTradeStateAsync(FundOrderTradeId fundOrderTradeId, TradeState tradeState)
        => await new ChangeFundOrderTradeStateParameter(IsArgumentNull.Set(fundOrderTradeId), tradeState, ChangeFundOrderTradeStateCommand.ErrorId)
        .ExecuteAsync(e => _commandSvc.ExecuteCommandAsync(FundUriPath.ChangeFundOrderTradeState, e));

    /// <summary>
    /// generate fund max profit
    /// </summary>
    /// <param name="fundOrder"></param>
    /// <param name="timePeriod"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> GenerateFundMaxProfitAsync(FundOrderReadModel fundOrder, TradeTimePeriodType timePeriod)
        => await new GenerateFundMaxProfitParameter(IsArgumentNull.Set(fundOrder), timePeriod, GenerateFundMaxProfitCommand.ErrorId)
        .ExecuteAsync(e => _commandSvc.ExecuteCommandAsync(FundUriPath.GenerateFundMaxProfit, e));

    /// <summary>
    /// create fund transaction
    /// </summary>
    /// <param name="fundTransaction"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> CreateFundTransactionAsync(FundTransactionReadModel fundTransaction)
        => await new CreateFundTransactionParameter(IsArgumentNull.Set(fundTransaction), CreateFundTransactionCommand.ErrorId)
        .ExecuteAsync(e => _commandSvc.ExecuteCommandAsync(FundTransactionUriPath.Create, e));

    /// <summary>
    /// create fund transactions
    /// </summary>
    /// <param name="transactionsId"></param>
    /// <param name="fundTransactions"></param>
    /// <param name="correlationId"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> CreateFundTransactionsAsync(FundTransactionEntityId transactionsId, FundTransactionReadModel[] fundTransactions, Guid correlationId)
        => await new CreateFundTransactionsParameter(transactionsId, IsArgumentNull.Set(fundTransactions), correlationId, CreateFundTransactionsCommand.ErrorId)
        .ExecuteAsync(e => _commandSvc.ExecuteCommandAsync(FundTransactionUriPath.CreateTransactions, e));

   
    /// <summary>
    /// process end of day fund transaction command
    /// </summary>
    /// <param name="correlationId"></param>
    /// <param name="fundTransaction"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> ProcessEndOfDayFundTransactionAsync(Guid correlationId, FundTransactionReadModel fundTransaction)
        => await new ProcessEndOfDayFundTransactionParameter(IsArgumentNull.Set(fundTransaction), ProcessEndOfDayFundTransactionCommand.ErrorId)
        .ExecuteAsync(e => _commandSvc.ExecuteCommandAsync(FundTransactionUriPath.ProcessEndOfDay, e));
}
