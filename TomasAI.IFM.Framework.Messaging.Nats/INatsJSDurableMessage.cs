using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using NATS.Net;
using System.Diagnostics;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;

namespace TomasAI.IFM.Framework.Messaging.Nats;

/// <summary>
/// Represents a process or replay message received from a durable queue transport.
/// </summary>
internal interface INatsJSDurableMessage
{
    /// <summary>Gets the serialized durable event envelope.</summary>
    byte[] Data { get; }

    /// <summary>Gets the W3C trace context carried by this delivery's transport headers.</summary>
    ActivityContext TraceContext { get; }

    /// <summary>Gets the number of times the message has been delivered, including the current delivery.</summary>
    ulong DeliveryCount { get; }

    /// <summary>Acknowledges successful or terminal processing of the message.</summary>
    /// <param name="cancellationToken">A token that cancels the acknowledgement.</param>
    /// <returns>A value task that completes when the acknowledgement has been sent.</returns>
    ValueTask AckAsync(CancellationToken cancellationToken);

    /// <summary>Negatively acknowledges the message and requests redelivery after a delay.</summary>
    /// <param name="delay">The minimum delay before the message is eligible for redelivery.</param>
    /// <param name="cancellationToken">A token that cancels the negative acknowledgement.</param>
    /// <returns>A value task that completes when the negative acknowledgement has been sent.</returns>
    ValueTask NakAsync(TimeSpan delay, CancellationToken cancellationToken);
}
