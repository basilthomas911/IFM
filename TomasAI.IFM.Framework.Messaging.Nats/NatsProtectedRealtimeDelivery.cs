using System.Diagnostics;
using System.Diagnostics.Metrics;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Framework.Messaging.NatsJetStream;

/// <summary>Retains one owned ordered input while bounded actor capacity recovers.</summary>
internal static class NatsProtectedRealtimeDelivery
{
    static readonly Meter Meter = new("TomasAI.IFM.Nats.ProtectedRealtime", "1.0");
    static readonly Histogram<double> Wait = Meter.CreateHistogram<double>("protected_realtime.admission_wait", "ms");

    /// <summary>Retries admission without disposing or duplicating the payload; shutdown cancels the wait.</summary>
    internal static async ValueTask<ActorAdmissionResult> AdmitAsync(IActorSupervisor supervisor,
        IActorMessage message, ActorSubject subject, CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        var waited = false;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // Re-resolve after every wait: a replacement generation owns a new queue.
            if (supervisor.Children.TryGetValue(subject.ActorId, out var actor))
            {
                var result = await actor.Mailbox.ThreadQueues.TryAdmitAsync(message, subject, cancellationToken).ConfigureAwait(false);
                if (result.Accepted || result.Reason == ActorAdmissionReason.PayloadTooLarge)
                {
                    if (waited) Wait.Record(Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                        new KeyValuePair<string, object?>("actor", subject.Name));
                    return result;
                }
            }
            waited = true;
            await Task.Delay(TimeSpan.FromMilliseconds(2), cancellationToken).ConfigureAwait(false);
        }
    }
}
