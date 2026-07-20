using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Contracts;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.Events;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.Fund;
using TomasAI.IFM.Shared.Fund.ViewModels;
using TomasAI.IFM.Shared.Fund.Events;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.Log;
using TomasAI.IFM.Shared.Log.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Models;

namespace TomasAI.IFM.ViewModels.Trade
{
    public class FundOrderEditorViewModel : BaseEditorViewModel
    {
        readonly IAppRoot _appRoot;
        readonly int _fundId;
        int _orderId;
        readonly DateTime _orderDate;
        readonly Shared.Fund.OrderStatus _orderStatus;
        readonly ICollection<FuturesContractViewModel> _baseContracts;
        readonly DateTime _valueDate;
        string _baseContractId;
        DateTime _tradeDate;
        DateTime _maturityDate;
        string _reference;
        FuturesEodDataViewModel _futuresEodData;

        /// <summary>
        /// fund order editor view model constructor
        /// </summary>
        /// <param name="appRoot"></param>
        /// <param name="valueDate"></param>
        /// <param name="baseContracts"></param>
        public FundOrderEditorViewModel(
            IAppRoot appRoot,
            DateTime valueDate,
            ICollection<FuturesContractViewModel> baseContracts, 
            int fundId) : base(appRoot)
        {
            _appRoot = appRoot;
            _fundId = fundId;
            _orderDate = DateTime.Now;
            _orderStatus = Shared.Fund.OrderStatus.Open;
            _baseContracts = baseContracts;
            _valueDate = valueDate;
            _tradeDate = valueDate;
            _maturityDate = _orderDate.Date;
            _reference = string.Empty;
        }

        /// <summary>
        /// public properties
        /// </summary>
        public int OrderId => _orderId;
        public DateTime OrderDate => _orderDate;
        public Shared.Fund.OrderStatus OrderStatus => _orderStatus;
        public DateTime TradeDate => _tradeDate;
        public DateTime MaturityDate => _maturityDate;
        public ICollection<string> BaseContractIds => _baseContracts.Select(e => e.ContractId).ToList();
        public string Reference => _reference;

        public FundOrderReadModel FundOrder => new (
            FundId: _fundId,
            OrderId: _orderId,
            OrderDate: _orderDate,
            OrderStatus: _orderStatus,
            BaseContractId: _baseContractId,
            TradeDate: _tradeDate,
            MaturityDate: _maturityDate,
            Reference: _reference,
            CreatedBy: $"{Environment.UserDomainName}\\{Environment.UserName}",
            CreatedOn: DateTime.Now,
            UpdatedBy: $"{Environment.UserDomainName}\\{Environment.UserName}",
            UpdatedOn: DateTime.Now
        );

        /// <summary>
        /// view changes
        /// </summary>
        public Action OnNewOrderId;
        public Action OnReferenceChanged;

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
        public void SetTradeDate(DateTime tradeDate)
        {
            _tradeDate = tradeDate.Date;
            SetReference();
        }

        /// <summary>
        /// set maturity date
        /// </summary>
        /// <param name="maturityDate"></param>
        public void SetMaturityDate(DateTime maturityDate)
        {
            _maturityDate = maturityDate.Date;
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
}
