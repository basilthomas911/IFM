using TomasAI.IFM.Domain.Application.Event;
using TomasAI.IFM.Domain.Application.Shared;

namespace TomasAI.IFM.Domain.Application.Actor.UnitTests;

public sealed class ApplicationStartupStatusStoreTests
{
    [Fact]
    public async Task Waiter_completes_only_when_a_new_snapshot_is_published()
    {
        var store = new ApplicationStartupStatusStore();
        var observed = store.Current;
        var waiting = store.WaitForChangeAsync(observed, CancellationToken.None).AsTask();

        Assert.False(waiting.IsCompleted);
        var published = new ApplicationStartupStatus
        {
            State = ApplicationLifecycleState.Running,
            Summary = "Running"
        };
        store.Set(published);

        Assert.Same(published, await waiting.WaitAsync(TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public async Task Waiter_observes_a_change_that_precedes_registration()
    {
        var store = new ApplicationStartupStatusStore();
        var observed = store.Current;
        var published = new ApplicationStartupStatus
        {
            State = ApplicationLifecycleState.Degraded,
            Summary = "Degraded"
        };
        store.Set(published);

        Assert.Same(published,
            await store.WaitForChangeAsync(observed, CancellationToken.None));
    }

    [Fact]
    public async Task Waiter_honors_shutdown_cancellation()
    {
        var store = new ApplicationStartupStatusStore();
        using var cancellation = new CancellationTokenSource();
        var waiting = store.WaitForChangeAsync(store.Current, cancellation.Token).AsTask();

        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
    }
}
