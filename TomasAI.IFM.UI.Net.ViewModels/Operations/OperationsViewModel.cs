using System.ComponentModel;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.ViewModels.Lifecycle;
using TomasAI.IFM.UI.Net.ViewModels.Presentation;

namespace TomasAI.IFM.UI.Net.ViewModels.Operations;

public enum OperationsViewType
{
    Strategy,
    Latency,
    Traffic,
    Errors,
    Saturation
}

/// <summary>Framework-neutral state and lifecycle for the five-view Operations region.</summary>
public sealed class OperationsViewModel : ObservableObject, IAsyncLifecycle, IAsyncDisposable
{
    readonly AsyncLifecycleCoordinator _lifecycle;
    OperationsViewType _selectedView = OperationsViewType.Strategy;

    public OperationsViewModel(IAppRoot appRoot, string contractId, DateOnly valueDate)
        : this(new StrategyOperationsViewModel(appRoot, contractId, valueDate))
    {
    }

    internal OperationsViewModel(StrategyOperationsViewModel strategy)
    {
        Strategy = strategy ?? throw new ArgumentNullException(nameof(strategy));
        Strategy.PropertyChanged += StrategyPropertyChanged;
        _lifecycle = new AsyncLifecycleCoordinator(
            Strategy.InitializeAsync,
            Strategy.StopAsync);
    }

    public StrategyOperationsViewModel Strategy { get; }

    public OperationsViewType SelectedView
    {
        get => _selectedView;
        private set => SetProperty(ref _selectedView, value);
    }

    public void SelectView(OperationsViewType view)
    {
        if (!Enum.IsDefined(view))
            throw new ArgumentOutOfRangeException(nameof(view));
        SelectedView = view;
    }

    public Task InitializeAsync(CancellationToken cancellationToken)
        => _lifecycle.InitializeAsync(cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken)
        => _lifecycle.StopAsync(cancellationToken);

    public async ValueTask DisposeAsync()
    {
        Strategy.PropertyChanged -= StrategyPropertyChanged;
        await _lifecycle.DisposeAsync();
        await Strategy.DisposeAsync();
    }

    void StrategyPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
        => OnPropertyChanged(nameof(Strategy));
}
