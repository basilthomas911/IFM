using TomasAI.IFM.Application.Api.Server.Core.Hosting;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using TomasAI.IFM.Domain.Application.Event;
using TomasAI.IFM.Domain.Application.Shared;

namespace TomasAI.IFM.Application.Api.Server.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0, launchCount: 1, warmupCount: 3, iterationCount: 8, invocationCount: 1)]
public class PollingTimerThreadingBenchmarks
{
    static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(100);
    static readonly ApplicationStartupStatus Running = new()
    {
        State = ApplicationLifecycleState.Running,
        Summary = "Running"
    };

    [Benchmark(Baseline = true, Description = "Before: 100 ms lifecycle polling")]
    public async Task<ApplicationStartupStatus> PollLifecycleStatus()
    {
        var store = new ApplicationStartupStatusStore();
        using var publication = new Timer(
            static state => ((ApplicationStartupStatusStore)state!).Set(Running),
            store,
            TimeSpan.FromMilliseconds(1),
            Timeout.InfiniteTimeSpan);
        while (true)
        {
            var status = store.Current;
            if (status.State == ApplicationLifecycleState.Running)
                return status;
            await Task.Delay(PollInterval).ConfigureAwait(false);
        }
    }

    [Benchmark(Description = "After: lifecycle change notification")]
    public async Task<ApplicationStartupStatus> ObserveLifecycleChange()
    {
        var store = new ApplicationStartupStatusStore();
        var observed = store.Current;
        using var publication = new Timer(
            static state => ((ApplicationStartupStatusStore)state!).Set(Running),
            store,
            TimeSpan.FromMilliseconds(1),
            Timeout.InfiniteTimeSpan);
        return await store.WaitForChangeAsync(observed, CancellationToken.None).ConfigureAwait(false);
    }

    [Benchmark(Description = "Before: custom one-shot timer wait")]
    public Task<bool> CustomTimerDelay() => LegacyDelayAsync();

    [Benchmark(Description = "After: runtime TimeProvider delay")]
    public Task<bool> RuntimeTimerDelay() =>
        HostedServiceLifecycle.DelayAsync(TimeSpan.Zero, TimeProvider.System, CancellationToken.None);

    static async Task<bool> LegacyDelayAsync()
    {
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var timer = TimeProvider.System.CreateTimer(
            static state => ((TaskCompletionSource<bool>)state!).TrySetResult(true),
            completion,
            TimeSpan.Zero,
            Timeout.InfiniteTimeSpan);
        return await completion.Task.ConfigureAwait(false);
    }
}
