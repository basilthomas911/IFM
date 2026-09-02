namespace TomasAI.IFM.Application.Api.Server;

/// <summary>
/// Exception-free waits for API hosted-service lifecycle signals.
/// Cancellation is represented as a normal completion result rather than a
/// faulted or cancelled task.
/// </summary>
internal static class HostedServiceLifecycle
{
    public static async Task<bool> DelayAsync(
        TimeSpan delay,
        TimeProvider timeProvider,
        CancellationToken stoppingToken)
    {
        if (stoppingToken.IsCancellationRequested)
            return false;

        var completion = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = stoppingToken.Register(
            static state => ((TaskCompletionSource<bool>)state!).TrySetResult(false),
            completion);
        using var timer = timeProvider.CreateTimer(
            static state => ((TaskCompletionSource<bool>)state!).TrySetResult(true),
            completion,
            delay,
            Timeout.InfiniteTimeSpan);
        var elapsed = await completion.Task.ConfigureAwait(false);
        return elapsed && !stoppingToken.IsCancellationRequested;
    }

    public static async Task<bool> WaitForSignalAsync(
        CancellationToken signal,
        CancellationToken stoppingToken)
    {
        if (signal.IsCancellationRequested)
            return true;
        if (stoppingToken.IsCancellationRequested)
            return false;

        var completion = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var signalRegistration = signal.Register(
            static state => ((TaskCompletionSource<bool>)state!).TrySetResult(true),
            completion);
        using var stopRegistration = stoppingToken.Register(
            static state => ((TaskCompletionSource<bool>)state!).TrySetResult(false),
            completion);
        return await completion.Task.ConfigureAwait(false);
    }
}
