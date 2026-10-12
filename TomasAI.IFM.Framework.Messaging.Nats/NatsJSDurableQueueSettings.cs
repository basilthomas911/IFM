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
/// Describes the JetStream resources and replay policy for one projector queue.
/// </summary>
/// <param name="Names">The names of the streams, subjects, and durable consumers.</param>
/// <param name="ReplayInterval">The acknowledgement wait and initial replay delay.</param>
/// <param name="MaxReplayAttempts">The application-owned maximum number of genuine replay failures.</param>
/// <param name="Backoff">The application redelivery delays, ordered by delivery attempt.</param>
internal sealed record NatsJSDurableQueueSettings(
    NatsJSDurableQueueNames Names,
    TimeSpan ReplayInterval,
    int MaxReplayAttempts,
    IReadOnlyList<TimeSpan> Backoff);
