using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Models;
using TomasAI.IFM.UI.Net.ViewModels.Extensions;
using TomasAI.IFM.UI.Net.ViewModels.Lifecycle;
using TomasAI.IFM.UI.Net.ViewModels.Operations;

namespace TomasAI.IFM.UI.Net.ViewModels.Reference;

/// <summary>
/// Coordinates economic-calendar maintenance and correlated external import completion.
/// </summary>
public sealed class EconomicCalendarEditorViewModel
    : BaseEditorViewModel, IAsyncLifecycle, IAsyncDisposable
{
    readonly EconomicCalendarEventModel _eventModel;
    readonly MarketDataCommandModel _commandModel;
    readonly MarketDataQueryModel _queryModel;
    readonly AsyncLifecycleCoordinator _lifecycle;
    readonly AsyncOperation _importOperation;
    readonly object _correlationGate = new();
    readonly Dictionary<Guid, IEvent> _earlyTerminalEvents = [];
    List<EconomicCalendarCountryCodeReadModel> _countryCodes = [];
    List<EconomicCalendarReadModel> _economicCalendars = [];
    DateTime? _pendingImportDate;
    string? _pendingImportCountryCode;
    TaskCompletionSource<IEvent>? _terminalCompletion;
    Guid _commandId;
    string _lastStatusMessage = string.Empty;

    /// <summary>Creates the editor and resolves its application Models.</summary>
    public EconomicCalendarEditorViewModel(IAppRoot appRoot) : base(appRoot)
    {
        _eventModel = appRoot.GetModel<EconomicCalendarEventModel>();
        _commandModel = appRoot.GetModel<MarketDataCommandModel>();
        _queryModel = appRoot.GetModel<MarketDataQueryModel>();
        _lifecycle = new AsyncLifecycleCoordinator(StartListenerCoreAsync, StopListenerCoreAsync);
        _importOperation = new AsyncOperation(
            ImportCoreAsync,
            () => _pendingImportDate.HasValue
                && !string.IsNullOrWhiteSpace(_pendingImportCountryCode)
                && _lifecycle.IsRunning);
    }

    public ICollection<EconomicCalendarCountryCodeReadModel> CountryCodes => _countryCodes;
    public ICollection<EconomicCalendarReadModel> EconomicCalendars => _economicCalendars;

    public Action OnCountryCodesLoaded = () => { };
    public Action OnEconomicCalendarsLoaded = () => { };
    public Action OnWaitCursor = () => { };
    public Action OnDefaultCursor = () => { };

    /// <summary>Gets the command identifier while an import awaits its terminal event.</summary>
    public Guid CommandId
    {
        get
        {
            lock (_correlationGate)
                return _commandId;
        }
    }

    /// <summary>Gets the latest successfully completed import status.</summary>
    public string LastStatusMessage
    {
        get => _lastStatusMessage;
        private set => SetProperty(ref _lastStatusMessage, value);
    }

    /// <summary>Gets the single-flight, terminally correlated import operation.</summary>
    public IAsyncOperation ImportOperation => _importOperation;

    /// <summary>Retrieves the country code at the specified list index.</summary>
    public string GetCountryCode(int index)
        => index >= 0 && index < _countryCodes.Count ? _countryCodes[index].CountryCode : null!;

    /// <summary>Retrieves the economic calendar at the specified list index.</summary>
    public EconomicCalendarReadModel GetEconomicCalendar(int index)
        => index >= 0 && index < _economicCalendars.Count ? _economicCalendars[index] : null!;

    /// <summary>Retrieves the index of the specified country code.</summary>
    public int GetCountryCodeIndex(string countryCode)
    {
        for (var index = 0; index < _countryCodes.Count; index++)
            if (_countryCodes[index].CountryCode == countryCode)
                return index;
        return -1;
    }

    /// <summary>Adds a new economic-calendar entry.</summary>
    public Task AddEconomicCalendar(EconomicCalendarReadModel economicCalendar, Action onCompleted)
        => _commandModel.ExecuteAsync(async model =>
        {
            model.OnError((errorCode, errorMsg) => OnError(errorCode, errorMsg));
            await model.AddEconomicCalendarAsync(economicCalendar);
            _ = LoadEconomicCalendars(
                DateOnly.FromDateTime(economicCalendar.EventDate),
                economicCalendar.CountryCode);
            _ = WriteStatusConsole(
                LogSourceType.Reference,
                $"Economic Calendar {economicCalendar.Id} Added");
            onCompleted?.Invoke();
        });

    /// <summary>Changes an economic-calendar entry.</summary>
    public Task ChangeEconomicCalendar(
        EconomicCalendarId economicCalendarId,
        EconomicCalendarReadModel economicCalendar,
        bool overwrite,
        Action onCompleted)
        => _commandModel.ExecuteAsync(async model =>
        {
            model.OnError((errorCode, errorMsg) => OnError(errorCode, errorMsg));
            await model.ChangeEconomicCalendarAsync(economicCalendarId, economicCalendar, overwrite);
            _ = LoadEconomicCalendars(
                DateOnly.FromDateTime(economicCalendar.EventDate),
                economicCalendar.CountryCode);
            _ = WriteStatusConsole(
                LogSourceType.Reference,
                $"Economic Calendar {economicCalendarId} Changed");
            onCompleted?.Invoke();
        });

    /// <summary>Removes an economic-calendar entry.</summary>
    public Task RemoveEconomicCalendar(EconomicCalendarId economicCalendarId, bool overwrite)
        => _commandModel.ExecuteAsync(async model =>
        {
            model.OnError((errorCode, errorMsg) => OnError(errorCode, errorMsg));
            await model.RemoveEconomicCalendarAsync(economicCalendarId, overwrite);
            _ = LoadEconomicCalendars(
                DateOnly.FromDateTime(economicCalendarId.EventDate),
                economicCalendarId.CountryCode);
            _ = WriteStatusConsole(
                LogSourceType.Reference,
                $"Economic Calendar {economicCalendarId} Removed");
        });

    /// <summary>Prepares an economic-calendar import for the guarded operation.</summary>
    public void PrepareImport(DateTime importDate, string countryCode)
    {
        if (string.IsNullOrWhiteSpace(countryCode))
            throw new ArgumentException("An economic-calendar country code is required.", nameof(countryCode));

        _pendingImportDate = importDate;
        _pendingImportCountryCode = countryCode.Trim();
        lock (_correlationGate)
            _earlyTerminalEvents.Clear();
        LastStatusMessage = string.Empty;
        _importOperation.NotifyCanExecuteChanged();
    }

    /// <summary>Loads country codes after starting the terminal-event listener.</summary>
    public async Task LoadCountryCodes()
    {
        await InitializeAsync(CancellationToken.None);
        await LoadCountryCodesCoreAsync(CancellationToken.None);
    }

    /// <summary>Loads the durable calendar projection for a date and country.</summary>
    public Task LoadEconomicCalendars(DateOnly eventDate, string countryCode)
        => LoadEconomicCalendarsCoreAsync(eventDate, countryCode, CancellationToken.None);

    /// <inheritdoc />
    public Task InitializeAsync(CancellationToken cancellationToken)
        => _lifecycle.InitializeAsync(cancellationToken);

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _importOperation.Cancel();
        await AwaitImportStoppedAsync();
        await _lifecycle.StopAsync(cancellationToken);
        ClearCorrelation();
        _importOperation.NotifyCanExecuteChanged();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await StopAsync(CancellationToken.None);
        await _lifecycle.DisposeAsync();
    }

    Task StartListenerCoreAsync(CancellationToken cancellationToken)
        => _eventModel.ExecuteObservableAsync(
            model => model.StartEconomicCalendarEventListenersAsync(
                _ => { },
                _ => { },
                _ => { },
                HandleTerminalEvent,
                HandleTerminalEvent),
            cancellationToken);

    Task StopListenerCoreAsync(CancellationToken cancellationToken)
        => _eventModel.ExecuteObservableAsync(
            model => model.StopEconomicCalendarEventListenersAsync(),
            cancellationToken);

    async Task LoadCountryCodesCoreAsync(CancellationToken cancellationToken)
    {
        EconomicCalendarCountryCodeReadModel[] loaded = [];
        await _queryModel.ExecuteObservableAsync(
            model => model.LoadEconomicCalendarCountryCodesAsync(values => loaded = values ?? []),
            cancellationToken);
        _countryCodes = [.. loaded];
        OnCountryCodesLoaded?.Invoke();
    }

    async Task LoadEconomicCalendarsCoreAsync(
        DateOnly eventDate,
        string countryCode,
        CancellationToken cancellationToken)
    {
        EconomicCalendarReadModel[] loaded = [];
        await _queryModel.ExecuteObservableAsync(
            model => model.LoadEconomicCalendarsAsync(
                eventDate,
                countryCode,
                values => loaded = values ?? []),
            cancellationToken);
        _economicCalendars = [.. loaded.OrderBy(value => value.EventDate)];
        OnEconomicCalendarsLoaded?.Invoke();
    }

    async Task ImportCoreAsync(CancellationToken cancellationToken)
    {
        var importDate = _pendingImportDate
            ?? throw new InvalidOperationException("No economic-calendar import date is prepared.");
        var countryCode = _pendingImportCountryCode
            ?? throw new InvalidOperationException("No economic-calendar country code is prepared.");

        try
        {
            Guid commandId = Guid.Empty;
            await _commandModel.ExecuteObservableAsync(
                async model => commandId = await model.ImportEconomicCalendarsAsync(
                    importDate,
                    [countryCode]),
                cancellationToken);
            if (commandId == Guid.Empty)
                throw new InvalidOperationException(
                    "The economic-calendar import command returned an empty correlation identifier.");

            var terminalEvent = await AwaitTerminalEventAsync(commandId, cancellationToken);
            if (terminalEvent is IErrorEvent error)
                throw new ModelOperationException(error.ErrorCode, error.ErrorMessage);

            await LoadEconomicCalendarsCoreAsync(
                DateOnly.FromDateTime(importDate),
                countryCode,
                cancellationToken);
            LastStatusMessage = $"Economic Calendars Imported for {importDate:yyyy-MM-dd} ({countryCode})";
            _pendingImportDate = null;
            _pendingImportCountryCode = null;
        }
        finally
        {
            ClearCorrelation();
            _importOperation.NotifyCanExecuteChanged();
        }
    }

    async Task<IEvent> AwaitTerminalEventAsync(Guid commandId, CancellationToken cancellationToken)
    {
        IEvent? earlyEvent;
        TaskCompletionSource<IEvent> completion;
        lock (_correlationGate)
        {
            _commandId = commandId;
            completion = new TaskCompletionSource<IEvent>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            _terminalCompletion = completion;
            _earlyTerminalEvents.Remove(commandId, out earlyEvent);
            _earlyTerminalEvents.Clear();
        }
        OnPropertyChanged(nameof(CommandId));
        if (earlyEvent is not null)
            completion.TrySetResult(earlyEvent);
        return await completion.Task.WaitAsync(cancellationToken);
    }

    void HandleTerminalEvent(IEvent @event)
    {
        TaskCompletionSource<IEvent>? completion;
        lock (_correlationGate)
        {
            if (_commandId == Guid.Empty)
            {
                if (_importOperation.IsRunning && _earlyTerminalEvents.Count < 32)
                    _earlyTerminalEvents[@event.CommandId] = @event;
                return;
            }
            if (_commandId != @event.CommandId)
                return;
            completion = _terminalCompletion;
        }
        completion?.TrySetResult(@event);
    }

    void ClearCorrelation()
    {
        lock (_correlationGate)
        {
            _commandId = Guid.Empty;
            _terminalCompletion = null;
            _earlyTerminalEvents.Clear();
        }
        OnPropertyChanged(nameof(CommandId));
    }

    async Task AwaitImportStoppedAsync()
    {
        try
        {
            await _importOperation.DisposeAsync();
        }
        catch (Exception) when (_importOperation.LastFailure is not null)
        {
            // The operation caller observes LastFailure; shutdown only awaits ownership cleanup.
        }
    }
}
