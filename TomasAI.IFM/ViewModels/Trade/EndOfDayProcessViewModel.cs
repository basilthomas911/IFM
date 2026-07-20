using System;
using System.Linq;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Threading.Tasks;
using TomasAI.IFM.Contracts;
using TomasAI.IFM.Models;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.Trade.Events;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Fund.Events;

namespace TomasAI.IFM.ViewModels.Trade
{
    public class EndOfDayProcessViewModel : BaseEditorViewModel
    {
        readonly IAppRoot _appRoot;
        readonly TradeEndOfDayParameter _eodParam;
        double _openPrice;
        double _highPrice;
        double _lowPrice;
        double _closePrice;
        int _volume;
        decimal _tradePnl;
        Guid _commandId;
        Action<Guid> _setCommandId;
        ICollection<IEvent> _consumeEvents;
        EndOfDayProcessEventModel _eventModel;
        FundQueryModel _fundQueryModel;
        TradeQueryModel _tradeQueryModel;
        MarketDataFeedQueryModel _mktDataFeedQueryModel;

        public EndOfDayProcessViewModel(IAppRoot appRoot, TradeEndOfDayParameter eodParam):base(appRoot)
        {
            _appRoot = appRoot;
            _eodParam = eodParam;
            _commandId = Guid.Empty;
            _setCommandId = commandId => _commandId = commandId;
            _consumeEvents = new IEvent[]{
                new EndOfDayFundTransactionProcessedCompleteEvent { }.SetEventSource($"{EventTopic.FundEvents}"),
                new EndOfDayFundTransactionProcessedFailEvent{ }.SetEventSource($"{EventTopic.FundEvents}"),
                new OptionTradeEndOfDayProcessedFailEvent { }.SetEventSource($"{EventTopic.MarketDataEvents}"),
            };
            _eventModel = AppRoot.GetModel<EndOfDayProcessEventModel>();
            _fundQueryModel = _appRoot.GetModel<FundQueryModel>();
            _tradeQueryModel = _appRoot.GetModel<TradeQueryModel>();
            _mktDataFeedQueryModel = _appRoot.GetModel<MarketDataFeedQueryModel>();
            ValueDate = eodParam.ValueDate.Date;
        }

        public int FundId => _eodParam.FundId;
        public int OrderId => _eodParam.OrderId;
        public int TradeId => _eodParam.TradeId;
        public TradeType TradeType => _eodParam.TradeType;
        public DateTime ValueDate { get; set; }
        public double OpenPrice => _openPrice;
        public double HighPrice => _highPrice;
        public double LowPrice => _lowPrice;
        public double ClosePrice => _closePrice;
        public int Volume => _volume;
        public decimal TradePnl => _tradePnl;
        public decimal FundBalance { get; set; }
        public string Reference { get; set; }
        public TradeType PutSpreadType(TradeType tradeType) => tradeType switch
        {
            TradeType.ShortIronCondor => TradeType.PutCreditSpread,
            TradeType.LongIronCondor => TradeType.PutDebitSpread,
            _ => throw new NotImplementedException()
        };
        public TradeType CallSpreadType(TradeType tradeType) => tradeType switch
        {
            TradeType.ShortIronCondor => TradeType.CallCreditSpread,
            TradeType.LongIronCondor => TradeType.CallDebitSpread,
            _ => throw new NotImplementedException()
        };

        public Action OnEndOfDayProcessCompleted;
        public Action<string> OnEndOfDayProcessFailed;

        public void StartListener()
           => _eventModel.Execute(async e => {
               e.OnError((errorCode, errorMsg) => OnError(errorCode, errorMsg));
               await e.StartEndOfDayProcessListenerAsync(_consumeEvents, HandleEventAsync);
           });

        public void StopListener()
            => _eventModel.Execute(async e => {
                e.OnError((errorCode, errorMsg) => OnError(errorCode, errorMsg));
                await e.StopEndOfDayProcessListenerAsync();
            });

        Task HandleEventAsync(IEvent e)
        {
            if (_commandId != e.CommandId)
                return Task.CompletedTask;
            return e switch
            {
                EndOfDayFundTransactionProcessedCompleteEvent o => EndOfDayFundProcessCompleted(o),
                EndOfDayFundTransactionProcessedFailEvent o => EndOfDayFundProcessFailed(o),
                OptionTradeEndOfDayProcessedFailEvent o => OptionTradeEndOfDayProcessedFailed(o),
                _ => Task.CompletedTask
            };

            Task EndOfDayFundProcessCompleted(EndOfDayFundTransactionProcessedCompleteEvent e)
            {
                OnEndOfDayProcessCompleted?.Invoke();
                _commandId = Guid.Empty;
                return Task.CompletedTask;
            }

            Task EndOfDayFundProcessFailed(EndOfDayFundTransactionProcessedFailEvent e)
            {
                OnEndOfDayProcessFailed?.Invoke(e.ErrorMessage);
                _commandId = Guid.Empty;
                return Task.CompletedTask;
            }

            Task OptionTradeEndOfDayProcessedFailed(OptionTradeEndOfDayProcessedFailEvent e)
            {
                OnEndOfDayProcessFailed?.Invoke(e.ErrorMessage);
                _commandId = Guid.Empty;
                return Task.CompletedTask;
            }
        }

        /// <summary>
        /// load data
        /// </summary>
        public void LoadData(Action onLoadDataComplete)
        {
            LoadFund(() => LoadOptionTrade(trade => LoadFuturesEodData(trade)));
            return;

            void LoadFund(Action onFundLoaded)
                => _fundQueryModel.Execute(async model => {
                    model.OnError((_, errorMsg) => MessageBox.Show(errorMsg, "Loading Fund Data Error", MessageBoxButtons.OK, MessageBoxIcon.Error));
                    await model.GetFundsAsync(funds =>
                    {
                        if ((funds?.Length ?? 0) > 0)
                        {
                            var fund = funds.Where(e => e.FundId == FundId).SingleOrDefault();
                            FundBalance = fund?.Balance ?? 0m;
                            onFundLoaded?.Invoke();
                        }
                    });
                });

            void LoadOptionTrade(Action<OptionTradeViewModel> onTradeLoaded)
                => _tradeQueryModel.Execute(async model => {
                    model.OnError((_, errorMsg) => MessageBox.Show(errorMsg, "Loading Option Trade Error", MessageBoxButtons.OK, MessageBoxIcon.Error));
                    await model.GetOptionTradeAsync(OrderId, TradeId, e => onTradeLoaded?.Invoke(e));
                });

            void LoadFuturesEodData(OptionTradeViewModel optionTrade)
                => _mktDataFeedQueryModel.Execute(async model => {
                    model.OnError((_, errorMsg) => MessageBox.Show(errorMsg, "Loading End Of Day Data Error", MessageBoxButtons.OK, MessageBoxIcon.Error));
                    await model.GetFuturesEodDataAsync(_eodParam.BaseContractId, ValueDate, futuresEodData => {
                        _openPrice = futuresEodData.OpenPrice;
                        _highPrice = futuresEodData.HighPrice;
                        _lowPrice = futuresEodData.LowPrice;
                        _closePrice = futuresEodData.ClosePrice;
                        _volume = futuresEodData.Volume;
                        var daysToExpiry = (optionTrade.MaturityDate - ValueDate).Days;
                        var psKey = new TradePositionId(OrderId, TradeId, PutSpreadType(optionTrade.TradeType), ValueDate, daysToExpiry, TradeStatus.IntraDay);
                        var csKey = new TradePositionId(OrderId, TradeId, CallSpreadType(optionTrade.TradeType), ValueDate, daysToExpiry, TradeStatus.IntraDay);
                        _tradePnl = (optionTrade.TradePositions.Get(psKey)?.TradePnl ?? 0m) + (optionTrade.TradePositions.Get(csKey)?.TradePnl ?? 0m);
                        FundBalance += TradePnl;
                        onLoadDataComplete?.Invoke();
                    });
                });
        }

        /// <summary>
        /// run end of day process
        /// </summary>
        public void RunEndOfDayProcess()
            => AppRoot.GetModel<TradeCommandModel>().Execute(async model => {
                model.OnError((_, errorMsg) => OnEndOfDayProcessFailed?.Invoke($"End Of Day process failed due to {errorMsg}"));
                await model.ProcessEndOfDayAsync(
                    FundId,
                    OrderId,
                    TradeId,
                    TradeType,
                    ValueDate,
                    TradeStatus.EndOfDay,
                    OpenPrice,
                    HighPrice,
                    LowPrice,
                    ClosePrice,
                    Volume,
                    Reference ?? string.Empty,
                    _setCommandId);
            });

    }
}
