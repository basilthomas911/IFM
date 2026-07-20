using TomasAI.IFM.Contracts;
using TomasAI.IFM.Models;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Domain.Fund.Shared;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;

namespace TomasAI.IFM.ViewModels.Trade;

public class FundOrderEditorViewModel : BaseEditorViewModel
{
    readonly IAppRoot _appRoot;
    readonly int _fundId;
    int _orderId;
    readonly DateTime _orderDate;
    readonly OrderStatus _orderStatus;
    readonly ICollection<FuturesContractV2ReadModel> _baseContracts;
    readonly DateOnly _valueDate;
    string _baseContractId = null!;
    DateOnly _tradeDate;
    DateOnly _maturityDate;
    string _reference;
    FuturesEodDataV2ReadModel _futuresEodData = null!;

    /// <summary>
    /// fund order editor view model constructor
    /// </summary>
    /// <param name="appRoot"></param>
    /// <param name="valueDate"></param>
    /// <param name="baseContracts"></param>
    public FundOrderEditorViewModel(
        IAppRoot appRoot,
        DateOnly valueDate,
        ICollection<FuturesContractV2ReadModel> baseContracts, 
        int fundId) : base(appRoot)
    {
        _appRoot = appRoot;
        _fundId = fundId;
        _orderDate = DateTime.Now;
        _orderStatus = OrderStatus.Open;
        _baseContracts = baseContracts;
        _valueDate = valueDate;
        _tradeDate = valueDate;
        _maturityDate = DateOnly.FromDateTime(_orderDate);
        _reference = string.Empty;
    }

    /// <summary>
    /// public properties
    /// </summary>
    public int OrderId => _orderId;
    public DateTime OrderDate => _orderDate;
    public OrderStatus OrderStatus => _orderStatus;
    public DateOnly TradeDate => _tradeDate;
    public DateOnly MaturityDate => _maturityDate;
    public ICollection<string> BaseContractIds => [.. _baseContracts.Select(e => e.ContractId)];
    public string Reference => _reference;

    public FundOrderReadModel FundOrder => new (
        fundId: _fundId,
        orderId: _orderId,
        orderDate: _orderDate,
        orderStatus: _orderStatus,
        baseContractId: _baseContractId,
        tradeDate: _tradeDate,
        maturityDate: _maturityDate,
        reference: _reference,
        createdBy: $"{Environment.UserDomainName}\\{Environment.UserName}",
        createdOn: DateTime.Now,
        updatedBy: $"{Environment.UserDomainName}\\{Environment.UserName}",
        updatedOn: DateTime.Now
    );

    /// <summary>
    /// view changes
    /// </summary>
    public Action OnNewOrderId = null!;
    public Action OnReferenceChanged = null!;

    /// <summary>
    /// load new order id
    /// </summary>
    public void LoadNewOrderId() => _appRoot.GetModel<ReferenceQueryModel>()
        .Execute(async model => await model.NewOrderIdAsync(newOrderId => {
            _orderId = newOrderId;
            OnNewOrderId?.Invoke();
        }));

    /// <summary>
    /// set base contract id
    /// </summary>
    /// <param name="index"></param>
    /// <exception cref="IndexOutOfRangeException"></exception>
    public void SetBaseContractId(int index)
    {
        _baseContractId = index > -1 && index < _baseContracts.Count
           ? _baseContracts.ElementAt(index).ContractId
            : throw new IndexOutOfRangeException($"Invalid Base Contract index: {index}");
        _appRoot.GetModel<MarketDataFeedQueryModel>()
            .Execute(async model => await model.GetFuturesEodDataAsync(_baseContractId, _valueDate, futuresEodData =>
            {
                _futuresEodData = futuresEodData;
                SetReference();
            }));
    }

    /// <summary>
    /// set trade date
    /// </summary>
    /// <param name="tradeDate"></param>
    public void SetTradeDate(DateOnly tradeDate)
    {
        _tradeDate = tradeDate;
        SetReference();
    }

    /// <summary>
    /// set maturity date
    /// </summary>
    /// <param name="maturityDate"></param>
    public void SetMaturityDate(DateOnly maturityDate)
    {
        _maturityDate = maturityDate;
        SetReference();
    }

    /// <summary>
    /// set reference
    /// </summary>
    private void SetReference()
    {
        if (_futuresEodData is null)
            _reference = $"{_baseContractId} @ {_tradeDate:MMM dd} - {_maturityDate:MMM dd}";
        else
            _reference = $"{_baseContractId} @ {_tradeDate:MMM dd} - {_maturityDate:MMM dd} => {_futuresEodData.MarketDirection}:{_futuresEodData.MarketVolatility}:{_futuresEodData.PriceDirection}:{_futuresEodData.PriceVolatility}";
        OnReferenceChanged?.Invoke();   
    }

}
