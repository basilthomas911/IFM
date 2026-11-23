namespace TomasAI.IFM.Application.MarketData.Rust.Host;

/// <summary>Coalesces repeated session-start commands. Dataset recovery stays native-owned.</summary>
public sealed class NativeSessionStartRequests
{
    readonly object gate = new();
    DateOnly? date;
    Task? started;

    public Task StartAsync(DateOnly valueDate, Func<Task> start, CancellationToken observer)
    {
        observer.ThrowIfCancellationRequested();
        Task task;
        lock (gate)
        {
            if (date == valueDate && started is not null) task = started;
            else
            {
                if (started is { IsCompleted: false })
                    throw new InvalidOperationException("A different native session is still starting.");
                date = valueDate;
                var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                started = task = completion.Task;
                _ = CompleteAsync(start, completion);
            }
        }
        // A lost reply or cancelled observer must not retire an accepted owner.
        return task.WaitAsync(observer);
    }

    static async Task CompleteAsync(Func<Task> start, TaskCompletionSource completion)
    {
        try { await start().ConfigureAwait(false); completion.TrySetResult(); }
        catch (Exception error) { completion.TrySetException(error); }
    }

    public void Stopped()
    {
        lock (gate) { date = null; started = null; }
    }
}
