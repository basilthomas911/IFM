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
/// Identifies the JetStream resources that make up a projector's process and replay queues.
/// </summary>
/// <param name="ProcessStream">The stream that stores newly enqueued process messages.</param>
/// <param name="ProcessSubject">The subject to which process messages are published.</param>
/// <param name="ProcessConsumer">The durable consumer that reads process messages.</param>
/// <param name="ReplayStream">The stream that stores messages awaiting replay.</param>
/// <param name="ReplaySubject">The subject to which failed process messages are published.</param>
/// <param name="ReplayConsumer">The durable consumer that reads replay messages.</param>
internal sealed record NatsJSDurableQueueNames(
    string ProcessStream,
    string ProcessSubject,
    string ProcessConsumer,
    string ReplayStream,
    string ReplaySubject,
    string ReplayConsumer);
