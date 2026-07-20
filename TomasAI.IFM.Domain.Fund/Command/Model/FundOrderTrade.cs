using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Domain.Fund.Shared;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Fund.Command.Model;

/// <summary>
/// fund order trade
/// </summary>
/// <remarks>
/// fund order trade constructor
/// </remarks>
/// <param name="fundId"></param>
/// <param name="orderId"></param>
/// <param name="tradeId"></param>
/// <param name="tradeType"></param>
/// <param name="tradeDate"></param>
/// <param name="maturityDate"></param>
/// <param name="tradeState"></param>
/// <param name="tradeAction"></param>
/// <param name="primaryTrade"></param>
/// <param name="createdOn"></param>
/// <param name="createdBy"></param>
public class FundOrderTrade(
    int fundId,
    int orderId,
    int tradeId,
    TradeType tradeType,
    DateOnly tradeDate,
    DateOnly maturityDate,
    TradeState tradeState,
    TradeAction tradeAction,
    string reference,
    bool primaryTrade,
    string baseContractSymbol,
    DateTime createdOn,
    string createdBy) : IFundOrderTrade
{

    /// <summary>
    /// fund order trade constructor from view model
    /// </summary>
    /// <param name="vm"></param>
    public FundOrderTrade(FundOrderTradeReadModel vm):this(
        fundId: vm.FundId,
        orderId: vm.OrderId,
        tradeId: vm.TradeId,
        tradeType: vm.TradeType,
        tradeDate: vm.TradeDate,
        maturityDate: vm.MaturityDate,
        tradeState: vm.TradeState,
        tradeAction: vm.TradeAction,
        reference: vm.Reference,
        primaryTrade: vm.PrimaryTrade,
        baseContractSymbol: vm.BaseContractSymbol,
        createdOn: vm.CreatedOn,
        createdBy: vm.CreatedBy)
    { 
    }

    public int FundId { get; } = fundId;
    public int OrderId { get; } = orderId;
    public int TradeId { get; } = tradeId;
    public TradeType TradeType { get; } = tradeType;
    public DateOnly TradeDate { get; } = tradeDate;
    public DateOnly MaturityDate { get; } = maturityDate;
    public TradeState TradeState { get; private set; } = tradeState;
    public TradeAction TradeAction { get; } = tradeAction;
    public string Reference { get; private set; } = reference;
    public bool PrimaryTrade { get; } = primaryTrade;
    public string BaseContractSymbol { get; } = baseContractSymbol;
    public DateTime CreatedOn { get; } = createdOn;
    public string CreatedBy { get; } = createdBy;

    /// <summary>
    /// return fund order trade view model
    /// </summary>
    /// <returns></returns>
    public FundOrderTradeReadModel ToViewModel()
        => new (
            fundId: this.FundId,
            orderId: this.OrderId,
            tradeId: this.TradeId,
            tradeType: this.TradeType,
            tradeDate: this.TradeDate,
            maturityDate: this.MaturityDate,
            tradeState: this.TradeState,
            tradeAction: this.TradeAction,
            reference: this.Reference,
            primaryTrade: this.PrimaryTrade,
            baseContractSymbol: this.BaseContractSymbol,
            createdOn: this.CreatedOn,
            createdBy: this.CreatedBy,
            updatedOn: this.CreatedOn,
            updatedBy: this.CreatedBy
        );

    /// <summary>
    /// change trade state
    /// </summary>
    /// <param name="tradeState"></param>
    public void SetTradeState(TradeState tradeState) => TradeState = tradeState;

    /// <summary>
    /// change reference
    /// </summary>
    /// <param name="reference"></param>
    public void SetReference(string reference) => Reference = reference;
}
