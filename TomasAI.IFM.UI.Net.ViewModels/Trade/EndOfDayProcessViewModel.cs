using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Models;
using TomasAI.IFM.UI.Net.ViewModels.Lifecycle;
using TomasAI.IFM.UI.Net.ViewModels.Extensions;
using TomasAI.IFM.UI.Net.ViewModels.Operations;
using TomasAI.IFM.UI.Net.ViewModels.Presentation;

namespace TomasAI.IFM.UI.Net.ViewModels.Trade;

/// <summary>Represents one coherent end-of-day calculation snapshot.</summary>
/// <param name="OpenPrice">The session opening price.</param>
/// <param name="HighPrice">The session high price.</param>
/// <param name="LowPrice">The session low price.</param>
/// <param name="ClosePrice">The session closing price.</param>
/// <param name="Volume">The session volume.</param>
/// <param name="TradePnl">The position's realized and unrealized profit or loss.</param>
/// <param name="FundBalance">The owning Fund's current balance.</param>
public sealed record EndOfDayProcessSnapshot(
    decimal OpenPrice,
    decimal HighPrice,
    decimal LowPrice,
    decimal ClosePrice,
    long Volume,
    decimal TradePnl,
    decimal FundBalance);

/// <summary>Loads a strategy position and moves it through its actor-owned end-of-day transition.</summary>
public sealed class EndOfDayProcessViewModel : ObservableObject, IAsyncLifecycle, IAsyncDisposable
{
    readonly TradeEndOfDayParameter _parameter;
    readonly AsyncOperation _loadOperation;
    readonly AsyncOperation _runOperation;
    readonly IAppRoot _appRoot;
    EndOfDayProcessSnapshot? _snapshot;
    PresentationError? _lastError;
    StrategyPositionSnapshot? _position;
    DateOnly _valueDate;
    string _reference = string.Empty;
    string _lastStatusMessage = string.Empty;
    Guid _commandId;
    bool _isCompleted;
    bool _isRunning;
    long _errorSequence;

    /// <summary>Creates the end-of-day screen state for one canonical strategy position.</summary>
    /// <param name="appRoot">The application service boundary.</param>
    /// <param name="parameter">The trade and position identity displayed by the screen.</param>
    public EndOfDayProcessViewModel(IAppRoot appRoot, TradeEndOfDayParameter parameter)
    {
        _appRoot = appRoot ?? throw new ArgumentNullException(nameof(appRoot));
        _parameter = parameter ?? throw new ArgumentNullException(nameof(parameter));
        _valueDate = parameter.ValueDate;
        _loadOperation = new AsyncOperation(LoadCoreAsync, () => !_runOperation.IsRunning);
        _runOperation = new AsyncOperation(RunCoreAsync,
            () => _isRunning && Snapshot is not null && !IsCompleted && !_loadOperation.IsRunning && CommandId == Guid.Empty);
        _loadOperation.PropertyChanged += OperationPropertyChanged;
        _runOperation.PropertyChanged += OperationPropertyChanged;
    }

    /// <summary>Gets the Portfolio that owns the position.</summary>
    public int PortfolioId => _parameter.PortfolioId;
    /// <summary>Gets the Fund that owns the position.</summary>
    public int FundId => _parameter.FundId;
    /// <summary>Gets the Trade Order identifier.</summary>
    public int OrderId => _parameter.OrderId;
    /// <summary>Gets the established Trade identifier.</summary>
    public int TradeId => _parameter.TradeId;
    /// <summary>Gets the legacy trade-type label displayed by the form.</summary>
    public TradeType TradeType => _parameter.TradeType;
    /// <summary>Gets the strategy actor that owns the position.</summary>
    public TradeStrategyKind StrategyKind => _parameter.ResolveStrategyKind();
    /// <summary>Gets the complete strategy-position identity.</summary>
    public StrategyPositionId PositionId => _parameter.ResolvePositionId();

    /// <summary>Gets or changes the selected valuation date.</summary>
    public DateOnly ValueDate { get => _valueDate; private set => SetProperty(ref _valueDate, value); }
    /// <summary>Gets the operator reference recorded in the screen status.</summary>
    public string Reference { get => _reference; private set => SetProperty(ref _reference, value); }

    /// <summary>Gets the currently displayed EOD inputs and position valuation.</summary>
    public EndOfDayProcessSnapshot? Snapshot
    {
        get => _snapshot;
        private set
        {
            if (!SetProperty(ref _snapshot, value)) return;
            OnPropertyChanged(nameof(OpenPrice)); OnPropertyChanged(nameof(HighPrice));
            OnPropertyChanged(nameof(LowPrice)); OnPropertyChanged(nameof(ClosePrice));
            OnPropertyChanged(nameof(Volume)); OnPropertyChanged(nameof(TradePnl));
            OnPropertyChanged(nameof(FundBalance)); NotifyRunState();
        }
    }

    /// <summary>Gets the EOD market open.</summary>
    public decimal OpenPrice => Snapshot?.OpenPrice ?? 0m;
    /// <summary>Gets the EOD market high.</summary>
    public decimal HighPrice => Snapshot?.HighPrice ?? 0m;
    /// <summary>Gets the EOD market low.</summary>
    public decimal LowPrice => Snapshot?.LowPrice ?? 0m;
    /// <summary>Gets the EOD market close.</summary>
    public decimal ClosePrice => Snapshot?.ClosePrice ?? 0m;
    /// <summary>Gets the EOD market volume.</summary>
    public long Volume => Snapshot?.Volume ?? 0;
    /// <summary>Gets the current strategy-position P&amp;L.</summary>
    public decimal TradePnl => Snapshot?.TradePnl ?? 0m;
    /// <summary>Gets the displayed Fund balance including current position P&amp;L.</summary>
    public decimal FundBalance => Snapshot?.FundBalance ?? 0m;
    /// <summary>Gets the active command identifier, or an empty value when idle.</summary>
    public Guid CommandId => _commandId;
    /// <summary>Gets whether the actor accepted the EOD transition.</summary>
    public bool IsCompleted { get => _isCompleted; private set { if (SetProperty(ref _isCompleted, value)) NotifyRunState(); } }
    /// <summary>Gets the latest presentation error.</summary>
    public PresentationError? LastError { get => _lastError; private set => SetProperty(ref _lastError, value); }
    /// <summary>Gets the latest user-facing operation status.</summary>
    public string LastStatusMessage { get => _lastStatusMessage; private set => SetProperty(ref _lastStatusMessage, value); }
    /// <summary>Gets whether the screen is loading or submitting data.</summary>
    public bool IsBusy => _loadOperation.IsRunning || _runOperation.IsRunning;
    /// <summary>Gets whether the EOD command can run.</summary>
    public bool CanRun => _runOperation.CanExecute;
    /// <summary>Gets the load operation exposed to the form.</summary>
    public IAsyncOperation LoadOperation => _loadOperation;
    /// <summary>Gets the run operation exposed to the form.</summary>
    public IAsyncOperation RunOperation => _runOperation;

    /// <summary>Changes the valuation date and invalidates the previously loaded inputs.</summary>
    public void SetValueDate(DateOnly valueDate)
    {
        if (ValueDate == valueDate) return;
        ValueDate = valueDate; Snapshot = null; IsCompleted = false;
    }

    /// <summary>Sets the operator reference displayed for the request.</summary>
    public void SetReference(string? reference) => Reference = reference?.Trim() ?? string.Empty;

    /// <summary>Maps an Iron Condor to its put-spread classification for existing display code.</summary>
    public TradeType PutSpreadType(TradeType tradeType) => tradeType switch
    {
        TradeType.ShortIronCondor => TradeType.PutCreditSpread,
        TradeType.LongIronCondor => TradeType.PutDebitSpread,
        _ => throw new NotSupportedException($"End-of-day put-spread mapping is not defined for {tradeType}.")
    };

    /// <summary>Maps an Iron Condor to its call-spread classification for existing display code.</summary>
    public TradeType CallSpreadType(TradeType tradeType) => tradeType switch
    {
        TradeType.ShortIronCondor => TradeType.CallCreditSpread,
        TradeType.LongIronCondor => TradeType.CallDebitSpread,
        _ => throw new NotSupportedException($"End-of-day call-spread mapping is not defined for {tradeType}.")
    };

    /// <summary>Loads current market, position, and Fund inputs.</summary>
    public Task LoadData() => _loadOperation.ExecuteAsync();
    /// <summary>Submits the strategy-position EOD command.</summary>
    public Task RunEndOfDayProcess() => _runOperation.ExecuteAsync();
    /// <summary>Starts the screen lifecycle.</summary>
    public Task StartListener() => InitializeAsync(CancellationToken.None);
    /// <summary>Stops the screen lifecycle.</summary>
    public Task StopListener() => StopAsync(CancellationToken.None);

    /// <inheritdoc />
    public Task InitializeAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _isRunning = true; NotifyRunState();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _isRunning = false; _commandId = Guid.Empty; NotifyRunState();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        _loadOperation.PropertyChanged -= OperationPropertyChanged;
        _runOperation.PropertyChanged -= OperationPropertyChanged;
        await StopAsync(CancellationToken.None);
        await DisposeOperationAsync(_loadOperation); await DisposeOperationAsync(_runOperation);
    }

    async Task LoadCoreAsync(CancellationToken cancellationToken)
    {
        try
        {
            FundReadModel[] funds = [];
            FuturesEodDataV2ReadModel? marketData = null;
            _position = await _appRoot.Services.StrategyPositions
                .GetCurrentAsync(PositionId, StrategyKind, cancellationToken).ConfigureAwait(false);
            await _appRoot.Services.FundQueries.ExecuteObservableAsync(
                model => model.GetFundsAsync(value => funds = value), cancellationToken);
            await _appRoot.Services.FeedQueries.ExecuteObservableAsync(
                model => model.GetFuturesEodDataAsync(_parameter.BaseContractId, ValueDate,
                    value => marketData = value), cancellationToken);
            var fund = funds.SingleOrDefault(value => value.FundId == FundId)
                ?? throw new InvalidOperationException($"Fund {FundId} was not found.");
            var eod = marketData ?? throw new InvalidOperationException(
                $"Futures EOD data for {_parameter.BaseContractId} on {ValueDate:yyyy-MM-dd} was not found.");
            var pnl = _position.RealizedPnl + _position.UnrealizedPnl;
            Snapshot = new(eod.OpenPrice, eod.HighPrice, eod.LowPrice, eod.ClosePrice,
                eod.Volume, pnl, fund.Balance + pnl);
            IsCompleted = _position.Phase == StrategyPositionPhase.EndOfDay;
            LastStatusMessage = $"End-of-day inputs loaded for {PositionId.Format()} on {ValueDate:yyyy-MM-dd}.";
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            PublishError(exception, "Loading End Of Day Data Error"); throw;
        }
    }

    async Task RunCoreAsync(CancellationToken cancellationToken)
    {
        _ = Snapshot ?? throw new InvalidOperationException("End-of-day inputs must be loaded before processing.");
        try
        {
            _commandId = await _appRoot.Services.StrategyPositions.EndOfDayAsync(
                PositionId, StrategyKind, EndOfDayEffectiveUtc(ValueDate), cancellationToken).ConfigureAwait(false);
            OnPropertyChanged(nameof(CommandId));
            _position = await _appRoot.Services.StrategyPositions
                .GetCurrentAsync(PositionId, StrategyKind, cancellationToken).ConfigureAwait(false);
            if (_position.Phase != StrategyPositionPhase.EndOfDay)
                throw new InvalidOperationException("The position command completed without an end-of-day position snapshot.");
            IsCompleted = true;
            LastStatusMessage = $"End-of-day processing completed for {PositionId.Format()} on {ValueDate:yyyy-MM-dd}.";
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            PublishError(exception, "End Of Day Process Failed"); throw;
        }
        finally
        {
            _commandId = Guid.Empty; OnPropertyChanged(nameof(CommandId)); NotifyRunState();
        }
    }

    static DateTime EndOfDayEffectiveUtc(DateOnly valueDate) =>
        DateTime.SpecifyKind(valueDate.ToDateTime(TimeOnly.MaxValue), DateTimeKind.Utc);

    void NotifyRunState()
    {
        OnPropertyChanged(nameof(CanRun)); _runOperation.NotifyCanExecuteChanged();
    }

    void OperationPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName != nameof(IAsyncOperation.IsRunning)) return;
        OnPropertyChanged(nameof(IsBusy)); NotifyRunState();
        if (ReferenceEquals(sender, _runOperation)) _loadOperation.NotifyCanExecuteChanged();
    }

    void PublishError(Exception exception, string caption)
    {
        var code = exception is UiServiceOperationException failure ? failure.ErrorCode : 0;
        LastError = new PresentationError(Interlocked.Increment(ref _errorSequence), code, exception.Message, caption);
    }

    static async ValueTask DisposeOperationAsync(AsyncOperation operation)
    {
        try { await operation.DisposeAsync(); }
        catch (Exception exception) when (ReferenceEquals(operation.LastFailure, exception)) { }
    }
}
