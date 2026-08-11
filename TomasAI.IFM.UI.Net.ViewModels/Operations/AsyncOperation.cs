using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.ViewModels.Presentation;

namespace TomasAI.IFM.UI.Net.ViewModels.Operations;

/// <summary>
/// Runs one observable instance of a cancellable presentation operation at a time.
/// </summary>
public sealed class AsyncOperation : ObservableObject, IAsyncOperation, IAsyncDisposable
{
    readonly object _gate = new();
    readonly Func<CancellationToken, Task> _operation;
    readonly Func<bool>? _canExecute;
    CancellationTokenSource? _executionCancellation;
    Task? _execution;
    Exception? _lastFailure;
    int _isRunning;

    public AsyncOperation(
        Func<CancellationToken, Task> operation,
        Func<bool>? canExecute = null)
    {
        _operation = operation ?? throw new ArgumentNullException(nameof(operation));
        _canExecute = canExecute;
    }

    public bool IsRunning => Volatile.Read(ref _isRunning) == 1;

    public bool CanExecute => !IsRunning && (_canExecute?.Invoke() ?? true);

    public Exception? LastFailure => _lastFailure;

    public Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (_execution is { IsCompleted: false })
                return _execution;

            cancellationToken.ThrowIfCancellationRequested();
            if (!CanExecute)
                return Task.CompletedTask;

            SetProperty(ref _lastFailure, null, nameof(LastFailure));
            _executionCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            SetRunning(true);
            _execution = ExecuteCoreAsync(_executionCancellation);
            return _execution;
        }
    }

    public void Cancel()
    {
        lock (_gate)
            _executionCancellation?.Cancel();
    }

    public void NotifyCanExecuteChanged() => OnPropertyChanged(nameof(CanExecute));

    public async ValueTask DisposeAsync()
    {
        Task? execution;
        lock (_gate)
        {
            _executionCancellation?.Cancel();
            execution = _execution;
        }

        if (execution is not null)
        {
            try
            {
                await execution;
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    async Task ExecuteCoreAsync(CancellationTokenSource executionCancellation)
    {
        try
        {
            await _operation(executionCancellation.Token);
            SetProperty(ref _lastFailure, null, nameof(LastFailure));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            SetProperty(ref _lastFailure, exception, nameof(LastFailure));
            throw;
        }
        finally
        {
            lock (_gate)
            {
                if (ReferenceEquals(_executionCancellation, executionCancellation))
                {
                    _executionCancellation.Dispose();
                    _executionCancellation = null;
                    SetRunning(false);
                }
            }
        }
    }

    void SetRunning(bool isRunning)
    {
        var value = isRunning ? 1 : 0;
        if (Interlocked.Exchange(ref _isRunning, value) == value)
            return;

        OnPropertyChanged(nameof(IsRunning));
        OnPropertyChanged(nameof(CanExecute));
    }
}
