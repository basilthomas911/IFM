using FluentValidation;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Domain.Fund.Shared;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Fund.Command.Model;

/// <summary>
/// Represents a fund, including its identifying information, balance, production status, creation details, and
/// associated orders.
/// </summary>
/// <param name="fundId">The unique identifier for the fund.</param>
/// <param name="name">The name of the fund.</param>
/// <param name="description">A description of the fund's purpose or characteristics.</param>
/// <param name="balance">The initial balance amount for the fund.</param>
/// <param name="isProduction">A value indicating whether the fund is in production. Set to <see langword="true"/> if the fund is active in a
/// production environment; otherwise, <see langword="false"/>.</param>
/// <param name="createdOn">The date and time when the fund was created.</param>
/// <param name="createdBy">The identifier of the user or process that created the fund.</param>
public class Fund(
    int fundId,
    string name,
    string description,
    decimal balance,
    bool isProduction,
    DateTime createdOn,
    string createdBy) : IFund
{
    IFundOrderCollection _orders = new FundOrderCollection();

    /// <summary>
    /// create fund from fund view model
    /// </summary>
    /// <param name="vm"></param>
    public Fund(FundReadModel vm) : this(
        fundId: vm.FundId, 
        name: vm.Name, 
        description: vm.Description, 
        balance: vm.Balance, 
        isProduction: vm.IsProduction,
        createdOn: vm.CreatedOn, 
        createdBy: vm.CreatedBy)
    {
        Orders.AddRange(vm.Orders.Select(e => new FundOrder(e)));
    }

    public int FundId => fundId;
    public string Name => name;
    public string Description=> description;
    public DateTime CreatedOn => createdOn;
    public string CreatedBy => createdBy;
    public IFundOrderCollection Orders => _orders;
    public decimal Balance { get; private set; } = balance;
    public bool IsProduction { get; private set;}= isProduction;

    /// <summary>
    /// return fund view model
    /// </summary>
    /// <returns></returns>
    public FundReadModel ToViewModel()
    {
        var fund = new FundReadModel(
            fundId: FundId,
            name: Name,
            description: Description,
            balance: Balance,
            isProduction: IsProduction,
            createdOn: CreatedOn,
            createdBy: CreatedBy
        );
        fund.AddRange(this.Orders.Select(e => e.ToViewModel()));
        return fund;
    }

    /// <summary>
    /// add new order to fund
    /// </summary>
    /// <param name="order"></param>
    public void AddOrderToFund(IFundOrder order)
    {
        if (Orders.Exists(order.OrderId))
            Orders.Remove(order);
        Orders.Add(order);
    }

    /// <summary>
    /// remove order from fund
    /// </summary>
    /// <param name="orderId"></param>
    public void RemoveOrderFromFund(int orderId)
    {
        var order = Orders
            .Where(e => e.OrderId == orderId)
            .FirstOrDefault();
        if (order is not null)
            Orders.Remove(order);
    }

    /// <summary>
    /// add new trade to fund order
    /// </summary>
    /// <param name="trade">fund order trade</param>
    public void AddTradeToFundOrder(IFundOrderTrade trade)
    {
        if (Orders.Exists(trade.OrderId))
        {
            if (Orders[trade.OrderId].Trades.Exists(trade.TradeId))
                Orders[trade.OrderId].Trades.Remove(trade);
            Orders[trade.OrderId].Trades.Add(trade);
        }
    }

    public void CompleteFundOrder(int orderId)
    {
        if (Orders.Exists(orderId))
            Orders[orderId].SetClosed();
    }

    /// <summary>
    /// remove trade from fund order
    /// </summary>
    /// <param name="orderId"></param>
    /// <param name="tradeId"></param>
    public void RemoveTradeFromFundOrder(int orderId, int tradeId)
    {
        if (Orders.Exists(orderId))
        {
            var trade = Orders[orderId].Trades
                .Where(e => e.TradeId == tradeId)
                .SingleOrDefault();
            if (trade != null)
                Orders[orderId].Trades.Remove(trade);
        }
    }

    /// <summary>
    /// update fund order trade state
    /// </summary>
    /// <param name="orderId"></param>
    /// <param name="tradeId"></param>
    /// <param name="tradeState"></param>
    public void ChangeFundOrderState(int orderId, int tradeId, TradeState tradeState)
    {
        if (Orders.Exists(orderId))
        {
            var trade = Orders[orderId].Trades
            .Where(e => e.TradeId == tradeId)
            .SingleOrDefault();
            trade?.SetTradeState(tradeState);
        }
    }

    /// <summary>
    /// remove order from fund when order is cancelled
    /// </summary>
    /// <param name="order"></param>
    public void CancelOrder(FundOrder order)
    {
        if (Orders.Exists(order.OrderId))
            Orders.Remove(order);
    }

    /// <summary>
    /// initialize fund balance amount
    /// </summary>
    /// <param name="amount"></param>
    public void SetBalance(decimal amount) => Balance = amount;

    /// <summary>
    /// append fund balance
    /// </summary>
    /// <param name="amount"></param>
    public void AppendBalance(decimal amount) => Balance += amount;
    
}


