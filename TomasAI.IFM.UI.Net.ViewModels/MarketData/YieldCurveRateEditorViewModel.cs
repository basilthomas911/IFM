using PropertyChangedEventArgs = System.ComponentModel.PropertyChangedEventArgs;
using TomasAI.IFM.Domain.MarketData.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Models;
using TomasAI.IFM.UI.Net.ViewModels.Extensions;
using TomasAI.IFM.UI.Net.ViewModels.Lifecycle;
using TomasAI.IFM.UI.Net.ViewModels.Operations;

namespace TomasAI.IFM.UI.Net.ViewModels.MarketData;

/// <summary>
/// Coordinates observable yield-curve editor state with correlated market-data command events.
/// </summary>
public sealed class YieldCurveRateEditorViewModel
    : BaseEditorViewModel, IAsyncLifecycle, IAsyncDisposable
{
    readonly AsyncLifecycleCoordinator _lifecycle;
    readonly MarketDataEventModel _eventModel;
    readonly MarketDataCommandModel _commandModel;
    readonly MarketDataQueryModel _queryModel;
    readonly ICollection<IEvent> _consumeEvents;
    readonly TerminalEventCorrelation _terminalCorrelation = new();
    readonly AsyncOperation _loadOperation;
    readonly AsyncOperation _loadRatesOperation;
    readonly AsyncOperation _addOperation;
    readonly AsyncOperation _changeOperation;
    readonly AsyncOperation _removeOperation;
    readonly AsyncOperation _importOperation;
    IReadOnlyList<string> _timePeriods = [];
    IReadOnlyList<YieldCurveRateReadModel> _yieldCurveRates = [];
    YieldCurveRateReadModel? _pendingAdd;
    YieldCurveRateReadModel? _pendingChange;
    YieldCurveRateReadModel? _pendingRemove;
    DateTime? _pendingImportDate;
    string _selectedTimePeriod = string.Empty;
    string _lastStatusMessage = string.Empty;
    DateOnly _rangeStart;
    DateOnly _rangeEnd;

    /// <summary>
    /// Creates the editor and resolves its Models from the application composition root.
    /// </summary>
    public YieldCurveRateEditorViewModel(IAppRoot appRoot) : base(appRoot)
    {
        _eventModel = AppRoot.GetModel<MarketDataEventModel>();
        _commandModel = AppRoot.GetModel<MarketDataCommandModel>();
        _queryModel = AppRoot.GetModel<MarketDataQueryModel>();
        _consumeEvents =
        [
            new YieldCurveRateAddedCompleteEvent().SetEventSource($"{EventTopic.MarketDataEvents}"),
            new YieldCurveRateAddedFailEvent().SetEventSource($"{EventTopic.MarketDataEvents}"),
            new YieldCurveRateChangedCompleteEvent().SetEventSource($"{EventTopic.MarketDataEvents}"),
            new YieldCurveRateChangedFailEvent().SetEventSource($"{EventTopic.MarketDataEvents}"),
            new YieldCurveRateRemovedCompleteEvent().SetEventSource($"{EventTopic.MarketDataEvents}"),
            new YieldCurveRateRemovedFailEvent().SetEventSource($"{EventTopic.MarketDataEvents}"),
            new YieldCurveRatesImportedCompleteEvent().SetEventSource($"{EventTopic.MarketDataEvents}"),
            new YieldCurveRatesImportedFailEvent().SetEventSource($"{EventTopic.MarketDataEvents}")
        ];

        _loadOperation = new AsyncOperation(LoadCoreAsync);
        _loadRatesOperation = new AsyncOperation(
            LoadRatesCoreAsync,
            () => RangeStart != default && RangeEnd >= RangeStart);
        _addOperation = new AsyncOperation(
            AddCoreAsync,
            () => _pendingAdd is not null && _lifecycle.IsRunning && !IsMutationRunning);
        _changeOperation = new AsyncOperation(
            ChangeCoreAsync,
            () => _pendingChange is not null && _lifecycle.IsRunning && !IsMutationRunning);
        _removeOperation = new AsyncOperation(
            RemoveCoreAsync,
            () => _pendingRemove is not null && _lifecycle.IsRunning && !IsMutationRunning);
        _importOperation = new AsyncOperation(
            ImportCoreAsync,
            () => _pendingImportDate is not null && _lifecycle.IsRunning && !IsMutationRunning);
        _addOperation.PropertyChanged += MutationOperationPropertyChanged;
        _changeOperation.PropertyChanged += MutationOperationPropertyChanged;
        _removeOperation.PropertyChanged += MutationOperationPropertyChanged;
        _importOperation.PropertyChanged += MutationOperationPropertyChanged;
        _lifecycle = new AsyncLifecycleCoordinator(StartListenerCoreAsync, StopListenerCoreAsync);
    }

    /// <summary>Gets the available time-period filters.</summary>
    public IReadOnlyList<string> TimePeriods
    {
        get => _timePeriods;
        private set => SetProperty(ref _timePeriods, value);
    }

    /// <summary>Gets the rates in the selected date range.</summary>
    public IReadOnlyList<YieldCurveRateReadModel> YieldCurveRates
    {
        get => _yieldCurveRates;
        private set
        {
            if (SetProperty(ref _yieldCurveRates, value))
                OnPropertyChanged(nameof(CanChangeRemove));
        }
    }

    /// <summary>Gets the selected time-period label.</summary>
    public string SelectedTimePeriod
    {
        get => _selectedTimePeriod;
        private set => SetProperty(ref _selectedTimePeriod, value);
    }

    /// <summary>Gets the inclusive start of the selected rate range.</summary>
    public DateOnly RangeStart
    {
        get => _rangeStart;
        private set => SetProperty(ref _rangeStart, value);
    }

    /// <summary>Gets the inclusive end of the selected rate range.</summary>
    public DateOnly RangeEnd
    {
        get => _rangeEnd;
        private set => SetProperty(ref _rangeEnd, value);
    }

    /// <summary>Gets the correlated command identifier while a mutation awaits its terminal event.</summary>
    public Guid CommandId
        => _terminalCorrelation.CommandId;

    /// <summary>Gets the latest successful mutation status.</summary>
    public string LastStatusMessage
    {
        get => _lastStatusMessage;
        private set => SetProperty(ref _lastStatusMessage, value);
    }

    /// <summary>Gets whether the current rate snapshot supports change and remove actions.</summary>
    public bool CanChangeRemove => YieldCurveRates.Count > 0;

    /// <summary>Gets whether the editor supports importing external rates.</summary>
    public bool CanImport => true;

    /// <summary>Gets the operation that starts the listener and loads the initial editor snapshot.</summary>
    public IAsyncOperation LoadOperation => _loadOperation;

    /// <summary>Gets the operation that reloads the selected date range.</summary>
    public IAsyncOperation LoadRatesOperation => _loadRatesOperation;

    /// <summary>Gets the correlated operation that adds the prepared rate.</summary>
    public IAsyncOperation AddOperation => _addOperation;

    /// <summary>Gets the correlated operation that changes the prepared rate.</summary>
    public IAsyncOperation ChangeOperation => _changeOperation;

    /// <summary>Gets the correlated operation that removes the prepared rate.</summary>
    public IAsyncOperation RemoveOperation => _removeOperation;

    /// <summary>Gets the correlated operation that imports external rates.</summary>
    public IAsyncOperation ImportOperation => _importOperation;

    /// <summary>Selects a time period and calculates its inclusive date range.</summary>
    public void SelectTimePeriod(int index, DateOnly currentDate)
    {
        if (index < 0 || index >= TimePeriods.Count)
            throw new ArgumentOutOfRangeException(nameof(index));
        SelectedTimePeriod = TimePeriods[index];
        SetRange(SelectedTimePeriod, currentDate);
        _loadRatesOperation.NotifyCanExecuteChanged();
    }

    /// <summary>Prepares a rate for the next add operation.</summary>
    public void PrepareAdd(YieldCurveRateReadModel rate)
    {
        _pendingAdd = rate ?? throw new ArgumentNullException(nameof(rate));
        PrepareMutation();
        _addOperation.NotifyCanExecuteChanged();
    }

    /// <summary>Prepares a rate for the next change operation.</summary>
    public void PrepareChange(YieldCurveRateReadModel rate)
    {
        _pendingChange = rate ?? throw new ArgumentNullException(nameof(rate));
        PrepareMutation();
        _changeOperation.NotifyCanExecuteChanged();
    }

    /// <summary>Prepares a rate for the next remove operation.</summary>
    public void PrepareRemove(YieldCurveRateReadModel rate)
    {
        _pendingRemove = rate ?? throw new ArgumentNullException(nameof(rate));
        PrepareMutation();
        _removeOperation.NotifyCanExecuteChanged();
    }

    /// <summary>Prepares an import date for the next external-rate import operation.</summary>
    public void PrepareImport(DateTime importDate)
    {
        _pendingImportDate = importDate;
        PrepareMutation();
        _importOperation.NotifyCanExecuteChanged();
    }

    /// <summary>Gets a yield-curve rate by presentation index, or <see langword="null"/> for an invalid index.</summary>
    public YieldCurveRateReadModel? GetYieldCurveRate(int index)
        => index >= 0 && index < YieldCurveRates.Count ? YieldCurveRates[index] : null;

    /// <summary>Starts the market-data terminal-event listener once.</summary>
    public Task StartListener() => InitializeAsync(CancellationToken.None);

    /// <summary>Stops the listener and cancels all owned operations.</summary>
    public Task StopListener() => StopAsync(CancellationToken.None);

    /// <inheritdoc />
    public Task InitializeAsync(CancellationToken cancellationToken)
        => _lifecycle.InitializeAsync(cancellationToken);

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        CancelOperations();
        await AwaitOperationsStoppedAsync();
        await _lifecycle.StopAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await StopAsync(CancellationToken.None);
        _addOperation.PropertyChanged -= MutationOperationPropertyChanged;
        _changeOperation.PropertyChanged -= MutationOperationPropertyChanged;
        _removeOperation.PropertyChanged -= MutationOperationPropertyChanged;
        _importOperation.PropertyChanged -= MutationOperationPropertyChanged;
        await _lifecycle.DisposeAsync();
    }

    Task StartListenerCoreAsync(CancellationToken cancellationToken)
        => _eventModel.ExecuteObservableAsync(
            model => model.StartMarketDataListenerAsync(_consumeEvents, HandleEventAsync).AsTask(),
            cancellationToken);

    Task StopListenerCoreAsync(CancellationToken cancellationToken)
        => _eventModel.ExecuteObservableAsync(
            model => model.StopMarketDataListenerAsync().AsTask(),
            cancellationToken);

    async Task LoadCoreAsync(CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken);
        var timePeriods = await QueryTimePeriodsAsync(cancellationToken);
        var selectedTimePeriod = timePeriods.FirstOrDefault() ?? string.Empty;
        var currentDate = DateOnly.FromDateTime(DateTime.Today);
        var (start, end) = CalculateRange(selectedTimePeriod, currentDate);
        var rates = start == default
            ? []
            : await QueryRatesAsync(start, end, cancellationToken);

        TimePeriods = timePeriods;
        SelectedTimePeriod = selectedTimePeriod;
        RangeStart = start;
        RangeEnd = end;
        YieldCurveRates = rates;
        _loadRatesOperation.NotifyCanExecuteChanged();
        NotifyMutationCanExecuteChanged();
    }

    async Task LoadRatesCoreAsync(CancellationToken cancellationToken)
        => YieldCurveRates = await QueryRatesAsync(RangeStart, RangeEnd, cancellationToken);

    async Task<IReadOnlyList<string>> QueryTimePeriodsAsync(CancellationToken cancellationToken)
    {
        string[] result = [];
        await _queryModel.ExecuteObservableAsync(
            model => model.GetYieldCurveRateTimePeriodsAsync(loaded => result = loaded ?? []),
            cancellationToken);
        return result;
    }

    async Task<IReadOnlyList<YieldCurveRateReadModel>> QueryRatesAsync(
        DateOnly start,
        DateOnly end,
        CancellationToken cancellationToken)
    {
        YieldCurveRateReadModel[] result = [];
        await _queryModel.ExecuteObservableAsync(
            model => model.GetYieldCurveRatesAsync(start, end, loaded => result = loaded ?? []),
            cancellationToken);
        return result;
    }

    Task AddCoreAsync(CancellationToken cancellationToken)
    {
        var rate = _pendingAdd ?? throw new InvalidOperationException("No yield-curve rate is prepared for add.");
        return ExecuteMutationAsync(
            model => model.AddYieldCurveRateAsync(rate, false),
            $"Yield Curve Rate {rate.ValueDate:yyyy-MM-dd} Added",
            () => _pendingAdd = null,
            cancellationToken);
    }

    Task ChangeCoreAsync(CancellationToken cancellationToken)
    {
        var rate = _pendingChange ?? throw new InvalidOperationException("No yield-curve rate is prepared for change.");
        return ExecuteMutationAsync(
            model => model.ChangeYieldCurveRateAsync(rate, true),
            $"Yield Curve Rate {rate.ValueDate:yyyy-MM-dd} Changed",
            () => _pendingChange = null,
            cancellationToken);
    }

    Task RemoveCoreAsync(CancellationToken cancellationToken)
    {
        var rate = _pendingRemove ?? throw new InvalidOperationException("No yield-curve rate is prepared for removal.");
        return ExecuteMutationAsync(
            model => model.RemoveYieldCurveRateAsync(rate.ValueDate, true),
            $"Yield Curve Rate {rate.ValueDate:yyyy-MM-dd} Removed",
            () => _pendingRemove = null,
            cancellationToken);
    }

    async Task ImportCoreAsync(CancellationToken cancellationToken)
    {
        var importDate = _pendingImportDate
            ?? throw new InvalidOperationException("No yield-curve import date is prepared.");
        await ExecuteMutationAsync(
            model => model.ImportYieldCurveRatesAsync(importDate),
            $"Yield Curve Rates Imported for {importDate:yyyy-MM-dd}",
            () => _pendingImportDate = null,
            cancellationToken);
    }

    async Task ExecuteMutationAsync(
        Func<MarketDataCommandModel, Task<Guid>> submit,
        string statusMessage,
        Action clearPending,
        CancellationToken cancellationToken)
    {
        _terminalCorrelation.BeginAttempt();
        try
        {
            Guid commandId = Guid.Empty;
            await _commandModel.ExecuteObservableAsync(
                async model => commandId = await submit(model),
                cancellationToken);
            if (commandId == Guid.Empty)
                throw new InvalidOperationException("The market-data command returned an empty correlation identifier.");

            var terminalTask = _terminalCorrelation.AwaitAsync(commandId, cancellationToken);
            OnPropertyChanged(nameof(CommandId));
            var terminalEvent = await terminalTask;
            if (terminalEvent is IErrorEvent error)
                throw new ModelOperationException(error.ErrorCode, error.ErrorMessage);

            await RefreshSnapshotAsync(cancellationToken);
            LastStatusMessage = statusMessage;
            clearPending();
        }
        finally
        {
            _terminalCorrelation.EndAttempt();
            OnPropertyChanged(nameof(CommandId));
        }
    }

    async Task RefreshSnapshotAsync(CancellationToken cancellationToken)
    {
        var timePeriods = await QueryTimePeriodsAsync(cancellationToken);
        var selected = timePeriods.Contains(SelectedTimePeriod, StringComparer.Ordinal)
            ? SelectedTimePeriod
            : timePeriods.FirstOrDefault() ?? string.Empty;
        var (start, end) = CalculateRange(selected, DateOnly.FromDateTime(DateTime.Today));
        var rates = start == default
            ? []
            : await QueryRatesAsync(start, end, cancellationToken);
        TimePeriods = timePeriods;
        SelectedTimePeriod = selected;
        RangeStart = start;
        RangeEnd = end;
        YieldCurveRates = rates;
        _loadRatesOperation.NotifyCanExecuteChanged();
    }

    ValueTask HandleEventAsync(IEvent @event)
    {
        _terminalCorrelation.TryPublish(@event);
        return ValueTask.CompletedTask;
    }

    void SetRange(string timePeriod, DateOnly currentDate)
    {
        var (start, end) = CalculateRange(timePeriod, currentDate);
        RangeStart = start;
        RangeEnd = end;
    }

    static (DateOnly Start, DateOnly End) CalculateRange(string timePeriod, DateOnly currentDate)
    {
        if (string.IsNullOrWhiteSpace(timePeriod))
            return default;
        if (timePeriod == "Current Month")
        {
            var start = new DateOnly(currentDate.Year, currentDate.Month, 1);
            return (start, start.AddMonths(1).AddDays(-1));
        }
        if (!int.TryParse(timePeriod, out var year) || year < 1 || year > 9999)
            throw new InvalidOperationException($"Invalid yield-curve time period '{timePeriod}'.");
        var yearStart = new DateOnly(year, 1, 1);
        return (yearStart, yearStart.AddYears(1).AddDays(-1));
    }

    bool IsMutationRunning =>
        _addOperation.IsRunning || _changeOperation.IsRunning ||
        _removeOperation.IsRunning || _importOperation.IsRunning;

    void PrepareMutation()
    {
        LastStatusMessage = string.Empty;
    }

    void MutationOperationPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(IAsyncOperation.IsRunning))
            NotifyMutationCanExecuteChanged();
    }

    void NotifyMutationCanExecuteChanged()
    {
        _addOperation.NotifyCanExecuteChanged();
        _changeOperation.NotifyCanExecuteChanged();
        _removeOperation.NotifyCanExecuteChanged();
        _importOperation.NotifyCanExecuteChanged();
    }

    void CancelOperations()
    {
        _loadOperation.Cancel();
        _loadRatesOperation.Cancel();
        _addOperation.Cancel();
        _changeOperation.Cancel();
        _removeOperation.Cancel();
        _importOperation.Cancel();
    }

    async Task AwaitOperationsStoppedAsync()
    {
        await AwaitOperationStoppedAsync(_loadOperation);
        await AwaitOperationStoppedAsync(_loadRatesOperation);
        await AwaitOperationStoppedAsync(_addOperation);
        await AwaitOperationStoppedAsync(_changeOperation);
        await AwaitOperationStoppedAsync(_removeOperation);
        await AwaitOperationStoppedAsync(_importOperation);
    }

    static async Task AwaitOperationStoppedAsync(AsyncOperation operation)
    {
        try
        {
            await operation.DisposeAsync();
        }
        catch (Exception) when (operation.LastFailure is not null)
        {
            // The operation's caller observes LastFailure; shutdown only awaits ownership cleanup.
        }
    }
}
