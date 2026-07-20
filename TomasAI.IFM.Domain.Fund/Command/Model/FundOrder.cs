using TomasAI.IFM.Domain.Fund.Shared;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Fund.Command.Model;

/// <summary>
/// fund order
/// </summary>
/// <remarks>
/// fund order constructor
/// </remarks>
/// <param name="orderId"></param>
/// <param name="fundId"></param>
/// <param name="orderDate"></param>
/// <param name="orderStatus"></param>
/// <param name="baseContractId"></param>
/// <param name="tradeDate"></param>
/// <param name="maturityDate"></param>
/// <param name="reference"></param>
/// <param name="createdOn"></param>
/// <param name="createdBy"></param>
/// <param name="updatedOn"></param>
/// <param name="updatedBy"></param>
public class FundOrder(
    int orderId,
    int fundId,
    DateTime orderDate,
    OrderStatus orderStatus,
    string baseContractId,
    DateOnly tradeDate,
    DateOnly maturityDate,
    string reference,
    DateTime createdOn,
    string createdBy,
    DateTime? updatedOn,
    string updatedBy) :  IFundOrder
{
    IFundOrderTradeCollection _trades = new FundOrderTradeCollection();

    /// <summary>
    /// fund order constructor from view model
    /// </summary>
    /// <param name="vm"></param>
    public FundOrder(FundOrderReadModel vm):this(
        orderId: vm.OrderId,
        fundId: vm.FundId,
        orderDate: vm.OrderDate,
        orderStatus: vm.OrderStatus,
        baseContractId: vm.BaseContractId,
        tradeDate: vm.TradeDate,    
        maturityDate: vm.MaturityDate,
        reference: vm.Reference,
        createdOn: vm.CreatedOn,
        createdBy: vm.CreatedBy,
        updatedOn: vm.UpdatedOn,
        updatedBy: vm.UpdatedBy)
    {
        if (vm.Trades.Length > 0)
            Trades.AddRange(vm.Trades.Select(e => new FundOrderTrade(e)));
    }

    public int OrderId => orderId;
    public int FundId =>  fundId;
    public DateTime OrderDate => orderDate;
    public OrderStatus OrderStatus{ get; private set; } = orderStatus;
    public string BaseContractId => baseContractId;
    public DateOnly TradeDate => tradeDate;
    public DateOnly MaturityDate => maturityDate;
    public string Reference => reference;
    public DateTime CreatedOn => createdOn;
    public string CreatedBy => createdBy;
    public DateTime? UpdatedOn => updatedOn;
    public string UpdatedBy => updatedBy;
    public IFundOrderTradeCollection Trades => _trades;

    public FundOrderReadModel ToViewModel()
    {
        var fundOrder = new FundOrderReadModel(
              fundId: FundId,
              orderId: OrderId,
              orderDate: OrderDate,
              orderStatus: OrderStatus,
              baseContractId: BaseContractId,
              tradeDate: TradeDate,
              maturityDate: MaturityDate,
              reference: Reference,
              createdOn: CreatedOn,
              createdBy: CreatedBy,
              updatedOn: UpdatedOn,
              updatedBy: UpdatedBy
        );
        fundOrder.Trades.AddRange(Trades.Select(e => e.ToViewModel()));
        return fundOrder;
    }

    /// <summary>
    /// set order status to closed
    /// </summary>
    public void SetClosed() => OrderStatus = OrderStatus.Closed;
}
