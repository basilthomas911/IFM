using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Threading.Tasks;
using TomasAI.IFM.Contracts;
using TomasAI.IFM.Models;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;
using TomasAI.IFM.Shared.Reference.ViewModels;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.ViewModels.MarketData;
using TomasAI.IFM.ViewModels.Reference;

namespace TomasAI.IFM.ViewModels.App
{
    public class StatusConsoleViewModel
    {
        IAppRoot _appRoot;
        string _contractId;
        DateTime _valueDate;
        List<FuturesItiSignalViewModel> _futuresItiSignals;
        FuturesTradeSignalViewModel _futuresTradeSignal;
        List<MDIForwardLossRatioUIViewModel> _mdiForwardLossRatios;

        public StatusConsoleViewModel(IAppRoot appRoot, string contractId, DateTime valueDate)
        {
            _appRoot = appRoot;
            _contractId = contractId;
            _valueDate = valueDate;
            _futuresItiSignals = new();
            _mdiForwardLossRatios = new();
        }

        public Action<string> OnErrorMessage;
        public Action<FuturesItiSignalViewModel[]> OnTradeStatusLoad;
        public Action<FuturesItiSignalViewModel> OnTrendExtremeChanged;
        public Action<FuturesTradeStatusUIViewModel> OnTradeStatusChanged;
        public Action<MDIForwardLossRatioUIViewModel[]> OnMDIForwardLossRatiosLoaded;

        public void LoadTradeStatus()
            => _appRoot.GetModel<MarketDataAnalyticsQueryModel>().Execute(async model => {
                    model.OnError((_, errorMessage) => OnErrorMessage?.Invoke(errorMessage));
                    await model.GetFuturesItiTrendDirectionChangedSignalsAsync(
                         _contractId, _valueDate, futuresItiSignals => {
                             if (futuresItiSignals is not null)
                             {
                                 _futuresItiSignals.Clear();
                                 _futuresItiSignals.AddRange(futuresItiSignals);
                                 OnTradeStatusLoad?.Invoke(futuresItiSignals);
                             }
                         });
                });

        public void LoadMDIForwardLossRatios()
              => _appRoot.GetModel<ReferenceQueryModel>().Execute(async model => {
                  model.OnError((_, errorMessage) => OnErrorMessage?.Invoke(errorMessage));
                  await model.LoadMDIFowardLossRatiosAsync(IntrinsicTimeTrendType.UpTrend, TradeType.ShortIronCondor, async o =>
                  {
                      if (o is not null)
                      {
                          _mdiForwardLossRatios.Clear();
                          _mdiForwardLossRatios.AddRange(o.OrderByDescending(e => e.MDI).Select(e => new MDIForwardLossRatioUIViewModel(e)));
                          await model.LoadMDIFowardLossRatiosAsync(IntrinsicTimeTrendType.DownTrend, TradeType.LongIronCondor, o =>
                          {
                              if (o is not null)
                              {
                                  _mdiForwardLossRatios.AddRange(o.OrderByDescending(e => e.MDI).Select(e => new MDIForwardLossRatioUIViewModel(e)));
                                  OnMDIForwardLossRatiosLoaded?.Invoke(_mdiForwardLossRatios.ToArray());
                              }
                          });
                      };
                  });
              });

        public void StartMarketDataAnalyticsEventConsumer()
        { 
            _appRoot.GetModel<MarketDataAnalyticsEventModel>().Execute(async model => {
                   model.OnError((_, errorMessage) => OnErrorMessage?.Invoke(errorMessage));
                   await model.StartFuturesItiSignalEventListenersAsync(
                       trendDirectionChangedAction: e => UpdateTrendDirectionStatus(e),
                       trendExtremeChangedAction: e => UpdateTrendExtremeStatus(e),
                       futuresTradeSignalAction: e => UpdateFuturesTradeSignal(e));
             });
            return;

            void UpdateTrendDirectionStatus(FuturesItiSignalViewModel futuresItiSignal)
            {
                if (futuresItiSignal?.IntrinsicTimeMode == IntrinsicTimeModeType.TrendDirectionChanged)
                {
                    _futuresItiSignals?.Insert(0, futuresItiSignal);
                    OnTradeStatusLoad?.Invoke(_futuresItiSignals.ToArray());
                }
            }

            void UpdateTrendExtremeStatus(FuturesItiSignalViewModel futuresItiSignal)
            {
                if (futuresItiSignal?.IntrinsicTimeMode == IntrinsicTimeModeType.TrendExtremeChanged)
                    OnTrendExtremeChanged?.Invoke(futuresItiSignal);
            }

            void UpdateFuturesTradeSignal(FuturesTradeSignalViewModel futuresTradeSignal)
            {
                _futuresTradeSignal = futuresTradeSignal;
                OnTradeStatusChanged?.Invoke(GetTradeStatus());
            }
        }

        public void StopMarketDataAnalyticsEventConsumer()
            => _appRoot.GetModel<MarketDataAnalyticsEventModel>().Execute(async model => {
                model.OnError((_, errorMessage) => OnErrorMessage?.Invoke(errorMessage));
                await model.StopFuturesItiSignalEventListenersAsync();
            });

        public FuturesTradeStatusUIViewModel GetTradeStatus( )
        {
            FuturesItiSignalViewModel futuresItiSignal;
            var tradeStatus = "No Trade Entry";
            if ((_futuresItiSignals?.Count ?? 0) > 0)
            {
                futuresItiSignal = _futuresItiSignals[0];
                var trendTrade = futuresItiSignal.IntrinsicTimeTrend switch
                {
                    IntrinsicTimeTrendType.UpTrend => "ShortIronCondor",
                    IntrinsicTimeTrendType.DownTrend => "LongIronCondor",
                    _ =>  null
                };
                if (trendTrade is not null )
                    tradeStatus = _futuresTradeSignal?.TradeExecuteState switch { 
                        null => tradeStatus,
                        TradeExecuteState.Enter => $"Open {trendTrade} Trade",
                        TradeExecuteState.ExitOnTrendReversion => $"Close {trendTrade} On Trade Reversion",
                        TradeExecuteState.ExitOnEntryLimit => $"Close  {trendTrade} On Trade {(futuresItiSignal.IntrinsicTimeTrend == IntrinsicTimeTrendType.UpTrend ? "Below":"Above")} Entry Limit",
                        TradeExecuteState.Hold => $"Hold {futuresItiSignal.IntrinsicTimeTrend} Trade Entry Due To Market Volatility",
                        TradeExecuteState.No => $"No {futuresItiSignal.IntrinsicTimeTrend} Trade Entry",
                        TradeExecuteState.InTrade => $"In {futuresItiSignal.IntrinsicTimeTrend} Trade",
                        TradeExecuteState.RangeBound => $"Trend is RangeBound",
                        _ => tradeStatus
                    };
            }
            return new FuturesTradeStatusUIViewModel(
                new FuturesTradeStatusReadModel(tradeStatus, _futuresTradeSignal?.TradeExecuteState));
        }
    }
}
