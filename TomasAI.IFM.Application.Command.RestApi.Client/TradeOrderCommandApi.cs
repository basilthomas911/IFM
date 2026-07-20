using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.Trade.ServiceApi;
using TomasAI.IFM.Shared.TradeOrder;
using TomasAI.IFM.Shared.TradeOrder.ViewModels;
using TomasAI.IFM.Shared.TradeOrder.Commands;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Framework.Messaging;

namespace TomasAI.IFM.Application.Command.Client;

/// <summary>
/// trade order command api
/// </summary>
/// <remarks>
/// trade order command api constructor
/// </remarks>
/// <param name="commandSvc"></param>
public class TradeOrderCommandApi(ICommandService commandSvc) : ITradeOrderCommandApi
{
    const string TradeOrderController = "TradeOrder";
     readonly ICommandService _commandSvc = IsArgumentNull.Set(commandSvc);

    /// <summary>
    /// submit trade order to broker 
    /// </summary>
    /// <param name="tradeOrder"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> SubmitTradeOrderAsync(TradeOrderReadModel tradeOrder)
        => await new PlaceTradeOrderCommand(tradeOrder)
        .ExecuteAsync(e => _commandSvc.PostApiCommandAsync(e, TradeOrderController));

    /// <summary>
    /// open trade order by broker
    /// </summary>
    /// <param name="tradeOrderId"></param>
    /// <param name="executed"></param>
    /// <param name="errorMessage"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> OpenTradeOrderAsync(TradeOrderEntityId tradeOrderId, bool executed = false, string errorMessage = "")
        => await new OpenOrderCommand(tradeOrderId, executed, errorMessage) 
            .ExecuteAsync(e => _commandSvc.PostApiCommandAsync(e, TradeOrderController));

    /// <summary>
    /// fill trade order for both partial and filled by broker
    /// </summary>
    /// <param name="tradeOrderId"></param>
    /// <param name="tradeFill"></param>
    /// <param name="executed"></param>
    /// <param name="errorMessage"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> FillTradeOrderAsync(TradeOrderEntityId tradeOrderId, TradeFillReadModel tradeFill, bool executed = false, string errorMessage = "")
        => await new FillOrderCommand(tradeOrderId, tradeFill, executed, errorMessage)  
            .ExecuteAsync(e => _commandSvc.PostApiCommandAsync(e, TradeOrderController));

    /// <summary>
    /// close trade order by broker
    /// </summary>
    /// <param name="tradeOrderId"></param>
    /// <param name="executed"></param>
    /// <param name="errorMessage"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> CloseTradeOrderAsync(TradeOrderEntityId tradeOrderId, bool executed = false, string errorMessage = "")
        => await new CloseOrderCommand(tradeOrderId, executed, errorMessage)
            .ExecuteAsync(e => _commandSvc.PostApiCommandAsync(e, TradeOrderController));

    /// <summary>
    /// submit update order to broker
    /// </summary>
    /// <param name="tradeOrderId"></param>
    /// <param name="orderPrice"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> SubmitUpdateOrderAsync(TradeOrderEntityId tradeOrderId, decimal orderPrice)
        => await new ExecuteUpdateOrderCommand(tradeOrderId, orderPrice)    
            .ExecuteAsync(e => _commandSvc.PostApiCommandAsync(e, TradeOrderController));

    /// <summary>
    /// update trade order price by broker
    /// </summary>
    /// <param name="tradeOrderId"></param>
    /// <param name="orderPrice"></param>
    /// <param name="executed"></param>
    /// <param name="errorMessage"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> UpdateOrderAsync(TradeOrderEntityId tradeOrderId, decimal orderPrice, bool executed = false, string errorMessage = "")
        => await new UpdateOrderCommand(tradeOrderId, orderPrice, executed, errorMessage)   
            .ExecuteAsync(e => _commandSvc.PostApiCommandAsync(e, TradeOrderController));

    /// <summary>
    /// submit cancel order to broker
    /// </summary>
    /// <param name="tradeOrderId"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> SubmitCancelOrderAsync(TradeOrderEntityId tradeOrderId)
        => await new ExecuteCancelOrderCommand(tradeOrderId)
            .ExecuteAsync(e => _commandSvc.PostApiCommandAsync(e, TradeOrderController));

    /// <summary>
    /// cancel trade order by broker
    /// </summary>
    /// <param name="tradeOrderId"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> CancelTradeOrderAsync(TradeOrderEntityId tradeOrderId, bool executed = false, string errorMessage = "")
        => await new CancelOrderCommand(tradeOrderId, executed, errorMessage)
            .ExecuteAsync(e => _commandSvc.PostApiCommandAsync(e, TradeOrderController));
}
