using TomasAI.IFM.Domain.Reference.Shared;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Models;
using TomasAI.IFM.UI.Net.ViewModels.Extensions;
using TomasAI.IFM.UI.Net.ViewModels.Lifecycle;
using TomasAI.IFM.UI.Net.ViewModels.Operations;
using TomasAI.IFM.UI.Net.ViewModels.Presentation;

namespace TomasAI.IFM.UI.Net.ViewModels.App;

/// <summary>
/// Owns the economic-calendar listener and exposes an observable read-only calendar snapshot.
/// </summary>
public sealed class MarketEconomicCalendarViewModel
    : ObservableObject, IAsyncLifecycle, IAsyncDisposable
{
    readonly MarketDataQueryModel _marketDataQueryModel;
    readonly EconomicCalendarEventModel _eventModel;
    readonly AsyncLifecycleCoordinator _lifecycle;
    IReadOnlyList<EconomicCalendarReadModel> _economicCalendars = [];
    IReadOnlyList<string> _countryCodes = [];
    string _selectedCountryCode = string.Empty;
    string _calendarDate = string.Empty;
    EconomicCalendarReadModel? _selectedEconomicCalendar;
    PresentationError? _lastError;
    DateTime _today = EasternTime.GetNow(TimeProvider.System).Date;
    EconomicCalendarViewType _calendarViewType = EconomicCalendarViewType.Today;
    long _errorSequence;
    int _acceptEvents;

    /// <summary>Creates an economic-calendar ViewModel from application Models.</summary>
    public MarketEconomicCalendarViewModel(IAppRoot appRoot)
    {
        ArgumentNullException.ThrowIfNull(appRoot);
        _marketDataQueryModel = appRoot.GetModel<MarketDataQueryModel>();
        _eventModel = appRoot.GetModel<EconomicCalendarEventModel>();
        _lifecycle = new AsyncLifecycleCoordinator(StartListenersCoreAsync, StopListenersCoreAsync);
        LoadCountryCodesOperation = new AsyncOperation(LoadCountryCodesCoreAsync);
        RefreshOperation = new AsyncOperation(RefreshCoreAsync);
    }

    /// <summary>Gets available country-code filters.</summary>
    public IReadOnlyList<string> CountryCodes
    {
        get => _countryCodes;
        private set => SetProperty(ref _countryCodes, value);
    }

    /// <summary>Gets the selected country-code filter.</summary>
    public string SelectedCountryCode
    {
        get => _selectedCountryCode;
        private set => SetProperty(ref _selectedCountryCode, value);
    }

    /// <summary>Gets the current calendar entries.</summary>
    public IReadOnlyList<EconomicCalendarReadModel> EconomicCalendars
    {
        get => _economicCalendars;
        private set => SetProperty(ref _economicCalendars, value);
    }

    /// <summary>Gets the display date returned for the selected calendar period.</summary>
    public string CalendarDate
    {
        get => _calendarDate;
        private set => SetProperty(ref _calendarDate, value);
    }

    /// <summary>Gets the selected calendar entry used by the details panel.</summary>
    public EconomicCalendarReadModel? SelectedEconomicCalendar
    {
        get => _selectedEconomicCalendar;
        private set => SetProperty(ref _selectedEconomicCalendar, value);
    }

    /// <summary>Gets the latest listener or query error.</summary>
    public PresentationError? LastError
    {
        get => _lastError;
        private set => SetProperty(ref _lastError, value);
    }

    /// <summary>Gets the single-flight country-code load operation.</summary>
    public IAsyncOperation LoadCountryCodesOperation { get; }

    /// <summary>Gets the single-flight calendar refresh operation.</summary>
    public IAsyncOperation RefreshOperation { get; }

    /// <summary>Selects a calendar period using the current local date.</summary>
    public void SelectCalendarPeriod(string calendarType, DateTime today)
    {
        _today = today.Date;
        _calendarViewType = ParseCalendarViewType(calendarType);
    }

    /// <summary>Selects a country by safe list index.</summary>
    public bool SelectCountryCode(int index)
    {
        if (index < 0 || index >= CountryCodes.Count)
            return false;

        SelectedCountryCode = CountryCodes[index];
        return true;
    }

    /// <summary>Selects a calendar entry by safe list index.</summary>
    public bool SelectEconomicCalendar(int index)
    {
        if (index < 0 || index >= EconomicCalendars.Count)
            return false;

        var selected = EconomicCalendars[index];
        SelectedEconomicCalendar = selected;
        var easternEventDate = EasternTime.FromUtc(selected.EventDate);
        CalendarDate = $"{easternEventDate.DayOfWeek}, {easternEventDate:MMMM} {easternEventDate:dd}, {easternEventDate:yyyy}";
        return true;
    }

    /// <inheritdoc />
    public Task InitializeAsync(CancellationToken cancellationToken)
        => _lifecycle.InitializeAsync(cancellationToken);

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
        => _lifecycle.StopAsync(cancellationToken);

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _lifecycle.DisposeAsync();
        await DisposeOperationAsync(LoadCountryCodesOperation);
        await DisposeOperationAsync(RefreshOperation);
    }

    async Task LoadCountryCodesCoreAsync(CancellationToken cancellationToken)
    {
        try
        {
            EconomicCalendarCountryCodeReadModel[] loaded = [];
            await _marketDataQueryModel.ExecuteObservableAsync(
                async model => await model.LoadEconomicCalendarCountryCodesAsync(
                    values => loaded = values ?? []),
                cancellationToken);

            CountryCodes = loaded.Select(value => value.CountryCode).ToArray();
            SelectedCountryCode = CountryCodes.FirstOrDefault(code => code == "US")
                ?? CountryCodes.FirstOrDefault()
                ?? string.Empty;
        }
        catch (ModelOperationException exception)
        {
            PublishError(exception.ErrorCode, exception.Message, "Economic Calendar Country Codes Error");
            throw;
        }
    }

    async Task RefreshCoreAsync(CancellationToken cancellationToken)
    {
        try
        {
            EconomicCalendarReadModel[] calendars = [];
            var calendarDate = string.Empty;
            await _marketDataQueryModel.ExecuteObservableAsync(
                async model =>
                {
                    await model.LoadEconomicCalendarAsync(
                        _today,
                        _calendarViewType,
                        SelectedCountryCode,
                        values => calendars = values ?? []);
                    await model.LoadEconomicCalendarDateAsync(
                        _today,
                        _calendarViewType,
                        value => calendarDate = value ?? string.Empty);
                },
                cancellationToken);

            EconomicCalendars = calendars;
            CalendarDate = calendarDate;
            SelectedEconomicCalendar = calendars.FirstOrDefault();
        }
        catch (ModelOperationException exception)
        {
            PublishError(exception.ErrorCode, exception.Message, "Economic Calendar Error");
            throw;
        }
    }

    async Task StartListenersCoreAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Interlocked.Exchange(ref _acceptEvents, 1);
        _eventModel.OnError((errorCode, errorMessage) =>
            PublishError(errorCode, errorMessage, "Economic Calendar Listener Error"));
        try
        {
            await _eventModel.ExecuteAsync(
                async model => await model.StartEconomicCalendarEventListenersAsync(
                    _ => QueueRefresh(),
                    failed => PublishError(failed.ErrorCode, failed.ErrorMessage, "Economic Calendar Add Failed"),
                    _ => QueueRefresh(),
                    failed => PublishError(failed.ErrorCode, failed.ErrorMessage, "Economic Calendar Change Failed"),
                    _ => QueueRefresh(),
                    failed => PublishError(failed.ErrorCode, failed.ErrorMessage, "Economic Calendar Remove Failed"),
                    _ => QueueRefresh(),
                    failed => PublishError(
                        failed.ErrorCode,
                        failed.ErrorMessage,
                        "Economic Calendar Import Failed")),
                cancellationToken);
        }
        catch
        {
            Interlocked.Exchange(ref _acceptEvents, 0);
            _eventModel.OnError(null!);
            throw;
        }
    }

    async Task StopListenersCoreAsync(CancellationToken cancellationToken)
    {
        Interlocked.Exchange(ref _acceptEvents, 0);
        try
        {
            await _eventModel.ExecuteAsync(
                async model => await model.StopEconomicCalendarEventListenersAsync(),
                cancellationToken);
        }
        finally
        {
            _eventModel.OnError(null!);
        }
    }

    void QueueRefresh()
    {
        if (Volatile.Read(ref _acceptEvents) == 0)
            return;

        try
        {
            _lifecycle.RunAsync(RefreshFromEventAsync);
        }
        catch (InvalidOperationException) when (Volatile.Read(ref _acceptEvents) == 0)
        {
            // Stop won the race after the event callback observed the running flag.
        }
    }

    async Task RefreshFromEventAsync(CancellationToken cancellationToken)
    {
        try
        {
            await RefreshOperation.ExecuteAsync(cancellationToken);
        }
        catch (ModelOperationException)
        {
            // RefreshOperation already published the coded failure for the view.
        }
    }

    void PublishError(int errorCode, string message, string caption)
        => LastError = new PresentationError(
            Interlocked.Increment(ref _errorSequence),
            errorCode,
            message,
            caption);

    static EconomicCalendarViewType ParseCalendarViewType(string calendarType)
        => calendarType switch
        {
            "Today" => EconomicCalendarViewType.Today,
            "Yesterday" => EconomicCalendarViewType.Yesterday,
            "Tomorrow" => EconomicCalendarViewType.Tomorrow,
            "This Week" => EconomicCalendarViewType.ThisWeek,
            "Next Week" => EconomicCalendarViewType.NextWeek,
            _ => EconomicCalendarViewType.Today
        };

    static async ValueTask DisposeOperationAsync(IAsyncOperation operation)
    {
        try
        {
            await ((IAsyncDisposable)operation).DisposeAsync();
        }
        catch (Exception exception) when (ReferenceEquals(operation.LastFailure, exception))
        {
            // The caller already observed this completed operation failure.
        }
    }
}
