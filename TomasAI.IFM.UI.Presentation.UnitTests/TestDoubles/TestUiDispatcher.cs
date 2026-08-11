using TomasAI.IFM.UI.Net.Contracts;

namespace TomasAI.IFM.UI.Presentation.UnitTests.TestDoubles;

/// <summary>
/// Executes dispatched work synchronously while recording each invocation.
/// </summary>
public sealed class TestUiDispatcher : IUiDispatcher
{
    readonly int _ownerThreadId = Environment.CurrentManagedThreadId;

    /// <summary>
    /// Gets the number of actions or functions dispatched by the test.
    /// </summary>
    public int InvocationCount { get; private set; }

    /// <inheritdoc />
    public void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        InvocationCount++;
        action();
    }

    /// <inheritdoc />
    public bool CheckAccess() => Environment.CurrentManagedThreadId == _ownerThreadId;

    /// <inheritdoc />
    public ValueTask InvokeAsync(Action action, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        cancellationToken.ThrowIfCancellationRequested();
        InvocationCount++;
        action();
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask<TResult> InvokeAsync<TResult>(
        Func<TResult> function,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(function);
        cancellationToken.ThrowIfCancellationRequested();
        InvocationCount++;
        return ValueTask.FromResult(function());
    }
}
