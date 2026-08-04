using NATS.Client.JetStream;

namespace TomasAI.IFM.Framework.Messaging.NatsJetStream;

/// <summary>
/// Coordinates one JetStream acknowledgement across all primary and routed
/// mailbox handoffs for an event. Actor processing remains at-least-once and is
/// intentionally outside the acknowledgement boundary.
/// </summary>
internal sealed class EventFanoutDelivery
{
    Func<ValueTask>? _acknowledge;
    Func<ValueTask>? _negativeAcknowledge;
    int _remaining;
    int _failures;
    int _finalized;

    internal EventFanoutDelivery(
        int destinationCount,
        Func<ValueTask> acknowledge,
        Func<ValueTask> negativeAcknowledge)
    {
        if (destinationCount <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(destinationCount),
                "An event delivery requires at least its primary destination.");
        _remaining = destinationCount;
        _acknowledge = acknowledge;
        _negativeAcknowledge = negativeAcknowledge;
    }

    internal static EventFanoutDelivery Create<T>(
        INatsJSMsg<T> message,
        int destinationCount)
        => new(
            destinationCount,
            () => message.AckAsync(cancellationToken: CancellationToken.None),
            () => message.NakAsync(cancellationToken: CancellationToken.None));

    internal int Remaining => Math.Max(0, Volatile.Read(ref _remaining));

    internal int Failures => Volatile.Read(ref _failures);

    internal bool IsFinalized => Volatile.Read(ref _finalized) != 0;

    internal async ValueTask CompleteHandoffAsync(bool succeeded)
    {
        if (!succeeded)
            Interlocked.Increment(ref _failures);

        var remaining = Interlocked.Decrement(ref _remaining);
        if (remaining > 0)
            return;
        if (remaining < 0)
            throw new InvalidOperationException(
                "Event fan-out delivery completed more branches than it registered.");
        if (Interlocked.Exchange(ref _finalized, 1) != 0)
            throw new InvalidOperationException("Event fan-out delivery finalized more than once.");

        var acknowledge = Interlocked.Exchange(ref _acknowledge, null);
        var negativeAcknowledge = Interlocked.Exchange(ref _negativeAcknowledge, null);
        if (Volatile.Read(ref _failures) == 0)
            await acknowledge!().ConfigureAwait(false);
        else
            await negativeAcknowledge!().ConfigureAwait(false);
    }
}
