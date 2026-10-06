using NATS.Client.JetStream;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using TomasAI.IFM.Shared.EventModelActor;

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
        Func<ValueTask> negativeAcknowledge,
        TimeSpan negativeAcknowledgeDelay = default)
    {
        if (destinationCount <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(destinationCount),
                "An event delivery requires at least its primary destination.");
        _remaining = destinationCount;
        _acknowledge = acknowledge;
        _negativeAcknowledge = negativeAcknowledge;
        NegativeAcknowledgeDelay = negativeAcknowledgeDelay;
    }

    internal static EventFanoutDelivery Create<T>(
        INatsJSMsg<T> message,
        int destinationCount,
        TimeSpan negativeAcknowledgeDelay,
        ActorType actorType,
        ILogger? logger = null)
        => new(
            destinationCount,
            () => SettleAsync(message, false, negativeAcknowledgeDelay, actorType, logger),
            () => SettleAsync(message, true, negativeAcknowledgeDelay, actorType, logger),
            negativeAcknowledgeDelay);

    static async ValueTask SettleAsync<T>(INatsJSMsg<T> message, bool negative, TimeSpan delay, ActorType actorType, ILogger? logger)
    {
        var started = Stopwatch.GetTimestamp();
        try
        {
            if (negative) await NegativeAcknowledgeAsync(message, delay, actorType).ConfigureAwait(false);
            else await message.AckAsync(cancellationToken: CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            if (logger is not null)
            {
                var metadata = message.Metadata;
                logger.LogError(exception,
                    "{Component}.{Method} settlement failed; Subject={Subject}; Action={Action}; Stream={Stream}; Consumer={Consumer}; StreamSequence={StreamSequence}; ConsumerSequence={ConsumerSequence}; DeliveryAttempt={DeliveryAttempt}; ElapsedMilliseconds={ElapsedMilliseconds}",
                    nameof(EventFanoutDelivery), nameof(SettleAsync), message.Subject, negative ? "NAK" : "ACK",
                    metadata?.Stream, metadata?.Consumer, metadata?.Sequence.Stream, metadata?.Sequence.Consumer,
                    metadata?.NumDelivered, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            }
            throw;
        }
    }

    static async ValueTask NegativeAcknowledgeAsync<T>(
        INatsJSMsg<T> message,
        TimeSpan delay,
        ActorType actorType)
    {
        try
        {
            await message.NakAsync(
                delay: delay,
                cancellationToken: CancellationToken.None).ConfigureAwait(false);
            NatsMessagingMetrics.RecordOverloadNak(actorType, "succeeded");
        }
        catch
        {
            NatsMessagingMetrics.RecordOverloadNak(actorType, "failed");
            throw;
        }
    }

    internal int Remaining => Math.Max(0, Volatile.Read(ref _remaining));

    internal int Failures => Volatile.Read(ref _failures);

    internal bool IsFinalized => Volatile.Read(ref _finalized) != 0;

    internal TimeSpan NegativeAcknowledgeDelay { get; }

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
