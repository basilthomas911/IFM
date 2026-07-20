using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Fund.Shared;
using TomasAI.IFM.Domain.Fund.Shared.Events;
using TomasAI.IFM.Domain.Fund.Command.Model;

namespace TomasAI.IFM.Domain.Fund.Command.State;

/// <summary>
/// Represents the persisted state and event application logic for a fund actor, including tracking fund, order, and
/// trade entities within the actor's lifecycle.
/// </summary>
/// <remarks>This state class is used by event-sourced fund actors to manage and apply domain events related to
/// funds, their orders, and associated trades. It provides methods to check for the existence of fund-related entities
/// and to apply state changes in response to domain events. This type is intended for use within the actor framework
/// and is not typically used directly by application code.</remarks>
public class FundCommandState
    :  BaseEventSourceActorState<FundCommandState>, IEventSourceActorState<FundCommandState>
{
    Model.Fund? _fund;

    public override ActorThreadId Id { get; set; } = default!;

    /// <summary>
    /// apply state change events
    /// </summary>
    /// <param name="domainEvent"></param>
    /// <returns></returns>
    protected override bool Apply(IEvent domainEvent)
    {
        try
        {
            return domainEvent switch
            {
                FundCreatedEvent e => On(e),
                OrderAddedToFundEvent e => On(e),
                TradeAddedToFundOrderEvent e => On(e),
                FundOrderTradeStateChangedEvent e => On(e),
                FundOrderClosedEvent e => On(e),
                OrderRemovedFromFundEvent e => On(e),
                TradeRemovedFromFundOrderEvent e => On(e),
                _ => false
            };
        }
        catch { }
        return false;
    }

    public int FundId
        => _fund?.FundId ?? 0;

    /// <summary>
    /// check if fund exists
    /// </summary>
    /// <param name="fundId"></param>
    /// <returns></returns>
    public bool FundExists
        => _fund is not null;


    /// <summary>
    /// check if fund order exists
    /// </summary>
    /// <param name="fundId"></param>
    /// <param name="orderId"></param>
    /// <returns></returns>
    public bool FundOrderExists(int fundId, int orderId)
        => FundExists && FundId == fundId && _fund!.Orders.Exists(orderId);

    /// <summary>
    /// Determines whether the specified fund order is closed.
    /// </summary>
    /// <param name="fundId">The unique identifier of the fund containing the order to check.</param>
    /// <param name="orderId">The unique identifier of the order to check within the specified fund.</param>
    /// <returns>true if the fund exists, the order exists within the fund, and the order status is Closed; otherwise, false.</returns>
    public bool IsFundOrderClosed(int fundId, int orderId)
        => FundExists && FundId == fundId && _fund!.Orders.Exists(orderId) && _fund!.Orders[orderId].OrderStatus == OrderStatus.Closed;

    /// <summary>
    /// check if fund order trade exists
    /// </summary>
    /// <param name="fundId"></param>
    /// <param name="orderId"></param>
    /// <param name="tradeId"></param>
    /// <returns></returns>
    public bool FundOrderTradeExists(int fundId, int orderId, int tradeId)
        => FundOrderExists(fundId, orderId) && _fund!.Orders[orderId].Trades.Exists(tradeId);

    bool On(FundCreatedEvent e)
    {
        if (_fund is null)
        {
            _fund = new Model.Fund(e.NewFund);
            return true;
        }
        return false;
    }

    bool On(OrderAddedToFundEvent e)
    {
        if (!FundOrderExists(e.FundOrder.FundId, e.FundOrder.OrderId))
        {
            _fund!.AddOrderToFund(new FundOrder(e.FundOrder));
            return true;
        }
        return false;
    }

    bool On(TradeAddedToFundOrderEvent e)
    {
        if (FundOrderExists(e.FundOrderTrade.FundId, e.FundOrderTrade.OrderId))
        {
            _fund!.AddTradeToFundOrder(new FundOrderTrade(e.FundOrderTrade));
            return true;
        }
        return false;
    }

    bool On(FundOrderTradeStateChangedEvent e)
    {
        if (FundOrderTradeExists(e.FundOrderTradeId.FundId, e.FundOrderTradeId.OrderId, e.FundOrderTradeId.TradeId))
        {
            var fundOrder = _fund!.Orders[e.FundOrderTradeId.OrderId];
            var fundOrderTrade = fundOrder.Trades[e.FundOrderTradeId.TradeId];
            fundOrderTrade?.SetTradeState(e.TradeState);
            return true;
        }
        return false;
    }

    bool On(FundOrderClosedEvent e)
    {
        if (FundOrderExists(e.FundOrderId.FundId, e.FundOrderId.OrderId))
        {
            _fund!.Orders[e.FundOrderId.OrderId].SetClosed();
            return true;
        }
        return false;
    }

    bool On(OrderRemovedFromFundEvent e)
    {
        if (FundOrderExists(e.FundOrderId.FundId, e.FundOrderId.OrderId))
        {
            var fundOrder = _fund!.Orders[e.FundOrderId.OrderId];
            _fund!.Orders.Remove(fundOrder);
            return true;
        }
        return false;
    }

    bool On(TradeRemovedFromFundOrderEvent e)
    {
        if (FundOrderTradeExists(e.FundOrderTradeId.FundId, e.FundOrderTradeId.OrderId, e.FundOrderTradeId.TradeId))
        {
            var fundOrder = _fund!.Orders[e.FundOrderTradeId.OrderId];
            var fundOrderTrade = fundOrder.Trades[e.FundOrderTradeId.TradeId]!;
            fundOrder.Trades.Remove(fundOrderTrade);
            return true;
        }
        return false;
    }
}
