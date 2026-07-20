using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using TomasAI.IFM.Contracts;
using TomasAI.IFM.Models;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.Fund;
using TomasAI.IFM.Shared.Fund.Events;
using TomasAI.IFM.Shared.Fund.ViewModels;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.TradeOrder.ViewModels;
using TomasAI.IFM.Shared.Reference.ViewModels;
using TomasAI.IFM.Shared.OptionPricer;
using TomasAI.IFM.Shared.EventSourcing;
using QLNet;

namespace TomasAI.IFM.ViewModels.Trade.IronCondor
{
    public class IronCondorTradeOrderReadModel
    {

        IAppRoot _appRoot;
        OptionTradeViewModel _ironCondorTrade;
        OptionTradeViewModel _parentTrade;
        DateTime _valueDate;
        int _fundId;
        FuturesContractViewModel _baseContract;
        FundOrderReadModel _fundOrder;
        FundOrderTradeReadModel _fundOrderTrade;
        OrderActionType _orderActionType;
        Action<DateTime> _updateDaysToExpiryAction;
        Action<DateTime> _updateTradeDateAction;
        DefaultFuturesContractDefinitionsReadModel _defaultFuturesContractDefinitions;
        double _riskFreeRate;
        int _putSpreadStrikeWidth;
        int _callSpreadStrikeWidth;
        Dictionary<(OptionLegAction OptionLegAction, OptionType OptionType), OptionLegReadModel> _optionLegMap;
        Dictionary<(DateTime ValueDate, TradeType TradeType, TradeStatus TradeStatus, OptionLegAction OptionLegAction, OptionType OptionType), OptionLegDataReadModel> _optionLegDataMap;
        Dictionary<(DateTime ValueDate, TradeType TradeType, TradeStatus TradeStatus), TradePositionReadModel> _tradePositionMap;
        Dictionary<(OptionLegAction OptionLegAction, OptionType OptionType), decimal> _optionPriceMap;
        RiskPositionType[] _riskPositionTypes;
        RiskPositionType _riskPositionType;
        Guid _liveFeedQuoteId;

        public IAppRoot AppRoot => _appRoot;
        public DateTime ValueDate => _valueDate;
        public DateTime TradeDate => _ironCondorTrade.TradeDate;
        public DateTime MaturityDate => _ironCondorTrade.MaturityDate;
        public TradeType TradeType => _ironCondorTrade?.TradeType ?? TradeType.Unknown;
        public int FundId => _fundId;
        public FuturesContractViewModel BaseContract => _baseContract;
        public FundOrderTradeReadModel FundOrderTrade => _fundOrderTrade;
        public Action<DateTime> UpdateDaysToExpiryAction => _updateDaysToExpiryAction;
        public Action<DateTime> UpdateTradeDateAction => _updateTradeDateAction;
        public DefaultFuturesContractDefinitionsReadModel DefaultFuturesContractDefinitions => _defaultFuturesContractDefinitions;
        public double RiskFreeRate => _riskFreeRate;
        public OptionTradeViewModel IronCondorTrade => _ironCondorTrade;
        public OptionLegReadModel[] OptionLegs => _ironCondorTrade.OptionLegs;
        public OptionLegDataReadModel[] OptionLegData => _ironCondorTrade.TradePositions.SelectMany(e => e.OptionLegData).ToArray();
        public TradePositionReadModel[] TradePosition => _ironCondorTrade.TradePositions;
        public OptionTradeViewModel ParentTrade => _parentTrade;
        public OrderActionType OrderActionType => _orderActionType;
        public decimal FundBalance { get; set; }
        public decimal OrderPrice { get; set; }
        public int OrderQuantity => _ironCondorTrade.OptionLegs.Sum(e => e.Quantity) / _ironCondorTrade.OptionLegs.Count();
        public int ParentTradeOrderQuantity => _parentTrade.OptionLegs.Sum(e => e.Quantity) / _ironCondorTrade.OptionLegs.Count();

        public decimal OrderAmount => _ironCondorTrade.TradePositions.Get(this.PutSpreadTradeType, this.TradeStatus).TradeValue 
            + _ironCondorTrade.TradePositions.Get(this.CallSpreadTradeType, this.TradeStatus).TradeValue;

        public decimal TradeCommission => OrderQuantity * 1.42m * _ironCondorTrade.OptionLegs.Count() * -1.0m;
        public decimal TotalAmount => OrderAmount + TradeCommission;

        public void GetIntraDayPnl( Action<decimal> onCompletion)
            =>  _appRoot.GetModel<TradeQueryModel>().Execute(async model => 
                    await model.GetTradeHistoryAsync(_ironCondorTrade.OrderId, intraDayPnl => {
                        if (intraDayPnl is not null && intraDayPnl.Length > 0)
                            onCompletion?.Invoke(intraDayPnl.Where(e => e.TradeStatus == TradeStatus.IntraDay).Sum(e => e.TradePnl));
                        else
                            onCompletion?.Invoke(0m);
                    }));

        public decimal Commission(TradePositionReadModel spd)
        {
            var optionLegCount = spd.OptionLegData.Count();
            var optionQuantity = spd.OptionLegData.Sum(e => e.OptionLeg.Quantity)/optionLegCount;
            return optionQuantity * 1.42m * optionLegCount;
        }

        public int DaysToExpiry => _ironCondorTrade.TradePositions.Get(this.PutSpreadTradeType, this.TradeStatus).Key.DaysToExpiry;

        public void SetTradeDate(DateTime tradeDate) => _ironCondorTrade = _ironCondorTrade with { TradeDate = tradeDate };
        public void SetMaturityDate(DateTime maturityDate) => _ironCondorTrade = _ironCondorTrade with { MaturityDate = maturityDate };
        public void SetDaysToExpiry()
        {
            var tradePosition = _ironCondorTrade.TradePositions.Get(this.PutSpreadTradeType, this.TradeStatus, this.ValueDate);
            tradePosition = tradePosition with { DaysToExpiry = (_ironCondorTrade.MaturityDate - this.ValueDate).Days };
            _ironCondorTrade.TradePositions.Set(tradePosition);
            tradePosition = _ironCondorTrade.TradePositions.Get(this.CallSpreadTradeType, this.TradeStatus, this.ValueDate);
            tradePosition = tradePosition with { DaysToExpiry = (_ironCondorTrade.MaturityDate - this.ValueDate).Days };
            _ironCondorTrade.TradePositions.Set(tradePosition);
        }

        public void SetTradeStatus(OrderActionType orderActionType)
        {
            var tradeStatus = orderActionType switch
            {
                OrderActionType.Open => TradeStatus.Open,
                OrderActionType.Close => TradeStatus.Close,
                _ => throw new NotImplementedException()
            };
            var oldTradePosition = _ironCondorTrade.TradePositions.Get(this.PutSpreadTradeType, this.TradeStatus, this.ValueDate);

            var newTradePosition = oldTradePosition with { TradeStatus = tradeStatus };
            foreach (var old in newTradePosition.OptionLegData)
                newTradePosition.OptionLegData.Set(old.OptionLegId, old with { TradeStatus = tradeStatus });
            _ironCondorTrade.TradePositions.Set(oldTradePosition, newTradePosition);
            oldTradePosition = _ironCondorTrade.TradePositions.Get(this.CallSpreadTradeType, this.TradeStatus, this.ValueDate);
            newTradePosition = oldTradePosition with { TradeStatus = tradeStatus };
            foreach (var old in newTradePosition.OptionLegData)
                newTradePosition.OptionLegData.Set(old.OptionLegId, old with { TradeStatus = tradeStatus });
            _ironCondorTrade.TradePositions.Set(oldTradePosition, newTradePosition);
            _orderActionType = orderActionType;
            MapOptionLegData();
        }

        public void SetTradeLimit(decimal maxLoss, decimal minProfitTarget, decimal dailyProfitTarget) => _ironCondorTrade = _ironCondorTrade.SetTradeLimit(maxLoss, minProfitTarget, dailyProfitTarget);

        public int SetPutSpreadStrike(int strikePrice) => strikePrice - _putSpreadStrikeWidth;
        public int SetCallSpreadStrike(int strikePrice) => strikePrice + _callSpreadStrikeWidth;
        public void SetOrderAction(OrderActionType orderActionType) => _orderActionType = orderActionType;
        public OrderAction GetOrderAction()
            => _ironCondorTrade.TradeType switch {
                TradeType.ShortIronCondor => OrderAction.Sell,
                TradeType.LongIronCondor => OrderAction.Buy,
                _ => throw new NotImplementedException()
            };

        public Action<string, string> ShowErrorMessage { get; set; }
        public Action ShowIronCondorTrade { get; set; }
        public Action ShowTradePositions { get; set; }
        public Action<TradeLimitReadModel, decimal> ShowTradeLimits { get; set; }
        public Action<TradeTypeLimitReadModel, TradeTypeLimitReadModel> ShowTradeTypeLimits { get; set; }
        public Action<FuturesEodDataViewModel> ShowLiveFeedValues { get; set; }
        public Action<object[], int> ShowStrikePrices { get; set; }
        public Action<decimal> ShowAssetPrice { get; set; }
        public Action HideClosingTradeControls { get; set; }
        public Action<OrderActionType> OrderActionTypeChanged { get; set; }
        public Action<Action> DrawView { get; set; }
        public Action<double> ShowForwardDelta { get; set; }
        public Action<RiskPositionType[]> ShowRiskPositionTypes { get; set; }
        public Action<int> SetRiskPositionTypeIndex { get; set; }
        public Action<FundMaxProfitGeneratedCompleteEvent> FundMaxProfitChanged { get; set; }
        public Action<FundMaxProfitGeneratedFailEvent> FundMaxProfitFailed { get; set; }
        public Action<CommandExceptionEvent> FundMaxProfitException { get; set; }

        public TradeType PutSpreadTradeType => _fundOrderTrade.TradeType == TradeType.ShortIronCondor ? TradeType.PutCreditSpread : TradeType.PutDebitSpread;
        public TradeType ParentPutSpreadTradeType => _parentTrade.TradeType == TradeType.ShortIronCondor ? TradeType.PutCreditSpread : TradeType.PutDebitSpread;
        public TradeType CallSpreadTradeType => _fundOrderTrade.TradeType == TradeType.ShortIronCondor ? TradeType.CallCreditSpread : TradeType.CallDebitSpread;
        public TradeType ParentCallSpreadTradeType => _parentTrade.TradeType == TradeType.ShortIronCondor ? TradeType.CallCreditSpread : TradeType.CallDebitSpread;
        public TradeStatus TradeStatus => _orderActionType == OrderActionType.Open ? TradeStatus.Open : TradeStatus.Close;
        public OptionLegAction ShortOptionLegAction => _fundOrderTrade.TradeType == TradeType.ShortIronCondor ? OptionLegAction.Short : OptionLegAction.Long;
        public OptionLegAction LongOptionLegAction => _fundOrderTrade.TradeType == TradeType.ShortIronCondor ? OptionLegAction.Long : OptionLegAction.Short;

        public OptionLegAction OptionLeg1Action => _fundOrderTrade.TradeType == TradeType.ShortIronCondor ? OptionLegAction.Short : OptionLegAction.Long;
        public OptionLegAction OptionLeg2Action => _fundOrderTrade.TradeType == TradeType.ShortIronCondor ? OptionLegAction.Long : OptionLegAction.Short;
        public OptionLegAction OptionLeg3Action => _fundOrderTrade.TradeType == TradeType.ShortIronCondor ? OptionLegAction.Short : OptionLegAction.Long;
        public OptionLegAction OptionLeg4Action => _fundOrderTrade.TradeType == TradeType.ShortIronCondor ? OptionLegAction.Long : OptionLegAction.Short;

        public OptionLegAction ParentOptionLeg1Action => _parentTrade.TradeType == TradeType.ShortIronCondor ? OptionLegAction.Short : OptionLegAction.Long;
        public OptionLegAction ParentOptionLeg2Action => _parentTrade.TradeType == TradeType.ShortIronCondor ? OptionLegAction.Long : OptionLegAction.Short;
        public OptionLegAction ParentOptionLeg3Action => _parentTrade.TradeType == TradeType.ShortIronCondor ? OptionLegAction.Short : OptionLegAction.Long;
        public OptionLegAction ParentOptionLeg4Action => _parentTrade.TradeType == TradeType.ShortIronCondor ? OptionLegAction.Long : OptionLegAction.Short;

        public int NearestPutStrike { get; set; }
        public int NearestCallStrike { get; set; }
        public double OTMPutProbability { get; set; }
        public double OTMCallProbability { get; set; }
        public string LocalSymbol { get; set; }

        public IronCondorTradeOrderReadModel(Control parent, IAppRoot appRoot, DateTime valueDate, int fundId, FuturesContractViewModel baseContract, FundOrderReadModel fundOrder, FundOrderTradeReadModel fundOrderTrade,
            OrderActionType orderActionType, Action<DateTime> updateDaysToExpiryAction, Action<DateTime> updateTradeDateAction)
        {
            switch(fundOrderTrade.TradeType)
            {
                case TradeType.ShortIronCondor:
                case TradeType.LongIronCondor:
                    break;
                default:
                    throw new InvalidOperationException($"IronCondorTradeOrderReadModel.Constructor: invalid trade type '{_fundOrderTrade.TradeType}'");
            }
            _appRoot = appRoot;
            _valueDate = valueDate;
            _fundId = fundId;
            _baseContract = baseContract;
            _fundOrder = fundOrder;
            _fundOrderTrade = fundOrderTrade;
            _orderActionType = orderActionType;
            _updateDaysToExpiryAction = updateDaysToExpiryAction;
            _updateTradeDateAction = updateTradeDateAction;
            _putSpreadStrikeWidth = 30; // change to get strike width from reference data...
            _callSpreadStrikeWidth = 15; // change to get strike width from reference data...
            _liveFeedQuoteId = Guid.Empty;
        }

        public void LoadDefaultFuturesContractDefinitions()
            => _appRoot.GetModel<ReferenceQueryModel>().Execute(async model => {
                model.OnError((_, errorMsg) => ShowErrorMessage(errorMsg, "Loading Default Futures Contract Definitions Error"));
                await model.LoadDefaultFuturesContractDefinitionsAsync(defContractDef => _defaultFuturesContractDefinitions = defContractDef);
                LoadForwardDelta();
                LoadRiskFreeRate();
            });

        public void SetRiskPositionType()
        {
            for (var index = 0; index < _riskPositionTypes.Length; index++)
                if (_riskPositionTypes[index] == _riskPositionType)
                {
                    SetRiskPositionTypeIndex?.Invoke(index);
                    break;
                }
        }

        public void SetFundMaxProfit()
            => _appRoot.GetModel<FundCommandModel>().Execute(async model => {
                model.OnError((_, errorMsg) => ShowErrorMessage(errorMsg, "Setting Fund Max Profit Error Error"));
                await model.StartFundRiskMarginEventConsumerAsync(FundMaxProfitChanged, FundMaxProfitFailed, FundMaxProfitException);
                await Task.Delay(TimeSpan.FromSeconds(1));
                await model.GenerateFundRiskMarginAsync(_fundOrder);
            });

        public void StopFundRiskMarginEventConsumer()
             => _appRoot.GetModel<FundCommandModel>().Execute(async model => {
                 await model.StopFundRiskMarginEventConsumerAsync();
             });

         void LoadForwardDelta()
            => _appRoot.GetModel<MarketDataFeedQueryModel>().Execute(async model => {
                model.OnError((_, errorMsg) => ShowErrorMessage(errorMsg, "Loading Risk Position Type Error"));
                await model.GetFuturesRiskPositionTypeAsync(_valueDate, _fundOrderTrade.TradeType,
                    e => _appRoot.GetModel<TradePlanQueryModel>().Execute(tpqModel =>
                    {
                        _riskPositionType = e.RiskPositionType;
                        tpqModel.OnError((_, errorMsg) => ShowErrorMessage(errorMsg, "Loading Forward Delta Error"));
                        /*
                        Task.Run(async () => 
                        {
                            await tpqModel.GetIronCondorForwardDeltaAsync(_valueDate, _fundOrderTrade.TradeType, e.RiskPositionType,
                                   forwardDelta =>
                                   {
                                       _forwardDelta = forwardDelta;
                                       _riskPositionTypes = new RiskPositionType[]
                                       {
                                                RiskPositionType.High,
                                                RiskPositionType.Medium,
                                                RiskPositionType.Low,
                                       };
                                       ShowRiskPositionTypes?.Invoke(_riskPositionTypes);
                                   });
                        });
                        */
                    }));
            });

        private void LoadRiskFreeRate()
            => _appRoot.GetModel<MarketDataQueryModel>().Execute(async model => {
                model.OnError((_, errorMsg) => ShowErrorMessage(errorMsg, "Loading Risk Free Rate Error"));
                await model.GetRiskFreeRateAsync(riskFreeRate => _riskFreeRate = riskFreeRate);
                LoadIronCondorTrade();
            });

        private void LoadIronCondorTrade()
        {

            this.LoadStrikePrices(strikePrices =>
                _appRoot.GetModel<TradeQueryModel>().Execute(async tradeModel =>
                {
                    var orderId = _fundOrderTrade.OrderId;
                    var tradeId = _fundOrderTrade.TradeId;
                    tradeModel.OnError((_, errorMsg) => ShowErrorMessage(errorMsg, "Loading Iron Condor Option Trade Error"));
                    await tradeModel.GetOptionTradeAsync(orderId, tradeId, optionTrade => {
                        _ironCondorTrade = optionTrade != null ? optionTrade : CreateIronCondorTrade(this.TradeStatus);
                        MapOptionLeg();
                        MapOptionLegData();
                        MapOptionPriceFromTradeReference(_fundOrderTrade.TradeAction, _fundOrderTrade.Reference);
                        _appRoot.GetModel<FundQueryModel>().Execute(async fundModel =>
                        {
                            fundModel.OnError((_, errorMsg) => ShowErrorMessage(errorMsg, "Loading Fund Balance Error"));
                            await fundModel.GetFundBalanceAsync(_fundId, fundBalance =>
                            {
                                FundBalance = fundBalance;
                                DrawView(() =>
                                {
                                    UpdateTradeDateAction(_ironCondorTrade.TradeDate);
                                    ShowTradeLimits(_ironCondorTrade.TradeLimit, fundBalance);
                                    ShowTradeTypeLimits(
                                       _ironCondorTrade.TradeTypeLimits.Get(this.PutSpreadTradeType),
                                       _ironCondorTrade.TradeTypeLimits.Get(this.CallSpreadTradeType));
                                    ShowStrikePrices(strikePrices, strikePrices.Length / 2);
                                    ShowIronCondorTrade();
                                    OrderActionTypeChanged(_orderActionType);
                                });
                            });
                        });
                        _appRoot.GetModel<MarketDataFeedQueryModel>().Execute(async mktDataFeedModel =>
                        {
                            mktDataFeedModel.OnError((_, errorMsg) => ShowErrorMessage(errorMsg, "Loading Futures Eod Data Error"));
                            await mktDataFeedModel.GetFuturesEodDataAsync(_baseContract.ContractId, _valueDate, futuresEodData =>
                            {
                                if (futuresEodData is not null)
                                {
                                    LoadBaseValues(futuresEodData);
                                    ShowAssetPrice(Convert.ToDecimal(futuresEodData.ClosePrice));
                                }
                            });
                        });
                    });
                    
                }));
            return;

            void MapOptionLeg()
            {
                _optionLegMap = new();
                var optionLeg = _ironCondorTrade.OptionLegs.Get(OptionLegAction.Long, OptionType.Put);
                if (optionLeg is not null)
                    _optionLegMap.Add((OptionLegAction.Long, OptionType.Put), optionLeg);
                optionLeg = _ironCondorTrade.OptionLegs.Get(OptionLegAction.Short, OptionType.Put);
                if (optionLeg is not null)
                    _optionLegMap.Add((OptionLegAction.Short, OptionType.Put), optionLeg);
                optionLeg = _ironCondorTrade.OptionLegs.Get(OptionLegAction.Long, OptionType.Call);
                if (optionLeg is not null)
                    _optionLegMap.Add((OptionLegAction.Long, OptionType.Call), optionLeg);
                optionLeg = _ironCondorTrade.OptionLegs.Get(OptionLegAction.Short, OptionType.Call);
                if (optionLeg is not null)
                    _optionLegMap.Add((OptionLegAction.Short, OptionType.Call), optionLeg);
            }

        }

        void MapOptionLegData()
        {
            _optionLegDataMap = new Dictionary<(DateTime ValueDate, TradeType TradeType, TradeStatus TradeStatus, OptionLegAction OptionLegAction, OptionType OptionType), OptionLegDataReadModel>();
            _tradePositionMap = new Dictionary<(DateTime ValueDate, TradeType TradeType, TradeStatus TradeStatus), TradePositionReadModel>();
            foreach (var e in _ironCondorTrade.TradePositions)
            {
                if (!_tradePositionMap.ContainsKey((e.Key.ValueDate, e.Key.TradeType, e.Key.TradeStatus)))
                    _tradePositionMap.Add((e.Key.ValueDate, e.Key.TradeType, e.Key.TradeStatus), e);
                foreach (var o in e.OptionLegData)
                    if (!_optionLegDataMap.ContainsKey((e.Key.ValueDate, e.Key.TradeType, e.Key.TradeStatus, o.OptionLeg.OptionLegAction, o.OptionLeg.OptionLegType)))
                        _optionLegDataMap.Add(
                            key: (e.Key.ValueDate, e.Key.TradeType, e.Key.TradeStatus, o.OptionLeg.OptionLegAction, o.OptionLeg.OptionLegType),
                            value: o);
            }
        }


        public void RemoveTradeFromFundOrder(FundOrderTradeId fundOrderTradeId)
            => _appRoot.GetModel<FundCommandModel>().Execute(async model => {
                  await model.RemoveTradeFromFundOrderAsync(fundOrderTradeId, null);
            });

        public void SubmitOrder(TradeOrderReadModel tradeOrder, Action<Guid> setCommandId)
        {
            // get manual trade fills if not sending to broker for trade fills...
            _ironCondorTrade = _ironCondorTrade with
            {
                TradeState = TradeState.OrderPlaced,
                IsPrimaryTrade = _fundOrderTrade.PrimaryTrade
            };

            // check if trade fills need to be added manually...
            AddManualTradeFills();
            //AddTradeTicketLegs();

            // populate options contracts if they do not exist...
            AddNewFuturesOptionContracts();

            // place order...
            _appRoot.GetModel<TradeCommandModel>().Execute(async model => {
                model.OnError((_, errorMsg) => ShowErrorMessage(errorMsg, "Submit Order Error"));
                await model.PlaceOrderAsync(tradeOrder, _ironCondorTrade, setCommandId);
            });
            return;

            void AddManualTradeFills()
            {
                if (tradeOrder.TradeFillType == TradeFillType.Manual)
                {
                    _ironCondorTrade = _ironCondorTrade
                        .AddTradeFills( GetManualTradeFills(tradeOrder.FundId, tradeOrder.OrderQuantity) )
                        with { TradeState = tradeOrder.OrderActionType == OrderActionType.Open
                            ? TradeState.TradeToOpen
                            : TradeState.TradeToClose };
                }
            }

            void AddNewFuturesOptionContracts()
            {
                var contracts = new List<FuturesOptionContractReadModel>();
                var contractIds = _ironCondorTrade.OptionLegs.Select(e => e.ContractId).ToArray();
                foreach (var contractId in contractIds.Select(e => new FuturesOptionContractId(e)))
                {
                    var futuresOptionContract = GetFuturesOptionContract(contractId.StrikePrice, contractId.OptionType, contractId.ContractMonth);
                    contracts.Add(futuresOptionContract);
                }
                var newContracts = contracts.ToArray();
                _appRoot
                    .GetModel<MarketDataCommandModel>()
                    .Execute(async model => await model.AddFuturesOptionContractsAsync(newContracts, () => { }));
            }

        }

        public void TurnLiveFeedOn()
        {
            if (_liveFeedQuoteId  != Guid.Empty) return;

            // save futures option contracts...
            _liveFeedQuoteId = Guid.NewGuid();
            var contracts = new List<FuturesOptionContractReadModel>();
            foreach (var contractId in _optionLegMap.Values.Select(e => new FuturesOptionContractId(e.ContractId)))
                contracts.Add(GetFuturesOptionContract(contractId.StrikePrice, contractId.OptionType, contractId.ContractMonth));

            var newContracts = contracts.ToArray();
            _appRoot
                .GetModel<MarketDataCommandModel>()
                .Execute(async model => await model.AddFuturesOptionContractsAsync(newContracts, () => StartStreamingFuturesOptionQuoteData(_liveFeedQuoteId, newContracts)));
            return;

            void StartStreamingFuturesOptionQuoteData(Guid quoteId, FuturesOptionContractReadModel[]  futuresOptionContracts)
            {
                _appRoot.GetModel<MarketDataFeedQueryModel>().Execute(async queryModel =>
                {
                    // generate request id's for each quote...
                    queryModel.OnError((_, errorMsg) => this.ShowErrorMessage(errorMsg, "Start Streaming Quotes Error"));
                    var futuresOptionQuotes = new List<FuturesOptionQuoteReadModel>();
                    foreach (var e in futuresOptionContracts)
                    {
                        var streamId = Guid.NewGuid();
                        var requestId = await queryModel.GetStreamingRequestIdAsync(streamId);
                        futuresOptionQuotes.Add(new FuturesOptionQuoteReadModel(streamId, requestId, quoteId, e.ContractId, "basilt", DateTime.Now));
                    }

                    // start streaming quotes...
                    _appRoot.GetModel<MarketDataFeedCommandModel>().Execute(async commandModel =>
                    {
                        commandModel.OnError((_, errorMsg) => this.ShowErrorMessage(errorMsg, "Start Streaming Quotes Error"));
                        await commandModel.StartStreamingFuturesOptionQuoteDataAsync(quoteId, futuresOptionQuotes.ToArray(), futuresOptionContracts);
                    });

                    // start quote streaming data listener...
                    _appRoot.GetModel<MarketDataFeedEventModel>().Execute(async eventModel =>
                    {
                        eventModel.OnError((_, errorMsg) => this.ShowErrorMessage(errorMsg, "Start Streaming Quote Data Listener Error"));
                        await eventModel.StartFuturesOptionQuoteDataListenerAsync(e => SetLiveFeedQuoteData(e.OptionQuoteData));
                    });
                });
            }

             void SetLiveFeedQuoteData(FuturesOptionQuoteDataReadModel futuresOptionQuoteData)
                => _appRoot.GetModel<MarketDataFeedQueryModel>().Execute(async model => {
                    model.OnError((_, errorMsg) => this.ShowErrorMessage(errorMsg, "Set Live Feed Quote Data Error"));
                    await model.GetFuturesEodDataAsync(_baseContract.ContractId, _valueDate, futuresEodData => {
                        SetLiveFeedQuoteDataValues(futuresEodData, futuresOptionQuoteData, () =>
                        {
                            ShowAssetPrice?.Invoke(Convert.ToDecimal(futuresEodData.ClosePrice));
                            ShowLiveFeedValues?.Invoke(futuresEodData);
                        });
                    });
                });
        }

        void SetLiveFeedQuoteDataValues(FuturesEodDataViewModel futuresEodData, FuturesOptionQuoteDataReadModel futuresOptionQuoteData, Action onLiveFeedCompleted)
        {
            var assetPrice = futuresEodData.ClosePrice;
            var daysToExpiry = (_ironCondorTrade.MaturityDate - _ironCondorTrade.TradeDate).Days;
            var timeValue = daysToExpiry / 365.0;
            var pcs = GetTradePosition(this.PutSpreadTradeType, this.TradeStatus);
            pcs = pcs with { AssetPrice = Convert.ToDecimal(assetPrice) };
            _ironCondorTrade.TradePositions.Set(pcs);

            var ccs = GetTradePosition(this.CallSpreadTradeType, this.TradeStatus);
            ccs = ccs with { AssetPrice = Convert.ToDecimal(assetPrice) };
            _ironCondorTrade.TradePositions.Set(ccs);

            _ = futuresOptionQuoteData switch
            {
                _ when IsShortPutOptionQuote() => SetShortPutOptionLegData(),
                _ when IsLongPutOptionQuote() => SetLongPutOptionLegData(),
                _ when IsShortCallOptionQuote() => SetShortCallOptionLegData(),
                _ when IsLongCallOptionQuote() => SetLongCallOptionLegData(),
                _ => false
            };
            onLiveFeedCompleted?.Invoke();
            return;

            bool IsShortPutOptionQuote()
            {
                var shortPutOptionContract = GetFuturesOptionContract(
                        strikePrice: Convert.ToDouble(GetOptionLeg(this.ShortOptionLegAction, OptionType.Put).StrikePrice),
                        futuresOptionType: OptionType.Put,
                        contractMonth: _ironCondorTrade.MaturityDate);
                return futuresOptionQuoteData.ContractId == shortPutOptionContract.ContractId;
            }

            bool SetShortPutOptionLegData()
            {
                var putOptionLegData = GetOptionLegData(this.PutSpreadTradeType, this.TradeStatus, this.ShortOptionLegAction, OptionType.Put)
                    with
                {
                    BidPrice = Convert.ToDecimal(futuresOptionQuoteData.BidPrice),
                    AskPrice = Convert.ToDecimal(futuresOptionQuoteData.AskPrice)
                };
                this.OptionLegData.Set(putOptionLegData.OptionLegId, putOptionLegData);
                UpdatePutCreditSpreadLiveFeedValues();
                return true;
            }

            bool IsLongPutOptionQuote()
            {
                var longPutOptionContract = GetFuturesOptionContract(
                    strikePrice: Convert.ToDouble(GetOptionLeg(this.LongOptionLegAction, OptionType.Put).StrikePrice),
                    futuresOptionType: OptionType.Put,
                    contractMonth: _ironCondorTrade.MaturityDate);
                return futuresOptionQuoteData.ContractId == longPutOptionContract.ContractId;
            }

            bool SetLongPutOptionLegData()
            {
                var putOptionLegData = GetOptionLegData(this.PutSpreadTradeType, this.TradeStatus, this.LongOptionLegAction, OptionType.Put)
                         with
                {
                    BidPrice = Convert.ToDecimal(futuresOptionQuoteData.BidPrice),
                    AskPrice = Convert.ToDecimal(futuresOptionQuoteData.AskPrice)
                };
                this.OptionLegData.Set(putOptionLegData.OptionLegId, putOptionLegData);
                UpdatePutCreditSpreadLiveFeedValues();
                return true;
            }

            bool IsShortCallOptionQuote()
            {
                var shortCallOptionContract = GetFuturesOptionContract(
                                strikePrice: Convert.ToDouble(_ironCondorTrade.OptionLegs.Get(this.ShortOptionLegAction, OptionType.Call).StrikePrice),
                                futuresOptionType: OptionType.Call,
                                contractMonth: _ironCondorTrade.MaturityDate);
                return futuresOptionQuoteData.ContractId == shortCallOptionContract.ContractId;
            }

            bool SetShortCallOptionLegData()
            {
                var callOptionLegData = GetOptionLegData(this.CallSpreadTradeType, this.TradeStatus, this.ShortOptionLegAction, OptionType.Call)
                             with
                {
                    BidPrice = Convert.ToDecimal(futuresOptionQuoteData.BidPrice),
                    AskPrice = Convert.ToDecimal(futuresOptionQuoteData.AskPrice)
                };
                this.OptionLegData.Set(callOptionLegData.OptionLegId, callOptionLegData);
                UpdateCallCreditSpreadLiveFeedValues();
                return true;
            }

            bool IsLongCallOptionQuote()
            {
                var longCallOptiononContract = GetFuturesOptionContract(
                               strikePrice: Convert.ToDouble(_ironCondorTrade.OptionLegs.Get(this.LongOptionLegAction, OptionType.Call).StrikePrice),
                               futuresOptionType: OptionType.Call,
                               contractMonth: _ironCondorTrade.MaturityDate);
                return futuresOptionQuoteData.ContractId == longCallOptiononContract.ContractId;
            }

            bool SetLongCallOptionLegData()
            {
                var callOptionLegData = GetOptionLegData(this.CallSpreadTradeType, this.TradeStatus, this.LongOptionLegAction, OptionType.Call)
                    with
                {
                    BidPrice = Convert.ToDecimal(futuresOptionQuoteData.BidPrice),
                    AskPrice = Convert.ToDecimal(futuresOptionQuoteData.AskPrice)
                };
                this.OptionLegData.Set(callOptionLegData.OptionLegId, callOptionLegData);
                UpdateCallCreditSpreadLiveFeedValues();
                return true;
            }
        }

        public void TurnLiveFeedOff()
        {
            if (_liveFeedQuoteId == Guid.Empty) return;

            // stop quote streaming data listener...
            _appRoot.GetModel<MarketDataFeedEventModel>().Execute(async model =>
            {
                model.OnError((_, errorMsg) => this.ShowErrorMessage(errorMsg, "Stop Streaming Quote Data Error"));
                await model.StopFuturesOptionQuoteDataListenerAsync();
            });

            // stop streaming quotes...
            _appRoot.GetModel<MarketDataFeedCommandModel>().Execute(async model =>
            {
                model.OnError((_, errorMsg) => this.ShowErrorMessage(errorMsg, "Stop Streaming Quotes Error"));
                await model.StopStreamingFuturesOptionQuoteDataAsync(_liveFeedQuoteId, () => _liveFeedQuoteId = Guid.Empty);
            });
        }
    
        public void CalculateTradeValues()
        {
            var oc = new OptionCalculator(_valueDate, _ironCondorTrade.MaturityDate);

            // update put spread trade values...
            var pcs = GetTradePosition(PutSpreadTradeType, TradeStatus);
            pcs = pcs with {
                Commission = this.Commission(pcs),
                TradePnl = this.Commission(pcs) * -1,
                RiskFreeRate = _riskFreeRate
            };
            foreach (var e in pcs.OptionLegData)
            {
                var og = oc.GetOptionGreeks(OptionTypeName.Put, Convert.ToDouble(pcs.AssetPrice), Convert.ToDouble(e.OptionLeg.StrikePrice), Convert.ToDouble(e.OptionPrice), _riskFreeRate);
                if (!og.Success) continue;
                pcs.OptionLegData.Set(e.OptionLegId, e with
                {
                    ImpliedVolatility = og.ImpliedVolatility,
                    Delta = og.Delta,
                    Gamma = og.Gamma,
                    Theta = og.Theta,
                    Vega = og.Vega,
                    Rho = og.Rho
                });
            }
            var optionLegData = GetOptionLegData(PutSpreadTradeType, TradeStatus, ShortOptionLegAction, OptionType.Put);
            if ((optionLegData?.Delta ?? 0.0) != 0.0)
                pcs = pcs with { OTMProbability = 1 - Math.Abs(optionLegData.Delta) };
            _ironCondorTrade.TradePositions.Set(pcs);

            // update call spread values...
            var ccs = GetTradePosition(CallSpreadTradeType, TradeStatus);
            ccs = ccs with
            {
                Commission = Commission(ccs),
                TradePnl = Commission(pcs) * -1,
                RiskFreeRate = _riskFreeRate
            };
            foreach (var e in ccs.OptionLegData)
            {
                var og = oc.GetOptionGreeks(OptionTypeName.Call, Convert.ToDouble(ccs.AssetPrice), Convert.ToDouble(e.OptionLeg.StrikePrice), Convert.ToDouble(e.OptionPrice), _riskFreeRate);
                if (!og.Success) 
                    continue;
                ccs.OptionLegData.Set(e.OptionLegId, e with {
                    ImpliedVolatility = og.ImpliedVolatility,
                    Delta = og.Delta,
                    Gamma = og.Gamma,
                    Theta = og.Theta,
                    Vega = og.Vega,
                    Rho = og.Rho });
            }
            optionLegData = GetOptionLegData(this.CallSpreadTradeType, this.TradeStatus, this.ShortOptionLegAction, OptionType.Call);
            if ((optionLegData?.Delta ?? 0.0) != 0.0)
                ccs = ccs with { OTMProbability = (1 - Math.Abs(optionLegData.Delta)) };
            _ironCondorTrade.TradePositions.Set(ccs);
        }

        public void UpdateOptionLegMap()
        {
            foreach (var e in _ironCondorTrade.OptionLegs)
                SetOptionLeg(e.OptionLegAction, e.OptionLegType, e);
        }

        public void UpdatePutCreditSpreadLiveFeedValues()
        {
            var tradeType = this.PutSpreadTradeType;
            var pcs = GetTradePosition(tradeType, TradeStatus);
            if (pcs == null) return;
            pcs = pcs with
            {
                NetSpread = GetNetSpreadValue(
                  shortBidPrice: Convert.ToDouble(GetOptionLegData(tradeType, TradeStatus, ShortOptionLegAction, OptionType.Put).BidPrice),
                  shortAskPrice: Convert.ToDouble(GetOptionLegData(tradeType, TradeStatus, ShortOptionLegAction, OptionType.Put).AskPrice),
                  longBidPrice: Convert.ToDouble(GetOptionLegData(tradeType, TradeStatus, LongOptionLegAction, OptionType.Put).BidPrice),
                  longAskPrice: Convert.ToDouble(GetOptionLegData(tradeType, TradeStatus, LongOptionLegAction, OptionType.Put).AskPrice))
            };
            var tradeTypeLimit = _ironCondorTrade.TradeTypeLimits.Get(tradeType);
            var maxLossLimit = Convert.ToDouble(pcs.NetSpread * 2);
            tradeTypeLimit = tradeTypeLimit with
            {
                MaxLossLimit = _ironCondorTrade.TradeType == TradeType.ShortIronCondor ? maxLossLimit : maxLossLimit / 8.0,
                MinProfitLimit = _ironCondorTrade.TradeType == TradeType.ShortIronCondor ? maxLossLimit / 8.0 : maxLossLimit,
                MaxProfitLimit = _ironCondorTrade.TradeType == TradeType.ShortIronCondor ? maxLossLimit / 8.0 : maxLossLimit,
            };
            _ironCondorTrade.TradeTypeLimits.Set(tradeType, tradeTypeLimit);
            var optionLeg = GetOptionLeg(ShortOptionLegAction, OptionType.Put);
            pcs = pcs with { TradeValue = optionLeg.Quantity != 0 ? (pcs.NetSpread * optionLeg.Quantity * 50) : 0 };
            _ironCondorTrade.TradePositions.Set(pcs);
            SetTradePositionMap(tradeType, TradeStatus, pcs);
            UpdateTradeLimitValues();
            CalculateTradeValues();
        }

        public void UpdateCallCreditSpreadLiveFeedValues()
        {
            var tradeType = CallSpreadTradeType;
            var ccs = GetTradePosition(tradeType, TradeStatus);
            if (ccs == null) return;
            ccs = ccs with
            {
                NetSpread = GetNetSpreadValue(
                  shortBidPrice: Convert.ToDouble(GetOptionLegData(tradeType, TradeStatus, ShortOptionLegAction, OptionType.Call).BidPrice),
                  shortAskPrice: Convert.ToDouble(GetOptionLegData(tradeType, TradeStatus, ShortOptionLegAction, OptionType.Call).AskPrice),
                  longBidPrice: Convert.ToDouble(GetOptionLegData(tradeType, TradeStatus, LongOptionLegAction, OptionType.Call).BidPrice),
                  longAskPrice: Convert.ToDouble(GetOptionLegData(tradeType, TradeStatus, LongOptionLegAction, OptionType.Call).AskPrice))
            };
            var tradeTypeLimit = _ironCondorTrade.TradeTypeLimits.Get(tradeType);
            var maxLossLimit = Convert.ToDouble(ccs.NetSpread * 2);
            tradeTypeLimit = tradeTypeLimit with {
                MaxLossLimit = _ironCondorTrade.TradeType == TradeType.ShortIronCondor ? maxLossLimit : maxLossLimit / 8.0,
                MinProfitLimit = _ironCondorTrade.TradeType == TradeType.ShortIronCondor ? maxLossLimit / 8.0 : maxLossLimit,
                MaxProfitLimit = _ironCondorTrade.TradeType == TradeType.ShortIronCondor ? maxLossLimit / 8.0 : maxLossLimit,
            };
            _ironCondorTrade.TradeTypeLimits.Set(tradeType, tradeTypeLimit);
            var optionLeg = GetOptionLeg(ShortOptionLegAction, OptionType.Call);
            ccs = ccs with { TradeValue = optionLeg.Quantity != 0 ? (ccs.NetSpread * optionLeg.Quantity * 50) : 0 };
            _ironCondorTrade.TradePositions.Set(ccs);
            SetTradePositionMap(tradeType, TradeStatus, ccs);
            UpdateTradeLimitValues();
            CalculateTradeValues();
        }

        public void UpdateTradeLimitValues()
        {
            var pcs = GetTradePosition(PutSpreadTradeType, TradeStatus);
            if (pcs is null) return;
            var ccs = GetTradePosition(CallSpreadTradeType, TradeStatus);
            if (ccs is null) return;
            _ironCondorTrade.SetTradeLimit(
                riskMargin: _ironCondorTrade.TradeLimit.RiskMargin,
                maxProfit: Convert.ToDecimal(pcs.TradeValue + ccs.TradeValue),
                maxLoss: Convert.ToDecimal(FundBalance * 0.02m ) * -1,
                maxReturn: _ironCondorTrade.TradeLimit.RiskMargin == 0m ? 0.0 : Convert.ToDouble(pcs.TradeValue + ccs.TradeValue) / Convert.ToDouble(_ironCondorTrade.TradeLimit.RiskMargin),
                maxLossLimit: _ironCondorTrade.TradeTypeLimits.Get(PutSpreadTradeType).MaxLossLimit + _ironCondorTrade.TradeTypeLimits.Get(CallSpreadTradeType).MaxLossLimit,
                minProfitLimit: _ironCondorTrade.TradeTypeLimits.Get(PutSpreadTradeType).MinProfitLimit + _ironCondorTrade.TradeTypeLimits.Get(CallSpreadTradeType).MinProfitLimit,
                maxProfitLimit: _ironCondorTrade.TradeTypeLimits.Get(PutSpreadTradeType).MaxProfitLimit + _ironCondorTrade.TradeTypeLimits.Get(CallSpreadTradeType).MaxProfitLimit,
                minProfitTarget: (0.50m * _ironCondorTrade.TradeLimit.MaxProfit) + (2 * TradeCommission),
                dailyProfitTarget: DaysToExpiry == 0 ? 0m : (_ironCondorTrade.TradeLimit.MaxProfit / DaysToExpiry) + (2 * TradeCommission));
        }

        public void LoadStrikePrices(Action<object[]> onLoadComplete)
        {
            _appRoot.GetModel<ReferenceQueryModel>().Execute(async refModel => {
                refModel.OnError((_, errorMsg) => ShowErrorMessage(errorMsg, "Loading Futures Option Strike Price Definitions Error"));
                await refModel.LoadFuturesOptionStrikePriceDefinitionsAsync(o => {
                    _appRoot.GetModel<MarketDataQueryModel>().Execute(async mktDataModel => {
                        await mktDataModel.GetCurrentFuturesEodDataAsync(_valueDate, futuresEodData => {
                            if (futuresEodData != null)
                            {
                                var strikePrice = (int)((int)futuresEodData.ClosePrice / 5) * 5;
                                var minStrike = strikePrice - 1000;
                                var maxStrike = strikePrice + 1000;
                                var strikePrices = GetStrikePriceRange(minStrike, maxStrike, o.Increment).ToArray();
                                onLoadComplete?.Invoke(strikePrices);
                            }
                        });
                    });
                });
            });
            return;

            IEnumerable<object> GetStrikePriceRange(int minStrikePrice, int maxStrikePrice, int increment)
            {
                for (var strikePrice = minStrikePrice; strikePrice < maxStrikePrice; strikePrice += increment)
                    yield return strikePrice;
            }
        }

        /// <summary>
        /// update order action if short iron condor and we are closing a trade
        /// </summary>
        /// <param name="orderActionType"></param>
        public void UpdateOrderAction(OrderActionType orderActionType, Action onUpdateComplete)
        {
            //if (!(TradeType == TradeType.LongIronCondor && orderActionType == OrderActionType.Close)) return;
            LoadFundOrderTrade((orderId, tradeId) => SetParentTrade(orderId, tradeId));
            return;

            void LoadFundOrderTrade(Action<int,int> onCompleted)
                => _appRoot.GetModel<FundQueryModel>().Execute(async model => {
                    model.OnError((_, errorMsg) => ShowErrorMessage(errorMsg, "Loading Funds Error"));
                    await model.GetFundsAsync(async funds => await LoadFundsAsync(model, funds, onCompleted));
                });

            async Task LoadFundsAsync(FundQueryModel model, FundReadModel[] allFunds, Action<int, int> onCompleted)
            {
                if ((allFunds?.Length ?? 0) > 0)
                {
                    var funds = new List<FundReadModel>(allFunds);
                    var fundOrders =  new List<FundOrderReadModel>(await model.GetFundOrdersAsync());
                    var fundOrderTrades = new List<FundOrderTradeReadModel>( await model.GetFundOrderTradesAsync());

                    var fund = funds.Where(e => e.FundId == FundId).FirstOrDefault();
                    if (fund is not null)
                    {
                        var fundOrder = fundOrders.Where(e => e.OrderId == FundOrderTrade.OrderId).FirstOrDefault();
                        if (fundOrder is not null)
                        {
                            var fundOrderTrade = fundOrderTrades.Where(e => e.OrderId == fundOrder.OrderId && e.TradeState == TradeState.TradeToOpen).FirstOrDefault();
                            if (fundOrderTrade is not null)
                                onCompleted(fundOrderTrade.OrderId, fundOrderTrade.TradeId);
                        }
                    }
                }
            }

            void SetParentTrade(int orderId, int tradeId)
                => _appRoot.GetModel<TradeQueryModel>().Execute(async model => {
                    model.OnError((_, errorMsg) => ShowErrorMessage(errorMsg, "Loading Short Iron Condor Parent Trade Error"));
                    await model.GetOptionTradeAsync(orderId, tradeId, optionTrade => {
                        _parentTrade = optionTrade;
                        onUpdateComplete?.Invoke();
                    });
                });
            
        }

        public OptionLegReadModel GetOptionLeg(OptionLegAction optionLegAction, OptionType optionType)
            => _optionLegMap.ContainsKey((optionLegAction, optionType)) ? _optionLegMap[(optionLegAction, optionType)] : null;

        public void SetOptionLeg(OptionLegAction optionLegAction, OptionType optionType, OptionLegReadModel optionLeg)
        { 
            if (_optionLegMap.ContainsKey((optionLegAction, optionType)))
            {
                _optionLegMap.Remove((optionLegAction, optionType));
                _optionLegMap.Add((optionLegAction, optionType), optionLeg);
            }
        }

        public OptionLegReadModel GetParentOptionLeg(OptionLegAction optionLegAction, OptionType optionType)
            => _parentTrade.OptionLegs.Get(optionLegAction, optionType);

        public OptionLegDataReadModel GetParentOptionLegData(TradeType tradeType, TradeStatus tradeStatus, OptionLegAction optionLegAction, OptionType optionType)
        {
            var optionLegData = default(OptionLegDataReadModel);
            var tradePosition = _parentTrade.TradePositions.Get(tradeType, tradeStatus);
            if (tradePosition is not null)
                optionLegData = tradePosition.OptionLegData.Get(optionLegAction, optionType);
            return optionLegData;
        }

        public OptionLegDataReadModel GetOptionLegData(TradeType tradeType, TradeStatus tradeStatus, OptionLegAction optionLegAction, OptionType optionType)
            => _optionLegDataMap.ContainsKey((_valueDate, tradeType, tradeStatus, optionLegAction, optionType)) ? _optionLegDataMap[(_valueDate, tradeType, tradeStatus, optionLegAction, optionType)] : null;

        public void SetOptionLegData(TradeType tradeType, TradeStatus tradeStatus, OptionLegAction optionLegAction, OptionType optionType, OptionLegDataReadModel optionLegData)
        {
            if (_optionLegDataMap.ContainsKey((_valueDate, tradeType, tradeStatus, optionLegAction, optionType)))
            {
                _optionLegDataMap.Remove((_valueDate, tradeType, tradeStatus, optionLegAction, optionType));
                _optionLegDataMap.Add((_valueDate, tradeType, tradeStatus, optionLegAction, optionType), optionLegData);
            }
            var tradePosition = _ironCondorTrade.TradePositions.Get(tradeType, tradeStatus);
            if (tradePosition != null)
            {
                tradePosition.OptionLegData.Set(optionLegData.OptionLegId, optionLegData);
                _ironCondorTrade.TradePositions.Set(tradePosition);
            }
        }

        public void SetTradePositionMap(TradeType tradeType, TradeStatus tradeStatus, TradePositionReadModel tradePosition)
        {
            if (_tradePositionMap.ContainsKey((_valueDate, tradeType, tradeStatus)))
            {
                _tradePositionMap.Remove((_valueDate, tradeType, tradeStatus));
                _tradePositionMap.Add((_valueDate, tradeType, tradeStatus), tradePosition);
            }
        }

        public TradePositionReadModel GetTradePosition(TradeType tradeType, TradeStatus tradeStatus)
            => _tradePositionMap.ContainsKey((_valueDate, tradeType, tradeStatus)) ? _tradePositionMap[(_valueDate, tradeType, tradeStatus)] : null;

        public bool StrikePriceMapped(OptionLegAction optionLegAction, OptionType optionType)
        {
            if (_optionPriceMap == null || _optionPriceMap.Count == 0) return false;
            return _optionPriceMap.ContainsKey((optionLegAction, optionType));
        }

        public decimal GetStrikePrice(OptionLegAction optionLegAction, OptionType optionType)
        {
            if (_optionPriceMap == null || _optionPriceMap.Count == 0 || !_optionPriceMap.ContainsKey((optionLegAction, optionType))) 
                return _optionLegMap[(optionLegAction, optionType)]?.StrikePrice ?? 0m;
            return _optionPriceMap[(optionLegAction, optionType)];
        }

        private void LoadBaseValues(FuturesEodDataViewModel futuresEodData)
        {
            var assetPrice = futuresEodData.ClosePrice;
            var pcs = GetTradePosition(PutSpreadTradeType, TradeStatus);
            if (pcs is not null)
            {
                pcs = pcs with { AssetPrice = Convert.ToDecimal(assetPrice) };
                _ironCondorTrade.TradePositions.Set(pcs);

            }
            var ccs = GetTradePosition(CallSpreadTradeType, TradeStatus);
            if (ccs is not null)
            {
                ccs = ccs with { AssetPrice = Convert.ToDecimal(assetPrice) };
                _ironCondorTrade.TradePositions.Set(pcs);
            }
        }

        private decimal GetNetSpreadValue(double shortBidPrice, double shortAskPrice, double longBidPrice, double longAskPrice)
            => Convert.ToDecimal(((shortBidPrice + shortAskPrice) / 2) - ((longBidPrice + longAskPrice) / 2));

        private OptionTradeViewModel CreateIronCondorTrade(TradeStatus tradeStatus)
        {
            var daysToExpiry = (_fundOrderTrade.MaturityDate - _fundOrderTrade.TradeDate).Days;
            var optionLegs = GetOptionLegs();
            var ironCondorTrade = new OptionTradeViewModel (
                OrderId: _fundOrderTrade.OrderId,
                TradeId: _fundOrderTrade.TradeId,
                TradeStrategy: string.Empty,
                TradeDate: _fundOrderTrade.TradeDate,
                MaturityDate: _fundOrderTrade.MaturityDate,
                TradeType: _fundOrderTrade.TradeType,
                TradeState: _fundOrderTrade.TradeState,
                TradeAction: _fundOrderTrade.TradeAction,
                UnderlyingContractId: _baseContract.ContractId,
                UnderlyingAssetType: AssetType.Futures,
                IsPrimaryTrade: true,
                IsHedgeTrade: false,
                CreatedOn: DateTime.Now,
                CreatedBy: string.Empty,
                UpdatedOn: DateTime.Now,
                UpdatedBy: string.Empty
            );
            return ironCondorTrade
                .AddOptionLegs(optionLegs)
                .AddTradePosition(new TradePositionReadModel[] {
                    CreateTradePosition(_fundOrderTrade.OrderId, _fundOrderTrade.TradeId, this.PutSpreadTradeType),
                    CreateTradePosition(_fundOrderTrade.OrderId, _fundOrderTrade.TradeId, this.CallSpreadTradeType)
                }).SetTradeLimit(TradeLimitReadModel.Default(tradeId: _fundOrderTrade.TradeId, tradeType: _fundOrderTrade.TradeType))
                    .AddTradeTypeLimits(new TradeTypeLimitReadModel[] {
                    new TradeTypeLimitReadModel(TradeId: _fundOrderTrade.TradeId, TradeType: this.PutSpreadTradeType, MaxLossLimit: 0.0, MinProfitLimit: 0.0, MaxProfitLimit: 0.0),
                    new TradeTypeLimitReadModel(TradeId: _fundOrderTrade.TradeId, TradeType: this.CallSpreadTradeType, MaxLossLimit: 0.0, MinProfitLimit: 0.0, MaxProfitLimit: 0.0)
                });

            OptionLegReadModel[] GetOptionLegs()
            {
                var contractIs = _fundOrderTrade.GetContractIds();
                var optionLegList = new List<OptionLegReadModel>();
                optionLegList.Add( OptionLegReadModel.Default (
                    tradeId: _fundOrderTrade.TradeId,
                    contractId: contractIs[0],
                    optionType: OptionType.Put,
                    optionLegAction: this.ShortOptionLegAction
                ));
                optionLegList.Add(OptionLegReadModel.Default (
                    tradeId: _fundOrderTrade.TradeId,
                    contractId: contractIs[1],
                    optionType: OptionType.Put,
                    optionLegAction: this.LongOptionLegAction
                ));
                optionLegList.Add(OptionLegReadModel.Default (
                    tradeId: _fundOrderTrade.TradeId,
                    contractId: contractIs[2],
                    optionType: OptionType.Call,
                    optionLegAction: this.ShortOptionLegAction
                ));
                optionLegList.Add(OptionLegReadModel.Default (
                    tradeId: _fundOrderTrade.TradeId,
                    contractId: contractIs[3],
                    optionType: OptionType.Call,
                    optionLegAction: this.LongOptionLegAction
                ));
                return optionLegList.ToArray();
            }

            TradePositionReadModel CreateTradePosition(int orderId, int tradeId, TradeType tradeType)
            {
                var optionType = GetOptionType(tradeType);
                return TradePositionReadModel.Default(orderId, tradeId, tradeType, _valueDate, daysToExpiry, tradeStatus)
                    .AddOptionLegData(new OptionLegDataReadModel[] {
                        OptionLegDataReadModel.Default(tradeId, tradeType, _valueDate, daysToExpiry, tradeStatus).SetOptionLeg(optionLegs.Get(this.ShortOptionLegAction, optionType)),
                        OptionLegDataReadModel.Default(tradeId, tradeType, _valueDate, daysToExpiry, tradeStatus).SetOptionLeg(optionLegs.Get(this.LongOptionLegAction, optionType))
                    });
            }

            OptionType GetOptionType(TradeType tradeType)
                => tradeType switch {
                    TradeType.CallCreditSpread => OptionType.Call,
                    TradeType.CallDebitSpread => OptionType.Call,
                    TradeType.PutCreditSpread => OptionType.Put,
                    TradeType.PutDebitSpread => OptionType.Put,
                    _ => throw new NotImplementedException()    
                };
        }

        void LoadLiveFeedValues(FuturesEodDataViewModel futuresEodData, Action onLiveFeedCompleted)
        {
            var assetPrice = futuresEodData.ClosePrice;
            var daysToExpiry = (_ironCondorTrade.MaturityDate - _ironCondorTrade.TradeDate).Days;
            var timeValue = daysToExpiry / 365.0;
            var pcs = GetTradePosition(this.PutSpreadTradeType, this.TradeStatus);
            pcs = pcs with { AssetPrice = Convert.ToDecimal(assetPrice) };
            _ironCondorTrade.TradePositions.Set(pcs);

            var ccs = GetTradePosition(this.CallSpreadTradeType, this.TradeStatus);
            ccs = ccs with { AssetPrice = Convert.ToDecimal(assetPrice) };
            _ironCondorTrade.TradePositions.Set(ccs);

            // get put credit spread market data...
            _appRoot.GetModel<MarketDataFeedQueryModel>().Execute(async model =>
            {
                model.OnError((_, errorMsg) =>
                {
                    this.ShowErrorMessage(errorMsg, "Live Feed Error");
                    onLiveFeedCompleted();
                });
                await model.GetFuturesOptionSpreadDataAsync(_ironCondorTrade.TradeDate, _ironCondorTrade.MaturityDate, assetPrice, _riskFreeRate, timeValue,
                    shortOptionContract: GetFuturesOptionContract(
                        strikePrice: Convert.ToDouble(GetOptionLeg(this.ShortOptionLegAction, OptionType.Put).StrikePrice),
                        futuresOptionType: OptionType.Put,
                        contractMonth: _ironCondorTrade.MaturityDate),
                    longOptionContract: GetFuturesOptionContract(
                        strikePrice: Convert.ToDouble(GetOptionLeg(this.LongOptionLegAction, OptionType.Put).StrikePrice),
                        futuresOptionType: OptionType.Put,
                        contractMonth: _ironCondorTrade.MaturityDate),
                        onCompleted: async putData => {
                            if (putData == null)
                            {
                                onLiveFeedCompleted();
                                return;
                            }
                            var putOptionLegData = GetOptionLegData(this.PutSpreadTradeType, this.TradeStatus, this.ShortOptionLegAction, OptionType.Put)
                                with { Delta = putData.ShortLeg.Delta,
                                    BidPrice = Convert.ToDecimal(putData.ShortLeg.BidPrice),
                                    AskPrice = Convert.ToDecimal(putData.ShortLeg.AskPrice) };
                            this.OptionLegData.Set(putOptionLegData.OptionLegId, putOptionLegData);

                            putOptionLegData = GetOptionLegData(this.PutSpreadTradeType, this.TradeStatus, this.LongOptionLegAction, OptionType.Put)
                                with
                            {
                                BidPrice = Convert.ToDecimal(putData.LongLeg.BidPrice),
                                AskPrice = Convert.ToDecimal(putData.LongLeg.AskPrice)
                            };
                            this.OptionLegData.Set(putOptionLegData.OptionLegId, putOptionLegData);
                            UpdatePutCreditSpreadLiveFeedValues();

                        // get call credit spread market data...
                        await model.GetFuturesOptionSpreadDataAsync(_ironCondorTrade.TradeDate, _ironCondorTrade.MaturityDate, assetPrice, _riskFreeRate, timeValue,
                                shortOptionContract: GetFuturesOptionContract(
                                    strikePrice: Convert.ToDouble(_ironCondorTrade.OptionLegs.Get(this.ShortOptionLegAction, OptionType.Call).StrikePrice),
                                    futuresOptionType: OptionType.Call,
                                    contractMonth: _ironCondorTrade.MaturityDate),
                                longOptionContract: GetFuturesOptionContract(
                                    strikePrice: Convert.ToDouble(_ironCondorTrade.OptionLegs.Get(this.LongOptionLegAction, OptionType.Call).StrikePrice),
                                    futuresOptionType: OptionType.Call,
                                    contractMonth: _ironCondorTrade.MaturityDate),
                                    onCompleted: callData => {
                                        if (callData == null)
                                        {
                                            onLiveFeedCompleted();
                                            return;
                                        }
                                        var callOptionLegData = GetOptionLegData(this.CallSpreadTradeType, this.TradeStatus, this.ShortOptionLegAction, OptionType.Call)
                                            with
                                        {
                                            Delta = callData.ShortLeg.Delta,
                                            BidPrice = Convert.ToDecimal(callData.ShortLeg.BidPrice),
                                            AskPrice = Convert.ToDecimal(callData.ShortLeg.AskPrice)
                                        };
                                        this.OptionLegData.Set(callOptionLegData.OptionLegId, callOptionLegData);

                                        callOptionLegData = GetOptionLegData(this.CallSpreadTradeType, this.TradeStatus, this.LongOptionLegAction, OptionType.Call)
                                            with
                                        {
                                            BidPrice = Convert.ToDecimal(callData.LongLeg.BidPrice),
                                            AskPrice = Convert.ToDecimal(callData.LongLeg.AskPrice)
                                        };
                                        this.OptionLegData.Set(callOptionLegData.OptionLegId, callOptionLegData);
                                        UpdateCallCreditSpreadLiveFeedValues();
                                        onLiveFeedCompleted();
                                    });
                        });

            });
        }

        FuturesOptionContractReadModel GetFuturesOptionContract(double strikePrice, OptionType futuresOptionType, DateTime contractMonth)
        {
            return new FuturesOptionContractReadModel(
                ContractId: $"{new FuturesOptionContractId(_defaultFuturesContractDefinitions.Symbol, contractMonth, futuresOptionType, strikePrice)}",
                Symbol: _defaultFuturesContractDefinitions.Symbol,
                LocalSymbol: GetLocalSymbol(_defaultFuturesContractDefinitions.Symbol, contractMonth, futuresOptionType, strikePrice),
                SecurityType: _defaultFuturesContractDefinitions.OptionSecurityType,
                Currency: _defaultFuturesContractDefinitions.Currency,
                Exchange: _defaultFuturesContractDefinitions.Exchange,
                Multiplier: _defaultFuturesContractDefinitions.Multiplier,
                ContractMonth: contractMonth,
                OptionType: $"{futuresOptionType}".ToUpper(),
                StrikePrice: strikePrice,
                Description: GetDescription()
            );

            string GetDescription()
            {
                var asset = _defaultFuturesContractDefinitions.Symbol;
                var year = contractMonth.Year;
                var month = $"{contractMonth:MMM}";
                var day = contractMonth.Day;
                var optionType = $"{futuresOptionType}".ToUpper();
                var exchange = _defaultFuturesContractDefinitions.Exchange;
                return $"{asset} {year} {month} {day} {optionType} {strikePrice:F0} @ {exchange}";
            }

            string GetLocalSymbol(string symbol, DateTime valueDate, OptionType futuresOptionType, double strikePrice)
            {
                if (!string.IsNullOrWhiteSpace(LocalSymbol)) return LocalSymbol;
                return $"{FuturesOptionContractReadModel.GetLocalSymbol(symbol, valueDate)} {(futuresOptionType == OptionType.Put ? "P" : "C")}{Convert.ToInt32(strikePrice)}";
            }
        }

        /// <summary>
        /// map trade action from trade reference string if populated
        /// </summary>
        /// <param name="reference"></param>
        void MapOptionPriceFromTradeReference(TradeAction tradeAction, string reference)
        {
            if (string.IsNullOrWhiteSpace(reference)) return;

            // parse reference into 4 trade action tokens...
            var tokens = reference.Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length != 3) return;

            // 2nd token must start with "P"...
            if (!tokens[0].ToLower().StartsWith("p")) return;

            // 3rd token must be "x"...
            if (tokens[1].ToLower() != "x") return;

            // 4th token must start with "C"...
            if (!tokens[2].ToLower().StartsWith("c")) return;

            // parse put leg prices..
            var putPriceTokens = tokens[0].Substring(1).Split(new string[] { ":" }, StringSplitOptions.RemoveEmptyEntries);
            if (putPriceTokens.Length != 2) return;

            // parse call leg prices..
            var callPriceTokens = tokens[2].Substring(1).Split(new string[] { ":" }, StringSplitOptions.RemoveEmptyEntries);
            if (callPriceTokens.Length != 2) return;

            // map trade strike prices...
            _optionPriceMap = new Dictionary<(OptionLegAction OptionLegAction, OptionType OptionType), decimal>();
            switch (tradeAction)
            {
                case TradeAction.Sell:
                    _optionPriceMap.Add((OptionLegAction.Short, OptionType.Put), decimal.Parse(putPriceTokens[0]));
                    _optionPriceMap.Add((OptionLegAction.Long, OptionType.Put), decimal.Parse(putPriceTokens[1]));
                    _optionPriceMap.Add((OptionLegAction.Short, OptionType.Call), decimal.Parse(callPriceTokens[0]));
                    _optionPriceMap.Add((OptionLegAction.Long, OptionType.Call), decimal.Parse(callPriceTokens[1]));
                    break;
                case TradeAction.Buy:
                    _optionPriceMap.Add((OptionLegAction.Long, OptionType.Put), decimal.Parse(putPriceTokens[0]));
                    _optionPriceMap.Add((OptionLegAction.Short, OptionType.Put), decimal.Parse(putPriceTokens[1]));
                    _optionPriceMap.Add((OptionLegAction.Long, OptionType.Call), decimal.Parse(callPriceTokens[0]));
                    _optionPriceMap.Add((OptionLegAction.Short, OptionType.Call), decimal.Parse(callPriceTokens[1]));
                    break;
            }

        }

        TradeFillReadModel[] GetManualTradeFills(int fundId, int fillQuantity)
        {
            var orderId = _ironCondorTrade.OrderId;
            var tradeId = _ironCondorTrade.TradeId;
            var fillDate = DateTime.Now;
            var tradeFills = new List<TradeFillReadModel>
            {
                new TradeFillReadModel(fundId, orderId, tradeId, fillDate, fillQuantity, fillDate, Environment.UserName)
                    .AddTradeFillData(new List<TradeFillDataReadModel> {
                        new TradeFillDataReadModel(fundId, orderId,tradeId,
                            ContractId: GetOptionLeg(ShortOptionLegAction, OptionType.Put).ContractId,
                            FillDate: fillDate,
                            BidPrice: GetOptionLegData(PutSpreadTradeType, TradeStatus, ShortOptionLegAction, OptionType.Put).BidPrice,
                            AskPrice: GetOptionLegData(PutSpreadTradeType, TradeStatus, ShortOptionLegAction, OptionType.Put).AskPrice,
                            Commission: fillQuantity * 1.42m,
                            OptionLegAction: this.ShortOptionLegAction, 
                            CreatedOn: fillDate,
                            CreatedBy: Environment.UserName
                        ),
                        new TradeFillDataReadModel(fundId, orderId,tradeId,
                            ContractId: GetOptionLeg(LongOptionLegAction, OptionType.Put).ContractId,
                            FillDate: fillDate,
                            BidPrice: GetOptionLegData(PutSpreadTradeType, TradeStatus, LongOptionLegAction, OptionType.Put).BidPrice,
                            AskPrice: GetOptionLegData(PutSpreadTradeType, TradeStatus, LongOptionLegAction, OptionType.Put).AskPrice,
                            Commission: fillQuantity * 1.42m,
                            OptionLegAction: LongOptionLegAction,
                            CreatedOn: fillDate,
                            CreatedBy: Environment.UserName
                        ),
                        new TradeFillDataReadModel(fundId, orderId,tradeId,
                            ContractId: GetOptionLeg(ShortOptionLegAction, OptionType.Call).ContractId,
                            FillDate: fillDate,
                            BidPrice: GetOptionLegData(CallSpreadTradeType, TradeStatus, ShortOptionLegAction, OptionType.Call).BidPrice,
                            AskPrice: GetOptionLegData(CallSpreadTradeType, TradeStatus, ShortOptionLegAction, OptionType.Call).AskPrice,
                            Commission: fillQuantity * 1.42m,
                            OptionLegAction: ShortOptionLegAction,
                            CreatedOn: fillDate,
                            CreatedBy: Environment.UserName
                        ),
                        new TradeFillDataReadModel(fundId, orderId,tradeId,
                            ContractId: GetOptionLeg(LongOptionLegAction, OptionType.Call).ContractId,
                            FillDate: fillDate,
                            BidPrice: GetOptionLegData(CallSpreadTradeType, TradeStatus, LongOptionLegAction, OptionType.Call).BidPrice,
                            AskPrice: GetOptionLegData(CallSpreadTradeType, TradeStatus, LongOptionLegAction, OptionType.Call).AskPrice,
                            Commission: fillQuantity * 1.42m,
                            OptionLegAction: LongOptionLegAction,
                            CreatedOn: fillDate,
                            CreatedBy: Environment.UserName
                        )
                    })
            };
            return tradeFills.ToArray();
        }
       
    }
}
