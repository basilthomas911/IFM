using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Models;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.Reference.ViewModels;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Shared.StatusConsole.ViewModels;
using TomasAI.IFM.UI.Net.ViewModels.MarketData;

namespace TomasAI.IFM.UI.Net.ViewModels.App;

public class IFMAppViewModel
{
    IAppRoot _appRoot;
    Guid _siteId;
    Version _appVersion = null!;
    string _appEnvironment = null!;
    DateOnly? _valueDate;
    ICollection<FuturesContractV2ReadModel> _baseContracts = null!;
    Action _onEnableMenuBarButtons = null!;
    Action<string, string> _errorMessage = null!;
    Action<StatusConsoleLogReadModel[]> _writeStatusConsole = null!;
    Action<string> _writeStatusLine = null!;
    Action<FuturesEodDataUIViewModel> _updateMarketOutlook = null!;
    Action<FuturesTradeSignalUIViewModel> _updateTradeSignal = null!;
    Action<PlaceTradeUIViewModel> _notifyTradePlacement = null!;
    Action<string, FuturesBarDataReadModel[]> _updateMarketData = null!;
    Action _closeTradeBlotters = null!;
    Action<string, DateOnly> _loadStatusConsole = null!;
    Action _unloadStatusConsole = null!;
    CancellationTokenSource? _resetCanceller;
    bool _resetCancelled;
    int _resetTicks;

    /// <summary>
    /// create IFM app root view model
    /// </summary>
    /// <param name="appRoot"></param>
    public IFMAppViewModel(IAppRoot appRoot)
    {
        _appRoot = appRoot;
        _siteId = Guid.NewGuid();
        _appRoot.GetModel<EventModel>().SetSiteId(_siteId);
        _resetCancelled = false;    
    }

    public DateOnly? ValueDate => _valueDate;
    public ICollection<FuturesContractV2ReadModel> BaseContracts => _baseContracts;

    /// <summary>
    /// initialze application
    /// </summary>
    /// <param name="onErrorMessage"></param>
    /// <param name="onEnableMenuBarButtons"></param>
    /// <param name="writeStatusConsole"></param>
    public void AppStartup(
        Version appVersion, 
        string appEnvironment, 
        Action<string, string> onErrorMessage, 
        Action onEnableMenuBarButtons,
        Action<string, DateOnly> loadStatusConsole,
        Action unloadStatusConsole,
        Action<string> writeStatusLine,
        Action<StatusConsoleLogReadModel[]> writeStatusConsole, 
        Action<FuturesEodDataUIViewModel> updateMarketOutlook,
        Action<FuturesTradeSignalUIViewModel> updateTradeSignal,
        Action<PlaceTradeUIViewModel> notifyTradePlacement,
        Action<string, FuturesBarDataReadModel[]> updateMarketData,
        Action closeTradeBlotters)
    {
        _appVersion = appVersion;
        _appEnvironment = appEnvironment;   
        _errorMessage = onErrorMessage;
        _onEnableMenuBarButtons = onEnableMenuBarButtons;
        _writeStatusConsole = writeStatusConsole;
        _writeStatusLine = writeStatusLine;
        _updateMarketOutlook = updateMarketOutlook;
        _updateTradeSignal = updateTradeSignal;
        _notifyTradePlacement = notifyTradePlacement;
        _updateMarketData = updateMarketData;
        _closeTradeBlotters = closeTradeBlotters;
        _loadStatusConsole = loadStatusConsole;
        _unloadStatusConsole = unloadStatusConsole;
        StartStatusConsoleListener();
        StartApplicationEventsListener();
        AppStartup();
    }

    /// <summary>
    /// application cleanup before it closes
    /// </summary>
    public void AppShutdown()
    {
        WriteStatusConsole($"IFMApp v{_appVersion} - {_appEnvironment}...shutting down");
        _unloadStatusConsole?.Invoke();
        _closeTradeBlotters?.Invoke();
        StopFuturesEodDataEventConsumer();
        StopFuturesBarDataEventConsumer();
        StopFuturesTradeSignalEventConsumer();
        StopTradePlacementEventConsumer();
        StopFuturesRsiSignalService();
        DisableMarketDataFeedResetListener();
        DisableTradeLiveFeed();
        StopResetLiveFeed();    
    }

    /// <summary>
    /// application startup
    /// </summary>
    void AppStartup(Action tradeStartup = default!)
        => _appRoot.GetModel<MarketDataQueryModel>().Execute(async model => {
            model.OnError((_, errorMsg) => _errorMessage($"Unable to connect to IFM servers {Environment.NewLine}{errorMsg}", "Market Data Error"));
            await model.GetCurrentlyTradedFuturesContractsAsync(async futuresContracts =>  
            {
                _baseContracts = futuresContracts;
                GetLastFuturesEodData();
                GetLastFuturesTradeSignal();
                GetLastFuturesBarData();
                await model.GetValueDateAsync(valueDate => 
                {
                    if (valueDate.HasValue)
                    {
                        _valueDate = valueDate;
                        ImportYieldCurveRates(() => ImportEconomicCalendars(() => {
                            StartFuturesEodDataEventConsumer();
                            StartFuturesBarDataEventConsumer();
                            StartFuturesTradeSignalEventConsumer();
                            StartTradePlacementEventConsumer();
                            EnableMarketDataFeedResetListener();
                            EnableTradeLiveFeed();
                            StartResetLiveFeed();
                            WriteStatusConsole($"IFMApp v{_appVersion} - {_appEnvironment}...initialization complete");
                            tradeStartup?.Invoke();
                        }));
                        StartFuturesRsiSignalService();
                        _loadStatusConsole?.Invoke(_baseContracts?.Where(e => e.Symbol == "ES")?.Select(e => e.ContractId).FirstOrDefault() ?? string.Empty, _valueDate.Value);
                    }
                    else
                        _errorMessage("Market Data Live Feed unavailable outside of valid Trading Hours", "Market Data Feed Error");
                });
                _onEnableMenuBarButtons?.Invoke();
            });
        });


    /// <summary>
    /// start console listener
    /// </summary>
    void StartStatusConsoleListener()
        => _appRoot.GetModel<StatusConsoleModel>().Execute(async model => {
            model.OnError((_, errorMessage) => _errorMessage(errorMessage, "Status Console Log Error"));
            await model.StartStatusConsoleLogListenerAsync(o => {
                if (o is not null && o.StatusConsoleLog is not null)
                {
                    _writeStatusConsole([ o.StatusConsoleLog ]);
                    _writeStatusLine(o.StatusConsoleLog.Message);
                }
            }, _siteId);
        });

    /// <summary>
    /// start application events listener
    /// </summary>
    void StartApplicationEventsListener()
        => _appRoot.GetModel<ApplicationEventModel>().Execute(async model => {
            model.OnError((_, errorMessage) => _errorMessage(errorMessage, "Application Events Listener Error"));
            await model.StartApplicationEventConsumerAsync(
                startupAction: _ => AppStartup( () => StartupOpenTrades()),
                shutdownAction: _ => AppShutdown());
        });

    /// <summary>
    /// startup any open trades
    /// </summary>
    void StartupOpenTrades()
    {

    }

    void GetLastFuturesEodData()
        => _appRoot.GetModel<MarketDataFeedQueryModel>().Execute(async model =>
        {
            model.OnError((_, errorMessage) => _errorMessage(errorMessage, "Loading Latest Futures Eod Data Error"));
            WriteStatusConsole("Loading Latest Futures Eod Data...");
            foreach (var contract in _baseContracts ?? [])
                await model.GetLastFuturesEodDataAsync(contract.ContractId, contract.LastTradeDate, futuresEodData => {
                    if (futuresEodData is not null)
                        _updateMarketOutlook?.Invoke(new FuturesEodDataUIViewModel(futuresEodData));
                });
        });

    void GetLastFuturesTradeSignal()
        => _appRoot.GetModel<MarketDataAnalyticsQueryModel>().Execute(async model =>
        {
            model.OnError((_, errorMessage) => _errorMessage(errorMessage, "Loading Latest Futures Trade Signal Error"));
            WriteStatusConsole("Loading Latest Futures Trade Signal...");
            await model.GetLastFuturesTradeSignalAsync(futuresTradeSignal =>
            {
                if (futuresTradeSignal is not null)
                    _updateTradeSignal?.Invoke(new FuturesTradeSignalUIViewModel(futuresTradeSignal));
            });
        });

        void GetLastFuturesBarData()
            => _appRoot.GetModel<MarketDataFeedQueryModel>().Execute(async model =>
            {
                model.OnError((_, errorMessage) => _errorMessage(errorMessage, "Loading Latest Futures Bar Data Error"));
                WriteStatusConsole("Loading Latest Futures Bar Data...");
                foreach (var contract in _baseContracts ?? [])
                    await model.GetLastFuturesBarDataAsync(contract.ContractId, contract.Symbol, DateOnly.FromDateTime(DateTime.UtcNow), futuresBarData =>
                    {
                        if (futuresBarData is not null)
                            _updateMarketData?.Invoke(futuresBarData.Symbol, [futuresBarData]);
                    });
            });

    /// <summary>
    /// start futures eod data event consumer
    /// </summary>
    void StartFuturesEodDataEventConsumer()
    {
        

        _appRoot.GetModel<MarketDataFeedCommandModel>().Execute(async model =>
        {
            model.OnError((_, errorMessage) => _errorMessage(errorMessage, "Starting Futures Eod Data Event Consumer Error"));
            WriteStatusConsole("Starting Futures Eod Data Event Consumer...");
            await Task.Delay(TimeSpan.FromSeconds(1));
            await model.StartFuturesEodDataEventConsumerAsync(
                _siteId, e =>
                {
                    _resetTicks = 0;
                    _updateMarketOutlook?.Invoke(new FuturesEodDataUIViewModel(e.FuturesEodData));
                    WriteStatusConsole($"{e.FuturesEodData.ContractId}={e.FuturesEodData.ClosePrice:F2}@{e.FuturesEodData.DailyPercentChange:P} {e.FuturesEodData.MarketDirection}:{e.FuturesEodData.MarketVolatility}:{e.FuturesEodData.PriceDirection}:{e.FuturesEodData.PriceVolatility}",
                                        LogSourceType.MarketDataFeedEvent);
                });
        });
    }

    /// <summary>
    /// stop futures eod data consumer
    /// </summary>
    void StopFuturesEodDataEventConsumer()
        => _appRoot.GetModel<MarketDataFeedCommandModel>().Execute(async model => {
            model.OnError((_, errorMessage) => _errorMessage(errorMessage, "Stopping Futures Eod Data Error"));
            WriteStatusConsole("Stopping Futures Eod Data...");
            await model.StopFuturesEodDataEventConsumerAsync(_siteId);
        });

    /// <summary>
    /// start futures trade signal event consumer
    /// </summary>
    void StartFuturesTradeSignalEventConsumer()
    {
        _appRoot.GetModel<MarketDataAnalyticsQueryModel>().Execute(async model =>
        {
            model.OnError((_, errorMessage) => _errorMessage(errorMessage, "Starting Futures Trade Signal Event Consumer Error"));
            WriteStatusConsole("Starting Futures Trade Signal event consumer...");
            await Task.Delay(TimeSpan.FromSeconds(1));
            var contractId = _baseContracts?.FirstOrDefault(e => e.Id.Symbol == "ES")?.ContractId;
            if (contractId is not null)
                await model.GetFuturesTradeSignalAsync(
                    contractId, _valueDate ?? DateOnly.MinValue, futuresTradeSignal =>
                    {
                        if (futuresTradeSignal is not null)
                            _updateTradeSignal?.Invoke(new FuturesTradeSignalUIViewModel( futuresTradeSignal));
                    });
        });
        _appRoot.GetModel<MarketDataAnalyticsCommandModel>().Execute(async model =>
        {
            model.OnError((_, errorMessage) => _errorMessage(errorMessage, "Starting Futures Trade Signal Event Consumer Error"));
            WriteStatusConsole("Starting Futures Trade Signal Event Consumer...");
            await model.StartFuturesTradeSignalEventConsumerAsync(
                _siteId, e =>
                {
                    if (e is not null && e.FuturesTradeSignal is not null)
                        _updateTradeSignal?.Invoke(new FuturesTradeSignalUIViewModel(e.FuturesTradeSignal));
                });
        });
    }

    /// <summary>
    /// stop futures trade signal consumer
    /// </summary>
    void StopFuturesTradeSignalEventConsumer()
        => _appRoot.GetModel<MarketDataFeedCommandModel>().Execute(async model => {
            model.OnError((_, errorMessage) => _errorMessage(errorMessage, "Stopping Futures Trade Signal Error"));
            WriteStatusConsole("Stopping Futures Trade Signal...");
            await model.StopFuturesTradeSignalEventConsumerAsync(_siteId);
        });

    /// <summary>
    /// start trade placement event consumer
    /// </summary>
    void StartTradePlacementEventConsumer()
        => _appRoot.GetModel<TradePlacementEventModel>().Execute(async model => {
            model.OnError((_, errorMessage) => _errorMessage(errorMessage, "Starting Trade Placement Event Consumer Error"));
            WriteStatusConsole("Starting Trade Placement Event Consumer...");
            await Task.Delay(TimeSpan.FromSeconds(1));
            await model.StartTradePlacementListenerAsync(e => _notifyTradePlacement?.Invoke(new PlaceTradeUIViewModel(e)));
            _appRoot.GetModel<TradePlacementCommandModel>().Execute(async tradePlacementModel => {
                var esContract = _baseContracts?.Where(e => e.ContractId.StartsWith("ES"))?.FirstOrDefault();
                if (esContract is not null && _valueDate.HasValue)
                {
                    await tradePlacementModel.StartTradePlacementAsync(esContract.ContractId, _valueDate.Value);
                    WriteStatusConsole("Starting Trade Placement Signal Service...");
                }
            });

        });

    /// <summary>
    /// stop trade placement consumer
    /// </summary>
    void StopTradePlacementEventConsumer()
        => _appRoot.GetModel<TradePlacementEventModel>().Execute(async model => {
            model.OnError((_, errorMessage) => _errorMessage(errorMessage, "Stopping Trade Placement Event Consumer Error"));
            WriteStatusConsole("Stopping Trade Placement Event Consumer...");
            await model.StopTradePlacementListenerAsync();
            _appRoot.GetModel<TradePlacementCommandModel>().Execute(async tradePlacementModel => {
                var esContract = _baseContracts?.Where(e => e.ContractId.StartsWith("ES"))?.FirstOrDefault();
                if (esContract is not null && _valueDate.HasValue)
                {
                    await tradePlacementModel.StopTradePlacementAsync(esContract.ContractId, _valueDate.Value);
                    WriteStatusConsole("Stopping Trade Placement Signal Service...");
                }
            });
        });

    /// <summary>
    /// start futures rsi signal service
    /// </summary>
    void StartFuturesRsiSignalService()
        => _appRoot.GetModel<MarketDataAnalyticsCommandModel>().Execute(async model => {
            model.OnError((_, errorMessage) => _errorMessage(errorMessage, "Starting Futures Rsi Signal Service Error"));
            await Task.Delay(TimeSpan.FromSeconds(1));
            var esContract = _baseContracts?.Where(e => e.ContractId.StartsWith("ES"))?.FirstOrDefault();
            if (esContract is not null && _valueDate.HasValue)
            {
                var entityId = FuturesRsiSignalEntityId.Create(esContract.ContractId, _valueDate.Value,  TradeTimePeriodType.Daily, 14);
                await model.StartFuturesRsiSignalServiceAsync(entityId);
                WriteStatusConsole("Starting Futures Rsi Signal Service...");
            }
        });

    /// <summary>
    /// stop futures rsi signal service
    /// </summary>
    void StopFuturesRsiSignalService()
        => _appRoot.GetModel<MarketDataAnalyticsCommandModel>().Execute(async model => {
            model.OnError((_, errorMessage) => _errorMessage(errorMessage, "Stopping Futures Rsi Signal Service Error"));
            var esContract = _baseContracts?.Where(e => e.ContractId.StartsWith("ES"))?.FirstOrDefault();
            if (esContract is not null && _valueDate.HasValue)
            {
                var entityId = FuturesRsiSignalEntityId.Create(esContract.ContractId,  _valueDate.Value, TradeTimePeriodType.Daily, 14);
                await model.StopFuturesRsiSignalServiceAsync(entityId);
                WriteStatusConsole("Stopping Futures Rsi Signal Service...");
            }
        });

    /// <summary>
    /// start futures bar data event consumer
    /// </summary>
    void StartFuturesBarDataEventConsumer()
        => _appRoot.GetModel<MarketDataFeedCommandModel>().Execute(async model => {
            model.OnError((_, errorMessage) => _errorMessage(errorMessage, "Starting Futures Bar Data Event Consumer Error"));
            WriteStatusConsole("Starting Futures Bar Data Event Consumer...");
            await Task.Delay(TimeSpan.FromSeconds(1));
            await model.StartFuturesBarDataEventConsumerAsync(
                _siteId, e => 
                _appRoot.GetModel<MarketDataFeedQueryModel>().Execute(async queryModel =>
                {
                    queryModel.OnError((_, errorMessage) => _errorMessage(errorMessage, "Loading Futures Bar Data Error"));
                    await queryModel.GetFuturesBarDataAsync(
                        e.FuturesBarData.ContractId,
                        e.FuturesBarData.Symbol,
                        e.FuturesBarData.ValueDate,
                        e.FuturesBarData.BarDate.AddHours(-6),
                        e.FuturesBarData.BarDate.AddSeconds(1), futuresBarData =>
                        {
                            _updateMarketData?.Invoke(e.FuturesBarData.Symbol, futuresBarData);
                            WriteStatusConsole($"FuturesBarData := {e.FuturesBarData.ContractId} @ {e.FuturesBarData.BarValue:F2}");
                        });
                }));
        });

    /// <summary>
    /// stop futures bar data consumer
    /// </summary>
    void StopFuturesBarDataEventConsumer()
        => _appRoot.GetModel<MarketDataFeedCommandModel>().Execute(async model => {
            model.OnError((_, errorMessage) => _errorMessage(errorMessage, "Stopping Futures Bar Data Event Consumer Error"));
            WriteStatusConsole("Stopping Futures Bar Data Event Consumer...");
            await model.StopFuturesBarDataEventConsumerAsync(_siteId);
        });

    /// <summary>
    /// import yiele curve rates
    /// </summary>
    void ImportYieldCurveRates(Action onCompleted)
        => _appRoot.GetModel<MarketDataCommandModel>()
            .Execute(async model => {
                model.OnError((_, errorMsg) => _errorMessage(errorMsg, "Import Yield Curve Rates Error"));
                YieldCurveRateReadModel[] yieldCurveRates = [];
                var importDate = DateTime.Now;
                await _appRoot.GetModel<MarketDataQueryModel>().GetExternalYieldCurveRatesAsync(e => yieldCurveRates = e);
                await model.ImportYieldCurveRatesAsync(importDate, yieldCurveRates ?? []);
                onCompleted?.Invoke();
                WriteStatusConsole($"{yieldCurveRates?.Length ?? 0} Yield Curve Rates Imported on: {importDate:yyyy-MM-dd}");
            });

    /// <summary>
    /// import economic calendars
    /// </summary>
    void ImportEconomicCalendars(Action onCompleted)
        => _appRoot.GetModel<ReferenceCommandModel>()
            .Execute(async model => {
                model.OnError((_, errorMsg) => _errorMessage(errorMsg, "Import Economic Calendars Error"));
                EconomicCalendarReadModel[] economicCalendars = [];
                var importDate = DateTime.Now;
                await _appRoot.GetModel<ReferenceQueryModel>().GetExternalEconomicCalendarsAsync(e => economicCalendars = e);
                await model.ImportEconomicCalendarsAsync(importDate, economicCalendars,
                    () => {
                        WriteStatusConsole($"Economic Calendars For: {importDate:yyyy-MM-dd} Imported");
                        onCompleted?.Invoke();
                    });
            });

    /// <summary>
    /// enable trade live feed
    /// </summary>
    void EnableTradeLiveFeed()
        => _appRoot.GetModel<MarketDataFeedCommandModel>().Execute(async model => {
            model.OnError((_, errorMessage) => _errorMessage(errorMessage, "Enable Trade Live Feed Error"));
            WriteStatusConsole("Starting Trade Data Feeds...");
            await Task.Delay(TimeSpan.FromSeconds(1));
            if (_valueDate is not null)
                await model.StartDataFeedAsync(_baseContracts, _valueDate.Value);
        }); 

    /// <summary>
    /// disable trade live feed
    /// </summary>
    /// <param name="resetAction"></param>
    void DisableTradeLiveFeed(Action? resetAction = null)
        => _appRoot.GetModel<MarketDataFeedCommandModel>().Execute(async model => {
            model.OnError((_, errorMessage) => _errorMessage(errorMessage, "Disable Trade Live Feed Error"));
            WriteStatusConsole("Stopping Trade Data Feeds...");
            if (_valueDate is not null)
                await model.StopDataFeedAsync(_valueDate.Value, async () => {
                    if (_baseContracts == null)
                        return;
                    foreach (var contract in _baseContracts)
                        await model.StopStreamingFuturesTickDataAsync(contract.ContractId, _valueDate.Value);
                    resetAction?.Invoke();
                });
        });

    /// <summary>
    /// enable market data feed reset listener
    /// </summary>
    void EnableMarketDataFeedResetListener()
        => _appRoot.GetModel<MarketDataFeedCommandModel>().Execute(async model => {
            model.OnError((_, errorMessage) => _errorMessage(errorMessage, "Enable MarketData Feed Reset Listener Error"));
            WriteStatusConsole("Starting Market Data Feed Reset Listener...");
            await Task.Delay(TimeSpan.FromSeconds(1));
            await model.StartMarketDataFeedResetListenerAsync(e => EnableTradeLiveFeed());
        });

    /// <summary>
    /// disable market data feed reset listener
    /// </summary>
    void DisableMarketDataFeedResetListener()
        => _appRoot.GetModel<MarketDataFeedCommandModel>().Execute(async model => {
            model.OnError((_, errorMessage) => _errorMessage(errorMessage, "Disable MarketData Feed Reset Listener Error"));
            await model.StopMarketDataFeedResetListenerAsync();
        });

    /// <summary>
    /// start reset live feed
    /// </summary>
    void StartResetLiveFeed()
    {
        _resetCancelled = false;
        _resetCanceller = new CancellationTokenSource();
        Task.Factory.StartNew(() =>
        {
            try
            {
                ResetLiveFeed();
            }
            catch (OperationCanceledException)
            {
                _resetCancelled = true;
            }
        }, _resetCanceller.Token);
        return;

        /// <summary>
        /// run every second to check if we have received any market data
        /// if not data received after max time expired (2 minutes), send reset market data feed command
        /// resetTicks will be cleared every time we receive data from market data feed
        /// only execute market data reset command outside of main trading hours
        /// </summary>
        /// <returns></returns>
        void ResetLiveFeed()
        {
            var resetMaxTicks = 900; // reset after 15 minutes of no market feed asset price updates...
            while (!_resetCancelled)
            {
                Task.Delay(TimeSpan.FromSeconds(1)).Wait();
                _resetTicks++;
                if (_resetTicks > resetMaxTicks)
                {
                    _resetTicks = 0;
                    _appRoot.GetModel<MarketDataQueryModel>().Execute(async mktDataModel => {
                        await mktDataModel.GetValueDateAsync(valueDate => {
                            if (valueDate.HasValue)
                            {
                                WriteStatusConsole("Reseting Live Feed...Market Data Feed Failing To Respond");
                                _appRoot.GetModel<MarketDataFeedCommandModel>().Execute(async model =>
                                {
                                    await model.ResetDataFeedAsync(_baseContracts,valueDate.Value);
                                    foreach(var e in _baseContracts)
                                        await model.DeleteFuturesBarDataAsync(new FuturesBarDataId(e.ContractId, e.Symbol, valueDate.Value));
                                });
                            }
                        });
                    });
                }
            }
        }
    }


    /// <summary>
    /// stop reset live feed
    /// </summary>
    void StopResetLiveFeed()
    {
        try
        {
            _resetCanceller?.Cancel();
            _resetCanceller?.Dispose();
            _resetCanceller = null;
        }
        catch { }
    }

    /// <summary>
    /// write message to console
    /// </summary>
    /// <param name="message"></param>
    /// <param name="logSourceType"></param>
    void WriteStatusConsole(string message, LogSourceType logSourceType = LogSourceType.IFMApp)
    {
        WriteStatusConsoleLog(async () => await _appRoot.GetStatusConsoleWriter().WriteConsoleAsync(logSourceType, message));
        return;

        static void WriteStatusConsoleLog(Action logWriter) => logWriter();
    }

}
