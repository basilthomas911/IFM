using TomasAI.IFM.Shared.Fund;
using TomasAI.IFM.Shared.Fund.Commands;
using TomasAI.IFM.Shared.Fund.ViewModels;
using TomasAI.IFM.Shared.Fund.ServiceApi;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Framework.Messaging;

namespace TomasAI.IFM.Application.Command.Client;

/// <summary>
/// create fund command api
/// </summary>
/// <param name="commandSvc"></param>
/// <exception cref="ArgumentNullException"></exception>
public class FundCommandApi(ICommandService commandSvc) : IFundCommandApi
{
    const string FundController = "Fund";
    const string FundTransactionController = "FundTransaction";
    readonly ICommandService _commandSvc = IsArgumentNull.Set(commandSvc);

    /// <summary>
    /// create fund
    /// </summary>
    /// <param name="newFund"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> CreateFundAsync(FundReadModel newFund)
        => await new CreateFundCommand (IsArgumentNull.Set(newFund))
        .ExecuteAsync(e => _commandSvc.ExecuteApiCommandAsync(e, FundController));

    /// <summary>
    /// add order to fund
    /// </summary>
    /// <param name="fundOrder"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> AddOrderToFundAsync(FundOrderReadModel fundOrder, Action<Guid> setCommandId)
        => await new AddOrderToFundCommand(IsArgumentNull.Set(fundOrder))
        .SetCommandId(setCommandId)
        .ExecuteAsync(e => _commandSvc.ExecuteApiCommandAsync(e, FundController));

    /// <summary>
    /// remove order from fund
    /// </summary>
    /// <param name="fundOrderId"></param>
    /// <param name="setCommandId"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> RemoveOrderFromFundAsync(FundOrderId fundOrderId, Action<Guid> setCommandId)
        => await new RemoveOrderFromFundCommand(IsArgumentNull.Set(fundOrderId))
        .SetCommandId(setCommandId)
        .ExecuteAsync(e => _commandSvc.ExecuteApiCommandAsync(e, FundController));

    /// <summary>
    /// add trade to fund order
    /// </summary>
    /// <param name="fundOrderTrade"></param>
    /// <param name="setCommandId"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> AddTradeToFundOrderAsync(FundOrderTradeReadModel fundOrderTrade, Action<Guid> setCommandId)
        => await new AddTradeToFundOrderCommand( IsArgumentNull.Set(fundOrderTrade))
        .SetCommandId(setCommandId)
        .ExecuteAsync(e => _commandSvc.ExecuteApiCommandAsync(e, FundController));

    /// <summary>
    /// remove trade from fund order
    /// </summary>
    /// <param name="fundOrderTradeId"></param>
    /// <param name="setCommandId"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> RemoveTradeFromFundOrderAsync(FundOrderTradeId fundOrderTradeId, Action<Guid> setCommandId)
        => await new RemoveTradeFromFundOrderCommand( IsArgumentNull.Set(fundOrderTradeId))
        .SetCommandId(setCommandId)
        .ExecuteAsync(e => _commandSvc.ExecuteApiCommandAsync(e, FundController));

    /// <summary>
    /// close fund order from any more changes
    /// </summary>
    /// <param name="fundOrderId"></param>
    /// <param name="setCommandId"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> CloseFundOrderAsync(FundOrderId fundOrderId, Action<Guid> setCommandId)
        => await new CloseFundOrderCommand {
            FundOrderId = IsArgumentNull.Set(fundOrderId)
        }
        .SetCommandId(setCommandId)
        .ExecuteAsync(e => _commandSvc.ExecuteApiCommandAsync(e, FundController));

    /// <summary>
    /// change fund order trade state
    /// </summary>
    /// <param name="fundOrderTradeId"></param>
    /// <param name="tradeState"></param>
    /// <param name="correlationId"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> ChangeFundOrderTradeStateAsync(FundOrderTradeId fundOrderTradeId, TradeState tradeState, Guid correlationId)
        => await new ChangeFundOrderTradeStateCommand( IsArgumentNull.Set(fundOrderTradeId),tradeState)
        .SetCommandId(correlationId)
        .ExecuteAsync(e => _commandSvc.ExecuteApiCommandAsync(e, FundController));

    /// <summary>
    /// change fund order trade state
    /// </summary>
    /// <param name="fundOrderTradeId"></param>
    /// <param name="tradeState"></param>
    /// <param name="setCommandId"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> ChangeFundOrderTradeStateAsync(FundOrderTradeId fundOrderTradeId, TradeState tradeState, Action<Guid> setCommandId)
        => await new ChangeFundOrderTradeStateCommand(IsArgumentNull.Set(fundOrderTradeId), tradeState)
        .SetCommandId(setCommandId)
        .ExecuteAsync(e => _commandSvc.ExecuteApiCommandAsync(e, FundController));

    /// <summary>
    /// create fund transaction
    /// </summary>
    /// <param name="fundTransaction"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> CreateFundTransactionAsync(FundTransactionReadModel fundTransaction, Action<Guid> setCommandId)
         => await new CreateFundTransactionCommand( IsArgumentNull.Set(fundTransaction))
         .SetCommandId(setCommandId)
         .ExecuteAsync(e => _commandSvc.ExecuteApiCommandAsync(e, FundTransactionController));

    /// <summary>
    /// create fund transactions
    /// </summary>
    /// <param name="transactionsId"></param>
    /// <param name="fundTransactions"></param>
    /// <param name="correlationId"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> CreateFundTransactionsAsync(FundTransactionEntityId transactionsId, FundTransactionReadModel[] fundTransactions, Guid correlationId)
        => await new CreateFundTransactionsCommand(transactionsId, IsArgumentNull.Set(fundTransactions))
        .SetCommandId(correlationId)
        .ExecuteAsync(e => _commandSvc.ExecuteApiCommandAsync(e, FundTransactionController));

    /// <summary>
    /// generate fund max profit
    /// </summary>
    /// <param name="fundOrder"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> GenerateFundMaxProfitAsync(FundOrderReadModel fundOrder)
        => await new GenerateFundMaxProfitCommand
        {
            FundOrder = IsArgumentNull.Set(fundOrder)
        }
        .ExecuteAsync(e => _commandSvc.ExecuteApiCommandAsync(e, FundController));

    /// <summary>
    /// process end of day fund transaction command
    /// </summary>
    /// <param name="correlationId"></param>
    /// <param name="fundTransaction"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> ProcessEndOfDayFundTransactionAsync(Guid correlationId, FundTransactionReadModel fundTransaction)
        => await new ProcessEndOfDayFundTransactionCommand ( IsArgumentNull.Set(fundTransaction))
            .SetCommandId(correlationId)
            .ExecuteAsync(e => _commandSvc.ExecuteApiCommandAsync(e, FundTransactionController));
}
