using TomasAI.IFM.Domain.Fund.Shared.Events;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Events;
using TomasAI.IFM.Domain.Trade.Shared.Extensions;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Models;
using TomasAI.IFM.UI.Net.ViewModels.Extensions;
using TomasAI.IFM.UI.Net.ViewModels.Lifecycle;
using TomasAI.IFM.UI.Net.ViewModels.Operations;
using TomasAI.IFM.UI.Net.ViewModels.Presentation;

namespace TomasAI.IFM.UI.Net.ViewModels.Trade;

/// <summary>Represents one coherent end-of-day calculation snapshot.</summary>
public sealed record EndOfDayProcessSnapshot(
    decimal OpenPrice,
    decimal HighPrice,
    decimal LowPrice,
    decimal ClosePrice,
    long Volume,
    decimal TradePnl,
    decimal FundBalance);

/// <summary>
/// Loads end-of-day inputs and correlates one processing command with its terminal NATS event.
/// </summary>
public sealed class EndOfDayProcessViewModel : ObservableObject, IAsyncLifecycle, IAsyncDisposable
{
    readonly AsyncLifecycleCoordinator _lifecycle;
    readonly TradeEndOfDayParameter _parameter;
    readonly EndOfDayProcessEventModel _eventModel;
    readonly FundQueryModel _fundQueryModel;
    readonly TradeQueryModel _tradeQueryModel;
    readonly MarketDataFeedQueryModel _marketDataFeedQueryModel;
    readonly TradeCommandModel _tradeCommandModel;
    readonly object _correlationGate = new();
    readonly Dictionary<Guid, IEvent> _earlyTerminalEvents = [];
    readonly AsyncOperation _loadOperation;
    readonly AsyncOperation _runOperation;
    TaskCompletionSource<IEvent>? _terminalCompletion;
    Guid _commandId;
    DateOnly _valueDate;
    string _reference = string.Empty;
    EndOfDayProcessSnapshot? _snapshot;
    PresentationError? _lastError;
    string _lastStatusMessage = string.Empty;
    bool _isCompleted;
    long _errorSequence;

    /// <summary>Creates an end-of-day workflow for the supplied fund-order trade.</summary>
    public EndOfDayProcessViewModel(IAppRoot appRoot, TradeEndOfDayParameter parameter)
    {
        ArgumentNullException.ThrowIfNull(appRoot);
        ArgumentNullException.ThrowIfNull(parameter);
        _parameter = parameter;
        _valueDate = parameter.ValueDate;
        _eventModel = appRoot.GetModel<EndOfDayProcessEventModel>();
        _fundQueryModel = appRoot.GetModel<FundQueryModel>();
        _tradeQueryModel = appRoot.GetModel<TradeQueryModel>();
        _marketDataFeedQueryModel = appRoot.GetModel<MarketDataFeedQueryModel>();
        _tradeCommandModel = appRoot.GetModel<TradeCommandModel>();
        _loadOperation = new AsyncOperation(LoadCoreAsync, () => !_runOperation.IsRunning);
        _runOperation = new AsyncOperation(
            RunCoreAsync,
            () => _lifecycle.IsRunning
                && Snapshot is not null
                && !IsCompleted
                && !_loadOperation.IsRunning
                && CommandId == Guid.Empty);
        _loadOperation.PropertyChanged += OperationPropertyChanged;
        _runOperation.PropertyChanged += OperationPropertyChanged;
        _lifecycle = new AsyncLifecycleCoordinator(StartListenerCoreAsync, StopListenerCoreAsync);
    }

    public int FundId => _parameter.FundId;
    public int OrderId => _parameter.OrderId;
    public int TradeId => _parameter.TradeId;
    public TradeType TradeType => _parameter.TradeType;
    public DateOnly ValueDate
    {
        get => _valueDate;
        private set => SetProperty(ref _valueDate, value);
    }
    public string Reference
    {
        get => _reference;
        private set => SetProperty(ref _reference, value);
    }
    public EndOfDayProcessSnapshot? Snapshot
    {
        get => _snapshot;
        private set
        {
            if (!SetProperty(ref _snapshot, value))
                return;
            OnPropertyChanged(nameof(OpenPrice));
            OnPropertyChanged(nameof(HighPrice));
            OnPropertyChanged(nameof(LowPrice));
            OnPropertyChanged(nameof(ClosePrice));
            OnPropertyChanged(nameof(Volume));
            OnPropertyChanged(nameof(TradePnl));
            OnPropertyChanged(nameof(FundBalance));
            OnPropertyChanged(nameof(CanRun));
            _runOperation.NotifyCanExecuteChanged();
        }
    }
    public decimal OpenPrice => Snapshot?.OpenPrice ?? 0m;
    public decimal HighPrice => Snapshot?.HighPrice ?? 0m;
    public decimal LowPrice => Snapshot?.LowPrice ?? 0m;
    public decimal ClosePrice => Snapshot?.ClosePrice ?? 0m;
    public long Volume => Snapshot?.Volume ?? 0;
    public decimal TradePnl => Snapshot?.TradePnl ?? 0m;
    public decimal FundBalance => Snapshot?.FundBalance ?? 0m;
    public Guid CommandId
    {
        get
        {
            lock (_correlationGate)
                return _commandId;
        }
    }
    public bool IsCompleted
    {
        get => _isCompleted;
        private set
        {
            if (!SetProperty(ref _isCompleted, value))
                return;
            OnPropertyChanged(nameof(CanRun));
            _runOperation.NotifyCanExecuteChanged();
        }
    }
    public PresentationError? LastError
    {
        get => _lastError;
        private set => SetProperty(ref _lastError, value);
    }
    public string LastStatusMessage
    {
        get => _lastStatusMessage;
        private set => SetProperty(ref _lastStatusMessage, value);
    }
    public bool IsBusy => _loadOperation.IsRunning || _runOperation.IsRunning;
    public bool CanRun => _runOperation.CanExecute;
    public IAsyncOperation LoadOperation => _loadOperation;
    public IAsyncOperation RunOperation => _runOperation;

    /// <summary>Changes the valuation date and invalidates the previous snapshot.</summary>
    public void SetValueDate(DateOnly valueDate)
    {
        if (ValueDate == valueDate)
            return;
        ValueDate = valueDate;
        Snapshot = null;
        IsCompleted = false;
    }

    /// <summary>Sets the operator reference submitted with the process.</summary>
    public void SetReference(string? reference) => Reference = reference?.Trim() ?? string.Empty;

    public TradeType PutSpreadType(TradeType tradeType) => tradeType switch
    {
        TradeType.ShortIronCondor => TradeType.PutCreditSpread,
        TradeType.LongIronCondor => TradeType.PutDebitSpread,
        _ => throw new NotSupportedException($"End-of-day put-spread mapping is not defined for {tradeType}.")
    };

    public TradeType CallSpreadType(TradeType tradeType) => tradeType switch
    {
        TradeType.ShortIronCondor => TradeType.CallCreditSpread,
        TradeType.LongIronCondor => TradeType.CallDebitSpread,
        _ => throw new NotSupportedException($"End-of-day call-spread mapping is not defined for {tradeType}.")
    };

    public Task LoadData() => _loadOperation.ExecuteAsync();
    public Task RunEndOfDayProcess() => _runOperation.ExecuteAsync();
    public Task StartListener() => InitializeAsync(CancellationToken.None);
    public Task StopListener() => StopAsync(CancellationToken.None);
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await _lifecycle.InitializeAsync(cancellationToken);
        OnPropertyChanged(nameof(CanRun));
        _runOperation.NotifyCanExecuteChanged();
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _lifecycle.StopAsync(cancellationToken);
        OnPropertyChanged(nameof(CanRun));
        _runOperation.NotifyCanExecuteChanged();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        _loadOperation.PropertyChanged -= OperationPropertyChanged;
        _runOperation.PropertyChanged -= OperationPropertyChanged;
        await _lifecycle.DisposeAsync();
        await DisposeOperationAsync(_loadOperation);
        await DisposeOperationAsync(_runOperation);
    }

    Task StartListenerCoreAsync(CancellationToken cancellationToken)
        => _eventModel.ExecuteObservableAsync(
            async model => await model.StartEndOfDayProcessListenerAsync(HandleEventAsync),
            cancellationToken);

    async Task StopListenerCoreAsync(CancellationToken cancellationToken)
    {
        CancelCorrelation();
        await _eventModel.ExecuteObservableAsync(
            async model => await model.StopEndOfDayProcessListenerAsync(),
            cancellationToken);
    }

    async Task LoadCoreAsync(CancellationToken cancellationToken)
    {
        try
        {
            FundReadModel[] funds = [];
            OptionTradeReadModel? optionTrade = null;
            FuturesEodDataV2ReadModel? futuresEodData = null;
            await _fundQueryModel.ExecuteObservableAsync(
                async model => await model.GetFundsAsync(value => funds = value),
                cancellationToken);
            await _tradeQueryModel.ExecuteObservableAsync(
                async model => await model.GetOptionTradeAsync(OrderId, TradeId, value => optionTrade = value),
                cancellationToken);
            await _marketDataFeedQueryModel.ExecuteObservableAsync(
                async model => await model.GetFuturesEodDataAsync(_parameter.BaseContractId, ValueDate, value => futuresEodData = value),
                cancellationToken);

            var fund = funds.SingleOrDefault(value => value.FundId == FundId)
                ?? throw new InvalidOperationException($"Fund {FundId} was not found.");
            var trade = optionTrade
                ?? throw new InvalidOperationException($"Option trade {OrderId}:{TradeId} was not found.");
            var marketData = futuresEodData
                ?? throw new InvalidOperationException($"Futures EOD data for {_parameter.BaseContractId} on {ValueDate:yyyy-MM-dd} was not found.");
            var daysToExpiry = trade.MaturityDate.DayNumber - ValueDate.DayNumber;
            var putKey = new TradePositionEntityId(OrderId, TradeId, ValueDate, PutSpreadType(trade.TradeType), TradeStatus.IntraDay, daysToExpiry);
            var callKey = new TradePositionEntityId(OrderId, TradeId, ValueDate, CallSpreadType(trade.TradeType), TradeStatus.IntraDay, daysToExpiry);
            var tradePnl = (trade.TradePositions?.Get(putKey)?.TradePnl ?? 0m)
                + (trade.TradePositions?.Get(callKey)?.TradePnl ?? 0m);
            Snapshot = new EndOfDayProcessSnapshot(
                marketData.OpenPrice,
                marketData.HighPrice,
                marketData.LowPrice,
                marketData.ClosePrice,
                marketData.Volume,
                tradePnl,
                fund.Balance + tradePnl);
            IsCompleted = false;
            LastStatusMessage = $"End-of-day inputs loaded for {OrderId}:{TradeId} on {ValueDate:yyyy-MM-dd}.";
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            PublishError(exception, "Loading End Of Day Data Error");
            throw;
        }
    }

    async Task RunCoreAsync(CancellationToken cancellationToken)
    {
        var snapshot = Snapshot ?? throw new InvalidOperationException("End-of-day inputs must be loaded before processing.");
        try
        {
            Guid commandId = Guid.Empty;
            await _tradeCommandModel.ExecuteObservableAsync(
                async model => commandId = await model.ProcessEndOfDayAsync(
                    FundId,
                    OrderId,
                    TradeId,
                    TradeType,
                    ValueDate,
                    TradeStatus.EndOfDay,
                    snapshot.OpenPrice,
                    snapshot.HighPrice,
                    snapshot.LowPrice,
                    snapshot.ClosePrice,
                    snapshot.Volume,
                    Reference),
                cancellationToken);
            if (commandId == Guid.Empty)
                throw new InvalidOperationException("The end-of-day command returned an empty correlation identifier.");
            var terminalEvent = await AwaitTerminalEventAsync(commandId, cancellationToken);
            if (terminalEvent is IErrorEvent error)
                throw new ModelOperationException(error.ErrorCode, error.ErrorMessage);
            if (terminalEvent is not EndOfDayFundTransactionProcessedCompleteEvent)
                throw new InvalidOperationException($"Unexpected end-of-day event {terminalEvent.GetType().Name}.");
            IsCompleted = true;
            LastStatusMessage = $"End-of-day processing completed for {OrderId}:{TradeId} on {ValueDate:yyyy-MM-dd}.";
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            PublishError(exception, "End Of Day Process Failed");
            throw;
        }
        finally
        {
            ClearCorrelation();
        }
    }

    async Task<IEvent> AwaitTerminalEventAsync(Guid commandId, CancellationToken cancellationToken)
    {
        IEvent? earlyEvent;
        TaskCompletionSource<IEvent> completion;
        lock (_correlationGate)
        {
            _commandId = commandId;
            completion = new TaskCompletionSource<IEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
            _terminalCompletion = completion;
            _earlyTerminalEvents.Remove(commandId, out earlyEvent);
            _earlyTerminalEvents.Clear();
        }
        OnPropertyChanged(nameof(CommandId));
        if (earlyEvent is not null)
            completion.TrySetResult(earlyEvent);
        return await completion.Task.WaitAsync(cancellationToken);
    }

    ValueTask HandleEventAsync(IEvent @event)
    {
        TaskCompletionSource<IEvent>? completion;
        lock (_correlationGate)
        {
            if (_commandId == Guid.Empty)
            {
                if (_runOperation.IsRunning)
                {
                    if (_earlyTerminalEvents.Count >= 16)
                        _earlyTerminalEvents.Remove(_earlyTerminalEvents.Keys.First());
                    _earlyTerminalEvents[@event.CommandId] = @event;
                }
                return ValueTask.CompletedTask;
            }
            if (_commandId != @event.CommandId)
                return ValueTask.CompletedTask;
            completion = _terminalCompletion;
        }
        completion?.TrySetResult(@event);
        return ValueTask.CompletedTask;
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
        OnPropertyChanged(nameof(CanRun));
        _runOperation.NotifyCanExecuteChanged();
    }

    void CancelCorrelation()
    {
        TaskCompletionSource<IEvent>? completion;
        lock (_correlationGate)
        {
            completion = _terminalCompletion;
            _commandId = Guid.Empty;
            _terminalCompletion = null;
            _earlyTerminalEvents.Clear();
        }
        completion?.TrySetCanceled();
        OnPropertyChanged(nameof(CommandId));
    }

    void OperationPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName != nameof(IAsyncOperation.IsRunning))
            return;
        OnPropertyChanged(nameof(IsBusy));
        OnPropertyChanged(nameof(CanRun));
        if (ReferenceEquals(sender, _loadOperation))
            _runOperation.NotifyCanExecuteChanged();
        else
            _loadOperation.NotifyCanExecuteChanged();
    }

    void PublishError(Exception exception, string caption)
    {
        var errorCode = exception is ModelOperationException modelFailure ? modelFailure.ErrorCode : 0;
        LastError = new PresentationError(
            Interlocked.Increment(ref _errorSequence),
            errorCode,
            exception.Message,
            caption);
    }

    static async ValueTask DisposeOperationAsync(AsyncOperation operation)
    {
        try
        {
            await operation.DisposeAsync();
        }
        catch (Exception exception) when (ReferenceEquals(operation.LastFailure, exception))
        {
        }
    }
}
