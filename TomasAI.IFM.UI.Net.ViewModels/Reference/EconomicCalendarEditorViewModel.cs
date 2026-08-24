using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Models;
using TomasAI.IFM.UI.Net.Models.Operations;
using TomasAI.IFM.UI.Net.Models.Reference;
using TomasAI.IFM.UI.Net.Services.Operations;
using TomasAI.IFM.UI.Net.Services.Reference;
using TomasAI.IFM.UI.Net.Services.Subscriptions;
using TomasAI.IFM.UI.Net.ViewModels.Lifecycle;
using TomasAI.IFM.UI.Net.ViewModels.Operations;
using TomasAI.IFM.UI.Net.ViewModels.Operations.Domain;

namespace TomasAI.IFM.UI.Net.ViewModels.Reference;

/// <summary>
/// Coordinates economic-calendar maintenance and correlated external import completion.
/// </summary>
public sealed class EconomicCalendarEditorViewModel
    : BaseEditorViewModel, IAsyncLifecycle, IAsyncDisposable
{
    readonly IEconomicCalendarService _service;
    readonly IUiEventSubscription _subscription;
    readonly AsyncLifecycleCoordinator _lifecycle;
    readonly AsyncOperation _importOperation;
    readonly TerminalNotificationCorrelation _terminalCorrelation = new();
    List<EconomicCalendarCountryCodeUiModel> _countryCodes = [];
    List<EconomicCalendarUiModel> _economicCalendars = [];
    DateTime? _pendingImportDate;
    string? _pendingImportCountryCode;
    string _lastStatusMessage = string.Empty;

    /// <summary>Creates the editor with its explicit economic-calendar service.</summary>
    public EconomicCalendarEditorViewModel(IAppRoot appRoot, IEconomicCalendarService service) : base(appRoot)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _subscription = _service.CreateSubscription(HandleTerminalNotification);
        _lifecycle = new AsyncLifecycleCoordinator(StartListenerCoreAsync, StopListenerCoreAsync);
        _importOperation = new AsyncOperation(
            ImportCoreAsync,
            () => _pendingImportDate.HasValue
                && !string.IsNullOrWhiteSpace(_pendingImportCountryCode)
                && _lifecycle.IsRunning);
    }

    /// <summary>Gets economic-calendar country-code selectors.</summary>
    public ICollection<EconomicCalendarCountryCodeUiModel> CountryCodes => _countryCodes;

    /// <summary>Gets economic-calendar entries for the active date and country.</summary>
    public ICollection<EconomicCalendarUiModel> EconomicCalendars => _economicCalendars;

    public Action OnCountryCodesLoaded = () => { };
    public Action OnEconomicCalendarsLoaded = () => { };
    public Action OnWaitCursor = () => { };
    public Action OnDefaultCursor = () => { };

    /// <summary>Gets the command identifier while an import awaits its terminal event.</summary>
    public Guid CommandId
        => _terminalCorrelation.CommandId;

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
    public EconomicCalendarUiModel GetEconomicCalendar(int index)
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
    public Task AddEconomicCalendar(EconomicCalendarUiModel economicCalendar, Action onCompleted)
        => ExecuteMutationAsync(
            cancellationToken => _service.AddAsync(economicCalendar, cancellationToken),
            DateOnly.FromDateTime(EasternTime.FromUtc(economicCalendar.EventDate)),
            economicCalendar.CountryCode,
            $"Economic Calendar {economicCalendar.CountryCode}:{economicCalendar.EventName} Added",
            onCompleted,
            CancellationToken.None);

    /// <summary>Changes an economic-calendar entry.</summary>
    public Task ChangeEconomicCalendar(
        DateTime originalEventDate,
        string originalCountryCode,
        string originalEventName,
        EconomicCalendarUiModel economicCalendar,
        bool overwrite,
        Action onCompleted)
        => ExecuteMutationAsync(
            cancellationToken => _service.ChangeAsync(
                originalEventDate,
                originalCountryCode,
                originalEventName,
                economicCalendar,
                overwrite,
                cancellationToken),
            DateOnly.FromDateTime(EasternTime.FromUtc(economicCalendar.EventDate)),
            economicCalendar.CountryCode,
            $"Economic Calendar {originalCountryCode}:{originalEventName} Changed",
            onCompleted,
            CancellationToken.None);

    /// <summary>Removes an economic-calendar entry.</summary>
    public Task RemoveEconomicCalendar(
        DateTime eventDate,
        string countryCode,
        string eventName,
        bool overwrite)
        => ExecuteMutationAsync(
            cancellationToken => _service.RemoveAsync(
                eventDate, countryCode, eventName, overwrite, cancellationToken),
            DateOnly.FromDateTime(EasternTime.FromUtc(eventDate)),
            countryCode,
            $"Economic Calendar {countryCode}:{eventName} Removed",
            null,
            CancellationToken.None);

    /// <summary>Prepares an economic-calendar import for the guarded operation.</summary>
    public void PrepareImport(DateTime importDate, string countryCode)
    {
        if (string.IsNullOrWhiteSpace(countryCode))
            throw new ArgumentException("An economic-calendar country code is required.", nameof(countryCode));

        _pendingImportDate = importDate;
        _pendingImportCountryCode = countryCode.Trim();
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
        _terminalCorrelation.EndAttempt();
        OnPropertyChanged(nameof(CommandId));
        _importOperation.NotifyCanExecuteChanged();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await StopAsync(CancellationToken.None);
        await _lifecycle.DisposeAsync();
        await _subscription.DisposeAsync();
    }

    Task StartListenerCoreAsync(CancellationToken cancellationToken)
        => _subscription.StartAsync(cancellationToken).AsTask();

    Task StopListenerCoreAsync(CancellationToken cancellationToken)
        => _subscription.StopAsync(cancellationToken).AsTask();

    async Task LoadCountryCodesCoreAsync(CancellationToken cancellationToken)
    {
        _countryCodes = [.. (await _service.GetCountryCodesAsync(cancellationToken)).RequireValue()];
        OnCountryCodesLoaded?.Invoke();
    }

    async Task LoadEconomicCalendarsCoreAsync(
        DateOnly eventDate,
        string countryCode,
        CancellationToken cancellationToken)
    {
        _economicCalendars = [.. (await _service.GetCalendarsAsync(
            eventDate,
            countryCode,
            cancellationToken)).RequireValue().OrderBy(value => value.EventDate)];
        OnEconomicCalendarsLoaded?.Invoke();
    }

    async Task ImportCoreAsync(CancellationToken cancellationToken)
    {
        var importDate = _pendingImportDate
            ?? throw new InvalidOperationException("No economic-calendar import date is prepared.");
        var countryCode = _pendingImportCountryCode
            ?? throw new InvalidOperationException("No economic-calendar country code is prepared.");

        await ExecuteMutationAsync(
            token => _service.ImportAsync(importDate, [countryCode], token),
            DateOnly.FromDateTime(importDate),
            countryCode,
            $"Economic Calendars Imported for {importDate:yyyy-MM-dd} ({countryCode})",
            () =>
            {
                _pendingImportDate = null;
                _pendingImportCountryCode = null;
            },
            cancellationToken);
    }

    async Task ExecuteMutationAsync(
        Func<CancellationToken, ValueTask<UiOperationResult<Guid>>> submit,
        DateOnly eventDate,
        string countryCode,
        string statusMessage,
        Action? onCompleted,
        CancellationToken cancellationToken)
    {
        _terminalCorrelation.BeginAttempt();
        try
        {
            var commandId = (await submit(cancellationToken)).RequireValue();
            if (commandId == Guid.Empty)
                throw new InvalidOperationException(
                    "The economic-calendar command returned an empty correlation identifier.");

            var terminalTask = _terminalCorrelation.AwaitAsync(commandId, cancellationToken);
            OnPropertyChanged(nameof(CommandId));
            var terminalNotification = await terminalTask;
            if (terminalNotification.IsFailure)
                throw new UiOperationException(new UiOperationError(
                    terminalNotification.ErrorCode,
                    terminalNotification.ErrorMessage));

            await LoadEconomicCalendarsCoreAsync(
                eventDate,
                countryCode,
                cancellationToken);
            LastStatusMessage = statusMessage;
            onCompleted?.Invoke();
        }
        finally
        {
            _terminalCorrelation.EndAttempt();
            OnPropertyChanged(nameof(CommandId));
            _importOperation.NotifyCanExecuteChanged();
        }
    }

    void HandleTerminalNotification(TerminalNotificationUiModel notification)
        => _terminalCorrelation.TryPublish(notification);

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
