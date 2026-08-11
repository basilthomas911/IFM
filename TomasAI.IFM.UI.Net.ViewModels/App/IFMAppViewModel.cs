using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Models;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Shared.StatusConsole.ViewModels;
using TomasAI.IFM.UI.Net.ViewModels.MarketData;
using TomasAI.IFM.UI.Net.ViewModels.Lifecycle;

namespace TomasAI.IFM.UI.Net.ViewModels.App;

public class IFMAppViewModel : IAsyncLifecycle, IAsyncDisposable
{
    readonly IAppRoot _appRoot;
    readonly TimeProvider _timeProvider;
    readonly AsyncLifecycleCoordinator _lifecycle;
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
    Func<ValueTask> _closeTradeBlotters = null!;
    Action _requestApplicationClose = null!;
    Action<string, DateOnly> _loadStatusConsole = null!;
    Func<ValueTask> _unloadStatusConsole = null!;
    int _resetTicks;

    /// <summary>
    /// create IFM app root view model
    /// </summary>
    /// <param name="appRoot"></param>
    public IFMAppViewModel(IAppRoot appRoot, TimeProvider? timeProvider = null)
    {
        _appRoot = appRoot ?? throw new ArgumentNullException(nameof(appRoot));
        _timeProvider = timeProvider ?? TimeProvider.System;
        _siteId = Guid.NewGuid();
        _appRoot.GetModel<EventModel>().SetSiteId(_siteId);
        _lifecycle = new AsyncLifecycleCoordinator(InitializeCoreAsync, StopCoreAsync);
    }

    public DateOnly? ValueDate => _valueDate;
    public ICollection<FuturesContractV2ReadModel> BaseContracts => _baseContracts;

    /// <summary>
    /// initialze application
    /// </summary>
    /// <param name="onErrorMessage"></param>
    /// <param name="onEnableMenuBarButtons"></param>
    /// <param name="writeStatusConsole"></param>
    public async Task AppStartup(
        Version appVersion,
        string appEnvironment,
        Action<string, string> onErrorMessage,
        Action onEnableMenuBarButtons,
        Action<string, DateOnly> loadStatusConsole,
        Func<ValueTask> unloadStatusConsole,
        Action<string> writeStatusLine,
        Action<StatusConsoleLogReadModel[]> writeStatusConsole,
        Action<FuturesEodDataUIViewModel> updateMarketOutlook,
        Action<FuturesTradeSignalUIViewModel> updateTradeSignal,
        Action<PlaceTradeUIViewModel> notifyTradePlacement,
        Action<string, FuturesBarDataReadModel[]> updateMarketData,
        Func<ValueTask> closeTradeBlotters,
        Action requestApplicationClose)
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
        _requestApplicationClose = requestApplicationClose;
        _loadStatusConsole = loadStatusConsole;
        _unloadStatusConsole = unloadStatusConsole;
        await InitializeAsync(CancellationToken.None);
    }

    /// <summary>
    /// application cleanup before it closes
    /// </summary>
    public async Task AppShutdown()
    {
        WriteStatusConsole($"IFMApp v{_appVersion} - {_appEnvironment}...shutting down");
        if (_unloadStatusConsole is not null)
            await _unloadStatusConsole();
        if (_closeTradeBlotters is not null)
            await _closeTradeBlotters();
        await StopAsync(CancellationToken.None);
    }

    /// <inheritdoc />
    public Task InitializeAsync(CancellationToken cancellationToken)
        => _lifecycle.InitializeAsync(cancellationToken);

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
        => _lifecycle.StopAsync(cancellationToken);

    /// <inheritdoc />
    public ValueTask DisposeAsync()
        => _lifecycle.DisposeAsync();

    async Task InitializeCoreAsync(CancellationToken cancellationToken)
    {
        await StartStatusConsoleListener();
        await StartApplicationEventsListener();
        await StartApplicationCoreAsync(cancellationToken);
    }

    async Task StopCoreAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await StopFuturesEodDataEventConsumer();
        await StopFuturesBarDataEventConsumer();
        await StopFuturesTradeSignalEventConsumer();
        await StopTradePlacementEventConsumer();
        await StopFuturesRsiSignalService();
        await DisableMarketDataFeedResetListener();
        await DisableTradeLiveFeed();
        await _appRoot.GetModel<ApplicationEventModel>().StopApplicationEventConsumerAsync();
        await _appRoot.GetModel<StatusConsoleModel>().StopStatusConsoleLogListener(_siteId);
    }

    /// <summary>
    /// application startup
    /// </summary>
    Task StartApplicationCoreAsync(CancellationToken cancellationToken, Action? tradeStartup = null)
        => _appRoot.GetModel<MarketDataQueryModel>().ExecuteAsync(async model =>
        {
            model.OnError((_, errorMsg) => _errorMessage($"Unable to connect to IFM servers {Environment.NewLine}{errorMsg}", "Market Data Error"));
            ICollection<FuturesContractV2ReadModel>? futuresContracts = null;
            await model.GetCurrentlyTradedFuturesContractsAsync(values => futuresContracts = values);
            _baseContracts = futuresContracts ?? [];
            await GetLastFuturesEodData();
            await GetLastFuturesTradeSignal();
            await GetLastFuturesBarData();

            DateOnly? valueDate = null;
            await model.GetValueDateAsync(value => valueDate = value);
            if (!valueDate.HasValue)
            {
                _errorMessage("Market Data Live Feed unavailable outside of valid Trading Hours", "Market Data Feed Error");
                return;
            }

            _valueDate = valueDate;
            await ImportYieldCurveRates(() => { });
            await ImportEconomicCalendars(() => { });
            await StartFuturesEodDataEventConsumer(cancellationToken);
            await StartFuturesBarDataEventConsumer(cancellationToken);
            await StartFuturesTradeSignalEventConsumer(cancellationToken);
            await StartTradePlacementEventConsumer(cancellationToken);
            await EnableMarketDataFeedResetListener(cancellationToken);
            await EnableTradeLiveFeed(cancellationToken);
            _lifecycle.RunAsync(ResetLiveFeedAsync);
            await StartFuturesRsiSignalService(cancellationToken);
            WriteStatusConsole($"IFMApp v{_appVersion} - {_appEnvironment}...initialization complete");
            tradeStartup?.Invoke();
            _loadStatusConsole?.Invoke(
                _baseContracts.Where(e => e.Symbol == "ES").Select(e => e.ContractId).FirstOrDefault() ?? string.Empty,
                _valueDate.Value);
            _onEnableMenuBarButtons?.Invoke();
        });


    /// <summary>
    /// start console listener
    /// </summary>
    Task StartStatusConsoleListener()
        => _appRoot.GetModel<StatusConsoleModel>().ExecuteAsync(async model => {
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
    Task StartApplicationEventsListener()
        => _appRoot.GetModel<ApplicationEventModel>().ExecuteAsync(async model => {
            model.OnError((_, errorMessage) => _errorMessage(errorMessage, "Application Events Listener Error"));
            await model.StartApplicationEventConsumerAsync(
                startupAction: _ =>
                {
                    StartupOpenTrades();
                    return ValueTask.CompletedTask;
                },
                shutdownAction: _ =>
                {
                    _requestApplicationClose();
                    return ValueTask.CompletedTask;
                });
        });

    /// <summary>
    /// startup any open trades
    /// </summary>
    void StartupOpenTrades()
    {

    }

    Task GetLastFuturesEodData()
        => _appRoot.GetModel<MarketDataFeedQueryModel>().ExecuteAsync(async model =>
        {
            model.OnError((_, errorMessage) => _errorMessage(errorMessage, "Loading Latest Futures Eod Data Error"));
            WriteStatusConsole("Loading Latest Futures Eod Data...");
            foreach (var contract in _baseContracts ?? [])
                await model.GetLastFuturesEodDataAsync(contract.ContractId, contract.LastTradeDate, futuresEodData => {
                    if (futuresEodData is not null)
                        _updateMarketOutlook?.Invoke(new FuturesEodDataUIViewModel(futuresEodData));
                });
        });

    Task GetLastFuturesTradeSignal()
        => _appRoot.GetModel<MarketDataAnalyticsQueryModel>().ExecuteAsync(async model =>
        {
            model.OnError((_, errorMessage) => _errorMessage(errorMessage, "Loading Latest Futures Trade Signal Error"));
            WriteStatusConsole("Loading Latest Futures Trade Signal...");
            await model.GetLastFuturesTradeSignalAsync(futuresTradeSignal =>
            {
                if (futuresTradeSignal is not null)
                    _updateTradeSignal?.Invoke(new FuturesTradeSignalUIViewModel(futuresTradeSignal));
            });
        });

        Task GetLastFuturesBarData()
            => _appRoot.GetModel<MarketDataFeedQueryModel>().ExecuteAsync(async model =>
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
    async Task StartFuturesEodDataEventConsumer(CancellationToken cancellationToken)
    {


        await _appRoot.GetModel<MarketDataFeedCommandModel>().ExecuteAsync(async model =>
        {
            model.OnError((_, errorMessage) => _errorMessage(errorMessage, "Starting Futures Eod Data Event Consumer Error"));
            WriteStatusConsole("Starting Futures Eod Data Event Consumer...");
            await DelayStartupAsync(cancellationToken);
            await model.StartFuturesEodDataEventConsumerAsync(
                _siteId, e =>
                {
                    Interlocked.Exchange(ref _resetTicks, 0);
                    _updateMarketOutlook?.Invoke(new FuturesEodDataUIViewModel(e.FuturesEodData));
                    WriteStatusConsole($"{e.FuturesEodData.ContractId}={e.FuturesEodData.ClosePrice:F2}@{e.FuturesEodData.DailyPercentChange:P} {e.FuturesEodData.MarketDirection}:{e.FuturesEodData.MarketVolatility}:{e.FuturesEodData.PriceDirection}:{e.FuturesEodData.PriceVolatility}",
                                        LogSourceType.MarketDataFeedEvent);
                });
        });
    }

    /// <summary>
    /// stop futures eod data consumer
    /// </summary>
    Task StopFuturesEodDataEventConsumer()
        => _appRoot.GetModel<MarketDataFeedCommandModel>().ExecuteAsync(async model => {
            model.OnError((_, errorMessage) => _errorMessage(errorMessage, "Stopping Futures Eod Data Error"));
            WriteStatusConsole("Stopping Futures Eod Data...");
            await model.StopFuturesEodDataEventConsumerAsync(_siteId);
        });

    /// <summary>
    /// start futures trade signal event consumer
    /// </summary>
    async Task StartFuturesTradeSignalEventConsumer(CancellationToken cancellationToken)
    {
        await _appRoot.GetModel<MarketDataAnalyticsQueryModel>().ExecuteAsync(async model =>
        {
            model.OnError((_, errorMessage) => _errorMessage(errorMessage, "Starting Futures Trade Signal Event Consumer Error"));
            WriteStatusConsole("Starting Futures Trade Signal event consumer...");
            await DelayStartupAsync(cancellationToken);
            var contractId = _baseContracts?.FirstOrDefault(e => e.Id.Symbol == "ES")?.ContractId;
            if (contractId is not null)
                await model.GetFuturesTradeSignalAsync(
                    contractId, _valueDate ?? DateOnly.MinValue, futuresTradeSignal =>
                    {
                        if (futuresTradeSignal is not null)
                            _updateTradeSignal?.Invoke(new FuturesTradeSignalUIViewModel( futuresTradeSignal));
                    });
        });
        await _appRoot.GetModel<MarketDataAnalyticsCommandModel>().ExecuteAsync(async model =>
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
    Task StopFuturesTradeSignalEventConsumer()
        => _appRoot.GetModel<MarketDataFeedCommandModel>().ExecuteAsync(async model => {
            model.OnError((_, errorMessage) => _errorMessage(errorMessage, "Stopping Futures Trade Signal Error"));
            WriteStatusConsole("Stopping Futures Trade Signal...");
            await model.StopFuturesTradeSignalEventConsumerAsync(_siteId);
        });

    /// <summary>
    /// start trade placement event consumer
    /// </summary>
    Task StartTradePlacementEventConsumer(CancellationToken cancellationToken)
        => _appRoot.GetModel<TradePlacementEventModel>().ExecuteAsync(async model => {
            model.OnError((_, errorMessage) => _errorMessage(errorMessage, "Starting Trade Placement Event Consumer Error"));
            WriteStatusConsole("Starting Trade Placement Event Consumer...");
            await DelayStartupAsync(cancellationToken);
            await model.StartTradePlacementListenerAsync(e => _notifyTradePlacement?.Invoke(new PlaceTradeUIViewModel(e)));
            await _appRoot.GetModel<TradePlacementCommandModel>().ExecuteAsync(async tradePlacementModel => {
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
    Task StopTradePlacementEventConsumer()
        => _appRoot.GetModel<TradePlacementEventModel>().ExecuteAsync(async model => {
            model.OnError((_, errorMessage) => _errorMessage(errorMessage, "Stopping Trade Placement Event Consumer Error"));
            WriteStatusConsole("Stopping Trade Placement Event Consumer...");
            await model.StopTradePlacementListenerAsync();
            await _appRoot.GetModel<TradePlacementCommandModel>().ExecuteAsync(async tradePlacementModel => {
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
    Task StartFuturesRsiSignalService(CancellationToken cancellationToken)
        => _appRoot.GetModel<MarketDataAnalyticsCommandModel>().ExecuteAsync(async model => {
            model.OnError((_, errorMessage) => _errorMessage(errorMessage, "Starting Futures Rsi Signal Service Error"));
            await DelayStartupAsync(cancellationToken);
            var esContract = _baseContracts?.Where(e => e.ContractId.StartsWith("ES"))?.FirstOrDefault();
            if (esContract is not null && _valueDate.HasValue)
            {
                var entityId = FuturesRsiSignalEntityId.Create(esContract.ContractId, _valueDate.Value,  TimeFrameType.Daily, 14);
                await model.StartFuturesRsiSignalServiceAsync(entityId);
                WriteStatusConsole("Starting Futures Rsi Signal Service...");
            }
        });

    /// <summary>
    /// stop futures rsi signal service
    /// </summary>
    Task StopFuturesRsiSignalService()
        => _appRoot.GetModel<MarketDataAnalyticsCommandModel>().ExecuteAsync(async model => {
            model.OnError((_, errorMessage) => _errorMessage(errorMessage, "Stopping Futures Rsi Signal Service Error"));
            var esContract = _baseContracts?.Where(e => e.ContractId.StartsWith("ES"))?.FirstOrDefault();
            if (esContract is not null && _valueDate.HasValue)
            {
                var entityId = FuturesRsiSignalEntityId.Create(esContract.ContractId,  _valueDate.Value, TimeFrameType.Daily, 14);
                await model.StopFuturesRsiSignalServiceAsync(entityId);
                WriteStatusConsole("Stopping Futures Rsi Signal Service...");
            }
        });

    /// <summary>
    /// start futures bar data event consumer
    /// </summary>
    Task StartFuturesBarDataEventConsumer(CancellationToken cancellationToken)
        => _appRoot.GetModel<MarketDataFeedCommandModel>().ExecuteAsync(async model => {
            model.OnError((_, errorMessage) => _errorMessage(errorMessage, "Starting Futures Bar Data Event Consumer Error"));
            WriteStatusConsole("Starting Futures Bar Data Event Consumer...");
            await DelayStartupAsync(cancellationToken);
            await model.StartFuturesBarDataEventConsumerAsync(
                _siteId, async e =>
                await _appRoot.GetModel<MarketDataFeedQueryModel>().ExecuteAsync(async queryModel =>
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
    Task StopFuturesBarDataEventConsumer()
        => _appRoot.GetModel<MarketDataFeedCommandModel>().ExecuteAsync(async model => {
            model.OnError((_, errorMessage) => _errorMessage(errorMessage, "Stopping Futures Bar Data Event Consumer Error"));
            WriteStatusConsole("Stopping Futures Bar Data Event Consumer...");
            await model.StopFuturesBarDataEventConsumerAsync(_siteId);
        });

    /// <summary>
    /// import yiele curve rates
    /// </summary>
    Task ImportYieldCurveRates(Action onCompleted)
        => _appRoot.GetModel<MarketDataCommandModel>()
            .ExecuteAsync(async model => {
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
    Task ImportEconomicCalendars(Action onCompleted)
        => _appRoot.GetModel<ReferenceCommandModel>()
            .ExecuteAsync(async model => {
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
    Task EnableTradeLiveFeed(CancellationToken cancellationToken = default)
        => _appRoot.GetModel<MarketDataFeedCommandModel>().ExecuteAsync(async model => {
            model.OnError((_, errorMessage) => _errorMessage(errorMessage, "Enable Trade Live Feed Error"));
            WriteStatusConsole("Starting Trade Data Feeds...");
            await DelayStartupAsync(cancellationToken);
            if (_valueDate is not null)
                await model.StartDataFeedAsync(_baseContracts, _valueDate.Value);
        });

    /// <summary>
    /// disable trade live feed
    /// </summary>
    /// <param name="resetAction"></param>
    Task DisableTradeLiveFeed(Action? resetAction = null)
        => _appRoot.GetModel<MarketDataFeedCommandModel>().ExecuteAsync(async model => {
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
    Task EnableMarketDataFeedResetListener(CancellationToken cancellationToken)
        => _appRoot.GetModel<MarketDataFeedCommandModel>().ExecuteAsync(async model => {
            model.OnError((_, errorMessage) => _errorMessage(errorMessage, "Enable MarketData Feed Reset Listener Error"));
            WriteStatusConsole("Starting Market Data Feed Reset Listener...");
            await DelayStartupAsync(cancellationToken);
            await model.StartMarketDataFeedResetListenerAsync(
                _ => new ValueTask(EnableTradeLiveFeed()));
        });

    /// <summary>
    /// disable market data feed reset listener
    /// </summary>
    Task DisableMarketDataFeedResetListener()
        => _appRoot.GetModel<MarketDataFeedCommandModel>().ExecuteAsync(async model => {
            model.OnError((_, errorMessage) => _errorMessage(errorMessage, "Disable MarketData Feed Reset Listener Error"));
            await model.StopMarketDataFeedResetListenerAsync();
        });

    /// <summary>
    /// Runs the market-data watchdog until the application lifetime is cancelled.
    /// </summary>
    async Task ResetLiveFeedAsync(CancellationToken cancellationToken)
    {
        const int resetMaxTicks = 900;
        while (true)
        {
            await Task.Delay(TimeSpan.FromSeconds(1), _timeProvider, cancellationToken).ConfigureAwait(false);
            if (Interlocked.Increment(ref _resetTicks) <= resetMaxTicks)
                continue;

            Interlocked.Exchange(ref _resetTicks, 0);
            DateOnly? valueDate = null;
            await _appRoot.GetModel<MarketDataQueryModel>().ExecuteAsync(async marketDataModel =>
                await marketDataModel.GetValueDateAsync(value => valueDate = value));

            if (!valueDate.HasValue)
                continue;

            WriteStatusConsole("Reseting Live Feed...Market Data Feed Failing To Respond");
            await _appRoot.GetModel<MarketDataFeedCommandModel>().ExecuteAsync(async model =>
            {
                await model.ResetDataFeedAsync(_baseContracts, valueDate.Value);
                foreach (var contract in _baseContracts)
                    await model.DeleteFuturesBarDataAsync(
                        new FuturesBarDataId(contract.ContractId, contract.Symbol, valueDate.Value));
            });
        }
    }

    Task DelayStartupAsync(CancellationToken cancellationToken)
        => Task.Delay(TimeSpan.FromSeconds(1), _timeProvider, cancellationToken);

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
