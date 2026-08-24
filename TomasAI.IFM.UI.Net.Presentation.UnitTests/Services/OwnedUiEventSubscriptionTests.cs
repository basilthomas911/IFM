using FluentAssertions;
using TomasAI.IFM.UI.Net.Services.Subscriptions;

namespace TomasAI.IFM.UI.Net.Presentation.UnitTests.Services;

/// <summary>Verifies deterministic event-subscription ownership.</summary>
public sealed class OwnedUiEventSubscriptionTests
{
    /// <summary>Verifies repeated start and stop requests invoke each backend transition once.</summary>
    [Fact]
    public async Task StartAndStop_AreIdempotent()
    {
        var starts = 0;
        var stops = 0;
        await using var subscription = new OwnedUiEventSubscription(
            _ =>
            {
                starts++;
                return ValueTask.CompletedTask;
            },
            () =>
            {
                stops++;
                return ValueTask.CompletedTask;
            });

        await subscription.StartAsync();
        await subscription.StartAsync();
        await subscription.StopAsync();
        await subscription.StopAsync();

        starts.Should().Be(1);
        stops.Should().Be(1);
        subscription.IsStarted.Should().BeFalse();
    }

    /// <summary>Verifies disposal stops an active backend listener exactly once.</summary>
    [Fact]
    public async Task DisposeAsync_StopsActiveSubscription()
    {
        var stops = 0;
        var subscription = new OwnedUiEventSubscription(
            _ => ValueTask.CompletedTask,
            () =>
            {
                stops++;
                return ValueTask.CompletedTask;
            });
        await subscription.StartAsync();

        await subscription.DisposeAsync();
        await subscription.DisposeAsync();

        stops.Should().Be(1);
    }

    /// <summary>Verifies cancellation prevents backend startup and leaves ownership unclaimed.</summary>
    [Fact]
    public async Task StartAsync_HonorsCancellation()
    {
        var starts = 0;
        await using var subscription = new OwnedUiEventSubscription(
            _ =>
            {
                starts++;
                return ValueTask.CompletedTask;
            },
            () => ValueTask.CompletedTask);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var action = async () => await subscription.StartAsync(cancellation.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
        starts.Should().Be(0);
        subscription.IsStarted.Should().BeFalse();
    }
}
